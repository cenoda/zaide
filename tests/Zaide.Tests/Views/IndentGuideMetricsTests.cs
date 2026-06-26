using System;
using Xunit;
using Zaide.Views;

namespace Zaide.Tests.Views;

public class IndentGuideMetricsTests
{
    [Theory]
    [InlineData("    if (ready)", 1)]
    [InlineData("\tif (ready)", 1)]
    [InlineData("  \tif (ready)", 1)]
    [InlineData("        if (ready)", 2)]
    [InlineData("            if (ready)", 3)]
    [InlineData("   if (ready)", 0)]
    [InlineData("if (ready)", 0)]
    [InlineData("", 0)]
    [InlineData("    ", 0)]
    [InlineData("\t", 0)]
    public void GetVisibleIndentGuideLevelCount_ReturnsExpectedValue(
        string lineText,
        int expected)
    {
        var result = IndentGuideMetrics.GetVisibleIndentGuideLevelCount(
            lineText,
            indentationSize: 4);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryGetFirstGuideVisualColumn_ReturnsFirstIndentBoundary()
    {
        var result = IndentGuideMetrics.TryGetFirstGuideVisualColumn(
            "        if (ready)",
            indentationSize: 4,
            out var visualColumn);

        Assert.True(result);
        Assert.Equal(4, visualColumn);
    }

    [Fact]
    public void TryGetFirstGuideVisualColumn_ReturnsFalse_WhenNoFullIndentLevelExists()
    {
        var result = IndentGuideMetrics.TryGetFirstGuideVisualColumn(
            "   if (ready)",
            indentationSize: 4,
            out var visualColumn);

        Assert.False(result);
        Assert.Equal(0, visualColumn);
    }

    [Fact]
    public void GetVisibleIndentGuideLevelCount_Throws_WhenIndentationSizeInvalid()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            IndentGuideMetrics.GetVisibleIndentGuideLevelCount("    x", 0));
    }

    [Theory]
    [InlineData("    if (ready)", new[] { 5 })]
    [InlineData("        if (ready)", new[] { 5, 9 })]
    [InlineData("\tif (ready)", new[] { 2 })]
    [InlineData("\t\tif (ready)", new[] { 2, 3 })]
    [InlineData("  \tif (ready)", new[] { 4 })]
    public void GetIndentBoundaryDocumentColumns_ReturnsExpectedColumns(
        string lineText,
        int[] expected)
    {
        var result = IndentGuideMetrics.GetIndentBoundaryDocumentColumns(
            lineText,
            indentationSize: 4);

        Assert.Equal(expected, result);
    }
}
