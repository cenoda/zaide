using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Contracts.Continuity;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application.Continuity;

internal sealed class AgentSessionContinuityCoordinator : IAgentSessionContinuityCoordinator
{
    private readonly AgentSessionContinuityCheckpointWriter _checkpointWriter;
    private readonly AgentSessionContinuityInspector _inspector;
    private readonly AgentSessionContinuityRevalidator _revalidator;
    private readonly IAgentActorBackendBindingStore _bindingStore;
    private readonly IReadOnlyDictionary<AgentBackendId, IAgentBackendContinuityAdapter> _adapters;
    private readonly AgentDurableWorkspaceStorageKeyResolver _workspaceKeyResolver;
    private readonly IAgentDurableRecordStore _store;
    private readonly ConcurrentDictionary<string, AgentSessionId> _resumedSessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _processedOperationKeys = new(StringComparer.Ordinal);
    private readonly object _activeCheckpointSync = new();
    private readonly Dictionary<ConversationId, AgentSessionContinuityScope> _activeScopes = new();

    public AgentSessionContinuityCoordinator(
        AgentSessionContinuityCheckpointWriter checkpointWriter,
        AgentSessionContinuityInspector inspector,
        AgentSessionContinuityRevalidator revalidator,
        IAgentActorBackendBindingStore bindingStore,
        IEnumerable<IAgentBackendContinuityAdapter> adapters,
        AgentDurableWorkspaceStorageKeyResolver workspaceKeyResolver,
        IAgentDurableRecordStore store)
    {
        _checkpointWriter = checkpointWriter ?? throw new ArgumentNullException(nameof(checkpointWriter));
        _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
        _revalidator = revalidator ?? throw new ArgumentNullException(nameof(revalidator));
        _bindingStore = bindingStore ?? throw new ArgumentNullException(nameof(bindingStore));
        _adapters = (adapters ?? throw new ArgumentNullException(nameof(adapters)))
            .ToDictionary(adapter => adapter.BackendId);
        _workspaceKeyResolver = workspaceKeyResolver
            ?? throw new ArgumentNullException(nameof(workspaceKeyResolver));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public AgentSessionContinuityReconcileSummary Reconcile(AgentSessionContinuityReconcileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        _storeEnsureLoaded(request.WorkspaceKey);
        var summary = _inspector.GetInterruptedSessions(request.WorkspaceKey, request.WorkspaceRoot);
        var reclassified = new List<AgentSessionContinuityInterruptedSession>();

        foreach (var interrupted in summary.InterruptedSessions)
        {
            var classification = _revalidator.ClassifyCheckpoint(
                interrupted.LatestCheckpoint,
                request.WorkspaceRoot);

            var checkpoint = new AgentSessionContinuityCheckpoint(
                AgentSessionContinuityCheckpointPhase.AfterStartupReconcile,
                interrupted.Scope,
                classification,
                interrupted.LatestCheckpoint.SessionStatus,
                interrupted.LatestCheckpoint.RunStatus,
                AgentSessionContinuityLimits.PayloadSchemaVersion,
                interrupted.LatestCheckpoint.BindingFingerprint,
                interrupted.LatestCheckpoint.CapabilitySnapshotVersion,
                DateTimeOffset.UtcNow,
                interrupted.LatestCheckpoint.BackendSessionToken,
                interrupted.LatestCheckpoint.LateCompletionEvidence,
                interrupted.LatestCheckpoint.DisconnectEvidence,
                interrupted.LatestCheckpoint.AcknowledgementState,
                interrupted.LatestCheckpoint.LocalTerminationIntentAtUtc,
                interrupted.LatestCheckpoint.LocalProcessAcknowledgedAtUtc,
                interrupted.LatestCheckpoint.BackendAcknowledgedAtUtc);

            _checkpointWriter.TryWrite(
                checkpoint,
                AgentSessionContinuityOperationKind.Reconcile,
                BuildOperationKey(AgentSessionContinuityOperationKind.Reconcile, request.WorkspaceKey.Value, interrupted.Scope.SessionId.Value));

            reclassified.Add(new AgentSessionContinuityInterruptedSession(
                interrupted.Scope,
                classification,
                checkpoint,
                interrupted.ResumeAdmitted,
                interrupted.Terminated));
        }

        int recoverable = reclassified.Count(item => item.Classification == AgentSessionContinuityClassification.Recoverable);
        int terminal = summary.TerminalCount;
        int indeterminate = reclassified.Count(item => item.Classification == AgentSessionContinuityClassification.Indeterminate);

        return new AgentSessionContinuityReconcileSummary(
            recoverable,
            terminal,
            indeterminate,
            reclassified);
    }

    public AgentSessionContinuityOperationResult Resume(AgentSessionContinuityResumeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_processedOperationKeys.ContainsKey(request.IdempotencyKey))
        {
            return DuplicateResult(
                AgentSessionContinuityOperationKind.Resume,
                AgentSessionContinuityClassification.Recoverable,
                AgentSessionContinuityAcknowledgementState.None);
        }

        _storeEnsureLoaded(request.WorkspaceKey);
        var interrupted = _inspector
            .GetInterruptedSessions(request.WorkspaceKey, request.WorkspaceRoot)
            .InterruptedSessions
            .FirstOrDefault(item => item.Scope.SessionId == request.SessionId);

        if (interrupted is null)
        {
            return Rejected(
                AgentSessionContinuityOperationKind.Resume,
                AgentSessionContinuityClassification.Indeterminate,
                AgentSessionContinuityAcknowledgementState.None,
                "No interrupted session checkpoint exists.");
        }

        if (interrupted.Scope.ConversationId != request.ConversationId
            || interrupted.Scope.ActorId != request.ActorId
            || interrupted.Scope.BackendId != request.BackendId)
        {
            return Rejected(
                AgentSessionContinuityOperationKind.Resume,
                AgentSessionContinuityClassification.Indeterminate,
                AgentSessionContinuityAcknowledgementState.None,
                "Resume identity mismatch.");
        }

        var classification = _revalidator.ClassifyCheckpoint(
            interrupted.LatestCheckpoint,
            request.WorkspaceRoot);

        if (classification != AgentSessionContinuityClassification.Recoverable)
        {
            return new AgentSessionContinuityOperationResult(
                AgentSessionContinuityOperationStatus.Indeterminate,
                AgentSessionContinuityOperationKind.Resume,
                classification,
                AgentSessionContinuityAcknowledgementState.None,
                reason: "Session is not recoverable from current evidence.");
        }

        if (!_bindingStore.TryGetBinding(request.ActorId, out var binding)
            || binding.BackendId != request.BackendId)
        {
            return Rejected(
                AgentSessionContinuityOperationKind.Resume,
                AgentSessionContinuityClassification.Indeterminate,
                AgentSessionContinuityAcknowledgementState.None,
                "Actor/backend binding is missing or mismatched.");
        }

        var adapter = _adapters[request.BackendId];
        var capability = adapter.GetCapabilityRow();
        if (!capability.CheckpointSupported)
        {
            return Rejected(
                AgentSessionContinuityOperationKind.Resume,
                AgentSessionContinuityClassification.Indeterminate,
                AgentSessionContinuityAcknowledgementState.None,
                "Backend does not support continuity checkpoints.");
        }

        var probe = adapter.ProbeBackendSession(new AgentBackendContinuityProbeRequest(
            interrupted.LatestCheckpoint.BackendSessionToken,
            interrupted.LatestCheckpoint.BindingFingerprint));

        var now = DateTimeOffset.UtcNow;
        var checkpoint = new AgentSessionContinuityCheckpoint(
            AgentSessionContinuityCheckpointPhase.AfterSessionReady,
            interrupted.Scope,
            AgentSessionContinuityClassification.Recoverable,
            AgentSessionStatus.Ready,
            runStatus: null,
            AgentSessionContinuityLimits.PayloadSchemaVersion,
            interrupted.LatestCheckpoint.BindingFingerprint,
            interrupted.LatestCheckpoint.CapabilitySnapshotVersion + 1,
            now,
            interrupted.LatestCheckpoint.BackendSessionToken,
            acknowledgementState: probe.AcknowledgementState);

        var writeResult = _checkpointWriter.TryWrite(
            checkpoint,
            AgentSessionContinuityOperationKind.Resume,
            request.IdempotencyKey);

        if (writeResult.Status == AgentSessionContinuityOperationStatus.Accepted
            || writeResult.Status == AgentSessionContinuityOperationStatus.DuplicateIgnored)
        {
            _processedOperationKeys.TryAdd(request.IdempotencyKey, 0);
            _resumedSessions[request.ConversationId.Value] = request.SessionId;
        }

        return writeResult;
    }

    public AgentSessionContinuityOperationResult Terminate(AgentSessionContinuityTerminateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_processedOperationKeys.ContainsKey(request.IdempotencyKey))
        {
            return DuplicateResult(
                request.TerminationKind,
                AgentSessionContinuityClassification.Terminal,
                AgentSessionContinuityAcknowledgementState.LocalIntentRecorded);
        }

        _storeEnsureLoaded(request.WorkspaceKey);
        var interrupted = _inspector
            .GetInterruptedSessions(request.WorkspaceKey, request.WorkspaceRoot)
            .InterruptedSessions
            .FirstOrDefault(item => item.Scope.SessionId == request.SessionId);

        var scope = interrupted?.Scope
            ?? new AgentSessionContinuityScope(
                request.ActorId,
                request.ConversationId,
                request.SessionId,
                runId: null,
                request.BackendId,
                request.WorkspaceKey,
                request.WorkspaceRoot);

        if (!_adapters.TryGetValue(request.BackendId, out var adapter))
        {
            return Rejected(
                request.TerminationKind,
                AgentSessionContinuityClassification.Indeterminate,
                AgentSessionContinuityAcknowledgementState.None,
                "Backend continuity adapter is not registered.");
        }

        var probe = adapter.ProbeBackendSession(new AgentBackendContinuityProbeRequest(
            interrupted?.LatestCheckpoint.BackendSessionToken,
            interrupted?.LatestCheckpoint.BindingFingerprint
            ?? AgentSessionContinuityBindingFingerprint.Compute(
                request.ActorId,
                request.BackendId,
                request.WorkspaceRoot)));

        var now = DateTimeOffset.UtcNow;
        var acknowledgement = AgentSessionContinuityAcknowledgementState.LocalIntentRecorded;
        DateTimeOffset? localProcessAck = now;
        DateTimeOffset? backendAck = null;

        if (probe.AcknowledgementState == AgentSessionContinuityAcknowledgementState.BackendAcknowledged)
        {
            acknowledgement = AgentSessionContinuityAcknowledgementState.BackendAcknowledged;
            backendAck = now;
        }
        else if (probe.AcknowledgementState
            == AgentSessionContinuityAcknowledgementState.BackendAcknowledgementUnavailable)
        {
            acknowledgement = AgentSessionContinuityAcknowledgementState.BackendAcknowledgementUnavailable;
            localProcessAck = now;
        }
        else if (adapter.GetCapabilityRow().TerminateAckSupported)
        {
            acknowledgement = AgentSessionContinuityAcknowledgementState.ProviderDeletionUnverified;
        }
        else
        {
            acknowledgement = AgentSessionContinuityAcknowledgementState.LocalProcessAcknowledged;
        }

        var checkpoint = new AgentSessionContinuityCheckpoint(
            AgentSessionContinuityCheckpointPhase.AfterRunTerminal,
            scope,
            AgentSessionContinuityClassification.Terminal,
            AgentSessionStatus.Ended,
            runStatus: interrupted?.LatestCheckpoint.RunStatus,
            AgentSessionContinuityLimits.PayloadSchemaVersion,
            interrupted?.LatestCheckpoint.BindingFingerprint
            ?? AgentSessionContinuityBindingFingerprint.Compute(
                request.ActorId,
                request.BackendId,
                request.WorkspaceRoot),
            interrupted?.LatestCheckpoint.CapabilitySnapshotVersion ?? 1,
            now,
            interrupted?.LatestCheckpoint.BackendSessionToken,
            interrupted?.LatestCheckpoint.LateCompletionEvidence,
            interrupted?.LatestCheckpoint.DisconnectEvidence,
            acknowledgement,
            localTerminationIntentAtUtc: now,
            localProcessAcknowledgedAtUtc: localProcessAck,
            backendAcknowledgedAtUtc: backendAck);

        var writeResult = _checkpointWriter.TryWrite(
            checkpoint,
            request.TerminationKind,
            request.IdempotencyKey);

        if (writeResult.Status == AgentSessionContinuityOperationStatus.Accepted
            || writeResult.Status == AgentSessionContinuityOperationStatus.DuplicateIgnored)
        {
            _processedOperationKeys.TryAdd(request.IdempotencyKey, 0);
            _resumedSessions.TryRemove(request.ConversationId.Value, out _);
        }

        return writeResult;
    }

    public bool TryGetResumedSessionId(
        ConversationId conversationId,
        out AgentSessionId sessionId) =>
        _resumedSessions.TryGetValue(conversationId.Value, out sessionId!);

    public bool TryGetActiveScope(
        ConversationId conversationId,
        out AgentSessionContinuityScope scope)
    {
        lock (_activeCheckpointSync)
        {
            return _activeScopes.TryGetValue(conversationId, out scope!);
        }
    }

    public void RecordCheckpoint(AgentSessionContinuityCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        lock (_activeCheckpointSync)
        {
            _activeScopes[checkpoint.Scope.ConversationId] = checkpoint.Scope;
        }

        _checkpointWriter.TryWrite(
            checkpoint,
            AgentSessionContinuityOperationKind.Checkpoint,
            AgentSessionContinuityCheckpointWriter.BuildCheckpointIdempotencyKey(
                checkpoint.Phase,
                checkpoint.Scope));
    }

    public void CheckpointActiveSessions(string workspaceRoot)
    {
        var workspaceKey = _workspaceKeyResolver.Resolve(workspaceRoot);
        List<AgentSessionContinuityScope> scopes;
        lock (_activeCheckpointSync)
        {
            scopes = _activeScopes.Values.ToList();
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var scope in scopes)
        {
            if (!string.Equals(scope.WorkspaceRoot, workspaceRoot, StringComparison.Ordinal))
            {
                continue;
            }

            if (!_bindingStore.TryGetBinding(scope.ActorId, out var binding))
            {
                continue;
            }

            var fingerprint = AgentSessionContinuityBindingFingerprint.Compute(
                scope.ActorId,
                scope.BackendId,
                workspaceRoot,
                binding.AcpRuntime?.ExecutablePath,
                binding.ExpectedAgentName,
                binding.ExpectedAgentVersion);

            var checkpoint = new AgentSessionContinuityCheckpoint(
                AgentSessionContinuityCheckpointPhase.BeforeApplicationShutdown,
                scope,
                AgentSessionContinuityClassification.Recoverable,
                AgentSessionStatus.Running,
                AgentRunStatus.Running,
                AgentSessionContinuityLimits.PayloadSchemaVersion,
                fingerprint,
                capabilitySnapshotVersion: 1,
                now);

            RecordCheckpoint(checkpoint);
        }

        _storeEnsureLoaded(workspaceKey);
    }

    private void _storeEnsureLoaded(AgentDurableWorkspaceStorageKey workspaceKey) =>
        _ = _store.LoadWorkspace(workspaceKey);

    private static string BuildOperationKey(
        AgentSessionContinuityOperationKind operation,
        string workspaceKey,
        string sessionId) =>
        $"continuity:{operation}:{workspaceKey}:{sessionId}";

    private static AgentSessionContinuityOperationResult DuplicateResult(
        AgentSessionContinuityOperationKind operation,
        AgentSessionContinuityClassification classification,
        AgentSessionContinuityAcknowledgementState acknowledgement) =>
        new(
            AgentSessionContinuityOperationStatus.DuplicateIgnored,
            operation,
            classification,
            acknowledgement,
            reason: "Operation already processed in this coordinator lifetime.");

    private static AgentSessionContinuityOperationResult Rejected(
        AgentSessionContinuityOperationKind operation,
        AgentSessionContinuityClassification classification,
        AgentSessionContinuityAcknowledgementState acknowledgement,
        string reason) =>
        new(
            AgentSessionContinuityOperationStatus.Rejected,
            operation,
            classification,
            acknowledgement,
            reason: reason);
}
