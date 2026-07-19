using System;
using System.Reflection;
using Xunit;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Conversations.Domain;

public sealed class ConversationEntryIdTests
{
    [Fact]
    public void Default_HasEmptyValueAndStableHashCode()
    {
        var id = default(ConversationEntryId);

        Assert.Equal(string.Empty, id.Value);
        Assert.Equal(0, id.GetHashCode());
    }

    [Fact]
    public void New_ProducesUniqueLockedNamespaceValues()
    {
        var first = ConversationEntryId.New();
        var second = ConversationEntryId.New();

        Assert.StartsWith("entry:", first.Value, StringComparison.Ordinal);
        Assert.StartsWith("entry:", second.Value, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Equality_IsOrdinalAndExact()
    {
        var left = ConversationEntryId.New();
        var right = left;
        var other = ConversationEntryId.New();

        Assert.True(left.Equals(right));
        Assert.True(left == right);
        Assert.False(left == other);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void PublicSurface_HasNoUserConstructibleConstructor()
    {
        var constructors = typeof(ConversationEntryId).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance);
        Assert.Empty(constructors);
    }
}
