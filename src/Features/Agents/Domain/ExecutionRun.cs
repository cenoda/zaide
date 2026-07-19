using System;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Minimum in-memory representation of one admitted send/routed execution
/// attempt from admission to a single terminal outcome.
/// </summary>
public sealed class ExecutionRun
{
    public ExecutionRun(
        ExecutionRunId id,
        ConversationId conversationId,
        ActorId initiatingActorId,
        ActorId targetActorId,
        string targetPanelId,
        ExecutionRunOutcome outcome)
    {
        if (id == default)
        {
            throw new ArgumentException("Execution run id is required.", nameof(id));
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

        if (string.IsNullOrWhiteSpace(targetPanelId))
        {
            throw new ArgumentException("Target panel id is required.", nameof(targetPanelId));
        }

        Id = id;
        ConversationId = conversationId;
        InitiatingActorId = initiatingActorId;
        TargetActorId = targetActorId;
        TargetPanelId = targetPanelId;
        Outcome = outcome;
    }

    public ExecutionRunId Id { get; }

    public ConversationId ConversationId { get; }

    public ActorId InitiatingActorId { get; }

    public ActorId TargetActorId { get; }

    public string TargetPanelId { get; }

    public ExecutionRunOutcome Outcome { get; }
}
