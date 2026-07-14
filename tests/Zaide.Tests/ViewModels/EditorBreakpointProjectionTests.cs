using System;
using Xunit;
using Zaide.Models;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

/// <summary>
/// Pure projection tests for editor breakpoint margin state.
/// </summary>
public sealed class EditorBreakpointProjectionTests
{
    [Fact]
    public void ForSource_FiltersByNormalizedPath_AndOrdersByLine()
    {
        var source = "/tmp/workspace/Program.cs";
        var other = "/tmp/workspace/Other.cs";
        var breakpoints = new[]
        {
            new PersistedBreakpoint(other, 3, true),
            new PersistedBreakpoint(source, 12, false),
            new PersistedBreakpoint(source, 4, true),
        };

        var markers = EditorBreakpointProjection.ForSource(breakpoints, source);

        Assert.Equal(2, markers.Count);
        Assert.Equal(4, markers[0].Line);
        Assert.True(markers[0].Enabled);
        Assert.Equal(12, markers[1].Line);
        Assert.False(markers[1].Enabled);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("a", 1)]
    [InlineData("a\nb", 2)]
    [InlineData("a\nb\n", 3)]
    public void GetLineCount_MatchesDocumentLines(string text, int expected)
    {
        Assert.Equal(expected, EditorBreakpointProjection.GetLineCount(text));
    }

    [Fact]
    public void NormalizeDocumentPath_ReturnsNullForUntitled()
    {
        Assert.Null(EditorBreakpointProjection.NormalizeDocumentPath(null));
        Assert.Null(EditorBreakpointProjection.NormalizeDocumentPath(""));
        Assert.Null(EditorBreakpointProjection.NormalizeDocumentPath("   "));
    }
}