using System;
using System.IO;
using Xunit;
using Zaide.App.Shell;

namespace Zaide.Tests.App.Shell;

/// <summary>
/// Phase 23 F11: non-settings status segments are display-only (no dead buttons).
/// Full <see cref="StatusBar"/> construction needs resource-backed
/// <c>Application.Current</c>; structural source proofs cover the product policy.
/// </summary>
public sealed class StatusBarTests
{
    [Fact]
    public void StatusBar_SourceRemovesNoOpSegmentCommand()
    {
        var source = ReadRepoFile("src/App/Shell/StatusBar.cs");

        Assert.DoesNotContain("StatusSegmentCommand", source, StringComparison.Ordinal);
        Assert.DoesNotContain("intentional no-op", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReactiveCommand.Create(() => { })", source, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusBar_SourceUsesDisplayOnlySegments_ExceptSettings()
    {
        var source = ReadRepoFile("src/App/Shell/StatusBar.cs");

        // Display-only path for document/caret/language/project/branch.
        Assert.Contains("BuildStatusSegment(\"Icon.Text\"", source, StringComparison.Ordinal);
        Assert.Contains("BuildStatusSegment(\"Icon.Selection\"", source, StringComparison.Ordinal);
        Assert.Contains("BuildStatusSegment(\"Icon.Code\"", source, StringComparison.Ordinal);
        Assert.Contains("BuildStatusSegment(\"Icon.Project\"", source, StringComparison.Ordinal);
        Assert.Contains("BuildStatusSegment(\"Icon.GitBranch\"", source, StringComparison.Ordinal);

        // Settings remains the only interactive control with a real command.
        Assert.Contains("AppButton.Ghost(", source, StringComparison.Ordinal);
        Assert.Contains("OpenSettingsCommand", source, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(_settingsButton, \"Settings\")", source, StringComparison.Ordinal);
        Assert.Contains("ToolTip.SetTip(_settingsButton, \"Settings\")", source, StringComparison.Ordinal);

        // Display segments must not be Buttons with Hand cursor.
        Assert.Contains("private static Control BuildStatusSegment(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildStatusSegmentButton(", source, StringComparison.Ordinal);
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
