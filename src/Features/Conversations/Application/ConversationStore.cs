using System;
using System.Collections.Generic;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Conversations.Application;

/// <summary>
/// Application-lifetime in-memory conversation owner for current channel and
/// panel-backed direct conversations.
/// </summary>
internal sealed class ConversationStore : IConversationStore
{
    private readonly object _sync = new();
    private readonly Dictionary<ConversationId, Conversation> _byId = new();
    private readonly Dictionary<string, ConversationId> _channelIndex = new(StringComparer.Ordinal);

    public Conversation CreateChannelConversation(string channelId)
    {
        ArgumentNullException.ThrowIfNull(channelId);

        var conversationId = ConversationId.ForChannel(channelId);
        lock (_sync)
        {
            if (_byId.TryGetValue(conversationId, out var existing))
            {
                return existing;
            }

            var conversation = Conversation.Channel(conversationId);
            _byId[conversationId] = conversation;
            _channelIndex[channelId] = conversationId;
            return conversation;
        }
    }

    public Conversation CreateDirectConversation(ActorId participantOne, ActorId participantTwo)
    {
        var participants = ConversationParticipants.ForDirect(participantOne, participantTwo);
        var conversation = Conversation.Direct(
            ConversationId.NewDirect(),
            participants);

        lock (_sync)
        {
            _byId[conversation.Id] = conversation;
        }

        return conversation;
    }

    public bool TryGet(ConversationId id, out Conversation conversation)
    {
        lock (_sync)
        {
            return _byId.TryGetValue(id, out conversation!);
        }
    }

    public bool TryGetChannelConversation(string channelId, out Conversation conversation)
    {
        ArgumentNullException.ThrowIfNull(channelId);

        lock (_sync)
        {
            if (_channelIndex.TryGetValue(channelId, out var conversationId)
                && _byId.TryGetValue(conversationId, out conversation!))
            {
                return true;
            }
        }

        conversation = null!;
        return false;
    }

    public ConversationEntry AppendEntry(ConversationId conversationId, ConversationEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_sync)
        {
            if (!_byId.TryGetValue(conversationId, out var conversation))
            {
                throw new KeyNotFoundException(
                    $"Conversation '{conversationId.Value}' was not found.");
            }

            conversation.AppendEntry(entry);
            return entry;
        }
    }
}
