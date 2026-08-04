using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Features.Agents.Application.Acp;

/// <summary>
/// Owns ACP initialize/session/prompt lifecycle for one admitted Zaide run.
/// Retains the session client when cancel acknowledgement is uncertain so a
/// later explicit-end retry can re-issue <c>session/cancel</c> without wrapping
/// or falling back to another backend.
/// </summary>
internal sealed class AcpAgentSessionAdapter
{
    private readonly IAcpSessionClientFactory _clientFactory;
    private readonly Func<string> _workingDirectoryProvider;
    private readonly IAgentActorBackendBindingStore? _bindingStore;
    private readonly Dictionary<AgentSessionId, AcpAgentSessionBinding> _bindings = new();
    private readonly Dictionary<AgentSessionId, PendingCancellationAcknowledgement> _pendingCancelAcks = new();
    private readonly object _pendingCancelSync = new();

    public AcpAgentSessionAdapter(
        IAcpSessionClientFactory clientFactory,
        Func<string> workingDirectoryProvider,
        IAgentActorBackendBindingStore? bindingStore = null)
    {
        _clientFactory = clientFactory
            ?? throw new ArgumentNullException(nameof(clientFactory));
        _workingDirectoryProvider = workingDirectoryProvider
            ?? throw new ArgumentNullException(nameof(workingDirectoryProvider));
        _bindingStore = bindingStore;
    }

    /// <summary>
    /// Re-issues a bounded cancel acknowledgement for a session whose prior
    /// cancel-ack timed out or failed. Uses a fresh independent token budget.
    /// </summary>
    public async Task<AgentCancellationAcknowledgementResult> AcknowledgeCancellationAsync(
        AgentSessionId sessionId,
        CancellationToken cancellationToken = default)
    {
        if (sessionId == default)
        {
            throw new ArgumentException("Session id is required.", nameof(sessionId));
        }

        PendingCancellationAcknowledgement? pending;
        lock (_pendingCancelSync)
        {
            if (!_pendingCancelAcks.TryGetValue(sessionId, out pending))
            {
                return AgentCancellationAcknowledgementResult.Unavailable(
                    "No pending ACP cancel acknowledgement target. "
                    + "Provider termination is not claimed.");
            }
        }

        try
        {
            // Independent bounded budget for this retry attempt — never reuse a
            // previously cancelled run token.
            using var cancelCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (AcpProcessLifecycleLimits.CancelPromptTimeout > TimeSpan.Zero)
            {
                cancelCts.CancelAfter(AcpProcessLifecycleLimits.CancelPromptTimeout);
            }

            await pending.Client.CancelPromptAsync(pending.AcpSessionId, cancelCts.Token)
                .ConfigureAwait(false);

            await ReleasePendingCancellationAsync(sessionId).ConfigureAwait(false);
            return AgentCancellationAcknowledgementResult.Succeeded();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return AgentCancellationAcknowledgementResult.TimedOut(
                "ACP cancel acknowledgement timed out. Local cancellation was requested; "
                + "live session ownership remains. Retry is available. "
                + "Provider termination is not claimed.");
        }
        catch (Exception)
        {
            return AgentCancellationAcknowledgementResult.Failed(
                "ACP cancel acknowledgement failed. Local cancellation was requested; "
                + "live session ownership remains. Retry is available. "
                + "Provider termination is not claimed.");
        }
    }

    public async IAsyncEnumerable<AgentBackendEvent> ExecuteAsync(
        AgentBackendExecutionContext context,
        AgentCapabilitySnapshot currentSnapshot,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var backendEvent in ExecuteAsync(
                           context,
                           currentSnapshot,
                           enableActionBridge: false,
                           cancellationToken).ConfigureAwait(false))
        {
            yield return backendEvent;
        }
    }

    public async IAsyncEnumerable<AgentBackendEvent> ExecuteAsync(
        AgentBackendExecutionContext context,
        AgentCapabilitySnapshot currentSnapshot,
        bool enableActionBridge,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<AgentBackendEvent>? events = null;
        AgentBackendEvent? faultEvent = null;
        IAcpSessionClient? client = null;
        var retainClientForCancelRetry = false;
        var zaideSessionId = context.Request.SessionId;

        try
        {
            events = new List<AgentBackendEvent>();
            var capabilitySnapshot = currentSnapshot;

            client = await _clientFactory.CreateAsync(context, cancellationToken).ConfigureAwait(false);
            var bridgeEnabled = enableActionBridge && context.Actions is not UnavailableAgentActionBroker;
            client.ConfigureActionBridge(
                null,
                bridgeEnabled
                    ? AcpClientCapabilityProfiles.CreateWithFilesystemBridge()
                    : AcpClientCapabilityProfiles.CreateWithoutFilesystemBridge());

            var negotiated = await client.InitializeAsync(cancellationToken).ConfigureAwait(false);
            var binding = GetOrCreateBinding(
                context.Request.SessionId,
                context.Request.TargetActorId,
                negotiated);
            VerifyAgentIdentity(binding, negotiated);

            capabilitySnapshot = AcpCapabilitySnapshotMapper.CreateAfterNegotiation(
                negotiated,
                context.ContextManifest is not null,
                usageObserved: false,
                capabilitySnapshot.Version);

            if (!binding.IsBoundToAcpSession)
            {
                var workingDirectory = _workingDirectoryProvider();
                var acpSessionId = await client.CreateSessionAsync(workingDirectory, cancellationToken)
                    .ConfigureAwait(false);
                binding.AcpSessionId = acpSessionId;
            }

            if (bridgeEnabled)
            {
                var bridge = new AcpClientActionBridge(
                    context.Actions,
                    _workingDirectoryProvider(),
                    binding.AcpSessionId!);
                ConfigureClientForBridge(client, bridge);
            }

            var prompt = AcpContextManifestEncoder.BuildPrompt(
                context.Request.MessageText,
                context.ContextManifest,
                negotiated.AgentCapabilities.PromptCapabilities.EmbeddedContext);

            var turn = await client.PromptAsync(binding.AcpSessionId!, prompt, cancellationToken)
                .ConfigureAwait(false);

            foreach (var update in turn.Updates)
            {
                if (!AcpSessionUpdateNormalizer.TryNormalizeActivity(update, out var activityPayload)
                    || activityPayload is null)
                {
                    continue;
                }

                if (activityPayload.ActivityKind == AcpBackendActivityKind.UsageUpdate)
                {
                    var updatedSnapshot = AcpCapabilitySnapshotMapper.WithUsageObserved(
                        capabilitySnapshot,
                        usageObserved: true);
                    if (updatedSnapshot.Version != capabilitySnapshot.Version)
                    {
                        capabilitySnapshot = updatedSnapshot;
                        events.Add(new AgentBackendEvent(
                            AgentBackendEventKind.CapabilitySnapshotChanged,
                            DateTimeOffset.UtcNow,
                            new AgentBackendCapabilityChangedPayload(capabilitySnapshot)));
                    }
                }

                events.Add(new AgentBackendEvent(
                    AgentBackendEventKind.ActivityReported,
                    DateTimeOffset.UtcNow,
                    activityPayload));
            }

            events.AddRange(MapPromptTurn(turn));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (client?.ActiveSessionId is { } activeSessionId)
            {
                try
                {
                    // Independent bounded token: the run token is already cancelled and
                    // cannot carry a cancel-prompt request/acknowledgement. This budget is
                    // separate from EndAsync's outer acknowledgement wait; the observer
                    // completes only after this path resolves so the two budgets do not race.
                    using var cancelCts = new CancellationTokenSource(
                        AcpProcessLifecycleLimits.CancelPromptTimeout);
                    await client.CancelPromptAsync(activeSessionId, cancelCts.Token)
                        .ConfigureAwait(false);
                    faultEvent = CreateFailure(AgentFailureKind.Cancellation, "Run was cancelled.");
                }
                catch (OperationCanceledException)
                {
                    // Bounded cancel acknowledgement timed out — not ordinary cancellation.
                    // Retain the live client so EndAsync retry can re-issue cancel-ack.
                    RetainPendingCancellation(zaideSessionId, client, activeSessionId);
                    retainClientForCancelRetry = true;
                    faultEvent = CreateFailure(
                        AgentFailureKind.Indeterminate,
                        "ACP cancel acknowledgement timed out. Local cancellation was requested; "
                        + "live session ownership remains. Retry is available. "
                        + "Provider termination is not claimed.",
                        cancellationAcknowledgementUncertain: true);
                }
                catch (Exception)
                {
                    // Cancel acknowledgement failed — not ordinary cancellation.
                    RetainPendingCancellation(zaideSessionId, client, activeSessionId);
                    retainClientForCancelRetry = true;
                    faultEvent = CreateFailure(
                        AgentFailureKind.Indeterminate,
                        "ACP cancel acknowledgement failed. Local cancellation was requested; "
                        + "live session ownership remains. Retry is available. "
                        + "Provider termination is not claimed.",
                        cancellationAcknowledgementUncertain: true);
                }
            }
            else
            {
                faultEvent = CreateFailure(AgentFailureKind.Cancellation, "Run was cancelled.");
            }
        }
        catch (AcpProcessLifecycleException ex)
        {
            faultEvent = CreateFailure(MapLifecycleFailure(ex.Kind), ex.Message);
        }
        catch (AcpProtocolException ex)
        {
            faultEvent = CreateFailure(AgentFailureKind.Transport, ex.Message);
        }
        catch (Exception ex)
        {
            var reason = string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
            faultEvent = CreateFailure(AgentFailureKind.Indeterminate, reason);
        }
        finally
        {
            // When cancel-ack is uncertain, keep the client for retry re-issue.
            if (client is not null && !retainClientForCancelRetry)
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
        }

        if (faultEvent is not null)
        {
            yield return faultEvent;
            yield break;
        }

        foreach (var backendEvent in events!)
        {
            yield return backendEvent;
        }
    }

    private AcpAgentSessionBinding GetOrCreateBinding(
        AgentSessionId sessionId,
        ActorId targetActorId,
        AcpNegotiatedCapabilities negotiated)
    {
        if (_bindings.TryGetValue(sessionId, out var existing))
        {
            return existing;
        }

        string expectedName;
        string expectedVersion;
        if (_bindingStore?.TryGetBinding(targetActorId, out var actorBinding) == true
            && !string.IsNullOrWhiteSpace(actorBinding.ExpectedAgentName)
            && !string.IsNullOrWhiteSpace(actorBinding.ExpectedAgentVersion))
        {
            expectedName = actorBinding.ExpectedAgentName;
            expectedVersion = actorBinding.ExpectedAgentVersion;
        }
        else
        {
            expectedName = negotiated.AgentInfo?.Name ?? "unknown-agent";
            expectedVersion = negotiated.AgentInfo?.Version ?? "unknown-version";
        }

        var binding = new AcpAgentSessionBinding(sessionId, expectedName, expectedVersion);
        _bindings[sessionId] = binding;
        return binding;
    }

    private static void VerifyAgentIdentity(
        AcpAgentSessionBinding binding,
        AcpNegotiatedCapabilities negotiated)
    {
        var observedName = negotiated.AgentInfo?.Name ?? string.Empty;
        var observedVersion = negotiated.AgentInfo?.Version ?? string.Empty;
        if (!string.Equals(binding.ExpectedAgentName, observedName, StringComparison.Ordinal)
            || !string.Equals(binding.ExpectedAgentVersion, observedVersion, StringComparison.Ordinal))
        {
            throw new AcpProtocolException(
                "ACP agent identity mismatch for the bound Agent Session.");
        }
    }

    private static IEnumerable<AgentBackendEvent> MapPromptTurn(AcpPromptTurnResult turn)
    {
        switch (turn.StopReason)
        {
            case AcpStopReason.EndTurn:
                if (string.IsNullOrWhiteSpace(turn.AgentMessageText))
                {
                    yield return CreateFailure(
                        AgentFailureKind.Execution,
                        "ACP prompt completed without assistant text.");
                    yield break;
                }

                yield return new AgentBackendEvent(
                    AgentBackendEventKind.MessageCompleted,
                    DateTimeOffset.UtcNow,
                    new AgentBackendMessageCompletedPayload(turn.AgentMessageText));
                yield break;

            case AcpStopReason.Cancelled:
                yield return CreateFailure(AgentFailureKind.Cancellation, "ACP prompt was cancelled.");
                yield break;

            case AcpStopReason.MaxTokens:
            case AcpStopReason.MaxTurnRequests:
            case AcpStopReason.Refusal:
                yield return CreateFailure(
                    AgentFailureKind.Execution,
                    $"ACP prompt stopped with reason '{turn.StopReason}'.");
                yield break;

            default:
                yield return CreateFailure(
                    AgentFailureKind.Indeterminate,
                    $"ACP prompt ended with unsupported stop reason '{turn.StopReason}'.");
                yield break;
        }
    }

    private static AgentFailureKind MapLifecycleFailure(AcpProcessLifecycleFailureKind kind) =>
        kind switch
        {
            AcpProcessLifecycleFailureKind.Cancellation => AgentFailureKind.Cancellation,
            AcpProcessLifecycleFailureKind.Timeout => AgentFailureKind.Timeout,
            AcpProcessLifecycleFailureKind.ProcessExit => AgentFailureKind.Transport,
            AcpProcessLifecycleFailureKind.ProtocolFailure => AgentFailureKind.Transport,
            AcpProcessLifecycleFailureKind.IndeterminateLateCompletion => AgentFailureKind.Indeterminate,
            _ => AgentFailureKind.Indeterminate,
        };

    private static AgentBackendEvent CreateFailure(
        AgentFailureKind failureKind,
        string reason,
        bool cancellationAcknowledgementUncertain = false) =>
        new(
            AgentBackendEventKind.FailureObserved,
            DateTimeOffset.UtcNow,
            new AgentBackendFailurePayload(
                failureKind,
                reason,
                cancellationAcknowledgementUncertain));

    private void RetainPendingCancellation(
        AgentSessionId sessionId,
        IAcpSessionClient client,
        string acpSessionId)
    {
        lock (_pendingCancelSync)
        {
            if (_pendingCancelAcks.TryGetValue(sessionId, out var existing)
                && !ReferenceEquals(existing.Client, client))
            {
                // Best-effort dispose of a superseded retained client.
                _ = existing.Client.DisposeAsync().AsTask();
            }

            _pendingCancelAcks[sessionId] = new PendingCancellationAcknowledgement(client, acpSessionId);
        }
    }

    private async Task ReleasePendingCancellationAsync(AgentSessionId sessionId)
    {
        PendingCancellationAcknowledgement? pending;
        lock (_pendingCancelSync)
        {
            if (!_pendingCancelAcks.Remove(sessionId, out pending))
            {
                return;
            }
        }

        await pending.Client.DisposeAsync().ConfigureAwait(false);
    }

    private static void ConfigureClientForBridge(IAcpSessionClient client, AcpClientActionBridge bridge)
    {
        var capabilities = AcpClientCapabilityProfiles.CreateWithFilesystemBridge();
        var fallbackRouter = new AcpInboundClientRequestRouter(capabilities);
        client.ConfigureActionBridge(bridge.CreateInboundHandler(fallbackRouter), capabilities);
    }

    private sealed class PendingCancellationAcknowledgement
    {
        public PendingCancellationAcknowledgement(IAcpSessionClient client, string acpSessionId)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
            if (string.IsNullOrWhiteSpace(acpSessionId))
            {
                throw new ArgumentException("ACP session id is required.", nameof(acpSessionId));
            }

            AcpSessionId = acpSessionId;
        }

        public IAcpSessionClient Client { get; }

        public string AcpSessionId { get; }
    }
}
