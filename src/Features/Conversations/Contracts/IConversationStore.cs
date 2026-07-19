using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Conversations.Contracts;

/// <summary>
/// Narrow application-facing contract for authoritative in-memory conversations.
/// </summary>
public interface IConversationStore
{
    Conversation CreateChannelConversation(string channelId);

    Conversation CreateDirectConversation(ActorId participantOne, ActorId participantTwo);

    bool TryGet(ConversationId id, out Conversation conversation);

    bool TryGetChannelConversation(string channelId, out Conversation conversation);
}
