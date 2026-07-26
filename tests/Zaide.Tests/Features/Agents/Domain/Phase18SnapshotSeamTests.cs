using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using Xunit;
using Zaide.Features.Editor.Application;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.SourceControl.Domain;
using Zaide.Features.Terminal.Application;
using Zaide.Features.Terminal.Contracts;

namespace Zaide.Tests.Features.Agents.Domain;

public sealed class Phase18SnapshotSeamTests
{
    [Fact]
    public void EditorStateSnapshotService_ExposesPassiveCurrentAndWhenChanged()
    {
        using var service = new TestEditorStateSnapshotService();
        var observed = 0;
        using var subscription = service.WhenChanged.Subscribe(_ => observed++);

        var initial = service.Current;
        service.Publish(new EditorStateSnapshot(
            generation: 2,
            activeFilePath: "/workspace/Program.cs",
            activeFileContent: "class Program {}",
            openFilePaths: new[] { "/workspace/Program.cs" },
            caretLine: 3,
            caretColumn: 5,
            selectionStart: 10,
            selectionLength: 4,
            selectionText: "Prog"));

        Assert.Equal(1, initial.Generation);
        Assert.Equal(2, service.Current.Generation);
        Assert.Equal("/workspace/Program.cs", service.Current.ActiveFilePath);
        Assert.Equal(1, observed);
    }

    [Fact]
    public void EditorStateSnapshot_OpenFilePathsRemainImmutableAfterInputMutation()
    {
        var openFilePaths = new List<string> { "/workspace/Program.cs" };
        var snapshot = new EditorStateSnapshot(
            generation: 1,
            openFilePaths: openFilePaths);

        openFilePaths.Add("/workspace/Other.cs");

        Assert.Single(snapshot.OpenFilePaths);
        Assert.Equal("/workspace/Program.cs", snapshot.OpenFilePaths[0]);
    }

    [Fact]
    public void TerminalSurfaceSnapshot_ExcludesScrollbackShape()
    {
        var snapshot = new TerminalSurfaceSnapshot(
            generation: 1,
            activeTabCount: 1,
            activeTabTitle: "bash",
            isActiveTabRunning: true,
            visibleRowCount: 24,
            visibleColumnCount: 80);

        Assert.Equal(1, snapshot.ActiveTabCount);
        Assert.DoesNotContain(
            snapshot.GetType().GetProperties(),
            property => property.Name.Contains("Scrollback", StringComparison.Ordinal));
    }

    [Fact]
    public void SourceControlSnapshotService_ExposesPassiveCurrentAndWhenChanged()
    {
        using var service = new TestSourceControlSnapshotService();
        var observed = 0;
        using var subscription = service.WhenChanged.Subscribe(_ => observed++);

        var initial = service.Current;
        var repositoryStatus = new RepositoryStatusSnapshot
        {
            CurrentBranchName = "main",
            AheadBy = 1,
            BehindBy = 0,
        };

        service.Publish(new SourceControlStatusSnapshot(
            generation: 2,
            availability: SourceControlSnapshotAvailability.Available,
            workspacePath: "/workspace",
            repositoryStatus: repositoryStatus,
            statusMessage: "ok"));

        Assert.Equal(SourceControlSnapshotAvailability.NoWorkspace, initial.Availability);
        Assert.Equal(SourceControlSnapshotAvailability.Available, service.Current.Availability);
        Assert.NotSame(repositoryStatus, service.Current.RepositoryStatus);
        Assert.Equal("main", service.Current.RepositoryStatus!.CurrentBranchName);
        Assert.Equal(1, observed);
    }

    [Fact]
    public void SourceControlStatusSnapshot_RemainsImmutableAfterRepositoryStatusMutation()
    {
        var branches = new List<GitBranch> { new("main", isCurrent: true) };
        var changes = new List<FileChange> { new("Program.cs", GitChangeType.Modified) };
        var repositoryStatus = new RepositoryStatusSnapshot
        {
            CurrentBranchName = "main",
            Branches = branches,
            Changes = changes,
        };

        var snapshot = new SourceControlStatusSnapshot(
            generation: 1,
            availability: SourceControlSnapshotAvailability.Available,
            repositoryStatus: repositoryStatus);

        branches.Add(new GitBranch("feature"));
        changes.Add(new FileChange("Other.cs", GitChangeType.Added));

        Assert.Single(snapshot.RepositoryStatus!.Branches);
        Assert.Single(snapshot.RepositoryStatus.Changes);
        Assert.Equal("main", snapshot.RepositoryStatus.CurrentBranchName);
    }

    [Fact]
    public void RepositoryStatusSnapshot_DefensiveCopyPreventsInputListMutation()
    {
        var branches = new List<GitBranch> { new("main", isCurrent: true) };
        var changes = new List<FileChange> { new("Program.cs", GitChangeType.Modified) };

        var snapshot = new RepositoryStatusSnapshot
        {
            CurrentBranchName = "main",
            Branches = branches,
            Changes = changes,
        };

        branches.Add(new GitBranch("feature"));
        changes.Add(new FileChange("Other.cs", GitChangeType.Added));

        Assert.Single(snapshot.Branches);
        Assert.Single(snapshot.Changes);
        Assert.Equal("main", snapshot.Branches[0].Name);
        Assert.Equal("Program.cs", snapshot.Changes[0].FilePath);
    }

    [Fact]
    public void RepositoryStatusSnapshot_DefensiveCopyPreventsBranchCollectionMutation()
    {
        // Test that original collection references cannot mutate the snapshot
        var branchesList = new List<GitBranch> { new("main", isCurrent: true) };
        var changesList = new List<FileChange> { new("Program.cs", GitChangeType.Modified) };
        var originalBranches = branchesList.ToArray();
        var originalChanges = changesList.ToArray();

        var snapshot = new RepositoryStatusSnapshot
        {
            CurrentBranchName = "main",
            Branches = originalBranches,
            Changes = originalChanges,
            IsDetachedHead = false,
            HasUpstream = false,
            AheadBy = 0,
            BehindBy = 0
        };

        // Mutate original collections - snapshot should be unaffected
        branchesList.Add(new GitBranch("feature"));
        changesList.Add(new FileChange("Other.cs", GitChangeType.Added));
        branchesList[0] = new GitBranch("changed-main", false);

        // Snapshot should still have original unmodified data
        Assert.Single(snapshot.Branches);
        Assert.Equal("main", snapshot.Branches[0].Name);
        Assert.True(snapshot.Branches[0].IsCurrent);
        Assert.Single(snapshot.Changes);
        Assert.Equal("Program.cs", snapshot.Changes[0].FilePath);

        // Verify that the snapshot's collections are read-only wrappers over defensive copies
        Assert.Throws<NotSupportedException>(() =>
            ((IList<GitBranch>)snapshot.Branches).Add(new GitBranch("new-branch")));
    }

    [Fact]
    public void RepositoryStatusSnapshot_DeepCloneCreatesIndependentNestedCollections()
    {
        // Test that CloneDefensively creates completely independent nested collections
        var branches = new List<GitBranch> { new("main", isCurrent: true) };
        var changes = new List<FileChange> {
            new("Program.cs", GitChangeType.Modified),
            new("Other.cs", GitChangeType.Added)
        };
        var original = new RepositoryStatusSnapshot
        {
            CurrentBranchName = "main",
            Branches = branches,
            Changes = changes,
            IsDetachedHead = false,
            HasUpstream = false,
            AheadBy = 0,
            BehindBy = 0
        };

        var clone = original.CloneDefensively();

        // Clone should have same content but different instances
        Assert.NotSame(original.Branches, clone.Branches);
        Assert.NotSame(original.Changes, clone.Changes);
        Assert.Equal(2, clone.Changes.Count);
        Assert.Equal("Program.cs", clone.Changes[0].FilePath);
        Assert.Equal("main", clone.CurrentBranchName);

        // Modify original branches and changes
        branches.Add(new GitBranch("feature"));
        branches[0] = new GitBranch("changed-main", false);
        branches.Add(new GitBranch("second", false));
        changes.Add(new FileChange("Third.cs", GitChangeType.Deleted));

        // Clone should remain unchanged
        Assert.Equal("main", clone.CurrentBranchName);
        Assert.Equal(2, clone.Changes.Count);
        Assert.Equal("Program.cs", clone.Changes[0].FilePath);
        Assert.Single(clone.Branches);
        Assert.Equal("main", clone.Branches[0].Name);
    }

    [Fact]
    public void SourceControlStatusSnapshot_NestedRepositoryStatusIsImmutable()
    {
        var branches = new GitBranch[] { new("main", isCurrent: true) };
        var changes = new FileChange[] { new("Program.cs", GitChangeType.Modified) };
        var repositoryStatus = new RepositoryStatusSnapshot
        {
            CurrentBranchName = "main",
            Branches = branches,
            Changes = changes,
            HasUpstream = false
        };

        var snapshot = new SourceControlStatusSnapshot(
            generation: 1,
            availability: SourceControlSnapshotAvailability.Available,
            repositoryStatus: repositoryStatus);

        // Original collections should not be modifiable via snapshot
        Assert.Throws<NotSupportedException>(() =>
            ((IList<GitBranch>)snapshot.RepositoryStatus!.Branches).Add(new GitBranch("feature")));
    }

    private sealed class TestEditorStateSnapshotService : IEditorStateSnapshotService
    {
        private readonly Subject<EditorStateSnapshot> _subject = new();
        private EditorStateSnapshot _current = new(generation: 1);

        public EditorStateSnapshot Current => _current;

        public IObservable<EditorStateSnapshot> WhenChanged => _subject;

        public void Publish(EditorStateSnapshot snapshot)
        {
            _current = snapshot;
            _subject.OnNext(snapshot);
        }

        public void Dispose() => _subject.Dispose();
    }

    private sealed class TestSourceControlSnapshotService : ISourceControlSnapshotService
    {
        private readonly Subject<SourceControlStatusSnapshot> _subject = new();
        private SourceControlStatusSnapshot _current = new(
            generation: 1,
            availability: SourceControlSnapshotAvailability.NoWorkspace);

        public SourceControlStatusSnapshot Current => _current;

        public IObservable<SourceControlStatusSnapshot> WhenChanged => _subject;

        public void Publish(SourceControlStatusSnapshot snapshot)
        {
            _current = snapshot;
            _subject.OnNext(snapshot);
        }

        public void Dispose() => _subject.Dispose();
    }
}
