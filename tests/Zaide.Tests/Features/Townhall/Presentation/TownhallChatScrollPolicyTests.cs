using Xunit;
using Zaide.Features.Townhall.Presentation;

namespace Zaide.Tests.Features.Townhall.Presentation;

public sealed class TownhallChatScrollPolicyTests
{
    [Theory]
    [InlineData(100, 100, true)]
    [InlineData(52, 100, true)]
    [InlineData(51, 100, false)]
    [InlineData(0, 0, true)]
    public void IsNearBottom_UsesThreshold(double offsetY, double maxOffsetY, bool expected)
    {
        Assert.Equal(expected, TownhallChatScrollPolicy.IsNearBottom(offsetY, maxOffsetY));
    }

    [Fact]
    public void ShouldAutoFollowOnAppend_MatchesNearBottomState()
    {
        Assert.True(TownhallChatScrollPolicy.ShouldAutoFollowOnAppend(true));
        Assert.False(TownhallChatScrollPolicy.ShouldAutoFollowOnAppend(false));
    }
}
