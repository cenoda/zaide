using System;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// One bounded in-memory audit fact for the current application lifetime.
/// </summary>
/// <remarks>
/// Workspace attribution is optional: when no workspace scope was captured,
/// both workspace fields are null. When a workspace was captured, both fields
/// carry the exact captured identity and generation.
/// </remarks>
internal sealed class AgentActionAuditRecord
{
    public AgentActionAuditRecord(
        AgentEventId eventId,
        AgentEventKind eventKind,
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        AgentBackendId backendId,
        ActorId initiatingActorId,
        ActorId targetActorId,
        long sequence,
        DateTimeOffset occurredAtUtc,
        AgentActivityEvidenceLevel evidenceLevel,
        AgentActionId actionId,
        AgentActionAttemptId attemptId,
        AgentActionKind actionKind,
        WorkspaceIdentity? workspaceIdentity,
        WorkspaceGeneration? workspaceGeneration,
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

        if (initiatingActorId == default)
        {
            throw new ArgumentException("Initiating actor id is required.", nameof(initiatingActorId));
        }

        if (targetActorId == default)
        {
            throw new ArgumentException("Target actor id is required.", nameof(targetActorId));
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

        if (workspaceIdentity is null != workspaceGeneration is null)
        {
            throw new ArgumentException(
                "Workspace identity and generation must both be present or both absent.");
        }

        if (workspaceIdentity is { } identity && identity == default)
        {
            throw new ArgumentException("Workspace identity is invalid.", nameof(workspaceIdentity));
        }

        if (workspaceGeneration is { } generation && generation == default)
        {
            throw new ArgumentException("Workspace generation is invalid.", nameof(workspaceGeneration));
        }

        ArgumentNullException.ThrowIfNull(summary);

        EventId = eventId;
        EventKind = eventKind;
        SessionId = sessionId;
        RunId = runId;
        ConversationId = conversationId;
        BackendId = backendId;
        InitiatingActorId = initiatingActorId;
        TargetActorId = targetActorId;
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

    public ActorId InitiatingActorId { get; }

    public ActorId TargetActorId { get; }

    public long Sequence { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public AgentActivityEvidenceLevel EvidenceLevel { get; }

    public AgentActionId ActionId { get; }

    public AgentActionAttemptId AttemptId { get; }

    public AgentActionKind ActionKind { get; }

    /// <summary>
    /// Captured workspace identity, or <c>null</c> when no workspace scope was captured.
    /// </summary>
    public WorkspaceIdentity? WorkspaceIdentity { get; }

    /// <summary>
    /// Captured workspace generation, or <c>null</c> when no workspace scope was captured.
    /// </summary>
    public WorkspaceGeneration? WorkspaceGeneration { get; }

    public AgentActionAuditSummary Summary { get; }

    public AgentEventId? CausationEventId { get; }
}
