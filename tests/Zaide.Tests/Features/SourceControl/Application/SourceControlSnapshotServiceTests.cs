using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Domain;

namespace Zaide.Tests.Features.SourceControl.Application;

public sealed class SourceControlSnapshotServiceTests
{
    [Fact]
    public void InitialSnapshot_IsNoWorkspace()
    {
        using var service = new SourceControlSnapshotService();

        Assert.Equal(0, service.Current.Generation);
        Assert.Equal(SourceControlSnapshotAvailability.NoWorkspace, service.Current.Availability);
    }

    [Fact]
    public void TryPublish_UpdatesCurrentAndEmitsWhenChanged()
    {
        using var service = new SourceControlSnapshotService();
        var observed = new List<SourceControlStatusSnapshot>();
        using var subscription = service.WhenChanged.Subscribe(observed.Add);

        var repositoryStatus = new RepositoryStatusSnapshot
        {
            CurrentBranchName = "main",
            Changes = new[] { new FileChange("Program.cs", GitChangeType.Modified) },
        };

        var published = service.TryPublish(new SourceControlStatusSnapshot(
            generation: 0,
            availability: SourceControlSnapshotAvailability.Available,
            workspacePath: "/workspace",
            repositoryStatus: repositoryStatus,
            statusMessage: null));

        Assert.True(published);
        Assert.Equal(1, service.Current.Generation);
        Assert.Equal(SourceControlSnapshotAvailability.Available, service.Current.Availability);
        Assert.Single(observed);
        Assert.NotSame(repositoryStatus, service.Current.RepositoryStatus);
        Assert.Equal("main", service.Current.RepositoryStatus!.CurrentBranchName);
    }

    [Fact]
    public void TryPublish_DefensivelyClonesNestedCollections()
    {
        using var service = new SourceControlSnapshotService();
        var branches = new List<GitBranch> { new("main", isCurrent: true) };
        var changes = new List<FileChange> { new("Program.cs", GitChangeType.Modified) };
        var repositoryStatus = new RepositoryStatusSnapshot
        {
            CurrentBranchName = "main",
            Branches = branches,
            Changes = changes,
        };

        Assert.True(service.TryPublish(new SourceControlStatusSnapshot(
            generation: 0,
            availability: SourceControlSnapshotAvailability.Available,
            repositoryStatus: repositoryStatus)));

        branches.Add(new GitBranch("feature"));
        changes.Add(new FileChange("Other.cs", GitChangeType.Added));

        Assert.Single(service.Current.RepositoryStatus!.Branches);
        Assert.Single(service.Current.RepositoryStatus.Changes);
    }

    [Fact]
    public void TryPublish_IncreasesGenerationMonotonically()
    {
        using var service = new SourceControlSnapshotService();

        Assert.True(service.TryPublish(new SourceControlStatusSnapshot(
            generation: 0,
            availability: SourceControlSnapshotAvailability.NotARepository,
            workspacePath: "/workspace")));
        Assert.True(service.TryPublish(new SourceControlStatusSnapshot(
            generation: 0,
            availability: SourceControlSnapshotAvailability.Available,
            workspacePath: "/workspace")));

        Assert.Equal(2, service.Current.Generation);
        Assert.Equal(SourceControlSnapshotAvailability.Available, service.Current.Availability);
    }

    [Fact]
    public void TryPublish_WorkspaceClose_ReturnsNoWorkspace()
    {
        using var service = new SourceControlSnapshotService();

        Assert.True(service.TryPublish(new SourceControlStatusSnapshot(
            generation: 0,
            availability: SourceControlSnapshotAvailability.Available,
            workspacePath: "/workspace")));
        Assert.True(service.TryPublish(new SourceControlStatusSnapshot(
            generation: 0,
            availability: SourceControlSnapshotAvailability.NoWorkspace,
            workspacePath: null)));

        Assert.Equal(SourceControlSnapshotAvailability.NoWorkspace, service.Current.Availability);
        Assert.Null(service.Current.WorkspacePath);
    }

    [Fact]
    public void TryPublish_RejectsStaleGeneration()
    {
        using var service = new SourceControlSnapshotService();

        Assert.True(service.TryPublish(new SourceControlStatusSnapshot(
            generation: 0,
            availability: SourceControlSnapshotAvailability.Available,
            workspacePath: "/workspace")));
        Assert.False(service.TryPublish(new SourceControlStatusSnapshot(
            generation: 1,
            availability: SourceControlSnapshotAvailability.Failed,
            workspacePath: "/workspace")));

        Assert.Equal(SourceControlSnapshotAvailability.Available, service.Current.Availability);
    }

    [Fact]
    public void TryPublish_AfterDispose_ReturnsFalse()
    {
        var service = new SourceControlSnapshotService();
        service.Dispose();

        Assert.False(service.TryPublish(new SourceControlStatusSnapshot(
            generation: 0,
            availability: SourceControlSnapshotAvailability.Available,
            workspacePath: "/workspace")));
    }

    [Fact]
    public void Dispose_CompletesWhenChanged()
    {
        var service = new SourceControlSnapshotService();
        var completed = false;
        using var subscription = service.WhenChanged.Subscribe(
            _ => { },
            _ => { },
            () => completed = true);

        service.Dispose();

        Assert.True(completed);
    }
}
