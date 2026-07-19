using System;
using Xunit;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Conversations.Domain;

public sealed class ConversationEntryCorrelationIdTests
{
    [Fact]
    public void FromValue_PreservesOpaqueValue()
    {
        var correlation = ConversationEntryCorrelationId.FromValue("run:abc123");

        Assert.Equal("run:abc123", correlation.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromValue_RejectsMissingValue(string? value)
    {
        Assert.Throws<ArgumentException>(() =>
            ConversationEntryCorrelationId.FromValue(value!));
    }

    [Fact]
    public void Equality_IsOrdinal()
    {
        var left = ConversationEntryCorrelationId.FromValue("run:shared");
        var right = ConversationEntryCorrelationId.FromValue("run:shared");
        var other = ConversationEntryCorrelationId.FromValue("run:other");

        Assert.Equal(left, right);
        Assert.NotEqual(left, other);
    }
}
