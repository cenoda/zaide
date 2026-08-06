using System;
using System.IO;
using Xunit;
using Zaide.App.Shell;

namespace Zaide.Tests.App.Shell;

/// <summary>
/// Phase 23 F10: Lucide-backed icons via IconFactory and icon-only a11y contracts.
/// Paint tests use source/map contracts — LucideIcon instantiation needs a render
/// platform and is verified manually (see F10 plan checklist).
/// </summary>
public sealed class Phase23IconFactoryTests
{
    [Fact]
    public void IconFactory_Create_UsesLucideIconContract()
    {
        var source = ReadRepoFile("src/App/Shell/IconFactory.cs");

        Assert.Contains("new LucideIcon", source, StringComparison.Ordinal);
        Assert.Contains("Foreground = foreground", source, StringComparison.Ordinal);
        Assert.Contains("IsHitTestVisible = false", source, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp(size / 8.0, 1.25, 2.0)", source, StringComparison.Ordinal);
        Assert.Contains("AttachedToVisualTree", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Viewbox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StreamGeometry", source, StringComparison.Ordinal);
    }

    [Fact]
    public void IconFactory_SetForeground_UpdatesLucideForeground()
    {
        var source = ReadRepoFile("src/App/Shell/IconFactory.cs");

        Assert.Contains("if (icon is LucideIcon lucideIcon)", source, StringComparison.Ordinal);
        Assert.Contains("lucideIcon.Foreground = foreground", source, StringComparison.Ordinal);
    }

    [Fact]
    public void IconLucideMap_AllKnownKeys_Resolve()
    {
        foreach (var key in IconLucideMap.AllKeys)
        {
            var kind = IconLucideMap.Resolve(key);
            Assert.True(Enum.IsDefined(kind));
        }
    }

    [Fact]
    public void IconLucideMap_IncludesLegacyAndNavKeys()
    {
        string[] required =
        [
            "Icon.ArrowClockwise",
            "Icon.GitBranch",
            "Icon.Folder",
            "Icon.Code",
            "Icon.Text",
            "Icon.Image",
            "Icon.Config",
            "Icon.Markup",
            "Icon.Project",
            "Icon.Unknown",
            "Icon.X",
            "Icon.Plus",
            "Icon.Search",
            "Icon.Terminal",
            "Icon.Broom",
            "Icon.ChevronDown",
            "Icon.ChevronLeft",
            "Icon.ArrowUp",
            "Icon.Selection",
            "Icon.Bell",
            "Icon.Info",
            "Icon.Pin",
            "Icon.Warning",
            "Icon.CheckCircle",
            "Icon.Explorer",
            "Icon.SourceControl",
            "Icon.Avatar",
        ];

        foreach (var key in required)
            Assert.Contains(key, IconLucideMap.AllKeys);
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
    public void NavBar_UsesIconFactory_NotInlinePaths()
    {
        var source = ReadRepoFile("src/App/Shell/NavBar.cs");

        Assert.Contains("IconFactory.Create(", source, StringComparison.Ordinal);
        Assert.Contains("\"Icon.Explorer\"", source, StringComparison.Ordinal);
        Assert.Contains("\"Icon.SourceControl\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateNavIcon", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StreamGeometry.Parse", source, StringComparison.Ordinal);
    }

    [Fact]
    public void IconsAxaml_RemovedFromAppResources()
    {
        var appAxaml = ReadRepoFile("src/App/Composition/App.axaml");
        Assert.DoesNotContain("Icons.axaml", appAxaml, StringComparison.Ordinal);

        var iconsPath = Path.Combine(ResolveRepoRoot(), "src/UI/DesignSystem/Icons.axaml");
        Assert.False(File.Exists(iconsPath));
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

    private static string ReadRepoFile(string relativePath) =>
        File.ReadAllText(ResolveRepoPath(relativePath));

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Zaide.slnx")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate repository root.");
    }

    private static string ResolveRepoPath(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate repo file: {relativePath}");
    }
}
