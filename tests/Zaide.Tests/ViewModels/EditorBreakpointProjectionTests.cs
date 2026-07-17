using System;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Features.Settings.Domain;

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
        Assert.Null(markers[0].Verification);
        Assert.Equal(12, markers[1].Line);
        Assert.False(markers[1].Enabled);
    }

    [Fact]
    public void ForSource_OverlaysSessionVerification_WithoutChangingPersistedLines()
    {
        var source = "/tmp/workspace/Program.cs";
        var breakpoints = new[]
        {
            new PersistedBreakpoint(source, 1, true),
            new PersistedBreakpoint(source, 2, true),
            new PersistedBreakpoint(source, 3, true),
        };
        var verifications = new[]
        {
            new DebugBreakpointVerification(source, 1, 1, DebugBreakpointVerificationState.Verified, null),
            new DebugBreakpointVerification(source, 2, 2, DebugBreakpointVerificationState.Pending, null),
            new DebugBreakpointVerification(source, 3, 3, DebugBreakpointVerificationState.Rejected, "bad line"),
        };

        var markers = EditorBreakpointProjection.ForSource(breakpoints, source, verifications);

        Assert.Equal(3, markers.Count);
        Assert.Equal(DebugBreakpointVerificationState.Verified, markers[0].Verification);
        Assert.Equal(DebugBreakpointVerificationState.Pending, markers[1].Verification);
        Assert.Equal(DebugBreakpointVerificationState.Rejected, markers[2].Verification);
        Assert.Equal("bad line", markers[2].AdapterMessage);
        Assert.Equal(1, markers[0].Line);
        Assert.Equal(2, markers[1].Line);
        Assert.Equal(3, markers[2].Line);
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