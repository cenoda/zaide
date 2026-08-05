using System;
using System.IO;
using Xunit;

namespace Zaide.Tests.Features.SourceControl.Presentation;

/// <summary>
/// Phase 23 F12a: Source Control change lists must not dual two-way bind one
/// <c>SelectedFileChange</c> to both staged and unstaged ListBoxes (multi-row
/// highlight). Selection is exclusive across the two lists.
/// </summary>
public sealed class Phase23SourceControlSelectionTests
{
    [Fact]
    public void SourceControlPanel_DoesNotDualBindSelectedFileChangeToBothLists()
    {
        var panelSource = ReadRepoFile(
            "src/Features/SourceControl/Presentation/SourceControlPanel.cs");

        // Dual two-way Bind on one property was the F12a root cause.
        Assert.DoesNotContain(
            "this.Bind(ViewModel, vm => vm.SelectedFileChange, v => v._unstagedList.SelectedItem)",
            panelSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "this.Bind(ViewModel, vm => vm.SelectedFileChange, v => v._stagedList.SelectedItem)",
            panelSource,
            StringComparison.Ordinal);

        // Exclusive projection + sibling clear is the replacement path.
        Assert.Contains("ApplyExclusiveListSelection", panelSource, StringComparison.Ordinal);
        Assert.Contains("_stagedList.SelectedItem = null", panelSource, StringComparison.Ordinal);
        Assert.Contains("_unstagedList.SelectedItem = null", panelSource, StringComparison.Ordinal);
        Assert.Contains("SelectFileCommand", panelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceControlPanel_ExposesUnstageAllSymmetricToStageAll()
    {
        var panelSource = ReadRepoFile(
            "src/Features/SourceControl/Presentation/SourceControlPanel.cs");

        Assert.Contains("Unstage All", panelSource, StringComparison.Ordinal);
        Assert.Contains("_unstageAllButton", panelSource, StringComparison.Ordinal);
        Assert.Contains("UnstageAllCommand", panelSource, StringComparison.Ordinal);
        Assert.Contains("_unstageAllButton.IsVisible = count > 0", panelSource, StringComparison.Ordinal);
        Assert.Contains("Stage All", panelSource, StringComparison.Ordinal);
        Assert.Contains("StageAllCommand", panelSource, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate '{relativePath}' walking up from {AppContext.BaseDirectory}.");
    }
}
