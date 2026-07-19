using System;

namespace Zaide.Features.Conversations.Domain;

/// <summary>
/// Authoritative in-memory conversation aggregate for Refactor 7 M2.
/// Ordered typed entries arrive in M3; this milestone owns identity, kind,
/// and participant membership only.
/// </summary>
public sealed class Conversation
{
    private Conversation(
        ConversationId id,
        ConversationKind kind,
        ConversationParticipants participants)
    {
        Id = id;
        Kind = kind;
        Participants = participants;
    }

    public ConversationId Id { get; }

    public ConversationKind Kind { get; }

    public ConversationParticipants Participants { get; }

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
}
