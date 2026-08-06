using System;
using System.IO;
using Xunit;

namespace Zaide.Tests.Features.Workspace.Presentation;

/// <summary>
/// Phase 23 F9: file tree hover/selection decoration must track the pointer
/// without sticky multi-row highlights.
/// </summary>
public sealed class Phase23FileTreeHoverSelectionTests
{
    [Fact]
    public void FileTreeView_SourceUsesInstantHoverWithoutAnimationTail()
    {
        var source = ReadRepoFile("src/Features/Workspace/Presentation/FileTreeView.cs");

        Assert.DoesNotContain("Animations.RunAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HoverBackground", source, StringComparison.Ordinal);
        Assert.Contains("_hoveredRow", source, StringComparison.Ordinal);
        Assert.Contains("RepaintAllFileTreeRows", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FileTreeView_PointerEntered_ClearsPreviousRowBeforeNewHover()
    {
        var source = ReadRepoFile("src/Features/Workspace/Presentation/FileTreeView.cs");

        Assert.Contains("var previous = _hoveredRow", source, StringComparison.Ordinal);
        Assert.Contains("_hoveredRow = rowBorder", source, StringComparison.Ordinal);
        Assert.Contains("PaintRowForSelection(previous", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FileTreeView_PointerExited_RestoresSelectionPaintForCurrentHoverRow()
    {
        var source = ReadRepoFile("src/Features/Workspace/Presentation/FileTreeView.cs");

        Assert.Contains("if (!ReferenceEquals(_hoveredRow, rowBorder))", source, StringComparison.Ordinal);
        Assert.Contains("_hoveredRow = null", source, StringComparison.Ordinal);
        Assert.Contains("PaintRowForSelection(rowBorder, activeStrip, node)", source, StringComparison.Ordinal);
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
