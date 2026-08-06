using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Xunit;
using Zaide.App.Shell;
using Zaide.Tests.Infrastructure;

namespace Zaide.Tests.App.Shell;

/// <summary>
/// Phase 23 F10: Phosphor icons paint as fill-oriented glyphs and icon-only
/// controls expose tooltip + automation names.
/// </summary>
public sealed class Phase23IconFactoryTests
{
    [Fact]
    public void IconFactory_Create_UsesFillBrush()
    {
        var brush = Brushes.Red;
        var icon = IconFactory.Create("Icon.ArrowClockwise", brush, 14);
        var path = AssertPath(icon);

        Assert.Same(brush, path.Fill);
        Assert.Null(path.Stroke);
    }

    [Fact]
    public void IconFactory_SetForeground_UpdatesFill()
    {
        var icon = IconFactory.Create("Icon.ArrowClockwise", Brushes.Red, 14);
        IconFactory.SetForeground(icon, Brushes.Blue);

        var path = AssertPath(icon);
        Assert.Same(Brushes.Blue, path.Fill);
    }

    [Fact]
    public void IconFactory_ResolveIconGeometry_KnownKey_ReturnsGeometry()
    {
        ReactiveUiTestBootstrap.EnsureApplication();

        foreach (var key in new[] { "Icon.ArrowClockwise", "Icon.GitBranch" })
        {
            var icon = IconFactory.Create(key, Brushes.Black, 14);
            var path = AssertPath(icon);
            Assert.NotNull(path.Data);
        }
    }

    [Fact]
    public void SourceControlPanel_RefreshButton_HasTooltipAndAutomationName()
    {
        var source = ReadRepoFile("src/Features/SourceControl/Presentation/SourceControlPanel.cs");

        Assert.Contains("ToolTip.SetTip(refreshButton, \"Refresh source control\")", source, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(refreshButton, \"Refresh source control\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Icon.GitBranch\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void IconOnlyControls_SourceHaveTooltipAndAutomationName()
    {
        AssertIconOnlyA11y(
            "src/Features/Workspace/Presentation/FileTreeView.cs",
            "_closeFolderButton",
            "Close folder");
        AssertIconOnlyA11y(
            "src/Features/Editor/Presentation/EditorTabBar.cs",
            "closeButton",
            "Close tab");
        AssertIconOnlyA11y(
            "src/Features/Terminal/Presentation/TerminalTabStrip.cs",
            "closeButton",
            "Close terminal tab");
        AssertIconOnlyA11y(
            "src/Features/Terminal/Presentation/TerminalTabStrip.cs",
            "button",
            "New terminal tab");
    }

    private static void AssertIconOnlyA11y(string relativePath, string controlName, string label)
    {
        var source = ReadRepoFile(relativePath);
        Assert.Contains($"ToolTip.SetTip({controlName}, \"{label}\")", source, StringComparison.Ordinal);
        Assert.Contains($"AutomationProperties.SetName({controlName}, \"{label}\")", source, StringComparison.Ordinal);
    }

    private static Avalonia.Controls.Shapes.Path AssertPath(Control icon)
    {
        var viewbox = Assert.IsType<Viewbox>(icon);
        return Assert.IsType<Avalonia.Controls.Shapes.Path>(viewbox.Child);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = System.IO.Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate repo file: {relativePath}");
    }
}
