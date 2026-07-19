using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using ReactiveUI.Builder;
using Xunit;
using Zaide.App.Shell;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests.App.Shell;

/// <summary>
/// Structural coverage for <see cref="RightColumnHost"/> after Refactor 8 M3.
/// Full host construction is not exercised here: the host always builds
/// <see cref="EditorView"/>, whose constructor allocates popups that read
/// <c>Application.Current.Resources</c> at construction time. In this test
/// assembly <see cref="ReactiveUI.Builder.RxAppBuilder.BuildApp"/> does not
/// establish a resource-backed <c>Application.Current</c>, and Avalonia does
/// not expose a supported setter in this stack version.
/// </summary>
public sealed class RightColumnHostSourceTests
{
    static RightColumnHostSourceTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    [Fact]
    public void RightColumnHost_IsInternalShellTypeWithExpectedSurfaceProperties()
    {
        var type = typeof(RightColumnHost);

        Assert.False(type.IsPublic);
        Assert.Equal("Zaide.App.Shell", type.Namespace);
        Assert.Contains(type.GetProperties(BindingFlags.Instance | BindingFlags.Public),
            p => p.Name == nameof(RightColumnHost.Root) && p.PropertyType == typeof(Grid));
        Assert.Contains(type.GetProperties(BindingFlags.Instance | BindingFlags.Public),
            p => p.Name == nameof(RightColumnHost.EditorTabBar) && p.PropertyType == typeof(EditorTabBar));
        Assert.Contains(type.GetProperties(BindingFlags.Instance | BindingFlags.Public),
            p => p.Name == nameof(RightColumnHost.SearchBar) && p.PropertyType == typeof(SearchBar));
        Assert.Contains(type.GetProperties(BindingFlags.Instance | BindingFlags.Public),
            p => p.Name == nameof(RightColumnHost.EditorView) && p.PropertyType == typeof(EditorView));
        Assert.Contains(type.GetProperties(BindingFlags.Instance | BindingFlags.Public),
            p => p.Name == nameof(RightColumnHost.WelcomeText) && p.PropertyType == typeof(TextBlock));
        Assert.Contains(type.GetProperties(BindingFlags.Instance | BindingFlags.Public),
            p => p.Name == nameof(RightColumnHost.AgentPanelHostView)
                 && p.PropertyType == typeof(AgentPanelHostView));
        Assert.Contains(type.GetMethods(BindingFlags.Instance | BindingFlags.Public),
            m => m.Name == nameof(RightColumnHost.AttachToLayoutGrid));
    }

    [Fact]
    public void RightColumnHost_SourceRetainsEditorSearchAndWelcomePlacement()
    {
        var source = ReadRepoFile("src/App/Shell/RightColumnHost.cs");

        Assert.Contains("Grid.SetRow(EditorTabBar, 0)", source);
        Assert.Contains("Grid.SetRow(SearchBar, 1)", source);
        Assert.Contains("Grid.SetRow(EditorView, 2)", source);
        Assert.Contains("Grid.SetRow(WelcomeText, 2)", source);
        Assert.Contains("WelcomeText.IsVisible = true", source);
        Assert.Contains("TextStyles.Body(\"Open a file to begin\")", source);
        Assert.Contains("new SearchBar(searchViewModel)", source);
        Assert.Contains("new AgentPanelHostView()", source);
    }

    [Fact]
    public void RightColumnHost_SourceRetainsVerticalSplitterAndStarRows()
    {
        var source = ReadRepoFile("src/App/Shell/RightColumnHost.cs");

        Assert.Contains("new GridLength(2, GridUnitType.Star)", source);
        Assert.Contains("new GridLength(4, GridUnitType.Pixel)", source);
        Assert.Contains("new GridLength(1, GridUnitType.Star)", source);
        Assert.Contains("ResizeDirection = GridResizeDirection.Rows", source);
        Assert.Contains("Grid.SetRow(AgentPanelHostView, 2)", source);
        Assert.Contains("LayoutTokens.Inset(1, 0, 0, 0)", source);
        Assert.Contains("SurfacePanelBrush", source);
    }

    [Fact]
    public void MainWindow_SourceDelegatesRightColumnToHost()
    {
        var source = ReadRepoFile("src/App/Shell/MainWindow.axaml.cs");

        Assert.Contains("new RightColumnHost(", source);
        Assert.Contains("rightColumnHost.AttachToLayoutGrid(grid)", source);
        Assert.Contains("RightColumnHost rightColumnHost", source);
        Assert.DoesNotContain("var rightColumn = new Grid", source);
        Assert.DoesNotContain("var rightSplitterH = new GridSplitter", source);
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
