using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 8.3 M3 integration tests for <see cref="ProjectContextService"/> wiring
/// to <see cref="Workspace.WorkspaceFolderChanged"/>, startup reconciliation,
/// deterministic disposal/unsubscription, and logger-event behavior.
///
/// Uses a deterministic fake <see cref="IProjectDiscovery"/> (no real filesystem,
/// no timing dependencies beyond short coordination delays) and the real
/// <see cref="Workspace"/> model as the event seam.
/// </summary>
public sealed class Phase83M3ProjectContextServiceIntegrationTests
{
    // ── Deterministic fake discovery ─────────────────────────────────────

    private sealed class FakeDiscovery : IProjectDiscovery
    {
        private readonly Queue<Func<CancellationToken, Task<ProjectDiscoveryResult>>> _behaviors = new();
        public int CallCount { get; private set; }
        public List<string> Roots { get; } = new();

        public void Enqueue(ProjectDiscoveryResult result)
            => _behaviors.Enqueue(_ => Task.FromResult(result));

        public TaskCompletionSource<ProjectDiscoveryResult> EnqueueGate()
        {
            var tcs = new TaskCompletionSource<ProjectDiscoveryResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _behaviors.Enqueue(_ => tcs.Task);
            return tcs;
        }

        public void EnqueueCancel()
            => _behaviors.Enqueue(ct =>
            {
                ct.ThrowIfCancellationRequested();
                var tcs = new TaskCompletionSource<ProjectDiscoveryResult>();
                tcs.TrySetCanceled();
                return tcs.Task;
            });

        public void EnqueueThrow(Exception ex)
            => _behaviors.Enqueue(_ =>
            {
                var tcs = new TaskCompletionSource<ProjectDiscoveryResult>();
                tcs.TrySetException(ex);
                return tcs.Task;
            });

        public Task<ProjectDiscoveryResult> DiscoverAsync(
            string workspaceRoot, CancellationToken cancellationToken)
        {
            CallCount++;
            Roots.Add(workspaceRoot);
            cancellationToken.ThrowIfCancellationRequested();
            var behavior = _behaviors.Count > 0
                ? _behaviors.Dequeue()
                : (_ => Task.FromResult(NoProject()));
            return behavior(cancellationToken);
        }
    }

    // ── Emitted-snapshot collector ──────────────────────────────────────

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

    // ── Helpers ──────────────────────────────────────────────────────────

    private static ProjectCandidate MakeCandidate(string path, ProjectKind kind = ProjectKind.CSharpProject) =>
        new(FilePath: System.IO.Path.GetFullPath(path),
            DisplayName: System.IO.Path.GetFileNameWithoutExtension(path),
            Kind: kind);

    private static ProjectDiscoveryResult NoProject() =>
        new(Array.Empty<ProjectCandidate>(), Array.Empty<string>(), Failure: null);

    private static ProjectDiscoveryResult SingleProject(string path = "/root/project.csproj") =>
        new(new[] { MakeCandidate(path) }, Array.Empty<string>(), Failure: null);

    private static ProjectDiscoveryResult Failed(string message = "disk gone") =>
        new(Array.Empty<ProjectCandidate>(), Array.Empty<string>(),
            new ProjectDiscoveryFailure(ProjectDiscoveryFailureKind.Io, message));

    private static (ProjectContextService Svc, Workspace Workspace, FakeDiscovery Discovery, Mock<ILogger<ProjectContextService>> Logger)
        Create(Workspace? workspace = null)
    {
        var ws = workspace ?? new Workspace();
        var discovery = new FakeDiscovery();
        var logger = new Mock<ILogger<ProjectContextService>>();
        var svc = new ProjectContextService(ws, discovery, logger.Object);
        return (svc, ws, discovery, logger);
    }

    // ── Tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task WorkspaceOpenEvent_WithNonNullPath_StartsDiscovery()
    {
        var (svc, ws, discovery, _) = Create();
        discovery.Enqueue(NoProject());
        using var collector = new SnapshotCollector(svc);

        ws.SetProjectFromPath("/root");
        await Task.Delay(150);

        Assert.Equal(1, discovery.CallCount);
        Assert.Contains("/root", discovery.Roots);
        // Loading then NoProject.
        Assert.Equal(2, collector.Snapshots.Count);
        Assert.Equal(ProjectContextState.Loading, collector.Snapshots[0].State);
        Assert.Equal(ProjectContextState.NoProject, collector.Snapshots[1].State);
        Assert.Equal(ProjectContextState.NoProject, svc.Current.State);
    }

    [Fact]
    public async Task WorkspaceCloseEvent_WithNullPath_UnloadsContext()
    {
        var (svc, ws, discovery, _) = Create();
        discovery.Enqueue(SingleProject("/root/a.csproj"));

        ws.SetProjectFromPath("/root");
        await Task.Delay(150);
        Assert.Equal(ProjectContextState.SingleProject, svc.Current.State);

        discovery.Enqueue(NoProject()); // unused: UnloadAsync does not discover
        using var collector = new SnapshotCollector(svc);

        ws.SetProjectFromPath(null);
        await Task.Delay(100);

        Assert.Equal(ProjectContextState.Unloaded, svc.Current.State);
        Assert.Single(collector.Snapshots);
        Assert.Equal(ProjectContextState.Unloaded, collector.Snapshots[0].State);
        // Unload did not call discovery again.
        Assert.Equal(1, discovery.CallCount);
    }

    [Fact]
    public async Task StartupReconciliation_DiscoversAlreadyOpenWorkspacePath()
    {
        var ws = new Workspace();
        ws.SetProjectFromPath("/already/open"); // raises event with no subscriber yet

        var discovery = new FakeDiscovery();
        discovery.Enqueue(NoProject());
        var logger = new Mock<ILogger<ProjectContextService>>();

        var svc = new ProjectContextService(ws, discovery, logger.Object);
        await Task.Delay(100);

        Assert.Equal(1, discovery.CallCount);
        Assert.Contains("/already/open", discovery.Roots);
        Assert.Equal(ProjectContextState.NoProject, svc.Current.State);
    }

    [Fact]
    public async Task NullStartupPath_RemainsUnloadedWithNoDiscovery()
    {
        var ws = new Workspace(); // WorkspacePath is null
        var discovery = new FakeDiscovery();
        var logger = new Mock<ILogger<ProjectContextService>>();

        var svc = new ProjectContextService(ws, discovery, logger.Object);
        await Task.Delay(50);

        Assert.Equal(0, discovery.CallCount);
        Assert.Equal(ProjectContextState.Unloaded, svc.Current.State);
        Assert.Null(svc.Current.WorkspaceRoot);
    }

    [Fact]
    public async Task EventTriggeredExpectedFailure_ProducesFailedState()
    {
        var (svc, ws, discovery, _) = Create();
        discovery.Enqueue(Failed("disk gone"));

        ws.SetProjectFromPath("/root");
        await Task.Delay(150);

        Assert.Equal(ProjectContextState.Failed, svc.Current.State);
        Assert.Equal("disk gone", svc.Current.ErrorMessage);
    }

    [Fact]
    public async Task EventTriggeredCancellation_IsLoggedAtDebugNotError()
    {
        var (svc, ws, discovery, logger) = Create();
        var gate = discovery.EnqueueGate();

        ws.SetProjectFromPath("/root");
        await Task.Delay(150);
        Assert.Equal(1, discovery.CallCount);

        gate.TrySetCanceled();
        await Task.Delay(150);

        // Cancellation is logged at Debug (event ID 8302), not Error.
        logger.Verify(l => l.Log(
            LogLevel.Debug,
            It.Is<EventId>(e => e.Id == 8302),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);

        // Cancelled load (no prior stable) restores Unloaded — not Failed.
        Assert.Equal(ProjectContextState.Unloaded, svc.Current.State);
    }

    [Fact]
    public async Task EventTriggeredUnexpectedException_IsLoggedAtErrorAndObserved()
    {
        var (svc, ws, discovery, logger) = Create();
        discovery.EnqueueThrow(new InvalidOperationException("boom"));

        ws.SetProjectFromPath("/root");
        await Task.Delay(150);

        // Unexpected exception is logged once at Error with event ID 8301.
        logger.Verify(l => l.Log(
            LogLevel.Error,
            It.Is<EventId>(e => e.Id == 8301),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("/root")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // No terminal snapshot emitted; state stays Loading (not Failed).
        Assert.Equal(ProjectContextState.Loading, svc.Current.State);

        // The task is observed (no UnobservedTaskException). Implicit: the test
        // completes and the host would surface an unobserved exception otherwise.
    }

    [Fact]
    public async Task Disposal_UnsubscribesAndStopsEventDrivenDiscovery()
    {
        var (svc, ws, discovery, _) = Create();
        discovery.Enqueue(NoProject());

        ws.SetProjectFromPath("/root");
        await Task.Delay(150);
        Assert.Equal(1, discovery.CallCount);
        Assert.Equal(ProjectContextState.NoProject, svc.Current.State);

        svc.Dispose();

        // Raising the event after disposal must not trigger discovery.
        ws.SetProjectFromPath("/other");
        await Task.Delay(100);

        Assert.Equal(1, discovery.CallCount);
        Assert.Equal(ProjectContextState.NoProject, svc.Current.State);
    }

    [Fact]
    public async Task DisposedService_IgnoresWorkspaceCloseEvents()
    {
        var (svc, ws, discovery, _) = Create();
        discovery.Enqueue(SingleProject("/root/a.csproj"));

        ws.SetProjectFromPath("/root");
        await Task.Delay(150);
        Assert.Equal(ProjectContextState.SingleProject, svc.Current.State);

        svc.Dispose();

        // A close event after disposal must not unload or re-discover.
        ws.SetProjectFromPath(null);
        await Task.Delay(100);

        Assert.Equal(1, discovery.CallCount);
        Assert.Equal(ProjectContextState.SingleProject, svc.Current.State);
    }
}
