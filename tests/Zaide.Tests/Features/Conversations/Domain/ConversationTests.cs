using System;
using System.Reflection;
using Xunit;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Conversations.Domain;

public sealed class ConversationTests
{
    [Fact]
    public void Channel_ProvisionsEmptyParticipantMembership()
    {
        var conversation = Conversation.Channel(ConversationId.ForChannel("channel-1"));

        Assert.Equal(ConversationKind.Channel, conversation.Kind);
        Assert.Empty(conversation.Participants.All);
    }

    [Fact]
    public void Direct_RequiresExactlyTwoParticipants()
    {
        var participants = ConversationParticipants.ForDirect(
            ActorId.HumanUser,
            ActorId.PanelSeed("alpha"));

        var conversation = Conversation.Direct(ConversationId.NewDirect(), participants);

        Assert.Equal(ConversationKind.Direct, conversation.Kind);
        Assert.Equal(2, conversation.Participants.All.Count);
    }

    [Fact]
    public void Direct_RejectsEmptyParticipantMembership()
    {
        Assert.Throws<ArgumentException>(() =>
            Conversation.Direct(
                ConversationId.NewDirect(),
                ConversationParticipants.ForChannel()));
    }

    [Fact]
    public void Direct_RejectsNullParticipants()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Conversation.Direct(ConversationId.NewDirect(), null!));
    }

    [Fact]
    public void PublicSurface_HasNoUserConstructibleConstructor()
    {
        var constructors = typeof(Conversation).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance);
        Assert.Empty(constructors);
    }
}
