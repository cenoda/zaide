using System;
using Moq;
using Xunit;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.Tests.Services;

public sealed class SourceControlDiffTabServiceTests
{
    [Fact]
    public void Format_BinaryFile_ReturnsNotice()
    {
        var change = new FileChange("app.dll", GitChangeType.Modified, isStaged: false);
        var result = new FileDiffResult { FilePath = "app.dll", IsBinary = true };

        Assert.Equal("Binary file — diff not available", SourceControlDiffContent.Format(change, result));
    }

    [Fact]
    public void Format_NullDiff_ReturnsGracefulNotice()
    {
        var change = new FileChange("missing.txt", GitChangeType.Deleted, isStaged: false);

        Assert.Equal(
            "No diff available for missing.txt",
            SourceControlDiffContent.Format(change, diff: null));
    }

    [Fact]
    public void ToVirtualPath_DoesNotCollideWithWorkspacePaths()
    {
        Assert.Equal(
            "zaide-sc-diff://src/Program.cs",
            SourceControlDiffTabKey.ToVirtualPath("src/Program.cs"));
    }
}
