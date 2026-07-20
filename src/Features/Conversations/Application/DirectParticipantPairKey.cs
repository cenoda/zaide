using System;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Conversations.Application;

/// <summary>
/// Unordered participant-pair index key for direct conversations (Phase 14 D05).
/// Two <see cref="ActorId"/> values are sorted by ordinal string comparison on
/// <see cref="ActorId.Value"/> so constructor order does not create duplicate directs.
/// </summary>
internal readonly struct DirectParticipantPairKey : IEquatable<DirectParticipantPairKey>
{
    private readonly ActorId _first;
    private readonly ActorId _second;

    private DirectParticipantPairKey(ActorId first, ActorId second)
    {
        _first = first;
        _second = second;
    }

    public static DirectParticipantPairKey FromActors(ActorId participantOne, ActorId participantTwo)
    {
        if (participantOne == participantTwo)
        {
            throw new ArgumentException(
                "Direct conversations require two distinct participants.",
                nameof(participantTwo));
        }

        if (string.CompareOrdinal(participantOne.Value, participantTwo.Value) <= 0)
        {
            return new DirectParticipantPairKey(participantOne, participantTwo);
        }

        return new DirectParticipantPairKey(participantTwo, participantOne);
    }

    public bool Equals(DirectParticipantPairKey other) =>
        _first == other._first && _second == other._second;

    public override bool Equals(object? obj) =>
        obj is DirectParticipantPairKey other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(_first, _second);
}
