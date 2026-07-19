using Xunit;
using Zaide.UI.DesignSystem;

namespace Zaide.Tests.UI.DesignSystem;

public class TypographyTokensTests
{
    [Fact]
    public void FontSizeSm_FallsBackToTwelve_WhenNoResource()
    {
        Assert.Equal(12d, TypographyTokens.FontSizeSm);
    }
}
