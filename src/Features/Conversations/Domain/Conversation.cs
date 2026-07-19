namespace Zaide.Features.Conversations.Domain;

/// <summary>
/// Authoritative in-memory conversation aggregate for Refactor 7 M2.
/// Ordered typed entries arrive in M3; this milestone owns identity, kind,
/// and participant membership only.
/// </summary>
public sealed class Conversation
{
    public Conversation(
        ConversationId id,
        ConversationKind kind,
        ConversationParticipants participants)
    {
        Id = id;
        Kind = kind;
        Participants = participants ?? throw new System.ArgumentNullException(nameof(participants));
    }

    public ConversationId Id { get; }

    public ConversationKind Kind { get; }

    public ConversationParticipants Participants { get; }
}
