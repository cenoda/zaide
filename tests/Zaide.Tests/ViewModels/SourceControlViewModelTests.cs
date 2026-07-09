using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Moq;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

public class SourceControlViewModelTests
{
    static SourceControlViewModelTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static ISourceControlSnapshotOrchestrator CreateOrchestrator(RepositoryStatusSnapshot snapshot)
    {
        var mock = new Mock<IGitRepositoryService>();
        mock.Setup(g => g.Discover(It.IsAny<string>()))
            .Returns(RepositoryDiscoveryResult.Found("/ws", "/ws/.git/"));
        mock.Setup(g => g.ReadStatus(It.IsAny<string>())).Returns(snapshot);
        return new SourceControlSnapshotOrchestrator(mock.Object);
    }

    private static ISourceControlSnapshotOrchestrator CreateOrchestrator(RepositoryDiscoveryResult discovery)
    {
        var mock = new Mock<IGitRepositoryService>();
        mock.Setup(g => g.Discover(It.IsAny<string>())).Returns(discovery);
        return new SourceControlSnapshotOrchestrator(mock.Object);
    }

    private static Workspace WorkspaceWithPath(string path = "/ws")
    {
        var workspace = new Workspace();
        workspace.SetProjectFromPath(path);
        return workspace;
    }

    private static RepositoryStatusSnapshot Snapshot(
        string currentBranch = "main",
        GitBranch[]? branches = null,
        FileChange[]? changes = null) =>
        new()
        {
            CurrentBranchName = currentBranch,
            Branches = branches ?? Array.Empty<GitBranch>(),
            Changes = changes ?? Array.Empty<FileChange>(),
        };

    [Fact]
    public void InitialState_LoadsBranchesFromSnapshot()
    {
        var state = new SourceControlState();
        var snapshot = Snapshot(branches: new[] { new GitBranch("main", true), new GitBranch("dev") });
        var vm = new SourceControlViewModel(state, CreateOrchestrator(snapshot), WorkspaceWithPath());

        Assert.Equal(2, vm.Branches.Count);
        Assert.Equal("main", vm.CurrentBranchName);
        Assert.Equal("main", state.Snapshot?.CurrentBranchName);
    }

    [Fact]
    public void InitialState_SplitsChangesByStagedFlag()
    {
        var snapshot = Snapshot(changes: new[]
        {
            new FileChange("a.cs", GitChangeType.Modified, isStaged: false),
            new FileChange("b.cs", GitChangeType.Added, isStaged: true),
        });
        var vm = new SourceControlViewModel(new SourceControlState(), CreateOrchestrator(snapshot), WorkspaceWithPath());

        Assert.Equal(1, vm.UnstagedCount);
        Assert.Equal(1, vm.StagedCount);
    }

    [Fact]
    public void InitialState_NoRepository_LeavesCollectionsEmpty()
    {
        var vm = new SourceControlViewModel(
            new SourceControlState(),
            CreateOrchestrator(RepositoryDiscoveryResult.NotFound("/ws")),
            WorkspaceWithPath());

        Assert.Empty(vm.Branches);
        Assert.Empty(vm.UnstagedChanges);
        Assert.Empty(vm.StagedChanges);
    }

    [Fact]
    public void InitialState_NoWorkspacePath_LeavesCollectionsEmpty()
    {
        var vm = new SourceControlViewModel(
            new SourceControlState(),
            new SourceControlSnapshotOrchestrator(new Mock<IGitRepositoryService>().Object),
            new Workspace());

        Assert.Empty(vm.Branches);
        Assert.Empty(vm.UnstagedChanges);
        Assert.Empty(vm.StagedChanges);
    }

    [Fact]
    public void StageFile_MovesFromUnstagedToStaged()
    {
        var snapshot = Snapshot(changes: new[]
        {
            new FileChange("a.cs", GitChangeType.Modified, isStaged: false),
        });
        var vm = new SourceControlViewModel(new SourceControlState(), CreateOrchestrator(snapshot), WorkspaceWithPath());

        var file = vm.UnstagedChanges.First();
        vm.StageFileCommand.Execute(file).Wait();

        Assert.DoesNotContain(file, vm.UnstagedChanges);
        Assert.Contains(file, vm.StagedChanges);
        Assert.True(file.IsStaged);
    }

    [Fact]
    public void UnstageFile_MovesFromStagedToUnstaged()
    {
        var snapshot = Snapshot(changes: new[]
        {
            new FileChange("b.cs", GitChangeType.Added, isStaged: true),
        });
        var vm = new SourceControlViewModel(new SourceControlState(), CreateOrchestrator(snapshot), WorkspaceWithPath());

        var file = vm.StagedChanges.First();
        vm.UnstageFileCommand.Execute(file).Wait();

        Assert.DoesNotContain(file, vm.StagedChanges);
        Assert.Contains(file, vm.UnstagedChanges);
        Assert.False(file.IsStaged);
    }

    [Fact]
    public void CommitCommand_ClearsStagedAndMessage()
    {
        var snapshot = Snapshot(changes: new[]
        {
            new FileChange("b.cs", GitChangeType.Added, isStaged: true),
        });
        var vm = new SourceControlViewModel(new SourceControlState(), CreateOrchestrator(snapshot), WorkspaceWithPath());

        vm.CommitMessage = "test commit";
        vm.CommitCommand.Execute().Wait();

        Assert.Empty(vm.StagedChanges);
        Assert.Equal(0, vm.StagedCount);
        Assert.Empty(vm.CommitMessage);
    }

    [Fact]
    public void SelectBranchCommand_UpdatesCurrentBranch()
    {
        var snapshot = Snapshot(branches: new[]
        {
            new GitBranch("main", true),
            new GitBranch("feature/agent-ui"),
        });
        var vm = new SourceControlViewModel(new SourceControlState(), CreateOrchestrator(snapshot), WorkspaceWithPath());

        var featureBranch = vm.Branches[1];
        vm.SelectBranchCommand.Execute(featureBranch).Wait();

        Assert.Equal(featureBranch, vm.SelectedBranch);
        Assert.Equal("feature/agent-ui", vm.CurrentBranchName);
    }

    [Fact]
    public void Refresh_Success_RepopulatesFromSnapshot()
    {
        var workspace = WorkspaceWithPath();
        var orchestrator = CreateOrchestrator(
            Snapshot(branches: new[] { new GitBranch("main", true) }));
        var vm = new SourceControlViewModel(new SourceControlState(), orchestrator, workspace);

        Assert.Equal(SnapshotRefreshStatus.Success, vm.LastRefreshStatus);
        Assert.Null(vm.LastRefreshError);
        Assert.Single(vm.Branches);
        Assert.Equal("main", vm.CurrentBranchName);

        vm.RefreshCommand.Execute().Wait();

        Assert.Equal(SnapshotRefreshStatus.Success, vm.LastRefreshStatus);
        Assert.Single(vm.Branches);
    }

    [Fact]
    public void Refresh_NonRepository_ProjectsEmptyStateTruthfully()
    {
        var workspace = WorkspaceWithPath();
        var orchestrator = CreateOrchestrator(RepositoryDiscoveryResult.NotFound("/ws"));
        var vm = new SourceControlViewModel(new SourceControlState(), orchestrator, workspace);

        Assert.Equal(SnapshotRefreshStatus.NotARepository, vm.LastRefreshStatus);
        Assert.Empty(vm.Branches);
        Assert.Empty(vm.UnstagedChanges);
        Assert.Empty(vm.StagedChanges);
        Assert.Null(vm.LastRefreshError);

        vm.RefreshCommand.Execute().Wait();

        Assert.Equal(SnapshotRefreshStatus.NotARepository, vm.LastRefreshStatus);
        Assert.Empty(vm.Branches);
    }

    [Fact]
    public void Refresh_Failure_ProjectsErrorWithoutFakeData()
    {
        var mock = new Mock<IGitRepositoryService>();
        mock.Setup(g => g.Discover(It.IsAny<string>()))
            .Returns(RepositoryDiscoveryResult.Found("/ws", "/ws/.git/"));
        mock.Setup(g => g.ReadStatus(It.IsAny<string>()))
            .Throws(new InvalidOperationException("boom"));
        var orchestrator = new SourceControlSnapshotOrchestrator(mock.Object);

        var vm = new SourceControlViewModel(
            new SourceControlState(), orchestrator, WorkspaceWithPath());

        Assert.Equal(SnapshotRefreshStatus.Failed, vm.LastRefreshStatus);
        Assert.Equal("boom", vm.LastRefreshError);
        Assert.Empty(vm.Branches);
        Assert.Empty(vm.UnstagedChanges);
        Assert.Empty(vm.StagedChanges);
    }
}
