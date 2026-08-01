using System;
using Xunit;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests.Features.Editor.Presentation;

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
    public void GetVisibleIndentGuideLevelCount_Span_MatchesStringOverload()
    {
        const string line = "        nested";
        var fromString = IndentGuideMetrics.GetVisibleIndentGuideLevelCount(line, 4);
        var fromSpan = IndentGuideMetrics.GetVisibleIndentGuideLevelCount(line.AsSpan(), 4);
        Assert.Equal(fromString, fromSpan);
        Assert.Equal(2, fromSpan);
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

    [Theory]
    [InlineData(1, 4, 2.0)]
    [InlineData(2, 4, 6.0)]
    [InlineData(3, 4, 10.0)]
    [InlineData(1, 2, 1.0)]
    [InlineData(2, 2, 3.0)]
    public void GetGuideVisualColumnMidpoint_ReturnsCenterOfIndentBlock(
        int guideLevel,
        int indentationSize,
        double expected)
    {
        var result = IndentGuideMetrics.GetGuideVisualColumnMidpoint(
            guideLevel,
            indentationSize);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetGuideVisualColumnMidpoint_Throws_WhenGuideLevelInvalid()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            IndentGuideMetrics.GetGuideVisualColumnMidpoint(0, 4));
    }

    [Theory]
    [InlineData(1, 4, 10.0, 0.0, 20.0)]   // mid visual col 2 * 10
    [InlineData(2, 4, 10.0, 0.0, 60.0)]   // mid visual col 6 * 10
    [InlineData(1, 4, 10.0, 5.0, 15.0)]   // subtract horizontal scroll
    public void GetGuideViewportX_UsesMonospaceWidthAndScroll(
        int guideLevel,
        int indentationSize,
        double wideSpaceWidth,
        double scrollOffsetX,
        double expected)
    {
        var result = IndentGuideMetrics.GetGuideViewportX(
            guideLevel,
            indentationSize,
            wideSpaceWidth,
            scrollOffsetX);

        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Document-column boundaries for complete indent levels map to the same
    /// monospaced visual midpoints used by the fast paint path (spaces, tabs, mixed).
    /// </summary>
    [Theory]
    [InlineData("    if", 4)]
    [InlineData("        if", 4)]
    [InlineData("\tif", 4)]
    [InlineData("\t\tif", 4)]
    [InlineData("  \tif", 4)]
    [InlineData("    \tif", 4)]
    public void GuideVisualMidpoints_MatchBoundaryLevelCenters(
        string lineText,
        int indentationSize)
    {
        var levelCount = IndentGuideMetrics.GetVisibleIndentGuideLevelCount(
            lineText,
            indentationSize);
        Assert.True(levelCount > 0);

        for (var level = 1; level <= levelCount; level++)
        {
            var expectedMid = IndentGuideMetrics.GetGuideVisualColumnMidpoint(
                level,
                indentationSize);
            // Complete indent levels always span visual columns
            // [(level-1)*size, level*size); midpoint is independent of tab/space mix.
            Assert.Equal((level - 0.5) * indentationSize, expectedMid);
        }
    }
}
