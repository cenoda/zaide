using System;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// One bounded in-memory audit fact for the current application lifetime.
/// </summary>
internal sealed class AgentActionAuditRecord
{
    public AgentActionAuditRecord(
        AgentEventId eventId,
        AgentEventKind eventKind,
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        AgentBackendId backendId,
        long sequence,
        DateTimeOffset occurredAtUtc,
        AgentActivityEvidenceLevel evidenceLevel,
        AgentActionId actionId,
        AgentActionAttemptId attemptId,
        AgentActionKind actionKind,
        WorkspaceIdentity workspaceIdentity,
        WorkspaceGeneration workspaceGeneration,
        AgentActionAuditSummary summary,
        AgentEventId? causationEventId = null)
    {
        if (eventId == default)
        {
            throw new ArgumentException("Event id is required.", nameof(eventId));
        }

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

        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Sequence must be positive.");
        }

        if (occurredAtUtc == default)
        {
            throw new ArgumentException("Occurred time is required.", nameof(occurredAtUtc));
        }

        if (actionId == default)
        {
            throw new ArgumentException("Action id is required.", nameof(actionId));
        }

        if (attemptId == default)
        {
            throw new ArgumentException("Attempt id is required.", nameof(attemptId));
        }

        if (workspaceIdentity == default)
        {
            throw new ArgumentException("Workspace identity is required.", nameof(workspaceIdentity));
        }

        if (workspaceGeneration == default)
        {
            throw new ArgumentException("Workspace generation is required.", nameof(workspaceGeneration));
        }

        ArgumentNullException.ThrowIfNull(summary);

        EventId = eventId;
        EventKind = eventKind;
        SessionId = sessionId;
        RunId = runId;
        ConversationId = conversationId;
        BackendId = backendId;
        Sequence = sequence;
        OccurredAtUtc = occurredAtUtc;
        EvidenceLevel = evidenceLevel;
        ActionId = actionId;
        AttemptId = attemptId;
        ActionKind = actionKind;
        WorkspaceIdentity = workspaceIdentity;
        WorkspaceGeneration = workspaceGeneration;
        Summary = summary;
        CausationEventId = causationEventId;
    }

    public AgentEventId EventId { get; }

    public AgentEventKind EventKind { get; }

    public AgentSessionId SessionId { get; }

    public ExecutionRunId RunId { get; }

    public ConversationId ConversationId { get; }

    public AgentBackendId BackendId { get; }

    public long Sequence { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public AgentActivityEvidenceLevel EvidenceLevel { get; }

    public AgentActionId ActionId { get; }

    public AgentActionAttemptId AttemptId { get; }

    public AgentActionKind ActionKind { get; }

    public WorkspaceIdentity WorkspaceIdentity { get; }

    public WorkspaceGeneration WorkspaceGeneration { get; }

    public AgentActionAuditSummary Summary { get; }

    public AgentEventId? CausationEventId { get; }
}
