using System;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Immutable backend execution request for one admitted run attempt.
/// </summary>
internal sealed class AgentBackendRequest
{
    public AgentBackendRequest(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        ActorId initiatingActorId,
        ActorId targetActorId,
        ConversationEntryId messageEntryId,
        string messageText)
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

        if (messageEntryId == default)
        {
            throw new ArgumentException("Message entry id is required.", nameof(messageEntryId));
        }

        if (string.IsNullOrWhiteSpace(messageText))
        {
            throw new ArgumentException("Message text is required.", nameof(messageText));
        }

        SessionId = sessionId;
        RunId = runId;
        ConversationId = conversationId;
        InitiatingActorId = initiatingActorId;
        TargetActorId = targetActorId;
        MessageEntryId = messageEntryId;
        MessageText = messageText;
    }

    public AgentSessionId SessionId { get; }

    public ExecutionRunId RunId { get; }

    public ConversationId ConversationId { get; }

    public ActorId InitiatingActorId { get; }

    public ActorId TargetActorId { get; }

    public ConversationEntryId MessageEntryId { get; }

    public string MessageText { get; }
}
