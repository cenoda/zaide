using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Contracts.Transparency.Trace;
using Zaide.Features.Agents.Contracts.Transparency.Usage;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Trace;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Workspace.Contracts;

namespace Zaide.Features.Agents.Infrastructure;

/// <summary>
/// Phase 19 Native Harness backend implementing the tool-calling execution loop.
/// </summary>
internal sealed class NativeHarnessAgentBackend : IAgentActionRequestCapableBackend
{
    internal const string BackendVersionValue = "zaide-native-harness/1";

    private readonly NativeHarnessLoopRunner _loopRunner;
    private readonly INativeHarnessProviderOptionsSource _optionsSource;
    private readonly IWorkspaceActionAuthority? _workspaceAuthority;
    private readonly IAgentTraceBackendEvidenceSource? _traceSource;
    private readonly IAgentUsageBackendEvidenceSource? _usageSource;
    private readonly AgentDurableWorkspaceStorageKeyResolver? _workspaceKeyResolver;
    private readonly object _capabilitySync = new();
    private AgentCapabilitySnapshot _capabilitySnapshot;
    private bool _capabilityInitialized;
    private CapabilityObservationState _capabilityObservationState;

    private enum CapabilityObservationState
    {
        Unconfigured,
        ConfiguredWithoutWorkspace,
        ConfiguredWithWorkspace,
        ResolutionUnavailable,
    }

    public NativeHarnessAgentBackend(
        AgentExecutionService executionService,
        INativeHarnessProviderTransport transport,
        INativeHarnessPriorConversationReader priorConversationReader,
        IWorkspaceActionAuthority? workspaceAuthority = null,
        IAgentTraceBackendEvidenceSource? traceSource = null,
        AgentDurableWorkspaceStorageKeyResolver? workspaceKeyResolver = null,
        IAgentUsageBackendEvidenceSource? usageSource = null)
        : this(
            new NativeHarnessProviderOptionsSource(executionService),
            transport,
            priorConversationReader,
            workspaceAuthority,
            traceSource,
            workspaceKeyResolver,
            usageSource)
    {
    }

    public NativeHarnessAgentBackend(
        INativeHarnessProviderOptionsSource optionsSource,
        INativeHarnessProviderTransport transport,
        INativeHarnessPriorConversationReader priorConversationReader,
        IWorkspaceActionAuthority? workspaceAuthority = null,
        IAgentTraceBackendEvidenceSource? traceSource = null,
        AgentDurableWorkspaceStorageKeyResolver? workspaceKeyResolver = null,
        IAgentUsageBackendEvidenceSource? usageSource = null)
    {
        _optionsSource = optionsSource
            ?? throw new ArgumentNullException(nameof(optionsSource));
        _workspaceAuthority = workspaceAuthority;
        _traceSource = traceSource?.BackendId == AgentBackendIds.NativeHarnessValue
            ? traceSource
            : null;
        _usageSource = usageSource?.BackendId == AgentBackendIds.NativeHarnessValue
            ? usageSource
            : null;
        _workspaceKeyResolver = workspaceKeyResolver;
        _loopRunner = new NativeHarnessLoopRunner(
            optionsSource,
            transport ?? throw new ArgumentNullException(nameof(transport)),
            priorConversationReader
                ?? throw new ArgumentNullException(nameof(priorConversationReader)));

        _capabilitySnapshot = CreateSnapshotForObservation(
            CapabilityObservationState.Unconfigured,
            version: 1);
    }

    internal NativeHarnessAgentBackend(NativeHarnessLoopRunner loopRunner)
    {
        _loopRunner = loopRunner ?? throw new ArgumentNullException(nameof(loopRunner));
        _optionsSource = new NullNativeHarnessProviderOptionsSource();
        _capabilitySnapshot = CreateSnapshotForObservation(
            CapabilityObservationState.Unconfigured,
            version: 1);
    }

    public AgentBackendId BackendId => AgentBackendIds.NativeHarness;

    public string BackendVersion => BackendVersionValue;

    public AgentCapabilitySnapshot CapabilitySnapshot
    {
        get
        {
            lock (_capabilitySync)
            {
                RefreshCapabilitySnapshotLocked();
                return _capabilitySnapshot;
            }
        }
    }

    public async IAsyncEnumerable<AgentBackendEvent> ExecuteAsync(
        AgentBackendExecutionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        NativeHarnessRunOutcome? outcome = null;
        AgentBackendEvent? faultEvent = null;
        var startedAtUtc = DateTimeOffset.UtcNow;
        TryCaptureTrace(context, AgentTraceKind.Request, "request", context.Request.MessageText);
        try
        {
            outcome = await _loopRunner.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            faultEvent = CreateFailureEvent(
                AgentFailureKind.Cancellation,
                "Run was cancelled.");
        }
        catch (Exception ex)
        {
            faultEvent = CreateFailureEvent(
                AgentFailureKind.Indeterminate,
                $"Harness execution failed: {ex.GetType().Name}");
        }

        var latencyMs = (decimal)Math.Max(
            0,
            (DateTimeOffset.UtcNow - startedAtUtc).TotalMilliseconds);
        TryCaptureMeasuredUsage(context, latencyMs);

        if (faultEvent is not null)
        {
            TryCaptureTrace(context, AgentTraceKind.Error, "failure", ((AgentBackendFailurePayload)faultEvent.Payload).Reason);
            yield return faultEvent;
            yield break;
        }

        foreach (var backendEvent in MapOutcome(outcome!))
        {
            TryCaptureTraceForEvent(context, backendEvent);
            yield return backendEvent;
        }
    }

    private void RefreshCapabilitySnapshotLocked()
    {
        try
        {
            var options = _optionsSource.ResolveOptions();
            var configured = options is not null && IsConfigured(options);
            if (!configured)
            {
                ApplyObservationStateLocked(CapabilityObservationState.Unconfigured);
                return;
            }

            var workspaceCaptured = _workspaceAuthority?.TryCaptureCurrentScope(out _) ?? false;
            ApplyObservationStateLocked(
                workspaceCaptured
                    ? CapabilityObservationState.ConfiguredWithWorkspace
                    : CapabilityObservationState.ConfiguredWithoutWorkspace);
        }
        catch
        {
            ApplyObservationStateLocked(CapabilityObservationState.ResolutionUnavailable);
        }
    }

    private void ApplyObservationStateLocked(CapabilityObservationState observation)
    {
        if (!_capabilityInitialized)
        {
            _capabilityObservationState = observation;
            _capabilitySnapshot = CreateSnapshotForObservation(observation, version: 1);
            _capabilityInitialized = true;
            return;
        }

        if (_capabilityObservationState == observation)
        {
            return;
        }

        _capabilityObservationState = observation;
        _capabilitySnapshot = CreateSnapshotForObservation(
            observation,
            version: _capabilitySnapshot.Version + 1);
    }

    private static AgentCapabilitySnapshot CreateSnapshotForObservation(
        CapabilityObservationState observation,
        int version)
    {
        return observation switch
        {
            CapabilityObservationState.Unconfigured =>
                NativeHarnessCapabilityRows.CreateInitialSnapshot(
                    providerConfigured: false,
                    workspaceCaptured: false,
                    contextManifestPresent: false,
                    streamingSupportedByProvider: true,
                    version: version),
            CapabilityObservationState.ConfiguredWithoutWorkspace =>
                NativeHarnessCapabilityRows.CreateInitialSnapshot(
                    providerConfigured: true,
                    workspaceCaptured: false,
                    contextManifestPresent: false,
                    streamingSupportedByProvider: true,
                    version: version),
            CapabilityObservationState.ConfiguredWithWorkspace =>
                NativeHarnessCapabilityRows.CreateInitialSnapshot(
                    providerConfigured: true,
                    workspaceCaptured: true,
                    contextManifestPresent: false,
                    streamingSupportedByProvider: true,
                    version: version),
            CapabilityObservationState.ResolutionUnavailable =>
                NativeHarnessCapabilityRows.CreateResolutionUnavailableSnapshot(version: version),
            _ => throw new ArgumentOutOfRangeException(nameof(observation), observation, null),
        };
    }

    private static bool IsConfigured(AgentExecutionOptions options) =>
        !string.IsNullOrWhiteSpace(options.ApiKey)
        && !string.IsNullOrWhiteSpace(options.BaseUrl)
        && !string.IsNullOrWhiteSpace(options.Model);

    private static IEnumerable<AgentBackendEvent> MapOutcome(NativeHarnessRunOutcome outcome)
    {
        var occurredAtUtc = DateTimeOffset.UtcNow;
        switch (outcome.TerminationKind)
        {
            case NativeHarnessRunTerminationKind.Completed:
                yield return new AgentBackendEvent(
                    AgentBackendEventKind.MessageCompleted,
                    occurredAtUtc,
                    new AgentBackendMessageCompletedPayload(outcome.FinalAssistantText!));
                yield break;
            case NativeHarnessRunTerminationKind.Cancelled:
                yield return new AgentBackendEvent(
                    AgentBackendEventKind.FailureObserved,
                    occurredAtUtc,
                    new AgentBackendFailurePayload(
                        AgentFailureKind.Cancellation,
                        outcome.FailureReason ?? "Run was cancelled."));
                yield break;
            case NativeHarnessRunTerminationKind.TurnBudgetExceeded:
                yield return new AgentBackendEvent(
                    AgentBackendEventKind.FailureObserved,
                    occurredAtUtc,
                    new AgentBackendFailurePayload(
                        AgentFailureKind.Execution,
                        outcome.FailureReason ?? "Model turn budget exceeded."));
                yield break;
            case NativeHarnessRunTerminationKind.Indeterminate:
                yield return new AgentBackendEvent(
                    AgentBackendEventKind.FailureObserved,
                    occurredAtUtc,
                    new AgentBackendFailurePayload(
                        AgentFailureKind.Indeterminate,
                        outcome.FailureReason ?? "Run outcome was indeterminate."));
                yield break;
            case NativeHarnessRunTerminationKind.Failed:
            default:
                yield return new AgentBackendEvent(
                    AgentBackendEventKind.FailureObserved,
                    occurredAtUtc,
                    new AgentBackendFailurePayload(
                        AgentFailureKind.Execution,
                        outcome.FailureReason ?? "Harness execution failed."));
                yield break;
        }
    }

    private static AgentBackendEvent CreateFailureEvent(AgentFailureKind failureKind, string reason) =>
        new(
            AgentBackendEventKind.FailureObserved,
            DateTimeOffset.UtcNow,
            new AgentBackendFailurePayload(failureKind, reason));

    private void TryCaptureTraceForEvent(
        AgentBackendExecutionContext context,
        AgentBackendEvent backendEvent)
    {
        switch (backendEvent.Payload)
        {
            case AgentBackendMessageCompletedPayload completed:
                TryCaptureTrace(context, AgentTraceKind.Response, "response", completed.AssistantText);
                break;
            case AgentBackendFailurePayload failure:
                TryCaptureTrace(context, AgentTraceKind.Error, "failure", failure.Reason);
                break;
        }
    }

    private void TryCaptureTrace(
        AgentBackendExecutionContext context,
        AgentTraceKind kind,
        string kindLabel,
        string publicText)
    {
        if (_traceSource is null
            || _workspaceKeyResolver is null
            || _workspaceAuthority?.TryCaptureCurrentScope(out var workspaceScope) != true)
        {
            return;
        }

        var capturedAtUtc = DateTimeOffset.UtcNow;
        _ = _traceSource.Submit(new AgentTraceCaptureRequest(
            _workspaceKeyResolver.Resolve(workspaceScope.RootPath),
            AgentBackendIds.NativeHarnessValue,
            kind,
            AgentTraceEvidenceLevel.BackendExecutedAndReported,
            NativeHarnessAgentTraceSource.SerializeLoopHistoryTurn(
                AgentBackendIds.NativeHarnessValue,
                kindLabel,
                turnIndex: 0,
                recordedAtUtc: capturedAtUtc,
                publicText: publicText),
            new AgentTraceRecordScope(
                context.Request.ConversationId.ToString(),
                context.Request.SessionId.ToString(),
                context.Request.RunId.ToString(),
                AgentBackendIds.NativeHarnessValue),
            idempotencyKey: $"trace:native:{context.Request.RunId}:{kindLabel}",
            capturedAtUtc: capturedAtUtc));
    }

    /// <summary>
    /// Publishes only Zaide-measured request count/latency and explicit
    /// unavailable token/cost markers. Provider responses do not expose tokens
    /// or prices; never invent them.
    /// </summary>
    private void TryCaptureMeasuredUsage(AgentBackendExecutionContext context, decimal latencyMs)
    {
        if (_usageSource is null
            || _workspaceKeyResolver is null
            || _workspaceAuthority?.TryCaptureCurrentScope(out var workspaceScope) != true)
        {
            return;
        }

        var workspaceKey = _workspaceKeyResolver.Resolve(workspaceScope.RootPath);
        var scope = new AgentUsageRecordScope(
            context.Request.ConversationId.ToString(),
            context.Request.SessionId.ToString(),
            context.Request.RunId.ToString(),
            AgentBackendIds.NativeHarnessValue);
        string? model = null;
        try
        {
            model = _optionsSource.ResolveOptions()?.Model;
        }
        catch
        {
            model = null;
        }

        var capturedAtUtc = DateTimeOffset.UtcNow;
        var runId = context.Request.RunId.ToString();

        _ = _usageSource.Submit(new AgentUsageCaptureRequest(
            workspaceKey,
            AgentBackendIds.NativeHarnessValue,
            AgentUsageKind.RequestCount,
            AgentUsageValueOrigin.Measured,
            "requests",
            "count",
            value: 1,
            scope,
            model: model,
            evidenceSourceDescription: "Zaide-measured native-harness request count (delta).",
            idempotencyKey: $"usage:native:{runId}:request-count",
            capturedAtUtc: capturedAtUtc,
            aggregationSemantics: AgentUsageAggregationSemantics.Delta));

        _ = _usageSource.Submit(new AgentUsageCaptureRequest(
            workspaceKey,
            AgentBackendIds.NativeHarnessValue,
            AgentUsageKind.LatencyMs,
            AgentUsageValueOrigin.Measured,
            "latency",
            "ms",
            value: latencyMs,
            scope,
            model: model,
            evidenceSourceDescription: "Zaide-measured native-harness wall-clock latency (point-in-time).",
            idempotencyKey: $"usage:native:{runId}:latency",
            capturedAtUtc: capturedAtUtc,
            aggregationSemantics: AgentUsageAggregationSemantics.PointInTime));

        _ = _usageSource.Submit(new AgentUsageCaptureRequest(
            workspaceKey,
            AgentBackendIds.NativeHarnessValue,
            AgentUsageKind.TotalTokens,
            AgentUsageValueOrigin.Unavailable,
            "tokens",
            "count",
            value: 0,
            scope,
            model: model,
            evidenceSourceDescription:
                "Native harness provider response does not expose token counts.",
            idempotencyKey: $"usage:native:{runId}:tokens-unavailable",
            capturedAtUtc: capturedAtUtc,
            aggregationSemantics: AgentUsageAggregationSemantics.PointInTime));

        _ = _usageSource.Submit(new AgentUsageCaptureRequest(
            workspaceKey,
            AgentBackendIds.NativeHarnessValue,
            AgentUsageKind.TotalCost,
            AgentUsageValueOrigin.Unavailable,
            "cost",
            "currency",
            value: 0,
            scope,
            model: model,
            evidenceSourceDescription:
                "Native harness provider response does not expose cost; pricing is unavailable.",
            idempotencyKey: $"usage:native:{runId}:cost-unavailable",
            capturedAtUtc: capturedAtUtc,
            aggregationSemantics: AgentUsageAggregationSemantics.PointInTime));
    }

    private sealed class NullNativeHarnessProviderOptionsSource : INativeHarnessProviderOptionsSource
    {
        public AgentExecutionOptions? ResolveOptions() => null;
    }
}
