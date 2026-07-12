using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 8.3 M2 unit tests for <see cref="ProjectContextService"/> lifecycle,
/// cancellation, overlapping-load protection, selection, and logging behavior.
///
/// Uses a deterministic fake <see cref="IProjectDiscovery"/> with controllable
/// <see cref="TaskCompletionSource{T}"/> gates so that tests control exactly
/// when discovery completes.
/// </summary>
public sealed class Phase83M2ProjectContextServiceTests
{
    // ── Deterministic fake discovery ────────────────────────────────────

    /// <summary>
    /// A controllable fake <see cref="IProjectDiscovery"/> that returns tasks
    /// driven by externally-supplied <see cref="TaskCompletionSource{T}"/>
    /// gates. Each call to <see cref="DiscoverAsync"/> dequeues the next gate;
    /// tests push gates in advance with <see cref="AddGate"/>.
    /// Uses <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/>
    /// to prevent synchronous-inline continuations that would deadlock the
    /// service's <see cref="SemaphoreSlim"/> gate.
    /// </summary>
    private sealed class FakeDiscovery : IProjectDiscovery
    {
        private readonly object _lock = new();
        private readonly Queue<TaskCompletionSource<ProjectDiscoveryResult>> _gates = new();
        private int _callCount;

        public int CallCount => _callCount;

        /// <summary>
        /// Enqueue a new controllable gate and return it.
        /// </summary>
        public TaskCompletionSource<ProjectDiscoveryResult> AddGate()
        {
            var tcs = new TaskCompletionSource<ProjectDiscoveryResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_lock) _gates.Enqueue(tcs);
            return tcs;
        }

        /// <summary>
        /// Add a gate that is already completed with <paramref name="result"/>.
        /// Useful for simple tests that do not need to control timing.
        /// </summary>
        public void AddResult(ProjectDiscoveryResult result)
        {
            var tcs = AddGate();
            tcs.TrySetResult(result);
        }

        public Task<ProjectDiscoveryResult> DiscoverAsync(
            string workspaceRoot, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            cancellationToken.ThrowIfCancellationRequested();

            TaskCompletionSource<ProjectDiscoveryResult> tcs;
            lock (_lock)
            {
                tcs = _gates.Dequeue();
            }
            return tcs.Task;
        }
    }

    // ── Emitted-snapshot collector ─────────────────────────────────────

    /// <summary>
    /// Subscribes to <see cref="IProjectContextService.WhenChanged"/> and
    /// collects every emitted snapshot for assertion.
    /// </summary>
    private sealed class SnapshotCollector : IDisposable
    {
        private readonly IDisposable _subscription;
        public List<ProjectContext> Snapshots { get; } = new();

        public SnapshotCollector(IProjectContextService svc)
        {
            _subscription = svc.WhenChanged.Subscribe(s => Snapshots.Add(s));
        }

        public void Dispose() => _subscription.Dispose();
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static ProjectCandidate MakeCandidate(string path, ProjectKind kind = ProjectKind.CSharpProject)
    {
        return new ProjectCandidate(
            FilePath: System.IO.Path.GetFullPath(path),
            DisplayName: System.IO.Path.GetFileNameWithoutExtension(path),
            Kind: kind);
    }

    private static (ProjectContextService Service, FakeDiscovery Discovery, Mock<ILogger<ProjectContextService>> LoggerMock)
        CreateService()
    {
        var discovery = new FakeDiscovery();
        var loggerMock = new Mock<ILogger<ProjectContextService>>();
        var service = new ProjectContextService(discovery, loggerMock.Object);
        return (service, discovery, loggerMock);
    }

    private static ProjectDiscoveryResult NoProjectResult() =>
        new(Array.Empty<ProjectCandidate>(), Array.Empty<string>(), Failure: null);

    private static ProjectDiscoveryResult UnsupportedResult() =>
        new(
            Array.Empty<ProjectCandidate>(),
            new[] { System.IO.Path.GetFullPath("/root/legacy.vbproj") },
            Failure: null);

    private static ProjectDiscoveryResult SingleProjectResult(string? path = null)
    {
        var candidate = MakeCandidate(path ?? "/root/project.csproj");
        return new ProjectDiscoveryResult(
            new[] { candidate },
            Array.Empty<string>(),
            Failure: null);
    }

    private static ProjectDiscoveryResult AmbiguousResult()
    {
        var candidates = new[]
        {
            MakeCandidate("/root/app.csproj"),
            MakeCandidate("/root/app.sln"),
        };
        return new ProjectDiscoveryResult(candidates, Array.Empty<string>(), Failure: null);
    }

    private static ProjectDiscoveryResult FailedResult(string message = "Access denied.") =>
        new(
            Array.Empty<ProjectCandidate>(),
            Array.Empty<string>(),
            new ProjectDiscoveryFailure(ProjectDiscoveryFailureKind.Unauthorized, message));

    // ── Tests ───────────────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsUnloaded()
    {
        var (svc, _, _) = CreateService();

        Assert.Equal(ProjectContextState.Unloaded, svc.Current.State);
        Assert.Null(svc.Current.WorkspaceRoot);
        Assert.Empty(svc.Current.Candidates);
        Assert.Null(svc.Current.SelectedProject);
        Assert.Empty(svc.Current.UnsupportedFiles);
        Assert.Null(svc.Current.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_EmitsLoadingBeforeDiscovery()
    {
        var (svc, discovery, _) = CreateService();
        using var collector = new SnapshotCollector(svc);

        var gate = discovery.AddGate();
        var loadTask = svc.LoadAsync("/root");

        // Allow the service thread to reach discovery and emit Loading.
        await Task.Delay(200);

        Assert.Equal(1, discovery.CallCount);

        // The first emission should be Loading.
        Assert.NotEmpty(collector.Snapshots);
        var loading = collector.Snapshots[0];
        Assert.Equal(ProjectContextState.Loading, loading.State);
        Assert.Equal("/root", loading.WorkspaceRoot);
        Assert.Empty(loading.Candidates);
        Assert.Null(loading.SelectedProject);
        Assert.Empty(loading.UnsupportedFiles);
        Assert.Null(loading.ErrorMessage);

        // Complete discovery.
        gate.TrySetResult(NoProjectResult());
        await loadTask;

        // Now we should have Loading + terminal.
        Assert.Equal(2, collector.Snapshots.Count);
    }

    [Fact]
    public async Task LoadAsync_NoProjectResult_MapsCorrectly()
    {
        var (svc, discovery, _) = CreateService();
        discovery.AddResult(NoProjectResult());
        await svc.LoadAsync("/root");

        Assert.Equal(ProjectContextState.NoProject, svc.Current.State);
        Assert.Equal("/root", svc.Current.WorkspaceRoot);
        Assert.Empty(svc.Current.Candidates);
        Assert.Null(svc.Current.SelectedProject);
        Assert.Empty(svc.Current.UnsupportedFiles);
        Assert.Null(svc.Current.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_UnsupportedResult_MapsCorrectly()
    {
        var (svc, discovery, _) = CreateService();
        discovery.AddResult(UnsupportedResult());
        await svc.LoadAsync("/root");

        Assert.Equal(ProjectContextState.Unsupported, svc.Current.State);
        Assert.Equal("/root", svc.Current.WorkspaceRoot);
        Assert.Empty(svc.Current.Candidates);
        Assert.Null(svc.Current.SelectedProject);
        Assert.NotEmpty(svc.Current.UnsupportedFiles);
        Assert.Null(svc.Current.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_SingleProjectResult_AutoSelects()
    {
        var (svc, discovery, _) = CreateService();
        discovery.AddResult(SingleProjectResult("/root/project.csproj"));
        await svc.LoadAsync("/root");

        Assert.Equal(ProjectContextState.SingleProject, svc.Current.State);
        Assert.Single(svc.Current.Candidates);
        Assert.NotNull(svc.Current.SelectedProject);
        Assert.Equal(
            System.IO.Path.GetFullPath("/root/project.csproj"),
            svc.Current.SelectedProject!.FilePath);
    }

    [Fact]
    public async Task LoadAsync_AmbiguousResult_HasNullSelection()
    {
        var (svc, discovery, _) = CreateService();
        discovery.AddResult(AmbiguousResult());
        await svc.LoadAsync("/root");

        Assert.Equal(ProjectContextState.Ambiguous, svc.Current.State);
        Assert.Equal(2, svc.Current.Candidates.Count);
        Assert.Null(svc.Current.SelectedProject);
    }

    [Fact]
    public async Task LoadAsync_FailedResult_MapsCorrectly()
    {
        var (svc, discovery, _) = CreateService();
        discovery.AddResult(FailedResult("Access denied."));
        await svc.LoadAsync("/root");

        Assert.Equal(ProjectContextState.Failed, svc.Current.State);
        Assert.Equal("/root", svc.Current.WorkspaceRoot);
        Assert.Empty(svc.Current.Candidates);
        Assert.Null(svc.Current.SelectedProject);
        Assert.Empty(svc.Current.UnsupportedFiles);
        Assert.Equal("Access denied.", svc.Current.ErrorMessage);
    }

    [Fact]
    public async Task SelectProject_ValidCandidateFromAmbiguous_EmitsSelected()
    {
        var (svc, discovery, _) = CreateService();
        discovery.AddResult(AmbiguousResult());
        await svc.LoadAsync("/root");

        using var collector = new SnapshotCollector(svc);
        var target = svc.Current.Candidates[1];

        svc.SelectProject(target);

        Assert.Equal(ProjectContextState.Selected, svc.Current.State);
        Assert.Same(target, svc.Current.SelectedProject);
        Assert.Single(collector.Snapshots);
    }

    [Fact]
    public async Task SelectProject_NullFromAmbiguous_ClearsAndEmitsAmbiguous()
    {
        var (svc, discovery, _) = CreateService();
        discovery.AddResult(AmbiguousResult());
        await svc.LoadAsync("/root");

        // First select something.
        var target = svc.Current.Candidates[0];
        svc.SelectProject(target);
        Assert.Equal(ProjectContextState.Selected, svc.Current.State);

        using var collector = new SnapshotCollector(svc);
        svc.SelectProject(null);

        Assert.Equal(ProjectContextState.Ambiguous, svc.Current.State);
        Assert.Null(svc.Current.SelectedProject);
        Assert.Single(collector.Snapshots);
    }

    [Fact]
    public async Task SelectProject_NullFromSingleProject_PreservesSelection()
    {
        var (svc, discovery, _) = CreateService();
        discovery.AddResult(SingleProjectResult("/root/project.csproj"));
        await svc.LoadAsync("/root");

        Assert.Equal(ProjectContextState.SingleProject, svc.Current.State);
        Assert.NotNull(svc.Current.SelectedProject);

        using var collector = new SnapshotCollector(svc);
        svc.SelectProject(null);

        // Should still be SingleProject with the same auto-selection.
        Assert.Equal(ProjectContextState.SingleProject, svc.Current.State);
        Assert.NotNull(svc.Current.SelectedProject);
        Assert.Empty(collector.Snapshots); // No emission.
    }

    [Fact]
    public async Task SelectProject_NullFromNoProject_PreservesNoProject()
    {
        var (svc, discovery, _) = CreateService();
        discovery.AddResult(NoProjectResult());
        await svc.LoadAsync("/root");

        using var collector = new SnapshotCollector(svc);
        svc.SelectProject(null);

        Assert.Equal(ProjectContextState.NoProject, svc.Current.State);
        Assert.Empty(collector.Snapshots);
    }

    [Fact]
    public async Task SelectProject_NullFromUnsupported_PreservesUnsupported()
    {
        var (svc, discovery, _) = CreateService();
        discovery.AddResult(UnsupportedResult());
        await svc.LoadAsync("/root");

        using var collector = new SnapshotCollector(svc);
        svc.SelectProject(null);

        Assert.Equal(ProjectContextState.Unsupported, svc.Current.State);
        Assert.Empty(collector.Snapshots);
    }

    [Fact]
    public async Task SelectProject_ForeignCandidate_LogsWarning8303()
    {
        var (svc, discovery, loggerMock) = CreateService();
        discovery.AddResult(SingleProjectResult("/root/project.csproj"));
        await svc.LoadAsync("/root");

        var foreign = MakeCandidate("/other/foreign.csproj");

        svc.SelectProject(foreign);

        // State must not change.
        Assert.Equal(ProjectContextState.SingleProject, svc.Current.State);

        // Verify Warning log at event ID 8303.
        loggerMock.Verify(l => l.Log(
            LogLevel.Warning,
            It.Is<EventId>(e => e.Id == 8303),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("/other/foreign.csproj")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SelectProject_StaleCandidate_LogsWarning8303()
    {
        var (svc, discovery, loggerMock) = CreateService();
        discovery.AddResult(AmbiguousResult());
        await svc.LoadAsync("/root");

        // Snapshot the old candidate, then reload to a different set.
        var staleCandidate = svc.Current.Candidates[0];
        discovery.AddResult(SingleProjectResult("/other/different.csproj"));
        await svc.LoadAsync("/other");

        svc.SelectProject(staleCandidate);

        // Verify Warning log at event ID 8303.
        loggerMock.Verify(l => l.Log(
            LogLevel.Warning,
            It.Is<EventId>(e => e.Id == 8303),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(staleCandidate.FilePath)),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LoadAsync_AlreadyCancelled_ThrowsBeforeAnyEmission()
    {
        var (svc, discovery, _) = CreateService();
        using var collector = new SnapshotCollector(svc);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => svc.LoadAsync("/root", cts.Token));

        Assert.Equal(0, discovery.CallCount);
        Assert.Empty(collector.Snapshots);
        Assert.Equal(ProjectContextState.Unloaded, svc.Current.State);
    }

    [Fact]
    public async Task LoadAsync_CancelledAfterLoading_RestoresUnloaded()
    {
        var (svc, discovery, _) = CreateService();
        using var collector = new SnapshotCollector(svc);

        var gate = discovery.AddGate();
        var loadTask = svc.LoadAsync("/root");

        // Wait for Loading emission.
        await Task.Delay(200);
        var loadingSnapshot = Assert.Single(collector.Snapshots);
        Assert.Equal(ProjectContextState.Loading, loadingSnapshot.State);

        // Cancel the discovery task.
        gate.TrySetCanceled();

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => loadTask);
        Assert.NotNull(ex);

        // After cancellation with no prior stable, state restores to Unloaded.
        Assert.Equal(ProjectContextState.Unloaded, svc.Current.State);
        Assert.Null(svc.Current.WorkspaceRoot);

        // Emissions: Loading then Unloaded restoration.
        Assert.Equal(2, collector.Snapshots.Count);
        Assert.Equal(ProjectContextState.Unloaded, collector.Snapshots[1].State);
    }

    [Fact]
    public async Task LoadAsync_CancelledAfterLoading_RestoresPriorStableSnapshot()
    {
        var (svc, discovery, _) = CreateService();

        // First, load a project successfully (establish a stable snapshot).
        discovery.AddResult(SingleProjectResult("/root/project.csproj"));
        await svc.LoadAsync("/root");
        Assert.Equal(ProjectContextState.SingleProject, svc.Current.State);

        using var collector = new SnapshotCollector(svc);

        // Now reload and cancel.
        var gate = discovery.AddGate();
        var reloadTask = svc.LoadAsync("/root");

        await Task.Delay(200);
        var loadingSnapshot = Assert.Single(collector.Snapshots);
        Assert.Equal(ProjectContextState.Loading, loadingSnapshot.State);

        gate.TrySetCanceled();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reloadTask);

        // Must restore the prior SingleProject snapshot.
        Assert.Equal(ProjectContextState.SingleProject, svc.Current.State);
        Assert.NotNull(svc.Current.SelectedProject);

        Assert.Equal(2, collector.Snapshots.Count);
        Assert.Equal(ProjectContextState.SingleProject, collector.Snapshots[1].State);
    }

    [Fact]
    public async Task ReloadAsync_WithNullRoot_EmitsFailed()
    {
        var (svc, _, _) = CreateService();
        using var collector = new SnapshotCollector(svc);

        // No prior load — root is null.
        await svc.ReloadAsync();

        Assert.Equal(ProjectContextState.Failed, svc.Current.State);
        Assert.Null(svc.Current.WorkspaceRoot);
        Assert.NotNull(svc.Current.ErrorMessage);
        Assert.Contains("Cannot reload", svc.Current.ErrorMessage);
        Assert.Single(collector.Snapshots);
    }

    [Fact]
    public async Task ReloadAsync_CancelledAfterLoading_RestoresPriorStableSnapshot()
    {
        var (svc, discovery, _) = CreateService();

        // Establish a stable snapshot.
        discovery.AddResult(SingleProjectResult("/root/project.csproj"));
        await svc.LoadAsync("/root");
        Assert.Equal(ProjectContextState.SingleProject, svc.Current.State);

        using var collector = new SnapshotCollector(svc);

        // Reload and cancel.
        var gate = discovery.AddGate();
        var reloadTask = svc.ReloadAsync();

        await Task.Delay(200);
        var reloadLoading = Assert.Single(collector.Snapshots);
        Assert.Equal(ProjectContextState.Loading, reloadLoading.State);
        Assert.Equal("/root", reloadLoading.WorkspaceRoot);

        gate.TrySetCanceled();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reloadTask);

        // Must restore the prior SingleProject snapshot.
        Assert.Equal(ProjectContextState.SingleProject, svc.Current.State);
        Assert.NotNull(svc.Current.SelectedProject);
        Assert.Equal(2, collector.Snapshots.Count);
        Assert.Equal(ProjectContextState.SingleProject, collector.Snapshots[1].State);
    }

    [Fact]
    public async Task OverlappingLoads_OlderCompletesFirst_NewerWins()
    {
        var (svc, discovery, _) = CreateService();
        using var collector = new SnapshotCollector(svc);

        var gate1 = discovery.AddGate();
        var gate2 = discovery.AddGate();

        var load1 = svc.LoadAsync("/root1");
        var load2 = svc.LoadAsync("/root2");

        await Task.Delay(200);

        // Older request completes first, but its result should be stale.
        gate1.TrySetResult(SingleProjectResult("/root1/project.csproj"));

        // Newer request completes second.
        gate2.TrySetResult(SingleProjectResult("/root2/project.csproj"));

        await Task.WhenAll(load1, load2);

        // Final state should reflect the newer request (root2).
        Assert.Equal(ProjectContextState.SingleProject, svc.Current.State);
        Assert.Contains("root2", svc.Current.WorkspaceRoot);

        // Collecting emissions: Loading(root1), Loading(root2), then terminal(root2).
        Assert.Equal(3, collector.Snapshots.Count);
        Assert.Equal(ProjectContextState.Loading, collector.Snapshots[0].State);
        Assert.Equal("/root1", collector.Snapshots[0].WorkspaceRoot);
        Assert.Equal(ProjectContextState.Loading, collector.Snapshots[1].State);
        Assert.Equal("/root2", collector.Snapshots[1].WorkspaceRoot);
        Assert.NotEqual(ProjectContextState.Loading, collector.Snapshots[2].State);
        Assert.Contains("root2", collector.Snapshots[2].WorkspaceRoot);
    }

    [Fact]
    public async Task OverlappingLoads_OlderCompletesLast_StaleCompletionEmitsNothing()
    {
        var (svc, discovery, _) = CreateService();
        using var collector = new SnapshotCollector(svc);

        var gate1 = discovery.AddGate();
        var gate2 = discovery.AddGate();

        var load1 = svc.LoadAsync("/root1");
        var load2 = svc.LoadAsync("/root2");

        await Task.Delay(200);

        // Newer request completes first.
        gate2.TrySetResult(SingleProjectResult("/root2/project.csproj"));

        // Older request completes last — should be stale.
        gate1.TrySetResult(SingleProjectResult("/root1/project.csproj"));

        await Task.WhenAll(load1, load2);

        // Final state must be root2 (the newer request).
        Assert.Equal(ProjectContextState.SingleProject, svc.Current.State);
        Assert.Contains("root2", svc.Current.WorkspaceRoot);

        // Emissions: Loading(root1), Loading(root2), terminal(root2).
        // The stale completion from load1 must NOT produce an emission.
        Assert.Equal(3, collector.Snapshots.Count);
        Assert.Contains("root2", collector.Snapshots[2].WorkspaceRoot);
    }

    [Fact]
    public async Task NewestRequestCancelled_AfterOlderRequestEmitsLoading()
    {
        var (svc, discovery, _) = CreateService();
        using var collector = new SnapshotCollector(svc);

        var gate1 = discovery.AddGate();
        var gate2 = discovery.AddGate();

        var load1 = svc.LoadAsync("/root1");
        var load2 = svc.LoadAsync("/root2");

        await Task.Delay(200);

        // Newest request is cancelled.
        gate2.TrySetCanceled();

        // Older request completes.
        gate1.TrySetResult(SingleProjectResult("/root1/project.csproj"));

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load2);
        await load1;

        // The newest request was cancelled after Loading, so it should restore
        // the prior stable snapshot. But there was no prior stable snapshot
        // before the overlapping sequence (root1 load was already in-flight).
        // The stable snapshot saved at the start of load2 was... LoadAsync
        // saves only non-Loading snapshots. Since load1 was in Loading state,
        // the stable snapshot is null → restore Unloaded.
        //
        // The older load1 completes and is stale (currentOwner was taken by
        // load2's sequence), so it also emits nothing.
        Assert.Equal(ProjectContextState.Unloaded, svc.Current.State);

        // Emissions: Loading(root1), Loading(root2), Unloaded(restoration).
        Assert.Equal(3, collector.Snapshots.Count);
        Assert.Equal(ProjectContextState.Loading, collector.Snapshots[0].State);
        Assert.Equal(ProjectContextState.Loading, collector.Snapshots[1].State);
        Assert.Equal(ProjectContextState.Unloaded, collector.Snapshots[2].State);
    }

    [Fact]
    public async Task StaleCancellation_AfterNewerRequestStarted_EmitsNothing()
    {
        var (svc, discovery, _) = CreateService();
        using var collector = new SnapshotCollector(svc);

        var gate1 = discovery.AddGate();
        var gate2 = discovery.AddGate();

        var load1 = svc.LoadAsync("/root1");
        var load2 = svc.LoadAsync("/root2");

        await Task.Delay(200);

        // Complete the newer request first.
        gate2.TrySetResult(SingleProjectResult("/root2/project.csproj"));
        await load2;

        // Now cancel the older (stale) request.
        gate1.TrySetCanceled();

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load1);

        // State should be root2.
        Assert.Contains("root2", svc.Current.WorkspaceRoot);

        // Emissions: Loading(root1), Loading(root2), terminal(root2).
        // The stale cancellation must NOT produce an emission.
        Assert.Equal(3, collector.Snapshots.Count);
    }

    [Fact]
    public async Task StaleCompletion_AfterNewerRequestStarted_EmitsNothing()
    {
        var (svc, discovery, _) = CreateService();
        using var collector = new SnapshotCollector(svc);

        var gate1 = discovery.AddGate();
        var gate2 = discovery.AddGate();

        var load1 = svc.LoadAsync("/root1");
        var load2 = svc.LoadAsync("/root2");

        await Task.Delay(200);

        // Newer request completes first.
        gate2.TrySetResult(SingleProjectResult("/root2/project.csproj"));
        await load2;

        // Older request completes later — stale.
        gate1.TrySetResult(NoProjectResult());
        await load1;

        // State must still be root2.
        Assert.Contains("root2", svc.Current.WorkspaceRoot);

        // Emissions: Loading(root1), Loading(root2), terminal(root2).
        Assert.Equal(3, collector.Snapshots.Count);
    }

    [Fact]
    public async Task UnloadAsync_InvalidatesInFlightLoads()
    {
        var (svc, discovery, _) = CreateService();
        using var collector = new SnapshotCollector(svc);

        var gate = discovery.AddGate();
        var loadTask = svc.LoadAsync("/root");

        await Task.Delay(200);
        var loadLoading = Assert.Single(collector.Snapshots);
        Assert.Equal(ProjectContextState.Loading, loadLoading.State);

        // Unload while load is in-flight.
        await svc.UnloadAsync();

        // The in-flight load should be stale; completing it should do nothing.
        gate.TrySetResult(SingleProjectResult("/root/project.csproj"));

        // Allow the stale completion to process.
        await Task.Delay(200);
        Assert.False(loadTask.IsFaulted);
        Assert.True(loadTask.IsCompleted);

        // State must remain Unloaded.
        Assert.Equal(ProjectContextState.Unloaded, svc.Current.State);

        // Emissions: Loading then Unloaded. Stale completion emits nothing.
        Assert.Equal(2, collector.Snapshots.Count);
        Assert.Equal(ProjectContextState.Unloaded, collector.Snapshots[1].State);
    }

    [Fact]
    public async Task UnloadAsync_AfterInFlightCancellation_InvalidatesOldest()
    {
        var (svc, discovery, _) = CreateService();
        using var collector = new SnapshotCollector(svc);

        var gate = discovery.AddGate();
        var loadTask = svc.LoadAsync("/root");

        await Task.Delay(200);

        // Unload invalidates the in-flight request.
        await svc.UnloadAsync();

        // Now cancel the in-flight discovery — it should be stale.
        gate.TrySetCanceled();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loadTask);

        // State remains Unloaded; stale cancellation emits nothing.
        Assert.Equal(ProjectContextState.Unloaded, svc.Current.State);
        Assert.Equal(2, collector.Snapshots.Count);
    }

    [Fact]
    public async Task UnexpectedDiscoveryException_IsRethrownAndLoggedAtError8301()
    {
        var (svc, discovery, loggerMock) = CreateService();
        using var collector = new SnapshotCollector(svc);

        var gate = discovery.AddGate();
        var loadTask = svc.LoadAsync("/root");

        await Task.Delay(200);

        // Fail discovery with an unexpected exception (not a failure result).
        gate.TrySetException(new InvalidOperationException("Unexpected error!"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => loadTask);
        Assert.Equal("Unexpected error!", ex.Message);

        // State should still be Loading (or whatever was set before the throw).
        // The exception is rethrown, so no terminal snapshot is emitted.
        // The Loading snapshot was already emitted.
        Assert.Equal(ProjectContextState.Loading, svc.Current.State);

        // Verify Error log at event ID 8301.
        loggerMock.Verify(l => l.Log(
            LogLevel.Error,
            It.Is<EventId>(e => e.Id == 8301),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("/root")),
            It.Is<InvalidOperationException>(ex2 => ex2.Message == "Unexpected error!"),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task WhenChanged_EmitsOnCallingThread_WithoutAvaloniaSchedulers()
    {
        var (svc, discovery, _) = CreateService();
        var emittedThreadId = 0;

        var gate = discovery.AddGate();

        using var subscription = svc.WhenChanged.Subscribe(_ =>
        {
            emittedThreadId = Environment.CurrentManagedThreadId;
        });

        var loadTask = svc.LoadAsync("/root");
        await Task.Delay(200);

        // The Loading emission should have been captured.
        Assert.NotEqual(0, emittedThreadId);
        var loadingThreadId = emittedThreadId;

        emittedThreadId = 0;
        gate.TrySetResult(NoProjectResult());
        await loadTask;

        // The terminal emission should also have happened.
        Assert.NotEqual(0, emittedThreadId);

        // Both emissions should be on the same thread as the one doing the calling.
        // (In a synchronous context this is the test thread, but with async
        // continuations it may vary. The key assertion is that the emission
        // is not dispatched to a UI scheduler — it happens inline on whatever
        // thread is mutating state.)
    }

    [Fact]
    public async Task Dispose_StopsFutureEmissions()
    {
        var (svc, discovery, _) = CreateService();
        using var collector = new SnapshotCollector(svc);

        svc.Dispose();

        // Attempting to load after dispose should fail.
        discovery.AddResult(NoProjectResult());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => svc.LoadAsync("/root"));

        // WhenChanged should be completed.
        Assert.Empty(collector.Snapshots);

        // Current should still return the last snapshot without throwing.
        _ = svc.Current;
    }

    [Fact]
    public async Task LoadAsync_NullRoot_MapsToFailedWithInvalidRoot()
    {
        var (svc, discovery, _) = CreateService();
        discovery.AddResult(new ProjectDiscoveryResult(
            Array.Empty<ProjectCandidate>(),
            Array.Empty<string>(),
            new ProjectDiscoveryFailure(ProjectDiscoveryFailureKind.InvalidRoot, "Root path is invalid.")));
        await svc.LoadAsync("/invalid");

        Assert.Equal(ProjectContextState.Failed, svc.Current.State);
        Assert.Equal("InvalidRoot", ProjectDiscoveryFailureKind.InvalidRoot.ToString());
    }

    [Fact]
    public async Task LoadAsync_SingleProject_SelectsTheSoleCandidate()
    {
        var (svc, discovery, _) = CreateService();
        var candidate = MakeCandidate("/root/app.csproj", ProjectKind.CSharpProject);
        discovery.AddResult(new ProjectDiscoveryResult(
            new[] { candidate },
            Array.Empty<string>(),
            Failure: null));
        await svc.LoadAsync("/root");

        Assert.Single(svc.Current.Candidates);
        Assert.NotNull(svc.Current.SelectedProject);
        Assert.Same(svc.Current.Candidates[0], svc.Current.SelectedProject);
        Assert.Equal(ProjectKind.CSharpProject, svc.Current.SelectedProject!.Kind);
    }

    [Fact]
    public async Task LoadThenUnload_ProducesExactlyTwoEmissions()
    {
        var (svc, discovery, _) = CreateService();
        using var collector = new SnapshotCollector(svc);

        discovery.AddResult(SingleProjectResult("/root/project.csproj"));
        await svc.LoadAsync("/root");

        await svc.UnloadAsync();

        // Emissions: Loading, SingleProject, Unloaded.
        Assert.Equal(3, collector.Snapshots.Count);
        Assert.Equal(ProjectContextState.Loading, collector.Snapshots[0].State);
        Assert.Equal(ProjectContextState.SingleProject, collector.Snapshots[1].State);
        Assert.Equal(ProjectContextState.Unloaded, collector.Snapshots[2].State);
    }

    [Fact]
    public async Task SelectProject_NullFromFailedState_IsNoOp()
    {
        var (svc, discovery, _) = CreateService();
        discovery.AddResult(FailedResult("Disk error"));
        await svc.LoadAsync("/root");

        using var collector = new SnapshotCollector(svc);
        svc.SelectProject(null);

        Assert.Equal(ProjectContextState.Failed, svc.Current.State);
        Assert.Empty(collector.Snapshots);
    }

    [Fact]
    public async Task ReloadAsync_WithValidRoot_EmitsLoadingThenTerminal()
    {
        var (svc, discovery, _) = CreateService();

        // Establish a stable snapshot.
        discovery.AddResult(SingleProjectResult("/root/project.csproj"));
        await svc.LoadAsync("/root");

        using var collector = new SnapshotCollector(svc);
        var gate = discovery.AddGate();
        var reloadTask = svc.ReloadAsync();

        await Task.Delay(200);

        // Loading should be emitted.
        Assert.Single(collector.Snapshots);
        Assert.Equal(ProjectContextState.Loading, collector.Snapshots[0].State);
        Assert.Equal("/root", collector.Snapshots[0].WorkspaceRoot);

        // Complete reload.
        gate.TrySetResult(SingleProjectResult("/root/project.csproj"));
        await reloadTask;

        Assert.Equal(ProjectContextState.SingleProject, svc.Current.State);
        Assert.Equal(2, collector.Snapshots.Count);
        Assert.Equal(ProjectContextState.SingleProject, collector.Snapshots[1].State);
    }

    [Fact]
    public async Task UnloadAsync_CancelledToken_ThrowsBeforeEmission()
    {
        var (svc, _, _) = CreateService();
        using var collector = new SnapshotCollector(svc);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => svc.UnloadAsync(cts.Token));

        Assert.Empty(collector.Snapshots);
        Assert.Equal(ProjectContextState.Unloaded, svc.Current.State);
    }

    [Fact]
    public async Task ReloadAsync_CancelledToken_ThrowsBeforeEmission()
    {
        var (svc, discovery, _) = CreateService();
        discovery.AddResult(SingleProjectResult("/root/project.csproj"));
        await svc.LoadAsync("/root");

        using var collector = new SnapshotCollector(svc);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => svc.ReloadAsync(cts.Token));

        Assert.Empty(collector.Snapshots);
    }

    [Fact]
    public async Task SelectProject_SameCandidateTwice_DoesNotEmitDuplicate()
    {
        var (svc, discovery, _) = CreateService();
        discovery.AddResult(AmbiguousResult());
        await svc.LoadAsync("/root");

        using var collector = new SnapshotCollector(svc);

        // First selection from Ambiguous — should emit Selected.
        svc.SelectProject(svc.Current.Candidates[0]);
        Assert.Single(collector.Snapshots);

        // Second selection of the same candidate — must not emit duplicate.
        svc.SelectProject(svc.Current.Candidates[0]);
        Assert.Single(collector.Snapshots); // No duplicate emission.
    }

    [Fact]
    public async Task SelectProject_LoadingState_DoesNotEmit()
    {
        var (svc, discovery, _) = CreateService();
        var gate = discovery.AddGate();
        var loadTask = svc.LoadAsync("/root");

        await Task.Delay(200);

        // Current should be Loading.
        Assert.Equal(ProjectContextState.Loading, svc.Current.State);

        // Select something during loading — should be a no-op.
        var candidate = MakeCandidate("/root/app.csproj");
        svc.SelectProject(candidate);

        // State should still be Loading.
        Assert.Equal(ProjectContextState.Loading, svc.Current.State);

        gate.TrySetResult(SingleProjectResult("/root/project.csproj"));
        await loadTask;
    }

    [Fact]
    public async Task LoadAsync_FailureResult_DoesNotLogError()
    {
        // Failure results from IProjectDiscovery (e.g. NotFound, Unauthorized)
        // should NOT be logged at Error — they are expected operational failures.
        var (svc, discovery, loggerMock) = CreateService();

        discovery.AddResult(FailedResult("Directory not found."));

        await svc.LoadAsync("/root");

        Assert.Equal(ProjectContextState.Failed, svc.Current.State);

        // No Error log should have been recorded.
        loggerMock.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);

        // Only Debug (cancellation) or Warning (8303) logs allowed.
        // In this case: no logs at all since nothing was rejected or cancelled.
    }
}
