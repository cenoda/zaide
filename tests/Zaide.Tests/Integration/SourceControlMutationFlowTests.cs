using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using LibGit2Sharp;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.Integration;

/// <summary>
/// Phase 7.4 M3 stabilization coverage. These tests exercise the full
/// stage/unstage/commit loop end-to-end through the real
/// <see cref="SourceControlViewModel"/> wired to the real read
/// (<see cref="GitRepositoryService"/>), refresh
/// (<see cref="SourceControlSnapshotOrchestrator"/>), diff
/// (<see cref="FileDiffService"/>), and mutation
/// (<see cref="GitMutationService"/>) seams against throwaway git
/// repositories. They are the reproducible equivalent of the manual smoke
/// loop named in the Phase 7.4 plan and assert that the panel projection
/// stays truthful across repeated mutation-refresh cycles.
/// </summary>
public sealed class SourceControlMutationFlowTests : IDisposable
{
    static SourceControlMutationFlowTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private readonly string _repoDir;
    private readonly string _bareRemoteDir;

    public SourceControlMutationFlowTests()
    {
        _bareRemoteDir = Directory.CreateTempSubdirectory("zaide-scflow-remote-").FullName;
        _repoDir = Directory.CreateTempSubdirectory("zaide-scflow-").FullName;
        Repository.Init(_bareRemoteDir, isBare: true);
        Repository.Init(_repoDir);
        using var repo = new Repository(_repoDir);
        repo.Network.Remotes.Add("origin", _bareRemoteDir);
        repo.Config.Set("user.name", "Flow Test");
        repo.Config.Set("user.email", "flow@example.com");
        File.WriteAllText(Path.Combine(_repoDir, "seed.txt"), "seed\n");
        Commands.Stage(repo, "seed.txt");
        var author = new Signature("Flow Test", "flow@example.com", DateTimeOffset.UtcNow);
        repo.Commit("seed", author, author);
        repo.Network.Push(repo.Network.Remotes["origin"], repo.Head.CanonicalName, new PushOptions());
        repo.Branches.Update(repo.Head, b =>
            b.TrackedBranch = $"refs/remotes/origin/{repo.Head.FriendlyName}");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_repoDir, recursive: true);
            Directory.Delete(_bareRemoteDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup; leaked temp dirs are harmless for CI.
        }
    }

    private (SourceControlViewModel ViewModel, EditorTabViewModel EditorTabs) CreateViewModel()
    {
        var gitRepo = new GitRepositoryService();
        var orchestrator = new SourceControlSnapshotOrchestrator(gitRepo);
        var mutation = new GitMutationService();
        var workspace = new Workspace();
        workspace.SetProjectFromPath(_repoDir);
        return SourceControlTestFactory.CreateWithDiffTabs(
            orchestrator,
            workspace,
            mutation,
            gitRepo);
    }

    private void Write(string relativePath, string content) =>
        File.WriteAllText(Path.Combine(_repoDir, relativePath), content);

    private int HeadCommitCount()
    {
        using var repo = new Repository(_repoDir);
        return repo.Commits.Count();
    }

    [Fact]
    public void FullLoop_CreateStageViewDiffCommit_FileDisappearsFromBothLists()
    {
        var (vm, tabs) = CreateViewModel();

        Write("a.txt", "hello\n");
        vm.RefreshCommand.Execute().Wait();

        var unstaged = vm.UnstagedChanges.Single(c => c.FilePath == "a.txt");
        Assert.Equal(GitChangeType.Added, unstaged.ChangeType);

        // Stage the new file, then confirm it moved to the staged list.
        vm.StageFileCommand.Execute(unstaged).Wait();
        Assert.DoesNotContain(vm.UnstagedChanges, c => c.FilePath == "a.txt");
        var staged = vm.StagedChanges.Single(c => c.FilePath == "a.txt");

        // Viewing the staged delta opens a coherent unified diff in the editor.
        vm.SelectFileCommand.Execute(staged).Wait();
        var diffTab = Assert.Single(tabs.OpenTabs);
        Assert.Equal("a.txt", diffTab.SourceControlDiffKey);
        Assert.True(diffTab.IsReadOnly);
        Assert.False(string.IsNullOrEmpty(diffTab.TextContent));
        Assert.Contains("diff --git", diffTab.TextContent);

        var beforeCommit = HeadCommitCount();
        vm.CommitMessage = "add a.txt";
        vm.CommitCommand.Execute().Wait();

        Assert.Equal(beforeCommit + 1, HeadCommitCount());
        Assert.Equal(string.Empty, vm.CommitMessage);
        Assert.Null(vm.CommitError);
        Assert.DoesNotContain(vm.StagedChanges, c => c.FilePath == "a.txt");
        Assert.DoesNotContain(vm.UnstagedChanges, c => c.FilePath == "a.txt");
        Assert.Null(vm.SelectedFileChange);
        Assert.Contains("no longer in the change list", diffTab.TextContent);
    }

    [Fact]
    public void StageThenModifyThenCommit_LeavesUnstagedModificationTruthfully()
    {
        // This mirrors the literal plan wording (create -> stage -> modify ->
        // commit) and documents the truthful git outcome: the post-stage
        // modification is never staged, so the commit captures only the staged
        // snapshot and the file remains as an unstaged modification. The panel
        // must reflect this rather than claiming the file fully disappeared.
        var (vm, _) = CreateViewModel();

        Write("a.txt", "hello\n");
        vm.RefreshCommand.Execute().Wait();
        var unstaged = vm.UnstagedChanges.Single(c => c.FilePath == "a.txt");

        vm.StageFileCommand.Execute(unstaged).Wait();
        Assert.Contains(vm.StagedChanges, c => c.FilePath == "a.txt");

        // Modify after staging -> staged "Added" + unstaged "Modified".
        Write("a.txt", "hello\nworld\n");
        vm.RefreshCommand.Execute().Wait();
        Assert.Contains(vm.StagedChanges, c => c.FilePath == "a.txt" && c.ChangeType == GitChangeType.Added);
        Assert.Contains(vm.UnstagedChanges, c => c.FilePath == "a.txt" && c.ChangeType == GitChangeType.Modified);

        vm.CommitMessage = "add a.txt (staged snapshot)";
        vm.CommitCommand.Execute().Wait();

        Assert.Null(vm.CommitError);
        // The committed (staged) snapshot is gone, but the later unstaged edit
        // survives as a truthful modification against the new HEAD.
        Assert.DoesNotContain(vm.StagedChanges, c => c.FilePath == "a.txt");
        Assert.Contains(vm.UnstagedChanges, c => c.FilePath == "a.txt" && c.ChangeType == GitChangeType.Modified);
    }

    [Fact]
    public void UnstageRestageCommitLoop_FileDisappearsFromBothLists()
    {
        var (vm, _) = CreateViewModel();

        Write("b.txt", "content\n");
        vm.RefreshCommand.Execute().Wait();
        var file = vm.UnstagedChanges.Single(c => c.FilePath == "b.txt");

        // Stage.
        vm.StageFileCommand.Execute(file).Wait();
        Assert.Contains(vm.StagedChanges, c => c.FilePath == "b.txt");

        // Unstage.
        var stagedFile = vm.StagedChanges.Single(c => c.FilePath == "b.txt");
        vm.UnstageFileCommand.Execute(stagedFile).Wait();
        Assert.Contains(vm.UnstagedChanges, c => c.FilePath == "b.txt");
        Assert.DoesNotContain(vm.StagedChanges, c => c.FilePath == "b.txt");

        // Restage.
        var reFile = vm.UnstagedChanges.Single(c => c.FilePath == "b.txt");
        vm.StageFileCommand.Execute(reFile).Wait();
        Assert.Contains(vm.StagedChanges, c => c.FilePath == "b.txt");

        // Commit.
        var beforeCommit = HeadCommitCount();
        vm.CommitMessage = "add b.txt";
        vm.CommitCommand.Execute().Wait();

        Assert.Equal(beforeCommit + 1, HeadCommitCount());
        Assert.Null(vm.CommitError);
        Assert.DoesNotContain(vm.StagedChanges, c => c.FilePath == "b.txt");
        Assert.DoesNotContain(vm.UnstagedChanges, c => c.FilePath == "b.txt");
    }

    [Fact]
    public void RepeatedStageUnstageCycles_StayTruthfulAndDoNotDegrade()
    {
        var (vm, _) = CreateViewModel();

        Write("c.txt", "content\n");
        vm.RefreshCommand.Execute().Wait();

        for (var i = 0; i < 5; i++)
        {
            var toStage = vm.UnstagedChanges.Single(c => c.FilePath == "c.txt");
            vm.StageFileCommand.Execute(toStage).Wait();

            // Exactly one staged entry, no unstaged duplicate, no lingering error.
            Assert.Single(vm.StagedChanges, c => c.FilePath == "c.txt");
            Assert.DoesNotContain(vm.UnstagedChanges, c => c.FilePath == "c.txt");
            Assert.Null(vm.StatusMessage);

            var toUnstage = vm.StagedChanges.Single(c => c.FilePath == "c.txt");
            vm.UnstageFileCommand.Execute(toUnstage).Wait();

            Assert.Single(vm.UnstagedChanges, c => c.FilePath == "c.txt");
            Assert.DoesNotContain(vm.StagedChanges, c => c.FilePath == "c.txt");
            Assert.Null(vm.StatusMessage);
        }
    }

    [Fact]
    public void SelectionPersistsAcrossStageMutationRefresh()
    {
        var (vm, tabs) = CreateViewModel();

        Write("d.txt", "content\n");
        vm.RefreshCommand.Execute().Wait();
        var file = vm.UnstagedChanges.Single(c => c.FilePath == "d.txt");

        vm.SelectFileCommand.Execute(file).Wait();
        Assert.Equal("d.txt", vm.SelectedFilePath);
        var diffTab = Assert.Single(tabs.OpenTabs);

        // Stage -> unconditional refresh; the selection must follow the file to
        // the staged list (re-selected by path) and the diff tab must stay coherent.
        vm.StageFileCommand.Execute(file).Wait();

        Assert.NotNull(vm.SelectedFileChange);
        Assert.Equal("d.txt", vm.SelectedFilePath);
        Assert.True(vm.SelectedFileChange!.IsStaged);
        Assert.Same(diffTab, Assert.Single(tabs.OpenTabs));
        Assert.Equal("d.txt", diffTab.SourceControlDiffKey);
        Assert.Equal("Staged Changes", diffTab.SourceControlComparisonState);
    }

    [Fact]
    public void EmptyCommitMessage_RejectedInViewModelWithoutCreatingCommit()
    {
        var (vm, _) = CreateViewModel();

        Write("e.txt", "content\n");
        vm.RefreshCommand.Execute().Wait();
        var file = vm.UnstagedChanges.Single(c => c.FilePath == "e.txt");
        vm.StageFileCommand.Execute(file).Wait();

        var beforeCommit = HeadCommitCount();
        vm.CommitMessage = "   ";
        vm.CommitCommand.Execute().Wait();

        Assert.Equal("Commit message cannot be empty.", vm.CommitError);
        Assert.Equal(beforeCommit, HeadCommitCount());
        // The staged change is untouched; nothing was committed.
        Assert.Contains(vm.StagedChanges, c => c.FilePath == "e.txt");
    }

    [Fact]
    public void NothingStaged_RejectedInViewModelWithoutCreatingCommit()
    {
        var (vm, _) = CreateViewModel();

        var beforeCommit = HeadCommitCount();
        vm.CommitMessage = "nothing to commit";
        vm.CommitCommand.Execute().Wait();

        Assert.Equal("Nothing staged to commit.", vm.CommitError);
        Assert.Equal(beforeCommit, HeadCommitCount());
    }

    [Fact]
    public void CommitPushWorkflow_TransitionsPrimaryActionFromCommitToPushAndBack()
    {
        var (vm, _) = CreateViewModel();

        Write("pushme.txt", "hello\n");
        vm.RefreshCommand.Execute().Wait();

        Assert.Equal(SourceControlPrimaryAction.Commit, vm.PrimaryAction);
        Assert.Equal("Commit", vm.PrimaryActionLabel);

        var unstaged = vm.UnstagedChanges.Single(c => c.FilePath == "pushme.txt");
        vm.StageFileCommand.Execute(unstaged).Wait();

        vm.CommitMessage = "add pushme.txt";
        vm.CommitCommand.Execute().Wait();

        Assert.Null(vm.CommitError);
        Assert.Equal(SourceControlPrimaryAction.Push, vm.PrimaryAction);
        Assert.Equal($"Push ({vm.AheadBy})", vm.PrimaryActionLabel);
        Assert.True(vm.AheadBy > 0);

        Write("interrupt.txt", "block push\n");
        vm.RefreshCommand.Execute().Wait();

        Assert.Equal(SourceControlPrimaryAction.Commit, vm.PrimaryAction);
        Assert.Equal("Commit", vm.PrimaryActionLabel);

        var interrupt = vm.UnstagedChanges.Single(c => c.FilePath == "interrupt.txt");
        vm.StageFileCommand.Execute(interrupt).Wait();
        vm.CommitMessage = "add interrupt.txt";
        vm.CommitCommand.Execute().Wait();

        Assert.Equal(SourceControlPrimaryAction.Push, vm.PrimaryAction);

        vm.PrimaryActionCommand.Execute().Wait();

        Assert.Null(vm.PushError);
        Assert.False(string.IsNullOrEmpty(vm.ActionNotice));
        Assert.Equal(SourceControlPrimaryAction.Commit, vm.PrimaryAction);
        Assert.Equal("Commit", vm.PrimaryActionLabel);
        Assert.Equal(0, vm.AheadBy);
        Assert.True(vm.HasUpstream);

        using var bare = new Repository(_bareRemoteDir);
        Assert.Equal(3, bare.Commits.Count());
    }

    [Fact]
    public void MissingGitIdentity_SurfacesCommitSpecificError()
    {
        // Use a dedicated repo whose identity is unset. On machines with global
        // git identity, BuildSignature falls back to it, so this test is
        // environment-aware (mirroring GitMutationServiceTests): it only asserts
        // the failure surface when no identity exists at any config level.
        var isolatedDir = Directory.CreateTempSubdirectory("zaide-scflow-noident-").FullName;
        try
        {
            Repository.Init(isolatedDir);
            using (var repo = new Repository(isolatedDir))
            {
                repo.Config.Unset("user.name");
                repo.Config.Unset("user.email");
            }

            bool identityAvailable;
            using (var repo = new Repository(isolatedDir))
            {
                identityAvailable = repo.Config.BuildSignature(DateTimeOffset.Now) is not null;
            }

            var gitRepo = new GitRepositoryService();
            var orchestrator = new SourceControlSnapshotOrchestrator(gitRepo);
            var workspace = new Workspace();
            workspace.SetProjectFromPath(isolatedDir);
            var (vm, _) = SourceControlTestFactory.CreateWithDiffTabs(
                orchestrator,
                workspace,
                new GitMutationService(),
                gitRepo);

            File.WriteAllText(Path.Combine(isolatedDir, "f.txt"), "content\n");
            vm.RefreshCommand.Execute().Wait();
            var file = vm.UnstagedChanges.Single(c => c.FilePath == "f.txt");
            vm.StageFileCommand.Execute(file).Wait();

            vm.CommitMessage = "commit without identity";
            vm.CommitCommand.Execute().Wait();

            if (identityAvailable)
            {
                // Global identity resolved the signature; the commit succeeds and
                // the commit-specific error surface stays clear.
                Assert.Null(vm.CommitError);
            }
            else
            {
                Assert.NotNull(vm.CommitError);
                Assert.Contains("user identity", vm.CommitError!, StringComparison.OrdinalIgnoreCase);
                // The error lives on the commit-specific surface only.
                Assert.Null(vm.LastRefreshError);
            }
        }
        finally
        {
            Directory.Delete(isolatedDir, recursive: true);
        }
    }
}
