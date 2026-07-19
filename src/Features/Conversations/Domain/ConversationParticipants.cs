using System;
using System.Collections.Generic;
using System.Linq;

namespace Zaide.Features.Conversations.Domain;

/// <summary>
/// Immutable participant membership for a conversation.
/// </summary>
public sealed class ConversationParticipants
{
    private readonly ActorId[] _participants;

    private ConversationParticipants(IReadOnlyList<ActorId> participants)
    {
        _participants = participants.ToArray();
    }

    public IReadOnlyList<ActorId> All => _participants;

    public static ConversationParticipants ForChannel() =>
        new(Array.Empty<ActorId>());

    public static ConversationParticipants ForDirect(ActorId participantOne, ActorId participantTwo)
    {
        if (participantOne == participantTwo)
        {
            throw new ArgumentException(
                "Direct conversations require two distinct participants.",
                nameof(participantTwo));
        }

        return new ConversationParticipants(new[] { participantOne, participantTwo });
    }

    public bool Contains(ActorId actorId) =>
        _participants.Any(participant => participant == actorId);
}
