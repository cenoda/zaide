using System;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Conversations.Contracts;

/// <summary>
/// Narrow application-facing contract for authoritative in-memory conversations.
/// </summary>
public interface IConversationStore
{
    /// <summary>
    /// Raised after an entry is appended to the authoritative conversation.
    /// </summary>
    event Action<ConversationId, ConversationEntry>? EntryAppended;
    Conversation CreateChannelConversation(string channelId);

    Conversation CreateDirectConversation(ActorId participantOne, ActorId participantTwo);

    bool TryGet(ConversationId id, out Conversation conversation);

    bool TryGetChannelConversation(string channelId, out Conversation conversation);

    /// <summary>
    /// Appends a factory-constructed entry to the authoritative conversation.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when <paramref name="conversationId"/> is unknown.
    /// </exception>
    ConversationEntry AppendEntry(ConversationId conversationId, ConversationEntry entry);
}
