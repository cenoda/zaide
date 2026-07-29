using System;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Contracts.Continuity;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application.Continuity;

/// <summary>
/// Subscribes to normalized agent events and records durable continuity
/// checkpoints without blocking the event pipeline.
/// </summary>
internal sealed class AgentSessionContinuityEventSubscriber : IDisposable
{
    private readonly IDisposable _subscription;
    private readonly IAgentSessionContinuityCoordinator _coordinator;

    public AgentSessionContinuityEventSubscriber(
        AgentEventStream eventStream,
        IAgentSessionContinuityCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(eventStream);
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _subscription = eventStream.Events.Subscribe(OnEvent);
    }

    public void Dispose() => _subscription.Dispose();

    private void OnEvent(AgentEvent agentEvent)
    {
        try
        {
            HandleEvent(agentEvent);
        }
        catch
        {
        }
    }

    private void HandleEvent(AgentEvent agentEvent)
    {
        if (!_coordinator.TryGetActiveScope(agentEvent.ConversationId, out var scope))
        {
            return;
        }

        var (phase, sessionStatus, runStatus, classification) = MapEvent(agentEvent);
        if (phase is null)
        {
            return;
        }

        var updatedScope = new AgentSessionContinuityScope(
            scope.ActorId,
            scope.ConversationId,
            scope.SessionId,
            agentEvent.RunId,
            scope.BackendId,
            scope.WorkspaceKey,
            scope.WorkspaceRoot);

        var checkpoint = new AgentSessionContinuityCheckpoint(
            phase.Value,
            updatedScope,
            classification,
            sessionStatus,
            runStatus,
            AgentSessionContinuityLimits.PayloadSchemaVersion,
            AgentSessionContinuityBindingFingerprint.Compute(
                scope.ActorId,
                scope.BackendId,
                scope.WorkspaceRoot),
            capabilitySnapshotVersion: 1,
            DateTimeOffset.UtcNow,
            disconnectEvidence: agentEvent.Kind == AgentEventKind.RunDisconnected
                ? "backend-disconnect-observed"
                : null,
            lateCompletionEvidence: agentEvent.Kind == AgentEventKind.RunCompleted
                ? "late-completion-observed"
                : null);

        _coordinator.RecordCheckpoint(checkpoint);
    }

    private static (
        AgentSessionContinuityCheckpointPhase? Phase,
        AgentSessionStatus SessionStatus,
        AgentRunStatus? RunStatus,
        AgentSessionContinuityClassification Classification) MapEvent(AgentEvent agentEvent)
    {
        return agentEvent.Kind switch
        {
            AgentEventKind.SessionReady => (
                AgentSessionContinuityCheckpointPhase.AfterSessionReady,
                AgentSessionStatus.Ready,
                null,
                AgentSessionContinuityClassification.Recoverable),
            AgentEventKind.RunAccepted => (
                AgentSessionContinuityCheckpointPhase.BeforeRunStart,
                AgentSessionStatus.Running,
                AgentRunStatus.Accepted,
                AgentSessionContinuityClassification.Recoverable),
            AgentEventKind.RunRunning => (
                AgentSessionContinuityCheckpointPhase.BeforeRunStart,
                AgentSessionStatus.Running,
                AgentRunStatus.Running,
                AgentSessionContinuityClassification.Recoverable),
            AgentEventKind.RunCompleted => (
                AgentSessionContinuityCheckpointPhase.AfterRunTerminal,
                AgentSessionStatus.Ready,
                AgentRunStatus.Completed,
                AgentSessionContinuityClassification.Terminal),
            AgentEventKind.RunFailed => (
                AgentSessionContinuityCheckpointPhase.AfterRunTerminal,
                AgentSessionStatus.Ready,
                AgentRunStatus.Failed,
                AgentSessionContinuityClassification.Indeterminate),
            AgentEventKind.RunCancelled => (
                AgentSessionContinuityCheckpointPhase.AfterRunTerminal,
                AgentSessionStatus.Ready,
                AgentRunStatus.Cancelled,
                AgentSessionContinuityClassification.Terminal),
            AgentEventKind.RunTimedOut => (
                AgentSessionContinuityCheckpointPhase.AfterRunTerminal,
                AgentSessionStatus.Ready,
                AgentRunStatus.TimedOut,
                AgentSessionContinuityClassification.Indeterminate),
            AgentEventKind.RunDisconnected => (
                AgentSessionContinuityCheckpointPhase.AfterRunTerminal,
                AgentSessionStatus.Ready,
                AgentRunStatus.Disconnected,
                AgentSessionContinuityClassification.Indeterminate),
            AgentEventKind.RunIndeterminate => (
                AgentSessionContinuityCheckpointPhase.AfterRunTerminal,
                AgentSessionStatus.Ready,
                AgentRunStatus.Indeterminate,
                AgentSessionContinuityClassification.Indeterminate),
            AgentEventKind.SessionEnded => (
                AgentSessionContinuityCheckpointPhase.AfterRunTerminal,
                AgentSessionStatus.Ended,
                null,
                AgentSessionContinuityClassification.Terminal),
            _ => (null, AgentSessionStatus.Ready, null, AgentSessionContinuityClassification.Indeterminate),
        };
    }
}
