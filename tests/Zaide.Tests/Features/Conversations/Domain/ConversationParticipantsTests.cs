using System;
using Xunit;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Conversations.Domain;

public sealed class ConversationParticipantsTests
{
    [Fact]
    public void ForChannel_HasEmptyMembership()
    {
        var participants = ConversationParticipants.ForChannel();

        Assert.Empty(participants.All);
        Assert.False(participants.Contains(ActorId.HumanUser));
    }

    [Fact]
    public void ForDirect_RequiresDistinctParticipants()
    {
        Assert.Throws<ArgumentException>(() =>
            ConversationParticipants.ForDirect(ActorId.HumanUser, ActorId.HumanUser));
    }

    [Fact]
    public void ForDirect_TracksCanonicalHumanAndPanelAgent()
    {
        var agent = ActorId.PanelSeed("alpha");
        var participants = ConversationParticipants.ForDirect(ActorId.HumanUser, agent);

        Assert.Equal(2, participants.All.Count);
        Assert.Contains(participants.All, id => id == ActorId.HumanUser);
        Assert.Contains(participants.All, id => id == agent);
        Assert.True(participants.Contains(ActorId.HumanUser));
        Assert.True(participants.Contains(agent));
    }
}
