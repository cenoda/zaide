using System;
using System.IO;
using Xunit;

namespace Zaide.Tests.Features.Workspace.Presentation;

/// <summary>
/// Phase 23 F9 / Refactor 10 M4c: file tree row hover uses the shared
/// <see cref="Zaide.UI.DesignSystem.ListRow"/> interactive surface; selection
/// paint (active strip, parent folder) remains explicit.
/// </summary>
public sealed class Phase23FileTreeHoverSelectionTests
{
    [Fact]
    public void FileTreeView_SourceUsesSharedListRowWithoutManualHoverHandlers()
    {
        var source = ReadRepoFile("src/Features/Workspace/Presentation/FileTreeView.cs");

        Assert.DoesNotContain("Animations.RunAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HoverBackground", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PointerEntered", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PointerExited", source, StringComparison.Ordinal);
        Assert.Contains("ListRow.Create", source, StringComparison.Ordinal);
        Assert.Contains("RepaintAllFileTreeRows", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FileTreeView_PaintRowForSelection_PreservesActiveStripAndParentFolderLogic()
    {
        var source = ReadRepoFile("src/Features/Workspace/Presentation/FileTreeView.cs");

        Assert.Contains("PaintRowForSelection(rowBorder, activeStrip, node)", source, StringComparison.Ordinal);
        Assert.Contains("activeStrip.Background = activeBrush", source, StringComparison.Ordinal);
        Assert.Contains("row.Background = activeBgBrush", source, StringComparison.Ordinal);
        Assert.Contains("row.Background = parentFolderBgBrush", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FileTreeView_SelectionChange_RepaintsVisibleRows()
    {
        var source = ReadRepoFile("src/Features/Workspace/Presentation/FileTreeView.cs");

        Assert.Contains("Subscribe(_ => RepaintAllFileTreeRows())", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RepaintRowsForSelectionChange", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ShouldRepaintSelectionRow", source, StringComparison.Ordinal);
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

        throw new FileNotFoundException($"Could not locate repo file: {relativePath}");
    }
}
