using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Zaide.Features.Conversations.Domain;

/// <summary>
/// Authoritative in-memory conversation aggregate for Refactor 7.
/// Owns identity, kind, participant membership, and ordered typed entries.
/// </summary>
public sealed class Conversation
{
    private readonly List<ConversationEntry> _entries = new();

    private Conversation(
        ConversationId id,
        ConversationKind kind,
        ConversationParticipants participants)
    {
        Id = id;
        Kind = kind;
        Participants = participants;
        Entries = new ReadOnlyCollection<ConversationEntry>(_entries);
    }

    public ConversationId Id { get; }

    public ConversationKind Kind { get; }

    public ConversationParticipants Participants { get; }

    /// <summary>
    /// Immutable ordered view of typed entries admitted to this conversation.
    /// </summary>
    public ReadOnlyCollection<ConversationEntry> Entries { get; }

    internal void AppendEntry(ConversationEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Add(entry);
    }

    public static Conversation Channel(ConversationId id)
    {
        return new Conversation(
            id,
            ConversationKind.Channel,
            ConversationParticipants.ForChannel());
    }

    public static Conversation Direct(
        ConversationId id,
        ConversationParticipants participants)
    {
        ArgumentNullException.ThrowIfNull(participants);

        if (participants.All.Count != 2)
        {
            throw new ArgumentException(
                "Direct conversations require exactly two participants.",
                nameof(participants));
        }

        return new Conversation(id, ConversationKind.Direct, participants);
    }

    /// <summary>
    /// Rebuilds a conversation aggregate from persisted metadata and ordered entries.
    /// </summary>
    internal static Conversation Restore(
        ConversationId id,
        ConversationKind kind,
        ConversationParticipants participants,
        IReadOnlyList<ConversationEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var conversation = kind switch
        {
            ConversationKind.Channel => Channel(id),
            ConversationKind.Direct => Direct(id, participants),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        foreach (var entry in entries)
        {
            conversation.AppendEntry(entry);
        }

        return conversation;
    }
}
