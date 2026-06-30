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
}
