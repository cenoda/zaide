using System;
using System.IO;
using Xunit;

namespace Zaide.Tests.Features.Townhall.Presentation;

/// <summary>
/// Phase 23 F1: Trace / Memory / Usage must live in a dedicated inspect host
/// beside the chat message list — not as Auto rows that replace the chat Star band.
/// Open-flag exclusivity is intentionally unchanged.
/// </summary>
public sealed class Phase23InspectHostChatStarRowTests
{
    [Fact]
    public void TownhallView_PreservesChatStarBand_AndHostsInspectSideSheet()
    {
        var townhallSource = ReadRepoFile(
            "src/Features/Townhall/Presentation/TownhallView.cs");

        // Star band for messages + side sheet column layout.
        Assert.Contains("messageWorkspace", townhallSource, StringComparison.Ordinal);
        Assert.Contains("new GridLength(1, GridUnitType.Star), MinWidth = 160", townhallSource, StringComparison.Ordinal);
        Assert.Contains(
            "new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 120 }",
            townhallSource,
            StringComparison.Ordinal);
        Assert.Contains("AgentInspectHost", townhallSource, StringComparison.Ordinal);
        Assert.Contains("_inspectHost", townhallSource, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumn(_inspectHost, 1)", townhallSource, StringComparison.Ordinal);

        // Panels must not reappear as independent chat-area Auto rows under chat.
        Assert.DoesNotContain("Grid.SetRow(_tracePanel,", townhallSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Grid.SetRow(_memoryPanel,", townhallSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Grid.SetRow(_usagePanel,", townhallSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Children =\n            {\n                filterGroup,\n                _chatPanel,\n                _tracePanel", townhallSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentInspectHost_VisibilityIsOrOfOpenFlags()
    {
        var hostSource = ReadRepoFile(
            "src/Features/Agents/Presentation/AgentInspectHost.cs");

        Assert.Contains("IsTracePanelOpen", hostSource, StringComparison.Ordinal);
        Assert.Contains("IsMemoryPanelOpen", hostSource, StringComparison.Ordinal);
        Assert.Contains("IsUsagePanelOpen", hostSource, StringComparison.Ordinal);
        Assert.Contains("DefaultSheetWidth", hostSource, StringComparison.Ordinal);
        Assert.Contains("Agent inspect host", hostSource, StringComparison.Ordinal);
        // Host stays visible for whichever single surface is open (OR of flags).
        Assert.Contains("|| _viewModel.IsMemoryPanelOpen", hostSource, StringComparison.Ordinal);
        Assert.Contains("|| _viewModel.IsUsagePanelOpen", hostSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentTransparencyManagement_OpenPathsEnforceMutualExclusivity()
    {
        var managementSource = ReadRepoFile(
            "src/Features/Agents/Presentation/Transparency/AgentTransparencyManagementViewModel.cs");

        Assert.Contains("CloseSiblingInspectSurfaces", managementSource, StringComparison.Ordinal);
        Assert.Contains("keepTrace: true", managementSource, StringComparison.Ordinal);
        Assert.Contains("keepMemory: true", managementSource, StringComparison.Ordinal);
        Assert.Contains("keepUsage: true", managementSource, StringComparison.Ordinal);
        Assert.Contains("ToggleTraceCommand", managementSource, StringComparison.Ordinal);
        Assert.Contains("ToggleMemoryCommand", managementSource, StringComparison.Ordinal);
        Assert.Contains("ToggleUsageCommand", managementSource, StringComparison.Ordinal);
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

        throw new FileNotFoundException($"Could not locate repository file: {relativePath}");
    }
}
