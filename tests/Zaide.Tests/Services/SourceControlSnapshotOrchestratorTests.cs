using System;
using Moq;
using Xunit;
using Zaide.Services;

namespace Zaide.Tests.Services;

public class SourceControlSnapshotOrchestratorTests
{
    [Fact]
    public void Refresh_NoWorkspacePath_ProjectsNonRepository()
    {
        var orchestrator = new SourceControlSnapshotOrchestrator(new Mock<IGitRepositoryService>().Object);

        var result = orchestrator.Refresh(null);

        Assert.Equal(SnapshotRefreshStatus.NotARepository, result.Status);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public void Refresh_NonRepository_ProjectsNonRepository()
    {
        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>()))
            .Returns(RepositoryDiscoveryResult.NotFound("/ws"));
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);

        var result = orchestrator.Refresh("/ws");

        Assert.Equal(SnapshotRefreshStatus.NotARepository, result.Status);
        Assert.Null(result.Snapshot);
        Assert.Empty(result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public void Refresh_Success_ReturnsSnapshotFromGitSeam()
    {
        var snapshot = new RepositoryStatusSnapshot { CurrentBranchName = "main" };
        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>()))
            .Returns(RepositoryDiscoveryResult.Found("/ws", "/ws/.git/"));
        git.Setup(g => g.ReadStatus(It.IsAny<string>())).Returns(snapshot);
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);

        var result = orchestrator.Refresh("/ws");

        Assert.Equal(SnapshotRefreshStatus.Success, result.Status);
        Assert.Same(snapshot, result.Snapshot);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Refresh_DiscoverThrows_ProjectsFailure()
    {
        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>()))
            .Throws(new InvalidOperationException("disk gone"));
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);

        var result = orchestrator.Refresh("/ws");

        Assert.Equal(SnapshotRefreshStatus.Failed, result.Status);
        Assert.Equal("disk gone", result.ErrorMessage);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public void Refresh_ReadStatusThrows_ProjectsFailure()
    {
        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>()))
            .Returns(RepositoryDiscoveryResult.Found("/ws", "/ws/.git/"));
        git.Setup(g => g.ReadStatus(It.IsAny<string>()))
            .Throws(new UnauthorizedAccessException("no access"));
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);

        var result = orchestrator.Refresh("/ws");

        Assert.Equal(SnapshotRefreshStatus.Failed, result.Status);
        Assert.Equal("no access", result.ErrorMessage);
        Assert.Null(result.Snapshot);
    }
}
