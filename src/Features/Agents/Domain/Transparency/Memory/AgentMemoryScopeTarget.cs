using System;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain.Transparency.Memory;

/// <summary>
/// Scope owner identifiers for one memory record.
/// </summary>
internal sealed class AgentMemoryScopeTarget
{
    public AgentMemoryScopeTarget(
        AgentMemoryScope scope,
        string? sessionId = null,
        ActorId? actorId = null,
        ConversationId? conversationId = null,
        string? projectId = null)
    {
        Scope = scope;

        switch (scope)
        {
            case AgentMemoryScope.Session when string.IsNullOrWhiteSpace(sessionId):
                throw new ArgumentException("Session scope requires session id.", nameof(sessionId));
            case AgentMemoryScope.Agent when actorId is null || actorId == default:
                throw new ArgumentException("Agent scope requires actor id.", nameof(actorId));
            case AgentMemoryScope.Conversation when conversationId is null || conversationId == default:
                throw new ArgumentException("Conversation scope requires conversation id.", nameof(conversationId));
            case AgentMemoryScope.ProjectShared when string.IsNullOrWhiteSpace(projectId):
                throw new ArgumentException("Project/shared scope requires project id.", nameof(projectId));
        }

        SessionId = sessionId is null ? null : AgentSessionId.FromValue(sessionId);
        ActorId = actorId;
        ConversationId = conversationId;
        ProjectId = projectId;
    }

    public AgentMemoryScope Scope { get; }

    public AgentSessionId? SessionId { get; }

    public ActorId? ActorId { get; }

    public ConversationId? ConversationId { get; }

    public string? ProjectId { get; }
}
