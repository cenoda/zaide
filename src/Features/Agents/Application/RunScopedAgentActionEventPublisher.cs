using System;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Run-scoped action event publisher that delegates ordering to the session
/// owner while recording bounded audit snapshots.
/// </summary>
internal sealed class RunScopedAgentActionEventPublisher : IAgentActionEventPublisher
{
    private readonly AgentSessionId _sessionId;
    private readonly ExecutionRunId _runId;
    private readonly ConversationId _conversationId;
    private readonly AgentBackendId _backendId;
    private readonly ActorId _initiatingActorId;
    private readonly ActorId _targetActorId;
    private readonly AgentEventStream _eventStream;
    private readonly IAgentActionAuditStore _auditStore;
    private readonly Func<long> _nextSequence;
    private readonly object _sequenceSync;

    public RunScopedAgentActionEventPublisher(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        AgentBackendId backendId,
        ActorId initiatingActorId,
        ActorId targetActorId,
        AgentEventStream eventStream,
        IAgentActionAuditStore auditStore,
        Func<long> nextSequence,
        object sequenceSync)
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

        if (backendId == default)
        {
            throw new ArgumentException("Backend id is required.", nameof(backendId));
        }

        if (initiatingActorId == default)
        {
            throw new ArgumentException("Initiating actor id is required.", nameof(initiatingActorId));
        }

        if (targetActorId == default)
        {
            throw new ArgumentException("Target actor id is required.", nameof(targetActorId));
        }

        _sessionId = sessionId;
        _runId = runId;
        _conversationId = conversationId;
        _backendId = backendId;
        _initiatingActorId = initiatingActorId;
        _targetActorId = targetActorId;
        _eventStream = eventStream ?? throw new ArgumentNullException(nameof(eventStream));
        _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
        _nextSequence = nextSequence ?? throw new ArgumentNullException(nameof(nextSequence));
        _sequenceSync = sequenceSync ?? throw new ArgumentNullException(nameof(sequenceSync));
    }

    public AgentEventId Publish(
        AgentEventKind kind,
        AgentActionFactPayload payload,
        AgentActivityEvidenceLevel evidenceLevel,
        AgentEventId? causationEventId = null)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var occurredAtUtc = DateTimeOffset.UtcNow;
        AgentEvent agentEvent;
        lock (_sequenceSync)
        {
            var receivedAtUtc = DateTimeOffset.UtcNow;
            if (receivedAtUtc < occurredAtUtc)
            {
                receivedAtUtc = occurredAtUtc;
            }

            agentEvent = new AgentEvent(
                AgentEventId.New(),
                AgentEvent.CurrentSchemaVersion,
                _sessionId,
                _runId,
                _conversationId,
                _backendId,
                _nextSequence(),
                occurredAtUtc,
                receivedAtUtc,
                causationEventId,
                evidenceLevel,
                kind,
                payload);
        }

        _eventStream.Publish(agentEvent);
        _auditStore.Record(new AgentActionAuditRecord(
            agentEvent.EventId,
            agentEvent.Kind,
            agentEvent.SessionId,
            agentEvent.RunId,
            agentEvent.ConversationId,
            agentEvent.BackendId,
            _initiatingActorId,
            _targetActorId,
            agentEvent.Sequence,
            agentEvent.OccurredAtUtc,
            agentEvent.EvidenceLevel,
            payload.ActionId,
            payload.AttemptId,
            payload.ActionKind,
            payload.WorkspaceIdentity,
            payload.WorkspaceGeneration,
            payload.Summary,
            agentEvent.CausationEventId));

        return agentEvent.EventId;
    }
}
