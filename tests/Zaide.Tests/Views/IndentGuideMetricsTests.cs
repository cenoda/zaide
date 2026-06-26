using System;
using Xunit;
using Zaide.Views;

namespace Zaide.Tests.Views;

public class IndentGuideMetricsTests
{
    [Theory]
    [InlineData("    if (ready)", true)]
    [InlineData("\tif (ready)", true)]
    [InlineData("  \tif (ready)", true)]
    [InlineData("        if (ready)", true)]
    [InlineData("   if (ready)", false)]
    [InlineData("if (ready)", false)]
    [InlineData("", false)]
    [InlineData("    ", false)]
    [InlineData("\t", false)]
    public void TryGetFirstGuideVisualColumn_ReturnsExpectedPresence(
        string lineText,
        bool expected)
    {
        var result = IndentGuideMetrics.TryGetFirstGuideVisualColumn(
            lineText,
            indentationSize: 4,
            out var visualColumn);

        Assert.Equal(expected, result);
        Assert.Equal(expected ? 4 : 0, visualColumn);
    }

    [Fact]
    public void TryGetFirstGuideVisualColumn_Throws_WhenIndentationSizeInvalid()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            IndentGuideMetrics.TryGetFirstGuideVisualColumn("    x", 0, out _));
    }
}
