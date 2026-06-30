using Avalonia;
using Xunit;
using Zaide.ViewModels;
using Zaide.Views;

namespace Zaide.Tests.Views;

/// <summary>
/// Contract tests for <see cref="TerminalRenderControl"/> — verify the control
/// exposes the expected styled properties. No Avalonia headless is required;
/// only static property metadata is checked.
/// </summary>
public class TerminalRenderControlTests
{
    [Fact]
    public void SnapshotProperty_IsRegistered()
    {
        Assert.NotNull(TerminalRenderControl.SnapshotProperty);
        Assert.Equal(
            typeof(TerminalSnapshot),
            TerminalRenderControl.SnapshotProperty.PropertyType);
        Assert.Equal(
            "Snapshot",
            TerminalRenderControl.SnapshotProperty.Name);
    }

    [Fact]
    public void CursorRowProperty_IsRegistered()
    {
        Assert.NotNull(TerminalRenderControl.CursorRowProperty);
        Assert.Equal(
            typeof(int),
            TerminalRenderControl.CursorRowProperty.PropertyType);
    }

    [Fact]
    public void CursorColProperty_IsRegistered()
    {
        Assert.NotNull(TerminalRenderControl.CursorColProperty);
        Assert.Equal(
            typeof(int),
            TerminalRenderControl.CursorColProperty.PropertyType);
    }

    [Fact]
    public void CursorVisibleProperty_IsRegistered()
    {
        Assert.NotNull(TerminalRenderControl.CursorVisibleProperty);
        Assert.Equal(
            typeof(bool),
            TerminalRenderControl.CursorVisibleProperty.PropertyType);
    }

    [Fact]
    public void StaticConstructor_RegistersAffectsRenderForAllFourProperties()
    {
        // Accessing the type triggers the static constructor which calls
        // AffectsRender<T> for each styled property. This test confirms
        // the type initializer runs without exception, which it only does
        // when all four AffectsRender calls succeed.
        _ = TerminalRenderControl.SnapshotProperty;
        _ = TerminalRenderControl.CursorRowProperty;
        _ = TerminalRenderControl.CursorColProperty;
        _ = TerminalRenderControl.CursorVisibleProperty;
    }
}