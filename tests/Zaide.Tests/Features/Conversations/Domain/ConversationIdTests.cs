using System;
using System.Reflection;
using Xunit;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Conversations.Domain;

public sealed class ConversationIdTests
{
    [Fact]
    public void Default_HasEmptyValueAndStableHashCode()
    {
        var id = default(ConversationId);

        Assert.Equal(string.Empty, id.Value);
        Assert.Equal(0, id.GetHashCode());
    }

    [Fact]
    public void FactoryMethods_ProduceLockedNamespaces()
    {
        Assert.Equal("channel:channel-1", ConversationId.ForChannel("channel-1").Value);
        Assert.Equal("channel:", ConversationId.ForChannel(string.Empty).Value);

        var direct = ConversationId.NewDirect();
        Assert.StartsWith("direct:", direct.Value, StringComparison.Ordinal);
        Assert.Equal(39, direct.Value.Length);
    }

    [Fact]
    public void TryGetChannelId_ReturnsChannelPresentationKey()
    {
        var channelConversation = ConversationId.ForChannel("channel-2");

        Assert.True(channelConversation.TryGetChannelId(out var channelId));
        Assert.Equal("channel-2", channelId);
    }

    [Fact]
    public void TryGetChannelId_RejectsDirectConversationIds()
    {
        Assert.False(ConversationId.NewDirect().TryGetChannelId(out var channelId));
        Assert.Equal(string.Empty, channelId);
        Assert.False(default(ConversationId).TryGetChannelId(out channelId));
    }

    [Fact]
    public void Equality_IsOrdinalAndExact()
    {
        var left = ConversationId.ForChannel("channel-2");
        var right = ConversationId.ForChannel("channel-2");
        var other = ConversationId.ForChannel("channel-3");

        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.False(left != right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.NotEqual(left, other);
        Assert.NotEqual(left, ConversationId.NewDirect());
    }

    [Fact]
    public void PublicSurface_HasNoUserConstructibleConstructor()
    {
        var constructors = typeof(ConversationId).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance);
        Assert.Empty(constructors);
    }
}
