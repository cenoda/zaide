using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Run-scoped broker that admits and classifies action requests without performing I/O.
/// Captures the current workspace scope at admission via
/// <see cref="IWorkspaceActionAuthority.TryCaptureCurrentScope"/>; when no workspace
/// is open all action requests are rejected with <see cref="AgentActionFailureKind.NoWorkspace"/>.
/// </summary>
internal sealed class ContractAgentActionBroker : IAgentActionBroker
{
    private readonly AgentSessionId _sessionId;
    private readonly ExecutionRunId _runId;
    private readonly ConversationId _conversationId;
    private readonly ActorId _initiatingActorId;
    private readonly ActorId _targetActorId;
    private readonly AgentBackendId _backendId;
    private readonly WorkspaceActionScope? _workspaceScope;
    private readonly IWorkspaceActionAuthority _workspaceAuthority;
    private readonly IAgentFileReader _fileReader;
    private readonly IAgentCommandResolver _commandResolver;
    private readonly AgentActionRunSlotTracker _runSlot;
    private readonly AgentActionCorrelationRegistry _correlationRegistry;
    private readonly object _admissionGate = new();
    private volatile bool _revoked;

    internal Action? TestProcessingHold { get; set; }

    /// <summary>
    /// Creates a run-scoped broker that captures the current workspace scope via
    /// <paramref name="workspaceAuthority"/> at admission. When no workspace is
    /// open (<see cref="TryCaptureCurrentScope"/> returns <c>false</c>), every
    /// action request is rejected with <see cref="AgentActionFailureKind.NoWorkspace"/>.
    /// </summary>
    public ContractAgentActionBroker(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        ActorId initiatingActorId,
        ActorId targetActorId,
        AgentBackendId backendId,
        IWorkspaceActionAuthority workspaceAuthority,
        IAgentFileReader fileReader,
        IAgentCommandResolver commandResolver,
        AgentActionRunSlotTracker runSlot,
        AgentActionCorrelationRegistry correlationRegistry)
    {
        if (sessionId == default)
        {
            throw new ArgumentException("Session id is required.", nameof(sessionId));
        }

        if (runId == default)
        {
            throw new ArgumentException("Run id is required.", nameof(runId));
        }

        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (initiatingActorId == default)
        {
            throw new ArgumentException("Initiating actor id is required.", nameof(initiatingActorId));
        }

        if (targetActorId == default)
        {
            throw new ArgumentException("Target actor id is required.", nameof(targetActorId));
        }

        if (backendId == default)
        {
            throw new ArgumentException("Backend id is required.", nameof(backendId));
        }

        _workspaceAuthority = workspaceAuthority ?? throw new ArgumentNullException(nameof(workspaceAuthority));
        if (!_workspaceAuthority.TryCaptureCurrentScope(out var capturedScope))
        {
            _workspaceScope = null;
        }
        else
        {
            _workspaceScope = capturedScope;
        }
        _sessionId = sessionId;
        _runId = runId;
        _conversationId = conversationId;
        _initiatingActorId = initiatingActorId;
        _targetActorId = targetActorId;
        _backendId = backendId;
        _fileReader = fileReader ?? throw new ArgumentNullException(nameof(fileReader));
        _commandResolver = commandResolver ?? throw new ArgumentNullException(nameof(commandResolver));
        _runSlot = runSlot ?? throw new ArgumentNullException(nameof(runSlot));
        _correlationRegistry = correlationRegistry ?? throw new ArgumentNullException(nameof(correlationRegistry));
    }

    public void Revoke()
    {
        _revoked = true;
        _correlationRegistry.Revoke();
    }

    public ValueTask<AgentActionResult> RequestAsync(
        AgentActionPayload payload,
        string? correlationKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_revoked)
        {
            return ValueTask.FromResult(CreateDeniedResult(
                payload,
                AgentActionFailureKind.BrokerRevoked,
                "Action broker authority was revoked."));
        }

        ArgumentNullException.ThrowIfNull(payload);
        if (!AgentActionPayload.MatchesKind(payload.Kind, payload))
        {
            return ValueTask.FromResult(CreateDeniedResult(
                payload,
                AgentActionFailureKind.InvalidRequest,
                "Action payload kind is inconsistent."));
        }

        // No workspace open: all action requests are rejected before composition.
        if (_workspaceScope is null)
        {
            return ValueTask.FromResult(CreateDeniedResult(
                payload,
                AgentActionFailureKind.NoWorkspace,
                "No workspace is open. Action requests require an active workspace."));
        }

        AgentActionRequest request;
        try
        {
            request = AgentActionRequestComposer.Compose(
                _sessionId,
                _runId,
                _conversationId,
                _initiatingActorId,
                _targetActorId,
                _backendId,
                _workspaceScope.Identity,
                _workspaceScope.Generation,
                _commandResolver,
                payload);
        }
        catch (Exception exception)
        {
            return ValueTask.FromResult(CreateDeniedResult(
                payload,
                AgentActionFailureKind.InvalidRequest,
                exception.Message));
        }

        AgentActionCorrelationKey? parsedCorrelationKey = null;
        if (!string.IsNullOrWhiteSpace(correlationKey))
        {
            try
            {
                parsedCorrelationKey = AgentActionCorrelationKey.FromValue(correlationKey);
            }
            catch (Exception exception)
            {
                return ValueTask.FromResult(new AgentActionResult(
                    request.ActionId,
                    request.AttemptId,
                    AgentActionResultKind.Denied,
                    AgentActionFailureKind.InvalidRequest,
                    exception.Message));
            }
        }

        AgentActionResult? terminalResult = null;
        if (parsedCorrelationKey is not null)
        {
            if (_correlationRegistry.TryRejectMismatchedFingerprint(
                    parsedCorrelationKey.Value,
                    request.Fingerprint,
                    out var mismatch))
            {
                return ValueTask.FromResult(mismatch!);
            }

            if (_correlationRegistry.TryGetTerminalResult(
                    parsedCorrelationKey.Value,
                    request.Fingerprint,
                    out var replay))
            {
                return ValueTask.FromResult(new AgentActionResult(
                    replay!.ActionId,
                    replay.AttemptId,
                    AgentActionResultKind.DuplicateReplay,
                    null,
                    replay.Summary,
                    content: replay.Content,
                    revision: replay.Revision,
                    byteLength: replay.ByteLength));
            }

            if (_correlationRegistry.TryWaitForInFlightReplay(
                    parsedCorrelationKey.Value,
                    request.Fingerprint,
                    cancellationToken,
                    out var inFlightReplay))
            {
                if (inFlightReplay!.ResultKind == AgentActionResultKind.Denied
                    && inFlightReplay.FailureKind == AgentActionFailureKind.CorrelationKeyMismatch)
                {
                    return ValueTask.FromResult(inFlightReplay);
                }

                return ValueTask.FromResult(new AgentActionResult(
                    inFlightReplay.ActionId,
                    inFlightReplay.AttemptId,
                    AgentActionResultKind.DuplicateReplay,
                    null,
                    inFlightReplay.Summary,
                    content: inFlightReplay.Content,
                    revision: inFlightReplay.Revision,
                    byteLength: inFlightReplay.ByteLength));
            }

            // Cancellation or revocation occurred during wait.
            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromResult(new AgentActionResult(
                    request.ActionId,
                    request.AttemptId,
                    AgentActionResultKind.Cancelled,
                    AgentActionFailureKind.Indeterminate,
                    "Action request was cancelled while waiting for an in-flight correlation."));
            }

            if (_correlationRegistry.IsRevoked)
            {
                return ValueTask.FromResult(CreateDeniedResult(
                    payload,
                    AgentActionFailureKind.BrokerRevoked,
                    "Correlation registry was revoked while waiting for in-flight action."));
            }
        }

        var reserved = false;
        lock (_admissionGate)
        {
            if (parsedCorrelationKey is not null)
            {
                if (_correlationRegistry.TryRejectMismatchedFingerprint(
                        parsedCorrelationKey.Value,
                        request.Fingerprint,
                        out var mismatch))
                {
                    return ValueTask.FromResult(mismatch!);
                }

                if (_correlationRegistry.TryGetTerminalResult(
                        parsedCorrelationKey.Value,
                        request.Fingerprint,
                        out var replay))
                {
                    return ValueTask.FromResult(new AgentActionResult(
                        replay!.ActionId,
                        replay.AttemptId,
                        AgentActionResultKind.DuplicateReplay,
                        null,
                        replay.Summary,
                        content: replay.Content,
                        revision: replay.Revision,
                        byteLength: replay.ByteLength));
                }
            }

            reserved = _runSlot.TryReserve(request.ActionId);
            if (reserved && parsedCorrelationKey is not null)
            {
                _correlationRegistry.BeginInFlightCorrelation(
                    parsedCorrelationKey.Value,
                    request.Fingerprint);
            }
        }

        if (!reserved)
        {
            if (parsedCorrelationKey is not null
                && _correlationRegistry.TryWaitForInFlightReplay(
                    parsedCorrelationKey.Value,
                    request.Fingerprint,
                    cancellationToken,
                    out var reservedReplay))
            {
                if (reservedReplay!.ResultKind == AgentActionResultKind.Denied
                    && reservedReplay.FailureKind == AgentActionFailureKind.CorrelationKeyMismatch)
                {
                    return ValueTask.FromResult(reservedReplay);
                }

                return ValueTask.FromResult(new AgentActionResult(
                    reservedReplay.ActionId,
                    reservedReplay.AttemptId,
                    AgentActionResultKind.DuplicateReplay,
                    null,
                    reservedReplay.Summary,
                    content: reservedReplay.Content,
                    revision: reservedReplay.Revision,
                    byteLength: reservedReplay.ByteLength));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromResult(new AgentActionResult(
                    request.ActionId,
                    request.AttemptId,
                    AgentActionResultKind.Cancelled,
                    AgentActionFailureKind.Indeterminate,
                    "Action request was cancelled while waiting for a run slot."));
            }

            return ValueTask.FromResult(new AgentActionResult(
                request.ActionId,
                request.AttemptId,
                AgentActionResultKind.Denied,
                AgentActionFailureKind.ConcurrentActionRejected,
                "Only one non-terminal action is allowed per run."));
        }

        try
        {
            TestProcessingHold?.Invoke();

            var lifecycle = new AgentActionLifecycleState();
            lifecycle.TransitionTo(AgentActionStatus.Classified);

            var classification = AgentActionPolicyClassifier.Classify(request.Payload);
            switch (classification)
            {
                case AgentActionPermissionClassification.DeniedByPolicy:
                    lifecycle.TransitionTo(AgentActionStatus.Denied);
                    terminalResult = new AgentActionResult(
                        request.ActionId,
                        request.AttemptId,
                        AgentActionResultKind.Denied,
                        AgentActionFailureKind.PolicyDenied,
                        "Action was denied by locked policy.");
                    break;

                case AgentActionPermissionClassification.RequiresUserDecision:
                    lifecycle.TransitionTo(AgentActionStatus.AwaitingPermissionDecision);
                    lifecycle.TransitionTo(AgentActionStatus.PermissionDenied);
                    lifecycle.TransitionTo(AgentActionStatus.Denied);
                    terminalResult = new AgentActionResult(
                        request.ActionId,
                        request.AttemptId,
                        AgentActionResultKind.Denied,
                        AgentActionFailureKind.PermissionUnavailable,
                        "Permission review is not available in Phase 17 M1.");
                    break;

                case AgentActionPermissionClassification.AllowedByLockedPolicy:
                    terminalResult = ExecuteAllowedRead(request, lifecycle, cancellationToken);
                    break;

                default:
                    lifecycle.TransitionTo(AgentActionStatus.Denied);
                    terminalResult = new AgentActionResult(
                        request.ActionId,
                        request.AttemptId,
                        AgentActionResultKind.Denied,
                        AgentActionFailureKind.PolicyDenied,
                        "Action was denied by locked policy.");
                    break;
            }
        }
        finally
        {
            lock (_admissionGate)
            {
                _runSlot.Release(request.ActionId);

                if (parsedCorrelationKey is not null)
                {
                    if (terminalResult is not null)
                    {
                        _correlationRegistry.RecordTerminalResult(
                            parsedCorrelationKey.Value,
                            request.Fingerprint,
                            terminalResult);
                    }
                    else
                    {
                        _correlationRegistry.ClearInFlightCorrelation(parsedCorrelationKey.Value);
                    }
                }
            }
        }

        return ValueTask.FromResult(terminalResult!);
    }

    private AgentActionResult ExecuteAllowedRead(
        AgentActionRequest request,
        AgentActionLifecycleState lifecycle,
        CancellationToken cancellationToken)
    {
        lifecycle.TransitionTo(AgentActionStatus.ReadyToExecute);

        // No workspace at execution time — rejected before any filesystem access.
        if (_workspaceScope is null)
        {
            lifecycle.TransitionTo(AgentActionStatus.Denied);
            return new AgentActionResult(
                request.ActionId,
                request.AttemptId,
                AgentActionResultKind.Denied,
                AgentActionFailureKind.NoWorkspace,
                "No workspace is open. Action requests require an active workspace.");
        }

        // Re-resolve authoritative workspace state immediately before execution.
        // A workspace close/switch (generation change) revokes stale authority.
        if (!_workspaceAuthority.IsCurrent(_workspaceScope))
        {
            lifecycle.TransitionTo(AgentActionStatus.Revoked);
            return new AgentActionResult(
                request.ActionId,
                request.AttemptId,
                AgentActionResultKind.Revoked,
                AgentActionFailureKind.StaleWorkspace,
                "Workspace generation changed before the read executed.");
        }

        if (request.Payload is not AgentReadFileActionPayload readPayload)
        {
            lifecycle.TransitionTo(AgentActionStatus.Executing);
            lifecycle.TransitionTo(AgentActionStatus.Failed);
            return new AgentActionResult(
                request.ActionId,
                request.AttemptId,
                AgentActionResultKind.Failed,
                AgentActionFailureKind.InvalidRequest,
                "Only a read request is allowed by locked policy.");
        }

        lifecycle.TransitionTo(AgentActionStatus.Executing);

        var readResult = _fileReader.Read(_workspaceScope, readPayload.Path, cancellationToken);
        var (resultKind, failureKind) = MapReadOutcome(readResult.Outcome);

        lifecycle.TransitionTo(resultKind switch
        {
            AgentActionResultKind.Succeeded => AgentActionStatus.Succeeded,
            AgentActionResultKind.Cancelled => AgentActionStatus.Cancelled,
            _ => AgentActionStatus.Failed,
        });

        // Preserve content, revision, and byte length for successful reads.
        // Rejection results are bounded and redacted — no content, default
        // revision, and zero byte length.
        return new AgentActionResult(
            request.ActionId,
            request.AttemptId,
            resultKind,
            failureKind,
            readResult.Summary,
            content: readResult.Content,
            revision: readResult.Revision,
            byteLength: readResult.ByteLength);
    }

    private static (AgentActionResultKind ResultKind, AgentActionFailureKind? FailureKind) MapReadOutcome(
        AgentFileReadOutcome outcome) =>
        outcome switch
        {
            AgentFileReadOutcome.Succeeded => (AgentActionResultKind.Succeeded, (AgentActionFailureKind?)null),
            AgentFileReadOutcome.Cancelled => (AgentActionResultKind.Cancelled, AgentActionFailureKind.Indeterminate),
            AgentFileReadOutcome.TooLarge => (AgentActionResultKind.Failed, AgentActionFailureKind.BudgetExceeded),
            AgentFileReadOutcome.PathEscaped => (AgentActionResultKind.Failed, AgentActionFailureKind.PathRejected),
            AgentFileReadOutcome.NotRegularFile => (AgentActionResultKind.Failed, AgentActionFailureKind.PathRejected),
            _ => (AgentActionResultKind.Failed, AgentActionFailureKind.ExecutionFailed),
        };

    private static AgentActionResult CreateDeniedResult(
        AgentActionPayload payload,
        AgentActionFailureKind failureKind,
        string summary)
    {
        _ = payload;
        return new AgentActionResult(
            AgentActionId.New(),
            AgentActionAttemptId.New(),
            AgentActionResultKind.Denied,
            failureKind,
            summary);
    }
}
