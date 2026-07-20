using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly Dictionary<DirectParticipantPairKey, ConversationId> _directPairIndex = new();

    public event Action<ConversationId, ConversationEntry>? EntryAppended;

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

    public Conversation GetOrCreateDirectConversation(ActorId participantOne, ActorId participantTwo)
    {
        var pairKey = DirectParticipantPairKey.FromActors(participantOne, participantTwo);

        lock (_sync)
        {
            if (_directPairIndex.TryGetValue(pairKey, out var existingId)
                && _byId.TryGetValue(existingId, out var existing))
            {
                return existing;
            }

            var participants = ConversationParticipants.ForDirect(participantOne, participantTwo);
            var conversation = Conversation.Direct(ConversationId.NewDirect(), participants);
            _byId[conversation.Id] = conversation;
            _directPairIndex[pairKey] = conversation.Id;
            return conversation;
        }
    }

    public bool TryGetDirectConversation(
        ActorId participantOne,
        ActorId participantTwo,
        out Conversation conversation)
    {
        var pairKey = DirectParticipantPairKey.FromActors(participantOne, participantTwo);

        lock (_sync)
        {
            if (_directPairIndex.TryGetValue(pairKey, out var conversationId)
                && _byId.TryGetValue(conversationId, out conversation!))
            {
                return true;
            }
        }

        conversation = null!;
        return false;
    }

    public IReadOnlyList<Conversation> ListConversations()
    {
        lock (_sync)
        {
            return _byId.Values
                .OrderBy(conversation => conversation.Id.Value, StringComparer.Ordinal)
                .ToArray();
        }
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
        }

        EntryAppended?.Invoke(conversationId, entry);
        return entry;
    }

    /// <summary>
    /// Replaces the in-memory store with persisted conversations. Used on startup
    /// recovery only; does not raise <see cref="EntryAppended"/>.
    /// </summary>
    internal void RestoreFromPersistence(IReadOnlyList<Conversation> conversations)
    {
        ArgumentNullException.ThrowIfNull(conversations);

        lock (_sync)
        {
            _byId.Clear();
            _channelIndex.Clear();
            _directPairIndex.Clear();

            foreach (var conversation in conversations)
            {
                _byId[conversation.Id] = conversation;

                if (conversation.Kind == ConversationKind.Channel
                    && conversation.Id.TryGetChannelId(out var channelId))
                {
                    _channelIndex[channelId] = conversation.Id;
                }
                else if (conversation.Kind == ConversationKind.Direct
                         && conversation.Participants.All.Count == 2)
                {
                    var pairKey = DirectParticipantPairKey.FromActors(
                        conversation.Participants.All[0],
                        conversation.Participants.All[1]);
                    _directPairIndex[pairKey] = conversation.Id;
                }
            }
        }
    }
}
