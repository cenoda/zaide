using System;
using Xunit;
using Zaide.Features.Terminal.Presentation;

namespace Zaide.Tests.Features.Terminal.Presentation;

/// <summary>
/// Pure unit tests for <see cref="TerminalSnapshot"/> and <see cref="TerminalCell"/>
/// structure projection. No Avalonia dependency.
/// </summary>
public class TerminalSnapshotTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var cells = new[] { new TerminalCell('A', 1, -1, false, false) };
        var lines = new[] { "A" };
        var snapshot = new TerminalSnapshot(80, 24, lines, cells);

        Assert.Equal(80, snapshot.Columns);
        Assert.Equal(24, snapshot.Rows);
        Assert.Same(lines, snapshot.Lines);
        Assert.Same(cells, snapshot.Cells);
    }

    [Fact]
    public void Cells_AreRowMajorOrder()
    {
        var cells = new[]
        {
            new TerminalCell('A', 1, -1, false, false),
            new TerminalCell('B', 2, -1, false, false),
            new TerminalCell('C', 3, -1, false, false),
            new TerminalCell('D', 4, -1, false, false),
        };

        var lines = new[] { "AB", "CD" };
        var snapshot = new TerminalSnapshot(2, 2, lines, cells);

        // Row 0: A, B
        Assert.Equal('A', snapshot.Cells[0].Char);
        Assert.Equal('B', snapshot.Cells[1].Char);
        // Row 1: C, D
        Assert.Equal('C', snapshot.Cells[2].Char);
        Assert.Equal('D', snapshot.Cells[3].Char);

        Assert.Equal(4, snapshot.Cells.Count);
        Assert.Equal(2, snapshot.Lines.Count);
    }

    [Fact]
    public void Cells_CountMatchesColumnsTimesRows()
    {
        var cells = new TerminalCell[80 * 24];
        var lines = new string[24];
        var snapshot = new TerminalSnapshot(80, 24, lines, cells);

        Assert.Equal(80 * 24, snapshot.Cells.Count);
        Assert.Equal(80, snapshot.Columns);
        Assert.Equal(24, snapshot.Rows);
    }

    [Fact]
    public void Lines_MatchPerRowStrings()
    {
        var lines = new[] { "Hello", "World" };
        var cells = new TerminalCell[10]; // 5 cols × 2 rows
        var snapshot = new TerminalSnapshot(5, 2, lines, cells);

        Assert.Equal("Hello", snapshot.Lines[0]);
        Assert.Equal("World", snapshot.Lines[1]);
    }

    [Fact]
    public void Constructor_RejectsNullLines()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TerminalSnapshot(80, 24, null!, Array.Empty<TerminalCell>()));
    }

    [Fact]
    public void Constructor_RejectsNullCells()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TerminalSnapshot(80, 24, Array.Empty<string>(), null!));
    }

    [Fact]
    public void TerminalCell_PropertiesAreSet()
    {
        var cell = new TerminalCell('Z', 3, 5, bold: true, inverse: true);

        Assert.Equal('Z', cell.Char);
        Assert.Equal(3, cell.Foreground);
        Assert.Equal(5, cell.Background);
        Assert.True(cell.Bold);
        Assert.True(cell.Inverse);
    }

    [Fact]
    public void TerminalCell_DefaultValues()
    {
        var cell = new TerminalCell(' ', -1, -1, bold: false, inverse: false);

        Assert.Equal(' ', cell.Char);
        Assert.Equal(-1, cell.Foreground);
        Assert.Equal(-1, cell.Background);
        Assert.False(cell.Bold);
        Assert.False(cell.Inverse);
    }
}