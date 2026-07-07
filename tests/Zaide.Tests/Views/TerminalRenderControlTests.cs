using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Xunit;
using Zaide.ViewModels;
using Zaide.Views;

namespace Zaide.Tests.Views;

/// <summary>
/// Contract tests for <see cref="TerminalRenderControl"/> — verify the control
/// exposes the expected styled properties and registers them in the Avalonia
/// property system. No Avalonia headless is required; only static property
/// metadata is checked.
/// </summary>
public class TerminalRenderControlTests
{
    static TerminalRenderControlTests()
    {
        EnsureApplication();
    }

    [Fact]
    public void SnapshotProperty_IsRegistered()
    {
        var prop = TerminalRenderControl.SnapshotProperty;
        Assert.NotNull(prop);
        Assert.Equal(typeof(TerminalSnapshot), prop.PropertyType);
        Assert.Equal("Snapshot", prop.Name);
        Assert.IsType<StyledProperty<TerminalSnapshot?>>(prop);
    }

    [Fact]
    public void SnapshotProperty_HasCorrectOwnerType()
    {
        // OwnerType confirms the property was registered on this class, not
        // inherited from a base type — a prerequisite for AffectsRender to work.
        Assert.Equal(
            typeof(TerminalRenderControl),
            TerminalRenderControl.SnapshotProperty.OwnerType);
    }

    [Fact]
    public void CursorRowProperty_IsRegistered()
    {
        var prop = TerminalRenderControl.CursorRowProperty;
        Assert.NotNull(prop);
        Assert.Equal(typeof(int), prop.PropertyType);
        Assert.Equal("CursorRow", prop.Name);
        Assert.Equal(typeof(TerminalRenderControl), prop.OwnerType);
    }

    [Fact]
    public void CursorColProperty_IsRegistered()
    {
        var prop = TerminalRenderControl.CursorColProperty;
        Assert.NotNull(prop);
        Assert.Equal(typeof(int), prop.PropertyType);
        Assert.Equal("CursorCol", prop.Name);
        Assert.Equal(typeof(TerminalRenderControl), prop.OwnerType);
    }

    [Fact]
    public void CursorVisibleProperty_IsRegistered()
    {
        var prop = TerminalRenderControl.CursorVisibleProperty;
        Assert.NotNull(prop);
        Assert.Equal(typeof(bool), prop.PropertyType);
        Assert.Equal("CursorVisible", prop.Name);
        Assert.Equal(typeof(TerminalRenderControl), prop.OwnerType);
    }

    [Fact]
    public void AllFourProperties_HaveDistinctNames()
    {
        var names = new[]
        {
            TerminalRenderControl.SnapshotProperty.Name,
            TerminalRenderControl.CursorRowProperty.Name,
            TerminalRenderControl.CursorColProperty.Name,
            TerminalRenderControl.CursorVisibleProperty.Name,
        };

        Assert.Equal(4, names.Distinct().Count());
    }

    [Fact]
    public void StaticConstructor_RegistersPropertiesInAvaloniaRegistry()
    {
        // Force the static constructor to run by touching static fields.
        // If any AffectsRender call fails (e.g. because a property was
        // mis-registered on the wrong type), the type initializer throws.
        // Then verify the registry knows about each property by owner.
        var registry = AvaloniaPropertyRegistry.Instance;

        Assert.True(
            registry.IsRegistered(typeof(TerminalRenderControl), TerminalRenderControl.SnapshotProperty));

        Assert.True(
            registry.IsRegistered(typeof(TerminalRenderControl), TerminalRenderControl.CursorRowProperty));

        Assert.True(
            registry.IsRegistered(typeof(TerminalRenderControl), TerminalRenderControl.CursorColProperty));

        Assert.True(
            registry.IsRegistered(typeof(TerminalRenderControl), TerminalRenderControl.CursorVisibleProperty));
    }



    [Fact]
    public void BuildSelectedText_SpansScrollbackAndViewportRows()
    {
        var snapshot = new TerminalSnapshot(
            3,
            2,
            new[] { "ghi", "jkl" },
            new[]
            {
                new TerminalCell('g', -1, -1, false, false),
                new TerminalCell('h', -1, -1, false, false),
                new TerminalCell('i', -1, -1, false, false),
                new TerminalCell('j', -1, -1, false, false),
                new TerminalCell('k', -1, -1, false, false),
                new TerminalCell('l', -1, -1, false, false),
            },
            new[] { "abc", "def" },
            new[]
            {
                new TerminalCell('a', -1, -1, false, false),
                new TerminalCell('b', -1, -1, false, false),
                new TerminalCell('c', -1, -1, false, false),
                new TerminalCell('d', -1, -1, false, false),
                new TerminalCell('e', -1, -1, false, false),
                new TerminalCell('f', -1, -1, false, false),
            });

        string selected = TerminalRenderControl.BuildSelectedText(snapshot, (1, 1), (2, 1));

        Assert.Equal("ef\ngh", selected);
    }

    [Fact]
    public void BuildSelectedText_IgnoresTrailingPaddingWithinRow()
    {
        var snapshot = new TerminalSnapshot(
            6,
            1,
            new[] { "abc   " },
            new[]
            {
                new TerminalCell('a', -1, -1, false, false),
                new TerminalCell('b', -1, -1, false, false),
                new TerminalCell('c', -1, -1, false, false),
                new TerminalCell(' ', -1, -1, false, false),
                new TerminalCell(' ', -1, -1, false, false),
                new TerminalCell(' ', -1, -1, false, false),
            });

        string selected = TerminalRenderControl.BuildSelectedText(snapshot, (0, 0), (0, 5));

        Assert.Equal("abc", selected);
    }

    // ── M3: alt-screen selection/scrollback isolation ─────────────

    [Fact]
    public void IsAlternateScreenActiveProperty_IsRegistered()
    {
        var prop = TerminalRenderControl.IsAlternateScreenActiveProperty;
        Assert.NotNull(prop);
        Assert.Equal(typeof(bool), prop.PropertyType);
        Assert.Equal("IsAlternateScreenActive", prop.Name);
        Assert.Equal(typeof(TerminalRenderControl), prop.OwnerType);
        Assert.IsType<StyledProperty<bool>>(prop);
    }

    [Fact]
    public void IsMainBufferSelectionEnabled_DisabledWhileAlternateScreenActive()
    {
        // The view gates main-buffer selection, manual scrollback, and copy on
        // this single decision; a full-screen TUI must not leak main cells.
        Assert.True(TerminalRenderControl.IsMainBufferSelectionEnabled(false));
        Assert.False(TerminalRenderControl.IsMainBufferSelectionEnabled(true));
    }

    // ── M1: selection/copy/paste polish ────────────────────────────

    private static TerminalSnapshot BuildWordLineSnapshot()
    {
        // Single viewport row: "foo bar-baz  qux"
        string line = "foo bar-baz  qux";
        var cells = new TerminalCell[16];
        for (int i = 0; i < line.Length; i++)
        {
            cells[i] = new TerminalCell(line[i], -1, -1, false, false);
        }

        return new TerminalSnapshot(
            16,
            1,
            new[] { line },
            cells,
            System.Array.Empty<string>(),
            System.Array.Empty<TerminalCell>());
    }

    [Fact]
    public void TryGetWordSelectionRange_SelectsWholeWord_AroundClickedCell()
    {
        var snapshot = BuildWordLineSnapshot();

        // Click inside "bar" (index 4..6), expect selection of just "bar".
        bool ok = TerminalRenderControl.TryGetWordSelectionRange(snapshot, (0, 5), out var start, out var end);

        Assert.True(ok);
        Assert.Equal((0, 4), start);
        Assert.Equal((0, 6), end);
    }

    [Fact]
    public void TryGetWordSelectionRange_TreatsHyphenAsBoundary()
    {
        var snapshot = BuildWordLineSnapshot();

        // "baz" starts at index 8 within "bar-baz".
        bool ok = TerminalRenderControl.TryGetWordSelectionRange(snapshot, (0, 9), out var start, out var end);

        Assert.True(ok);
        Assert.Equal((0, 8), start);
        Assert.Equal((0, 10), end);
    }

    [Fact]
    public void BuildSelectedText_SelectsWholeWord_OnWordSelectionRange()
    {
        var snapshot = BuildWordLineSnapshot();

        TerminalRenderControl.TryGetWordSelectionRange(snapshot, (0, 5), out var start, out var end);
        string selected = TerminalRenderControl.BuildSelectedText(snapshot, start, end);

        Assert.Equal("bar", selected);
    }

    [Fact]
    public void GetLineSelectionRange_CoversFullLogicalLine()
    {
        var snapshot = BuildWordLineSnapshot();

        TerminalRenderControl.GetLineSelectionRange(snapshot, 0, out var start, out var end);

        Assert.Equal((0, 0), start);
        Assert.Equal((0, 15), end);
    }

    [Fact]
    public void BuildSelectedText_SelectsWholeLine_OnLineSelectionRange()
    {
        var snapshot = BuildWordLineSnapshot();

        TerminalRenderControl.GetLineSelectionRange(snapshot, 0, out var start, out var end);
        string selected = TerminalRenderControl.BuildSelectedText(snapshot, start, end);

        Assert.Equal("foo bar-baz  qux", selected);
    }

    [Fact]
    public void TryGetSelectedText_ReturnsFalse_WhenAlternateScreenActive()
    {
        var control = new TerminalRenderControl
        {
            Snapshot = BuildWordLineSnapshot(),
            IsAlternateScreenActive = true
        };

        bool ok = control.TryGetSelectedText(out string? text);

        Assert.False(ok);
        Assert.Null(text);
    }

    [Fact]
    public void ScrollToBottom_ClearsSelection_WhenRequested()
    {
        var control = new TerminalRenderControl
        {
            Snapshot = BuildWordLineSnapshot()
        };

        // Selection is a private concern; ScrollToBottom(clearSelection: true)
        // is the public seam that must leave no selected text behind.
        control.ScrollToBottom(clearSelection: true);

        bool ok = control.TryGetSelectedText(out string? text);
        Assert.False(ok);
        Assert.Null(text);
    }

    private static void EnsureApplication()
    {
        if (Application.Current is App app)
        {
            if (!app.Resources.ContainsKey("PrimaryAccentBrush"))
            {
                app.Initialize();
            }

            return;
        }

        var createdApp = new App();
        createdApp.Initialize();
    }
}
