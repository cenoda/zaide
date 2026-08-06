using System;
using System.IO;
using Xunit;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Workspace.Presentation;

namespace Zaide.Tests.Features.Workspace.Presentation;

/// <summary>
/// Phase 23 F9: file tree hover/selection decoration must track the pointer
/// without a multi-row highlight tail.
/// </summary>
public sealed class Phase23FileTreeHoverSelectionTests
{
    [Fact]
    public void FileTreeView_SourceUsesInstantHoverWithoutFullTreeRepaint()
    {
        var source = ReadRepoFile("src/Features/Workspace/Presentation/FileTreeView.cs");

        Assert.DoesNotContain("Animations.RunAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HoverBackground", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RepaintAllFileTreeRows", source, StringComparison.Ordinal);
        Assert.Contains("RepaintRowsForSelectionChange", source, StringComparison.Ordinal);
        Assert.Contains("GetRowsNeedingSelectionRepaint", source, StringComparison.Ordinal);
        Assert.Contains("_hoveredRow", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FileTreeView_PointerEntered_ClearsPreviousHoveredRowBeforePaintingNewHover()
    {
        var source = ReadRepoFile("src/Features/Workspace/Presentation/FileTreeView.cs");

        Assert.Contains("OnRowPointerEntered", source, StringComparison.Ordinal);
        Assert.Contains("PaintRowForSelection(_hoveredRow", source, StringComparison.Ordinal);
        Assert.Contains("row.Background = hoverBrush", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Animations.RunAsync(rowBorder", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FileTreeView_PointerExited_CancelsStaleExitWithoutClearingCurrentHover()
    {
        var source = ReadRepoFile("src/Features/Workspace/Presentation/FileTreeView.cs");

        Assert.Contains("OnRowPointerExited", source, StringComparison.Ordinal);
        Assert.Contains("if (!ReferenceEquals(_hoveredRow, row))", source, StringComparison.Ordinal);
        Assert.Contains("_hoveredRow = null", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FileTreeView_SelectionChange_UsesTargetedRepaint_NotFullTreeWalk()
    {
        var source = ReadRepoFile("src/Features/Workspace/Presentation/FileTreeView.cs");

        Assert.Contains("RepaintRowsForSelectionChange(previous, selected)", source, StringComparison.Ordinal);
        Assert.Contains("ShouldRepaintSelectionRow", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Subscribe(_ => RepaintAllFileTreeRows())", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldRepaintSelectionRow_SiblingFileMove_OnlyTouchesPreviousAndNew()
    {
        var previous = CreateFile("/tmp/a.md", "a.md");
        var current = CreateFile("/tmp/b.md", "b.md");
        var untouched = CreateFile("/tmp/c.md", "c.md");

        Assert.True(FileTreeView.ShouldRepaintSelectionRow(previous, previous, current));
        Assert.True(FileTreeView.ShouldRepaintSelectionRow(current, previous, current));
        Assert.False(FileTreeView.ShouldRepaintSelectionRow(untouched, previous, current));
    }

    [Fact]
    public void ShouldRepaintSelectionRow_ParentFolderTint_OnlyWhenAncestorStateChanges()
    {
        var docsFolder = CreateFolder("/tmp/docs", "docs");
        var previous = CreateFile("/tmp/docs/a.md", "a.md");
        var current = CreateFile("/tmp/other/b.md", "b.md");
        var otherFolder = CreateFolder("/tmp/other", "other");

        Assert.True(FileTreeView.ShouldRepaintSelectionRow(docsFolder, previous, current));
        Assert.True(FileTreeView.ShouldRepaintSelectionRow(otherFolder, previous, current));
        Assert.False(FileTreeView.ShouldRepaintSelectionRow(otherFolder, previous, previous));
    }

    [Fact]
    public void FileTreeView_FastPointerScrub_Invariant_IsSingleHoveredRowTracking()
    {
        var source = ReadRepoFile("src/Features/Workspace/Presentation/FileTreeView.cs");

        // Entering a new row clears the previous hover row before assignment.
        Assert.Contains("_hoveredRow = row", source, StringComparison.Ordinal);
        Assert.Contains("!ReferenceEquals(_hoveredRow, row)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HoverDuration", source, StringComparison.Ordinal);
    }

    private static FileTreeNode CreateFile(string fullPath, string name) =>
        new()
        {
            Name = name,
            FullPath = fullPath,
            IsDirectory = false,
            Depth = 0,
        };

    private static FileTreeNode CreateFolder(string fullPath, string name) =>
        new()
        {
            Name = name,
            FullPath = fullPath,
            IsDirectory = true,
            Depth = 0,
        };

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
