using System;
using System.IO;
using Xunit;

namespace Zaide.Tests.Features.Townhall.Presentation;

/// <summary>
/// Phase 23 F4: Townhall message filters and transparency openers must be
/// structurally separated in the toolbar chrome.
/// </summary>
public sealed class Phase23TownhallToolbarTests
{
    [Fact]
    public void TownhallView_SeparatesMessageFiltersFromTransparencyOpeners()
    {
        var townhallSource = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "Features",
                "Townhall",
                "Presentation",
                "TownhallView.cs"));

        Assert.Contains("Message filter", townhallSource, StringComparison.Ordinal);
        Assert.Contains("Transparency evidence panels", townhallSource, StringComparison.Ordinal);
        Assert.Contains("Show all messages", townhallSource, StringComparison.Ordinal);
        Assert.Contains("Show chat messages only", townhallSource, StringComparison.Ordinal);
        Assert.Contains("Show activity messages only", townhallSource, StringComparison.Ordinal);
        Assert.Contains("CreateMessageFilterToggle", townhallSource, StringComparison.Ordinal);
        Assert.Contains("CreateTransparencyOpenerButton", townhallSource, StringComparison.Ordinal);
        Assert.Contains("Open or close agent trace evidence", townhallSource, StringComparison.Ordinal);
        Assert.Contains("Open or close agent durable memory", townhallSource, StringComparison.Ordinal);
        Assert.Contains("Open or close agent usage and cost evidence", townhallSource, StringComparison.Ordinal);
        Assert.Contains("RadiusFull", townhallSource, StringComparison.Ordinal);
        Assert.Contains("BorderThickness = new Thickness(1)", townhallSource, StringComparison.Ordinal);
        Assert.Contains("Width = 2", townhallSource, StringComparison.Ordinal);
        Assert.Contains("Spacing = LayoutTokens.SpacingMd", townhallSource, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Zaide.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
