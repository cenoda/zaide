using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Session-scoped IDE context policy override boundary. Overrides affect subsequent
/// admitted runs only and never mutate the application default.
/// </summary>
public interface IAgentContextSessionPolicyService
{
    AgentContextSessionPolicyState GetPolicyState(ConversationId conversationId);

    bool TrySetSessionOverride(
        ConversationId conversationId,
        AgentSessionContextPolicyLevel level);

    bool ClearSessionOverride(ConversationId conversationId);
}
