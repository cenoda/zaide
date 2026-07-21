using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using ReactiveUI.Builder;
using Xunit;
using Zaide.App.Shell;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests.App.Shell;

/// <summary>
/// Structural coverage for <see cref="RightColumnHost"/> after Refactor 8 M3 and
/// Phase 14 M8 (editor-only right column; no Agent Panel chrome).
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
        Assert.DoesNotContain(type.GetProperties(BindingFlags.Instance | BindingFlags.Public),
            p => p.Name.Contains("AgentPanel", StringComparison.Ordinal));
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
        Assert.DoesNotContain("AgentPanelHostView", source);
        Assert.DoesNotContain("GridSplitter", source);
    }

    [Fact]
    public void MainWindow_SourceDoesNotWireAgentPanelChrome()
    {
        var source = ReadRepoFile("src/App/Shell/MainWindow.axaml.cs");

        Assert.DoesNotContain("AgentPanelHostView", source);
        Assert.DoesNotContain("SendAgentMessageAsync", source);
        Assert.DoesNotContain("PanelSendRequested", source);
    }

    [Fact]
    public void MainLayoutBuilder_SourceDelegatesRightColumnToHost()
    {
        var source = ReadRepoFile("src/App/Shell/MainLayoutBuilder.cs");

        Assert.Contains("new RightColumnHost(", source);
        Assert.Contains("rightColumnHost.AttachToLayoutGrid(grid)", source);
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
