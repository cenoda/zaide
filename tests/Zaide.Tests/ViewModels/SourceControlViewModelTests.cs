using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Returns a mock <see cref="IFileDiffService"/> whose <c>GetDiff</c> returns null.
    /// Used by existing tests that do not exercise diff behavior.
    /// </summary>
    private static IFileDiffService NullDiffService()
    {
        var mock = new Mock<IFileDiffService>();
        mock.Setup(d => d.GetDiff(It.IsAny<string>(), It.IsAny<FileChange>()))
            .Returns((FileDiffResult?)null);
        return mock.Object;
    }

    private static Workspace WorkspaceWithPath(string path = "/ws")
    {
        var workspace = new Workspace();
        workspace.SetProjectFromPath(path);
        return workspace;
    }

    /// <summary>Default no-op mutation service for tests that do not exercise stage/unstage.</summary>
    private static IGitMutationService DefaultMutation() => Mock.Of<IGitMutationService>();

    /// <summary>Default git-repository mock whose Discover() resolves "/ws" to a valid repository root.</summary>
    private static IGitRepositoryService DefaultGitRepo()
    {
        var mock = new Mock<IGitRepositoryService>();
        mock.Setup(g => g.Discover(It.IsAny<string>()))
            .Returns(RepositoryDiscoveryResult.Found("/ws", "/ws/.git/"));
        return mock.Object;
    }

    private static RepositoryStatusSnapshot Snapshot(
        string currentBranch = "main",
        GitBranch[]? branches = null,
        FileChange[]? changes = null,
        int aheadBy = 0,
        bool hasUpstream = false) =>
        new()
        {
            CurrentBranchName = currentBranch,
            Branches = branches ?? Array.Empty<GitBranch>(),
            Changes = changes ?? Array.Empty<FileChange>(),
            AheadBy = aheadBy,
            HasUpstream = hasUpstream,
        };

    [Fact]
    public void InitialState_LoadsBranchesFromSnapshot()
    {
        var snapshot = Snapshot(branches: new[] { new GitBranch("main", true), new GitBranch("dev") });
        var vm = new SourceControlViewModel(CreateOrchestrator(snapshot), WorkspaceWithPath(), NullDiffService(), DefaultMutation(), DefaultGitRepo());

        Assert.Equal(2, vm.Branches.Count);
        Assert.Equal("main", vm.CurrentBranchName);
    }

    [Fact]
    public void InitialState_SplitsChangesByStagedFlag()
    {
        var snapshot = Snapshot(changes: new[]
        {
            new FileChange("a.cs", GitChangeType.Modified, isStaged: false),
            new FileChange("b.cs", GitChangeType.Added, isStaged: true),
        });
        var vm = new SourceControlViewModel(CreateOrchestrator(snapshot), WorkspaceWithPath(), NullDiffService(), DefaultMutation(), DefaultGitRepo());

        Assert.Equal(1, vm.UnstagedCount);
        Assert.Equal(1, vm.StagedCount);
    }

    [Fact]
    public void InitialState_NoRepository_LeavesCollectionsEmpty()
    {
        var vm = new SourceControlViewModel(
            CreateOrchestrator(RepositoryDiscoveryResult.NotFound("/ws")),
            WorkspaceWithPath(),
            NullDiffService(),
            DefaultMutation(),
            DefaultGitRepo());

        Assert.Empty(vm.Branches);
        Assert.Empty(vm.UnstagedChanges);
        Assert.Empty(vm.StagedChanges);
    }

    [Fact]
    public void InitialState_NoWorkspacePath_LeavesCollectionsEmpty()
    {
        var vm = new SourceControlViewModel(
            new SourceControlSnapshotOrchestrator(new Mock<IGitRepositoryService>().Object),
            new Workspace(),
            NullDiffService(),
            DefaultMutation(),
            DefaultGitRepo());

        Assert.Empty(vm.Branches);
        Assert.Empty(vm.UnstagedChanges);
        Assert.Empty(vm.StagedChanges);
    }

    [Fact]
    public void StageFileCommand_CallsMutationSeamAndRefreshes()
    {
        var unstagedSnapshot = Snapshot(changes: new[]
        {
            new FileChange("a.cs", GitChangeType.Modified, isStaged: false),
        });
        var stagedSnapshot = Snapshot(changes: new[]
        {
            new FileChange("a.cs", GitChangeType.Modified, isStaged: true),
        });

        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover("/ws")).Returns(RepositoryDiscoveryResult.Found("/ws", "/ws/.git/"));
        git.SetupSequence(g => g.ReadStatus("/ws/.git/"))
            .Returns(unstagedSnapshot)
            .Returns(stagedSnapshot);
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);

        var mutation = new Mock<IGitMutationService>();
        mutation.Setup(m => m.Stage("/ws/.git/", "a.cs")).Returns(StageResult.Success());

        var vm = new SourceControlViewModel(orchestrator, WorkspaceWithPath(), NullDiffService(), mutation.Object, git.Object);

        var file = vm.UnstagedChanges.First();
        vm.StageFileCommand.Execute(file).Wait();

        mutation.Verify(m => m.Stage("/ws/.git/", "a.cs"), Times.Once);
        // Refresh was called after mutation: the second (staged) snapshot is now in effect.
        Assert.Empty(vm.UnstagedChanges);
        Assert.Single(vm.StagedChanges);
        Assert.Null(vm.StatusMessage);
    }

    [Fact]
    public void UnstageFileCommand_CallsMutationSeamAndRefreshes()
    {
        var stagedSnapshot = Snapshot(changes: new[]
        {
            new FileChange("b.cs", GitChangeType.Added, isStaged: true),
        });
        var unstagedSnapshot = Snapshot(changes: new[]
        {
            new FileChange("b.cs", GitChangeType.Added, isStaged: false),
        });

        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover("/ws")).Returns(RepositoryDiscoveryResult.Found("/ws", "/ws/.git/"));
        git.SetupSequence(g => g.ReadStatus("/ws/.git/"))
            .Returns(stagedSnapshot)
            .Returns(unstagedSnapshot);
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);

        var mutation = new Mock<IGitMutationService>();
        mutation.Setup(m => m.Unstage("/ws/.git/", "b.cs")).Returns(StageResult.Success());

        var vm = new SourceControlViewModel(orchestrator, WorkspaceWithPath(), NullDiffService(), mutation.Object, git.Object);

        var file = vm.StagedChanges.First();
        vm.UnstageFileCommand.Execute(file).Wait();

        mutation.Verify(m => m.Unstage("/ws/.git/", "b.cs"), Times.Once);
        Assert.Empty(vm.StagedChanges);
        Assert.Single(vm.UnstagedChanges);
        Assert.Null(vm.StatusMessage);
    }

    [Fact]
    public void StageFileCommand_MutationFailure_SurfacesStatusMessageAndStillRefreshes()
    {
        var snapshot = Snapshot(changes: new[]
        {
            new FileChange("a.cs", GitChangeType.Modified, isStaged: false),
        });

        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover("/ws")).Returns(RepositoryDiscoveryResult.Found("/ws", "/ws/.git/"));
        git.Setup(g => g.ReadStatus("/ws/.git/")).Returns(snapshot);
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);

        var mutation = new Mock<IGitMutationService>();
        mutation.Setup(m => m.Stage("/ws/.git/", "a.cs")).Returns(StageResult.Failure("boom"));

        var vm = new SourceControlViewModel(orchestrator, WorkspaceWithPath(), NullDiffService(), mutation.Object, git.Object);

        var file = vm.UnstagedChanges.First();
        vm.StageFileCommand.Execute(file).Wait();

        mutation.Verify(m => m.Stage("/ws/.git/", "a.cs"), Times.Once);
        git.Verify(g => g.ReadStatus("/ws/.git/"), Times.AtLeast(2));
        Assert.Equal("boom", vm.StatusMessage);
    }

    [Fact]
    public void StageFileCommand_NoRepository_SurfacesStatusMessageAndDoesNotCallMutation()
    {
        var mutation = new Mock<IGitMutationService>();
        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>())).Returns(RepositoryDiscoveryResult.NotFound("/ws"));
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);

        var vm = new SourceControlViewModel(orchestrator, WorkspaceWithPath(), NullDiffService(), mutation.Object, git.Object);
        var file = new FileChange("a.cs", GitChangeType.Modified, isStaged: false);

        vm.StageFileCommand.Execute(file).Wait();

        mutation.Verify(m => m.Stage(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        Assert.Equal("No repository - open a folder inside a git repository", vm.StatusMessage);
    }

    [Fact]
    public void StageAllCommand_StagesAllUnstagedFilesAndRefreshes()
    {
        var unstagedSnapshot = Snapshot(changes: new[]
        {
            new FileChange("a.cs", GitChangeType.Modified, isStaged: false),
            new FileChange("b.cs", GitChangeType.Added, isStaged: false),
            new FileChange("c.cs", GitChangeType.Modified, isStaged: true),
        });
        var afterStageSnapshot = Snapshot(changes: new[]
        {
            new FileChange("a.cs", GitChangeType.Modified, isStaged: true),
            new FileChange("b.cs", GitChangeType.Added, isStaged: true),
            new FileChange("c.cs", GitChangeType.Modified, isStaged: true),
        });

        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover("/ws")).Returns(RepositoryDiscoveryResult.Found("/ws", "/ws/.git/"));
        git.SetupSequence(g => g.ReadStatus("/ws/.git/"))
            .Returns(unstagedSnapshot)
            .Returns(afterStageSnapshot);
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);

        var mutation = new Mock<IGitMutationService>();
        mutation.Setup(m => m.StageAll("/ws/.git/", It.IsAny<IReadOnlyList<string>>()))
            .Returns(StageResult.Success());

        var vm = new SourceControlViewModel(orchestrator, WorkspaceWithPath(), NullDiffService(), mutation.Object, git.Object);

        Assert.Equal(2, vm.UnstagedCount);
        Assert.True(vm.StageAllCommand.CanExecute.FirstAsync().Wait());

        vm.StageAllCommand.Execute(Unit.Default).Wait();

        mutation.Verify(m => m.StageAll(
            "/ws/.git/",
            It.Is<IReadOnlyList<string>>(paths =>
                paths.Count == 2 && paths.Contains("a.cs") && paths.Contains("b.cs"))),
            Times.Once);
        Assert.Empty(vm.UnstagedChanges);
        Assert.Equal(3, vm.StagedCount);
        Assert.Null(vm.StatusMessage);
        Assert.False(vm.StageAllCommand.CanExecute.FirstAsync().Wait());
        Assert.Equal(SourceControlPrimaryAction.Commit, vm.PrimaryAction);
    }

    [Fact]
    public void StageAllCommand_MutationFailure_SurfacesStatusMessageAndStillRefreshes()
    {
        var before = Snapshot(changes: new[]
        {
            new FileChange("a.cs", GitChangeType.Modified, isStaged: false),
            new FileChange("b.cs", GitChangeType.Added, isStaged: false),
        });
        // Partial success reflected by repo truth after failure.
        var afterPartial = Snapshot(changes: new[]
        {
            new FileChange("a.cs", GitChangeType.Modified, isStaged: true),
            new FileChange("b.cs", GitChangeType.Added, isStaged: false),
        });

        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover("/ws")).Returns(RepositoryDiscoveryResult.Found("/ws", "/ws/.git/"));
        git.SetupSequence(g => g.ReadStatus("/ws/.git/"))
            .Returns(before)
            .Returns(afterPartial);
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);

        var mutation = new Mock<IGitMutationService>();
        mutation.Setup(m => m.StageAll("/ws/.git/", It.IsAny<IReadOnlyList<string>>()))
            .Returns(StageResult.Failure("partial boom"));

        var vm = new SourceControlViewModel(orchestrator, WorkspaceWithPath(), NullDiffService(), mutation.Object, git.Object);

        vm.StageAllCommand.Execute(Unit.Default).Wait();

        mutation.Verify(m => m.StageAll("/ws/.git/", It.IsAny<IReadOnlyList<string>>()), Times.Once);
        git.Verify(g => g.ReadStatus("/ws/.git/"), Times.AtLeast(2));
        Assert.Equal("partial boom", vm.StatusMessage);
        Assert.Single(vm.StagedChanges);
        Assert.Single(vm.UnstagedChanges);
        Assert.Equal("b.cs", vm.UnstagedChanges[0].FilePath);
    }

    [Fact]
    public void StageAllCommand_NoUnstagedChanges_CannotExecute()
    {
        var snapshot = Snapshot(changes: new[]
        {
            new FileChange("a.cs", GitChangeType.Modified, isStaged: true),
        });
        var vm = new SourceControlViewModel(CreateOrchestrator(snapshot), WorkspaceWithPath(), NullDiffService(), DefaultMutation(), DefaultGitRepo());

        Assert.Equal(0, vm.UnstagedCount);
        Assert.False(vm.StageAllCommand.CanExecute.FirstAsync().Wait());
    }

    [Fact]
    public void StageAllCommand_NoRepository_SurfacesStatusMessageAndDoesNotCallMutation()
    {
        // Seed unstaged changes via orchestrator, but Discover at mutation time fails.
        var snapshot = Snapshot(changes: new[]
        {
            new FileChange("a.cs", GitChangeType.Modified, isStaged: false),
        });
        var orchestratorMock = new Mock<ISourceControlSnapshotOrchestrator>();
        orchestratorMock.Setup(o => o.Refresh(It.IsAny<string?>()))
            .Returns(SnapshotRefreshResult.Success("/ws", snapshot));

        var mutation = new Mock<IGitMutationService>();
        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>())).Returns(RepositoryDiscoveryResult.NotFound("/ws"));

        var vm = new SourceControlViewModel(orchestratorMock.Object, WorkspaceWithPath(), NullDiffService(), mutation.Object, git.Object);

        Assert.True(vm.StageAllCommand.CanExecute.FirstAsync().Wait());
        vm.StageAllCommand.Execute(Unit.Default).Wait();

        mutation.Verify(m => m.StageAll(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()), Times.Never);
        Assert.Equal("No repository - open a folder inside a git repository", vm.StatusMessage);
    }

    [Fact]
    public void PrimaryAction_WithUncommittedChanges_IsCommit()
    {
        var snapshot = Snapshot(changes: new[]
        {
            new FileChange("a.cs", GitChangeType.Modified, isStaged: false),
        }, aheadBy: 2, hasUpstream: true);
        var vm = new SourceControlViewModel(CreateOrchestrator(snapshot), WorkspaceWithPath(), NullDiffService(), DefaultMutation(), DefaultGitRepo());

        Assert.Equal(SourceControlPrimaryAction.Commit, vm.PrimaryAction);
        Assert.Equal("Commit", vm.PrimaryActionLabel);
    }

    [Fact]
    public void PrimaryAction_CleanTreeAheadOfUpstream_IsPush()
    {
        var snapshot = Snapshot(aheadBy: 1, hasUpstream: true);
        var vm = new SourceControlViewModel(CreateOrchestrator(snapshot), WorkspaceWithPath(), NullDiffService(), DefaultMutation(), DefaultGitRepo());

        Assert.Equal(SourceControlPrimaryAction.Push, vm.PrimaryAction);
        Assert.Equal("Push", vm.PrimaryActionLabel);
    }

    [Fact]
    public void PrimaryAction_NewChangesBeforePush_RevertsToCommit()
    {
        var cleanAheadSnapshot = Snapshot(aheadBy: 1, hasUpstream: true);
        var dirtySnapshot = Snapshot(changes: new[]
        {
            new FileChange("a.cs", GitChangeType.Modified, isStaged: false),
        }, aheadBy: 1, hasUpstream: true);

        var orchestratorMock = new Mock<ISourceControlSnapshotOrchestrator>();
        orchestratorMock.SetupSequence(o => o.Refresh("/ws"))
            .Returns(SnapshotRefreshResult.Success("/ws", cleanAheadSnapshot))
            .Returns(SnapshotRefreshResult.Success("/ws", dirtySnapshot));

        var vm = new SourceControlViewModel(
            orchestratorMock.Object,
            WorkspaceWithPath(),
            NullDiffService(),
            DefaultMutation(),
            DefaultGitRepo());

        Assert.Equal(SourceControlPrimaryAction.Push, vm.PrimaryAction);

        vm.RefreshCommand.Execute().Wait();

        Assert.Equal(SourceControlPrimaryAction.Commit, vm.PrimaryAction);
        Assert.Equal("Commit", vm.PrimaryActionLabel);
    }

    [Fact]
    public void PushCommand_Success_ClearsErrorAndRefreshes()
    {
        var aheadSnapshot = Snapshot(aheadBy: 1, hasUpstream: true);
        var upToDateSnapshot = Snapshot(aheadBy: 0, hasUpstream: true);

        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover("/ws")).Returns(RepositoryDiscoveryResult.Found("/ws", "/ws/.git/"));
        git.SetupSequence(g => g.ReadStatus("/ws/.git/"))
            .Returns(aheadSnapshot)
            .Returns(upToDateSnapshot);
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);

        var mutation = new Mock<IGitMutationService>();
        mutation.Setup(m => m.Push("/ws/.git/")).Returns(PushResult.Success());

        var vm = new SourceControlViewModel(orchestrator, WorkspaceWithPath(), NullDiffService(), mutation.Object, git.Object);

        Assert.Equal(SourceControlPrimaryAction.Push, vm.PrimaryAction);

        vm.PushCommand.Execute().Wait();

        mutation.Verify(m => m.Push("/ws/.git/"), Times.Once);
        Assert.Null(vm.PushError);
        Assert.Equal("Pushed main.", vm.ActionNotice);
        Assert.Equal(SourceControlPrimaryAction.Commit, vm.PrimaryAction);
        Assert.Equal(0, vm.AheadBy);
    }

    [Fact]
    public void PushCommand_DirtyTree_DoesNotCallMutation()
    {
        var snapshot = Snapshot(changes: new[]
        {
            new FileChange("a.cs", GitChangeType.Modified, isStaged: false),
        }, aheadBy: 1, hasUpstream: true);
        var orchestrator = CreateOrchestrator(snapshot);
        var mutation = new Mock<IGitMutationService>();

        var vm = new SourceControlViewModel(orchestrator, WorkspaceWithPath(), NullDiffService(), mutation.Object, DefaultGitRepo());

        vm.PushCommand.Execute().Wait();

        mutation.Verify(m => m.Push(It.IsAny<string>()), Times.Never);
        Assert.Equal("Cannot push with uncommitted changes.", vm.PushError);
    }

    [Fact]
    public void PrimaryActionCommand_WhenPush_InvokesPushPath()
    {
        var aheadSnapshot = Snapshot(aheadBy: 1, hasUpstream: true);
        var upToDateSnapshot = Snapshot(aheadBy: 0, hasUpstream: true);

        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover("/ws")).Returns(RepositoryDiscoveryResult.Found("/ws", "/ws/.git/"));
        git.SetupSequence(g => g.ReadStatus("/ws/.git/"))
            .Returns(aheadSnapshot)
            .Returns(upToDateSnapshot);
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);

        var mutation = new Mock<IGitMutationService>();
        mutation.Setup(m => m.Push("/ws/.git/")).Returns(PushResult.Success());

        var vm = new SourceControlViewModel(orchestrator, WorkspaceWithPath(), NullDiffService(), mutation.Object, git.Object);

        vm.PrimaryActionCommand.Execute().Wait();

        mutation.Verify(m => m.Push("/ws/.git/"), Times.Once);
        mutation.Verify(m => m.Commit(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void CommitCommand_Success_ClearsMessageAndErrorAndRefreshes()
    {
        var stagedSnapshot = Snapshot(changes: new[]
        {
            new FileChange("a.cs", GitChangeType.Modified, isStaged: true),
        });
        var emptyAheadSnapshot = Snapshot(aheadBy: 1, hasUpstream: true);

        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover("/ws")).Returns(RepositoryDiscoveryResult.Found("/ws", "/ws/.git/"));
        git.SetupSequence(g => g.ReadStatus("/ws/.git/"))
            .Returns(stagedSnapshot)
            .Returns(emptyAheadSnapshot);
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);

        var mutation = new Mock<IGitMutationService>();
        mutation.Setup(m => m.Commit("/ws/.git/", "test commit")).Returns(CommitResult.Success("abc123"));

        var vm = new SourceControlViewModel(orchestrator, WorkspaceWithPath(), NullDiffService(), mutation.Object, git.Object);
        vm.CommitMessage = "test commit";

        vm.CommitCommand.Execute().Wait();

        mutation.Verify(m => m.Commit("/ws/.git/", "test commit"), Times.Once);
        Assert.Equal(string.Empty, vm.CommitMessage);
        Assert.Null(vm.CommitError);
        Assert.Empty(vm.StagedChanges);
        Assert.Equal(SourceControlPrimaryAction.Push, vm.PrimaryAction);
    }

    [Fact]
    public void CommitCommand_NothingStaged_SetsCommitErrorAndDoesNotCallMutation()
    {
        var snapshot = Snapshot(changes: Array.Empty<FileChange>());
        var orchestrator = CreateOrchestrator(snapshot);
        var mutation = new Mock<IGitMutationService>();

        var vm = new SourceControlViewModel(orchestrator, WorkspaceWithPath(), NullDiffService(), mutation.Object, DefaultGitRepo());
        vm.CommitMessage = "test commit";

        vm.CommitCommand.Execute().Wait();

        mutation.Verify(m => m.Commit(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        Assert.Equal("Nothing staged to commit.", vm.CommitError);
        Assert.Equal("test commit", vm.CommitMessage);
    }

    [Fact]
    public void CommitCommand_EmptyMessage_SetsCommitErrorAndDoesNotCallMutation()
    {
        var stagedSnapshot = Snapshot(changes: new[]
        {
            new FileChange("a.cs", GitChangeType.Modified, isStaged: true),
        });
        var orchestrator = CreateOrchestrator(stagedSnapshot);
        var mutation = new Mock<IGitMutationService>();

        var vm = new SourceControlViewModel(orchestrator, WorkspaceWithPath(), NullDiffService(), mutation.Object, DefaultGitRepo());
        vm.CommitMessage = string.Empty;

        vm.CommitCommand.Execute().Wait();

        mutation.Verify(m => m.Commit(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        Assert.Equal("Commit message cannot be empty.", vm.CommitError);
    }

    [Fact]
    public void CommitCommand_Failure_SetsCommitErrorAndRefreshes()
    {
        var stagedSnapshot = Snapshot(changes: new[]
        {
            new FileChange("a.cs", GitChangeType.Modified, isStaged: true),
        });
        var orchestrator = CreateOrchestrator(stagedSnapshot);
        var mutation = new Mock<IGitMutationService>();
        mutation.Setup(m => m.Commit("/ws/.git/", "test commit")).Returns(CommitResult.Failure("commit failed"));

        var vm = new SourceControlViewModel(orchestrator, WorkspaceWithPath(), NullDiffService(), mutation.Object, DefaultGitRepo());
        vm.CommitMessage = "test commit";

        vm.CommitCommand.Execute().Wait();

        mutation.Verify(m => m.Commit("/ws/.git/", "test commit"), Times.Once);
        Assert.Equal("commit failed", vm.CommitError);
        Assert.Equal("test commit", vm.CommitMessage);
    }

    [Fact]
    public void SelectBranchCommand_UpdatesCurrentBranch()
    {
        var snapshot = Snapshot(branches: new[]
        {
            new GitBranch("main", true),
            new GitBranch("feature/agent-ui"),
        });
        var vm = new SourceControlViewModel(CreateOrchestrator(snapshot), WorkspaceWithPath(), NullDiffService(), DefaultMutation(), DefaultGitRepo());

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
        var vm = new SourceControlViewModel(orchestrator, workspace, NullDiffService(), DefaultMutation(), DefaultGitRepo());

        Assert.Equal(SnapshotRefreshStatus.Success, vm.LastRefreshStatus);
        Assert.Null(vm.LastRefreshError);
        Assert.Single(vm.Branches);
        Assert.Equal("main", vm.CurrentBranchName);
        Assert.Null(vm.StatusMessage);

        vm.RefreshCommand.Execute().Wait();

        Assert.Equal(SnapshotRefreshStatus.Success, vm.LastRefreshStatus);
        Assert.Single(vm.Branches);
    }

    [Fact]
    public void Refresh_NonRepository_ProjectsEmptyStateTruthfully()
    {
        var workspace = WorkspaceWithPath();
        var orchestrator = CreateOrchestrator(RepositoryDiscoveryResult.NotFound("/ws"));
        var vm = new SourceControlViewModel(orchestrator, workspace, NullDiffService(), DefaultMutation(), DefaultGitRepo());

        Assert.Equal(SnapshotRefreshStatus.NotARepository, vm.LastRefreshStatus);
        Assert.Empty(vm.Branches);
        Assert.Empty(vm.UnstagedChanges);
        Assert.Empty(vm.StagedChanges);
        Assert.Null(vm.LastRefreshError);
        Assert.Equal("no repo", vm.CurrentBranchName);
        Assert.Equal("No repository — open a folder inside a git repository", vm.StatusMessage);

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
            orchestrator, WorkspaceWithPath(), NullDiffService(), DefaultMutation(), DefaultGitRepo());

        Assert.Equal(SnapshotRefreshStatus.Failed, vm.LastRefreshStatus);
        Assert.Equal("boom", vm.LastRefreshError);
        Assert.Empty(vm.Branches);
        Assert.Empty(vm.UnstagedChanges);
        Assert.Empty(vm.StagedChanges);
        Assert.Equal("—", vm.CurrentBranchName);
        Assert.Equal("Source Control unavailable: boom", vm.StatusMessage);
    }

    // ---------------------------------------------------------------
    // M2: File selection and diff state
    // ---------------------------------------------------------------

    [Fact]
    public void SelectFileCommand_LoadsDiffForUnstagedFile()
    {
        var file = new FileChange("a.cs", GitChangeType.Modified, isStaged: false);
        var snapshot = Snapshot(changes: new[] { file });
        var diffService = new Mock<IFileDiffService>();
        var expectedDiff = new FileDiffResult
        {
            FilePath = "a.cs",
            DiffText = "diff --git a/a.cs b/a.cs\n@@ -1 +1 @@\n-old\n+new\n",
            AddedLines = 1,
            DeletedLines = 1,
        };
        diffService.Setup(d => d.GetDiff("/ws", file)).Returns(expectedDiff);

        var vm = new SourceControlViewModel(
            CreateOrchestrator(snapshot),
            WorkspaceWithPath(),
            diffService.Object,
            DefaultMutation(),
            DefaultGitRepo());

        vm.SelectFileCommand.Execute(file).Wait();

        Assert.Same(file, vm.SelectedFileChange);
        Assert.Equal("a.cs", vm.SelectedFilePath);
        Assert.Same(expectedDiff, vm.CurrentDiff);
    }

    [Fact]
    public void SelectFileCommand_LoadsDiffForStagedFile()
    {
        var file = new FileChange("b.cs", GitChangeType.Added, isStaged: true);
        var snapshot = Snapshot(changes: new[] { file });
        var diffService = new Mock<IFileDiffService>();
        var expectedDiff = new FileDiffResult
        {
            FilePath = "b.cs",
            DiffText = "diff --git b/b.cs...",
            AddedLines = 5,
            DeletedLines = 0,
        };
        diffService.Setup(d => d.GetDiff("/ws", file)).Returns(expectedDiff);

        var vm = new SourceControlViewModel(
            CreateOrchestrator(snapshot),
            WorkspaceWithPath(),
            diffService.Object,
            DefaultMutation(),
            DefaultGitRepo());

        vm.SelectFileCommand.Execute(file).Wait();

        Assert.Same(file, vm.SelectedFileChange);
        Assert.Equal("b.cs", vm.SelectedFilePath);
        Assert.Equal(expectedDiff, vm.CurrentDiff);
    }

    [Fact]
    public void SelectFileCommand_BinaryFile_SetsIsBinaryState()
    {
        var file = new FileChange("binary.dll", GitChangeType.Modified, isStaged: false);
        var snapshot = Snapshot(changes: new[] { file });
        var diffService = new Mock<IFileDiffService>();
        diffService.Setup(d => d.GetDiff("/ws", file)).Returns(
            new FileDiffResult { FilePath = "binary.dll", IsBinary = true });

        var vm = new SourceControlViewModel(
            CreateOrchestrator(snapshot),
            WorkspaceWithPath(),
            diffService.Object,
            DefaultMutation(),
            DefaultGitRepo());

        vm.SelectFileCommand.Execute(file).Wait();

        Assert.NotNull(vm.CurrentDiff);
        Assert.True(vm.CurrentDiff.IsBinary);
        Assert.Null(vm.CurrentDiff.DiffText);
    }

    [Fact]
    public void SelectFileCommand_NoWorkspacePath_ClearsDiff()
    {
        var file = new FileChange("a.cs", GitChangeType.Modified, isStaged: false);
        var snapshot = Snapshot(changes: new[] { file });
        var orchestrator = CreateOrchestrator(snapshot);

        // Create a workspace without a path — constructor snapshot still loads
        // because the orchestrator is called with null path.
        var workspace = new Workspace();

        var diffService = new Mock<IFileDiffService>();
        diffService.Setup(d => d.GetDiff(It.IsAny<string>(), It.IsAny<FileChange>()))
            .Returns((FileDiffResult?)null);

        var vm = new SourceControlViewModel(orchestrator, workspace, diffService.Object, DefaultMutation(), DefaultGitRepo());

        vm.SelectFileCommand.Execute(file).Wait();

        // File is selected but diff is null because there is no workspace path
        // to pass to the diff service.
        Assert.Same(file, vm.SelectedFileChange);
        Assert.Equal("a.cs", vm.SelectedFilePath);
        Assert.Null(vm.CurrentDiff);
    }

    [Fact]
    public void Refresh_SamePathAfterRefresh_ReselectsAndPreservesDiff()
    {
        var file = new FileChange("a.cs", GitChangeType.Modified, isStaged: false);
        var snapshot = Snapshot(changes: new[] { file });
        var diffService = new Mock<IFileDiffService>();
        var expectedDiff = new FileDiffResult
        {
            FilePath = "a.cs",
            DiffText = "diff --git a/a.cs b/a.cs\n@@ -1 +1 @@\n-old\n+new\n",
            AddedLines = 1,
            DeletedLines = 1,
        };
        // Set up the diff service so it returns the same diff for both the initial
        // select and the re-select after refresh.
        diffService.Setup(d => d.GetDiff("/ws", file)).Returns(expectedDiff);

        var vm = new SourceControlViewModel(
            CreateOrchestrator(snapshot),
            WorkspaceWithPath(),
            diffService.Object,
            DefaultMutation(),
            DefaultGitRepo());

        vm.SelectFileCommand.Execute(file).Wait();

        // Confirm selection state before refresh.
        Assert.Same(file, vm.SelectedFileChange);
        Assert.NotNull(vm.CurrentDiff);
        Assert.Equal("a.cs", vm.CurrentDiff.FilePath);

        // Refresh — the file still exists in the snapshot, so the VM should
        // re-select it and reload its diff.
        vm.RefreshCommand.Execute().Wait();

        Assert.NotNull(vm.SelectedFileChange);
        Assert.Equal("a.cs", vm.SelectedFileChange.FilePath);
        Assert.Equal("a.cs", vm.SelectedFilePath);
        Assert.NotNull(vm.CurrentDiff);
        Assert.Equal(expectedDiff.DiffText, vm.CurrentDiff.DiffText);
    }

    [Fact]
    public void Refresh_FileRemovedAfterRefresh_ClearsSelectionAndDiff()
    {
        var file = new FileChange("a.cs", GitChangeType.Modified, isStaged: false);
        var snapshotWithFile = Snapshot(changes: new[] { file });
        var snapshotWithoutFile = Snapshot(changes: Array.Empty<FileChange>());

        var diffService = new Mock<IFileDiffService>();
        diffService.Setup(d => d.GetDiff("/ws", file)).Returns(
            new FileDiffResult { FilePath = "a.cs", DiffText = "some diff" });

        // Mock the orchestrator directly so we can return different snapshots
        // on successive calls.
        var orchestratorMock = new Mock<ISourceControlSnapshotOrchestrator>();
        orchestratorMock.SetupSequence(o => o.Refresh("/ws"))
            .Returns(SnapshotRefreshResult.Success("/ws", snapshotWithFile))
            .Returns(SnapshotRefreshResult.Success("/ws", snapshotWithoutFile));

        var vm = new SourceControlViewModel(
            orchestratorMock.Object,
            WorkspaceWithPath(),
            diffService.Object,
            DefaultMutation(),
            DefaultGitRepo());

        // First snapshot has a.cs — select it.
        vm.SelectFileCommand.Execute(file).Wait();
        Assert.NotNull(vm.SelectedFileChange);
        Assert.Equal("a.cs", vm.SelectedFilePath);
        Assert.NotNull(vm.CurrentDiff);

        // Refresh with a snapshot that no longer contains a.cs.
        vm.RefreshCommand.Execute().Wait();

        Assert.Null(vm.SelectedFileChange);
        Assert.Null(vm.SelectedFilePath);
        Assert.Null(vm.CurrentDiff);
    }

    [Fact]
    public void Refresh_FileMovedToStaged_ReselectsAndPreservesDiff()
    {
        // Simulate a file changing from unstaged to staged across refresh.
        var unstagedFile = new FileChange("a.cs", GitChangeType.Modified, isStaged: false);
        var stagedFile = new FileChange("a.cs", GitChangeType.Modified, isStaged: true);
        var snapshotUnstaged = Snapshot(changes: new[] { unstagedFile });
        var snapshotStaged = Snapshot(changes: new[] { stagedFile });

        var diffService = new Mock<IFileDiffService>();
        diffService.Setup(d => d.GetDiff("/ws", It.Is<FileChange>(c => c.FilePath == "a.cs")))
            .Returns(new FileDiffResult { FilePath = "a.cs", DiffText = "diff content" });

        var orchestratorMock = new Mock<ISourceControlSnapshotOrchestrator>();
        orchestratorMock.SetupSequence(o => o.Refresh("/ws"))
            .Returns(SnapshotRefreshResult.Success("/ws", snapshotUnstaged))
            .Returns(SnapshotRefreshResult.Success("/ws", snapshotStaged));

        var vm = new SourceControlViewModel(
            orchestratorMock.Object,
            WorkspaceWithPath(),
            diffService.Object,
            DefaultMutation(),
            DefaultGitRepo());

        // Select file when it's unstaged.
        vm.SelectFileCommand.Execute(unstagedFile).Wait();
        Assert.NotNull(vm.CurrentDiff);

        // Refresh: now the file is staged (same path, different FileChange instance).
        vm.RefreshCommand.Execute().Wait();

        Assert.NotNull(vm.SelectedFileChange);
        Assert.Equal("a.cs", vm.SelectedFileChange.FilePath);
        Assert.True(vm.SelectedFileChange.IsStaged);
        Assert.Equal("a.cs", vm.SelectedFilePath);
        Assert.NotNull(vm.CurrentDiff);
    }

    [Fact]
    public void Refresh_NonRepository_ClearsSelectionAndDiff()
    {
        var file = new FileChange("a.cs", GitChangeType.Modified, isStaged: false);
        var snapshot = Snapshot(changes: new[] { file });
        var diffService = new Mock<IFileDiffService>();
        diffService.Setup(d => d.GetDiff("/ws", file)).Returns(
            new FileDiffResult { FilePath = "a.cs", DiffText = "diff" });

        var orchestratorMock = new Mock<ISourceControlSnapshotOrchestrator>();
        orchestratorMock.SetupSequence(o => o.Refresh("/ws"))
            .Returns(SnapshotRefreshResult.Success("/ws", snapshot))
            .Returns(SnapshotRefreshResult.NotARepository("/ws"));

        var vm = new SourceControlViewModel(
            orchestratorMock.Object,
            WorkspaceWithPath(),
            diffService.Object,
            DefaultMutation(),
            DefaultGitRepo());

        vm.SelectFileCommand.Execute(file).Wait();
        Assert.NotNull(vm.SelectedFileChange);
        Assert.NotNull(vm.CurrentDiff);

        // Refresh transitions to non-repository state.
        vm.RefreshCommand.Execute().Wait();

        Assert.Null(vm.SelectedFileChange);
        Assert.Null(vm.SelectedFilePath);
        Assert.Null(vm.CurrentDiff);
    }
}
