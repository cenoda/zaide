using System;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain.Transparency.Memory;

/// <summary>
/// Run-scoped identifiers used to evaluate memory scope eligibility.
/// </summary>
internal sealed class AgentMemoryRetrievalContext
{
    public AgentMemoryRetrievalContext(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        ActorId targetActorId,
        string? projectId = null)
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

        if (targetActorId == default)
        {
            throw new ArgumentException("Target actor id is required.", nameof(targetActorId));
        }

        SessionId = sessionId;
        RunId = runId;
        ConversationId = conversationId;
        TargetActorId = targetActorId;
        ProjectId = projectId;
    }

    public AgentSessionId SessionId { get; }

    public ExecutionRunId RunId { get; }

    public ConversationId ConversationId { get; }

    public ActorId TargetActorId { get; }

    public string? ProjectId { get; }
}
