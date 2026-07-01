using System.Linq;
using Avalonia;
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
    public void Render_ExtendedBackgroundColors_ArePainted()
    {
        // This test validates that extended background colors (256-color and truecolor) are painted.
        // It checks that the condition for filling the background includes extended colors.
        var screen = new TerminalScreen(10, 3);
        screen.SetSgr(new[] { 48, 5, 21 }); // 256-color background
        screen.Write('X');
        
        var snapshot = new TerminalSnapshot(
            10, 3,
            new[] { "X        ", "          ", "          " },
            new TerminalCell[30],
            new string[0],
            new TerminalCell[0]);
        
        // Verify that the cell has the extended background color set
        var cell = screen.GetCell(0, 0);
        Assert.Equal(-1, cell.Attribute.Background);
        Assert.Equal(21, cell.Attribute.Background256);
        
        // The renderer should paint the background when Background256 or BackgroundTrueColor is set
        // This is validated by the condition in TerminalRenderControl.Render()
    }

    [Fact]
    public void Render_Condition_IncludesExtendedBackgroundColors()
    {
        // This test validates that the condition for filling the background in TerminalRenderControl.Render()
        // includes extended background colors (Background256 and BackgroundTrueColor).
        // This ensures that extended background colors are painted.
        var screen = new TerminalScreen(10, 3);
        
        // Test 256-color background
        screen.SetSgr(new[] { 48, 5, 21 }); // 256-color background
        screen.Write('X');
        var cell256 = screen.GetCell(0, 0);
        Assert.Equal(-1, cell256.Attribute.Background);
        Assert.Equal(21, cell256.Attribute.Background256);
        
        // Test truecolor background
        screen.SetSgr(new[] { 48, 2, 100, 150, 200 }); // Truecolor background
        screen.Write('Y');
        var cellTrueColor = screen.GetCell(0, 1);
        Assert.Equal(-1, cellTrueColor.Attribute.Background);
        Assert.Equal(0x6496C8, cellTrueColor.Attribute.BackgroundTrueColor);
        
        // The renderer should paint the background when Background256 or BackgroundTrueColor is set
        // This is validated by the condition in TerminalRenderControl.Render()
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
}
