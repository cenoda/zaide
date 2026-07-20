using System;
using Xunit;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Conversations.Application;

public sealed class DirectParticipantPairKeyTests
{
    [Fact]
    public void FromActors_IsOrderIndependent()
    {
        var human = ActorId.HumanUser;
        var agent = ActorId.PanelSeed("alpha");

        var forward = DirectParticipantPairKey.FromActors(human, agent);
        var reverse = DirectParticipantPairKey.FromActors(agent, human);

        Assert.Equal(forward, reverse);
    }

    [Fact]
    public void FromActors_RejectsSameParticipant()
    {
        Assert.Throws<ArgumentException>(() =>
            DirectParticipantPairKey.FromActors(ActorId.HumanUser, ActorId.HumanUser));
    }

    [Fact]
    public void FromActors_SortsByOrdinalActorIdValue()
    {
        var lower = ActorId.PanelSeed("alpha");
        var higher = ActorId.PanelSeed("beta");

        var key = DirectParticipantPairKey.FromActors(higher, lower);
        var sameKey = DirectParticipantPairKey.FromActors(lower, higher);

        Assert.Equal(key, sameKey);
    }
}
