using System;
using Xunit;
using Zaide.Features.Terminal.Presentation;

namespace Zaide.Tests.Features.Terminal.Presentation;

public class TerminalGeometryTests
{
    [Theory]
    [InlineData(800, 400, 8.0, 16.0, 0, 0, 0, 0, 100, 25)]
    [InlineData(1000, 500, 10.0, 20.0, 0, 0, 0, 0, 100, 25)]
    [InlineData(800, 400, 8.0, 16.0, 16, 8, 16, 8, 96, 24)]
    [InlineData(640, 320, 8.0, 16.0, 0, 0, 0, 0, 80, 20)]
    public void Compute_ReturnsExpectedColumnsAndRows(
        double width, double height,
        double cellWidth, double lineHeight,
        double pl, double pt, double pr, double pb,
        int expectedCols, int expectedRows)
    {
        var (cols, rows) = TerminalGeometry.Compute(
            width, height, cellWidth, lineHeight, pl, pt, pr, pb);

        Assert.Equal(expectedCols, cols);
        Assert.Equal(expectedRows, rows);
    }

    [Theory]
    [InlineData(0, 400, 8.0, 16.0)]    // zero width
    [InlineData(-10, 400, 8.0, 16.0)]   // negative width
    [InlineData(800, 0, 8.0, 16.0)]     // zero height
    [InlineData(800, -5, 8.0, 16.0)]    // negative height
    public void Compute_ClampsToMinimumOne_WhenSurfaceTooSmall(
        double width, double height,
        double cellWidth, double lineHeight)
    {
        var (cols, rows) = TerminalGeometry.Compute(
            width, height, cellWidth, lineHeight);

        Assert.True(cols >= 1, $"Columns should be >= 1, got {cols}");
        Assert.True(rows >= 1, $"Rows should be >= 1, got {rows}");
    }

    [Fact]
    public void Compute_ClampsToOne_WhenPaddingExceedsSurface()
    {
        var (cols, rows) = TerminalGeometry.Compute(
            100, 100, 8.0, 16.0,
            paddingLeft: 200, paddingTop: 200,
            paddingRight: 0, paddingBottom: 0);

        Assert.Equal(1, cols);
        Assert.Equal(1, rows);
    }

    [Fact]
    public void Compute_ThrowsOnZeroCellWidth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TerminalGeometry.Compute(800, 400, 0, 16.0));
    }

    [Fact]
    public void Compute_ThrowsOnNegativeCellWidth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TerminalGeometry.Compute(800, 400, -1, 16.0));
    }

    [Fact]
    public void Compute_ThrowsOnZeroLineHeight()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TerminalGeometry.Compute(800, 400, 8.0, 0));
    }

    [Fact]
    public void Compute_ThrowsOnNegativeLineHeight()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TerminalGeometry.Compute(800, 400, 8.0, -1));
    }

    [Fact]
    public void Compute_TypicalTerminal1280x250_ReturnsReasonableValues()
    {
        // Simulates a 1280×250 terminal panel, 8px char width, 16px line height,
        // with 16px horizontal padding and 8px vertical padding.
        var (cols, rows) = TerminalGeometry.Compute(
            1280, 250, 8.0, 16.0, 16, 8, 16, 8);

        // (1280 - 32) / 8 = 156 cols, (250 - 16) / 16 = 14.625 → 14 rows
        Assert.Equal(156, cols);
        Assert.Equal(14, rows);
    }
}