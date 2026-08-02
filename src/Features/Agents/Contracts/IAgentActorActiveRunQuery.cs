using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Application boundary used to reject durable binding update/unbind while an
/// actor has an admitted in-flight run. Conversation-keyed coordinators remain
/// the source of busy truth; this projects that truth to ActorId.
/// </summary>
internal interface IAgentActorActiveRunQuery
{
    /// <summary>
    /// True when any live session for <paramref name="actorId"/> currently has a
    /// non-terminal active run.
    /// </summary>
    bool HasActiveRun(ActorId actorId);
}
