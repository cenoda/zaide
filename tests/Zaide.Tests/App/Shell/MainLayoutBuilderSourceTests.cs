using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using ReactiveUI.Builder;
using Xunit;
using Zaide.App.Shell;

namespace Zaide.Tests.App.Shell;

/// <summary>
/// Structural coverage for <see cref="MainLayoutBuilder"/> after Refactor 8 M4.
/// Full layout construction is not exercised here: <see cref="RightColumnHost"/>
/// transitively builds <see cref="Zaide.Features.Editor.Presentation.EditorView"/>,
/// which requires resource-backed <c>Application.Current</c> at construction time.
/// </summary>
public sealed class MainLayoutBuilderSourceTests
{
    static MainLayoutBuilderSourceTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    [Fact]
    public void MainLayoutBuilder_IsInternalShellTypeWithBuildSurface()
    {
        var type = typeof(MainLayoutBuilder);

        Assert.False(type.IsPublic);
        Assert.Equal("Zaide.App.Shell", type.Namespace);
        Assert.Contains(type.GetMethods(BindingFlags.Instance | BindingFlags.Public),
            m => m.Name == nameof(MainLayoutBuilder.Build));
        Assert.NotNull(type.GetNestedType(
            nameof(MainLayoutBuilder.MainLayoutBuildResult),
            BindingFlags.NonPublic));
    }

    [Fact]
    public void MainLayoutBuilder_SourceRetainsColumnAndRowDefinitions()
    {
        var source = ReadRepoFile("src/App/Shell/MainLayoutBuilder.cs");

        Assert.Contains("new GridLength(40)", source);
        Assert.Contains("new GridLength(260), MinWidth = 180, MaxWidth = 320", source);
        Assert.Contains("new GridLength(2, GridUnitType.Star), MinWidth = 300", source);
        Assert.Contains("new GridLength(1.5, GridUnitType.Star), MinWidth = 240", source);
        Assert.Contains("new GridLength(24, GridUnitType.Pixel)", source);
        Assert.Contains("SurfaceBaseBrush", source);
    }

    [Fact]
    public void MainLayoutBuilder_SourceRetainsSplittersAndHostAttachment()
    {
        var source = ReadRepoFile("src/App/Shell/MainLayoutBuilder.cs");

        Assert.Contains("PreservePixelColumnAndNormalizeStarColumns(grid, 1, 3, 5)", source);
        Assert.Contains("NormalizeStarColumns(grid, 3, 5)", source);
        Assert.Contains("new RightColumnHost(", source);
        Assert.Contains("rightColumnHost.AttachToLayoutGrid(grid)", source);
        Assert.Contains("new BottomPanelHost(settings)", source);
        Assert.Contains("bottomPanelHost.AttachToLayoutGrid(grid, bottomSplitterRow, bottomPanelRow)", source);
        Assert.Contains("Grid.SetColumnSpan(statusBar, 6)", source);
        Assert.Contains("Grid.SetRow(statusBar, 3)", source);
    }

    [Fact]
    public void MainWindow_SourceDelegatesLayoutToBuilder()
    {
        var source = ReadRepoFile("src/App/Shell/MainWindow.axaml.cs");

        Assert.Contains("new MainLayoutBuilder().Build(", source);
        Assert.DoesNotContain("private (NavBar navBar", source);
        Assert.DoesNotContain("BuildLayout()", source);
        Assert.DoesNotContain("new GridLength(40)", source);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate repository file: {relativePath}");
    }
}
