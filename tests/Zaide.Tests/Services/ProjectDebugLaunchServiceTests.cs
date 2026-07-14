using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 12 M3a tests for <see cref="ProjectDebugLaunchService"/> handoff orchestration.
/// </summary>
public sealed class ProjectDebugLaunchServiceTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase12-m3a-launch-" + Guid.NewGuid().ToString("N"));

    static ProjectDebugLaunchServiceTests()
    {
        Directory.CreateDirectory(TempRoot);
    }

    private sealed class FakeProjectContextService : IProjectContextService
    {
        private ProjectContext _current = Unloaded();

        public ProjectContext Current => _current;

        public IObservable<ProjectContext> WhenChanged => new Subject<ProjectContext>();

        public void Set(ProjectContext context) => _current = context;

        public Task LoadAsync(string workspaceRoot, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task ReloadAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UnloadAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void SelectProject(ProjectCandidate? candidate) =>
            throw new NotSupportedException();

        public void Dispose()
        {
        }

        private static ProjectContext Unloaded() => new(
            ProjectContextState.Unloaded,
            WorkspaceRoot: null,
            Candidates: Array.Empty<ProjectCandidate>(),
            SelectedProject: null,
            UnsupportedFiles: Array.Empty<string>(),
            ErrorMessage: null);
    }

    private sealed class FakeWorkflowService : IProjectWorkflowService
    {
        public int BuildForHandoffCount { get; private set; }
        public ProjectWorkflowOutcomeKind BuildOutcome { get; set; } = ProjectWorkflowOutcomeKind.Succeeded;
        public TaskCompletionSource<bool>? BuildGate { get; set; }

        public ProjectWorkflowSnapshot Current { get; } = new(
            ProjectWorkflowOperationState.Idle,
            0,
            null,
            null,
            null,
            null,
            Array.Empty<ManagedProcessOutputLine>(),
            null);

        public IObservable<ProjectWorkflowSnapshot> WhenChanged => new Subject<ProjectWorkflowSnapshot>();
        public IObservable<ManagedProcessOutputLine> WhenOutputReceived => new Subject<ManagedProcessOutputLine>();

        public Task<ProjectWorkflowOperationResult> StartBuildAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Debug launch must use handoff build.");

        public async Task<ProjectWorkflowOperationResult> StartBuildForDebugHandoffAsync(
            IProjectOperationHandoffLease handoffLease,
            CancellationToken cancellationToken = default)
        {
            BuildForHandoffCount++;
            if (BuildGate is not null)
                await BuildGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            return new ProjectWorkflowOperationResult(
                BuildOutcome,
                1,
                ProjectWorkflowOperation.Build,
                Path.Combine(TempRoot, "App.csproj"),
                BuildOutcome == ProjectWorkflowOutcomeKind.Succeeded ? 0 : 1);
        }

        public Task<ProjectWorkflowOperationResult> StartRunAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProjectWorkflowOperationResult> StartTestAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task CancelAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class FakeTargetResolver : IProjectDebugTargetResolver
    {
        public ProjectDebugTargetResolution Next { get; set; } =
            ProjectDebugTargetResolution.Success(Path.Combine(TempRoot, "App.dll"));

        public Task<ProjectDebugTargetResolution> ResolveTargetPathAsync(
            string absoluteCsprojPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Next);
    }

    private sealed class FakeDebugSessionService : IDebugSessionService
    {
        public DebugLaunchRequest? LastLaunch { get; private set; }
        public DebugSessionOperationResult NextStartResult { get; set; } =
            new(true, null, null);
        public List<(DebugSessionOutcomeKind Kind, string Message)> PreLaunchFailures { get; } = new();

        public DebugSessionSnapshot Current { get; private set; } = new(
            DebugSessionState.Idle,
            0,
            null,
            null,
            null,
            null,
            null,
            Array.Empty<string>(),
            DebugSessionSnapshot.EmptyVerifications);

        public IObservable<DebugSessionSnapshot> WhenChanged => new Subject<DebugSessionSnapshot>();

        public Task<DebugSessionOperationResult> StartLaunchAsync(
            DebugLaunchRequest request,
            CancellationToken cancellationToken = default)
        {
            LastLaunch = request;
            return Task.FromResult(NextStartResult);
        }

        public Task<DebugSessionOperationResult> ReportPreLaunchFailureAsync(
            DebugSessionOutcomeKind kind,
            string message,
            CancellationToken cancellationToken = default)
        {
            PreLaunchFailures.Add((kind, message));
            Current = new DebugSessionSnapshot(
                DebugSessionState.Failed,
                Current.Generation + 1,
                null,
                null,
                null,
                null,
                new DebugSessionFailure(kind, message),
                new[] { $"[error] {message}" },
                DebugSessionSnapshot.EmptyVerifications);
            return Task.FromResult(new DebugSessionOperationResult(false, kind, message));
        }

        public Task<DebugSessionOperationResult> StopAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DebugSessionOperationResult(true, null, null));

        public Task<DebugSessionOperationResult> ContinueAsync(
            int threadId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DebugSessionOperationResult> PauseAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DebugSessionOperationResult> StepOverAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DebugSessionOperationResult> StepIntoAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DebugSessionOperationResult> StepOutAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<System.Text.Json.JsonElement?> RequestThreadsAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<System.Text.Json.JsonElement?> RequestStackTraceAsync(
            int threadId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<System.Text.Json.JsonElement?> RequestScopesAsync(
            int frameId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<System.Text.Json.JsonElement?> RequestVariablesAsync(
            int variablesReference,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DebugSessionOperationResult> ReplaceBreakpointsBySourceAsync(
            System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<int>> replacementBySource,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Dispose()
        {
        }
    }

    private sealed class FakeBreakpointService : IBreakpointService
    {
        public IReadOnlyList<PersistedBreakpoint> Breakpoints { get; set; } =
            Array.Empty<PersistedBreakpoint>();

        public IReadOnlyList<PersistedBreakpoint> GetBreakpoints() => Breakpoints;

        public Task<BreakpointOperationResult> AddAsync(
            string sourcePath,
            int line,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BreakpointOperationResult> RemoveAsync(
            string sourcePath,
            int line,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BreakpointOperationResult> ToggleAsync(
            string sourcePath,
            int line,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IReadOnlyDictionary<string, IReadOnlyList<int>> MapToDapReplacementBySource(
            IReadOnlyCollection<string> sourcePaths) =>
            sourcePaths.ToDictionary(path => path, _ => (IReadOnlyList<int>)Array.Empty<int>());
    }

    private static ProjectCandidate MakeCandidate(string fileName, ProjectKind kind = ProjectKind.CSharpProject)
    {
        var path = Path.GetFullPath(Path.Combine(TempRoot, fileName));
        return new ProjectCandidate(path, Path.GetFileNameWithoutExtension(path), kind);
    }

    private static (
        ProjectDebugLaunchService Service,
        FakeProjectContextService Context,
        FakeWorkflowService Workflow,
        FakeTargetResolver Resolver,
        FakeDebugSessionService Debug,
        ProjectOperationGate Gate)
        CreateHarness(ProjectContext? initial = null)
    {
        var context = new FakeProjectContextService();
        var workflow = new FakeWorkflowService();
        var resolver = new FakeTargetResolver();
        var debug = new FakeDebugSessionService();
        var gate = new ProjectOperationGate(debug);
        var breakpoints = new FakeBreakpointService();

        if (initial is not null)
            context.Set(initial);

        var service = new ProjectDebugLaunchService(
            context,
            gate,
            workflow,
            resolver,
            debug,
            breakpoints,
            NullLogger<ProjectDebugLaunchService>.Instance);

        return (service, context, workflow, resolver, debug, gate);
    }

    [Fact]
    public async Task StartDebuggingAsync_Solution_RejectsContext()
    {
        var candidate = MakeCandidate("App.sln", ProjectKind.Solution);
        var (service, _, _, _, _, _) = CreateHarness(new ProjectContext(
            ProjectContextState.Selected,
            TempRoot,
            new[] { candidate },
            candidate,
            Array.Empty<string>(),
            null));

        var result = await service.StartDebuggingAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(DebugSessionOutcomeKind.RejectedContext, result.Outcome);
    }

    [Fact]
    public async Task StartDebuggingAsync_WorkflowBusy_RejectsConcurrent()
    {
        var candidate = MakeCandidate("App.csproj");
        var (service, _, _, _, _, gate) = CreateHarness(new ProjectContext(
            ProjectContextState.SingleProject,
            TempRoot,
            new[] { candidate },
            candidate,
            Array.Empty<string>(),
            null));

        var lease = await gate.TryAcquireWorkflowOperationAsync(ProjectOperationKind.Build);
        Assert.True(lease.IsSuccess);

        var result = await service.StartDebuggingAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(DebugSessionOutcomeKind.RejectedConcurrent, result.Outcome);
        Assert.Equal(ProjectOperationGateMessages.WorkflowBusy, result.Message);

        lease.Lease!.Dispose();
    }

    [Fact]
    public async Task StartDebuggingAsync_BuildFailure_ReturnsBuildFailedAndReleasesHandoff()
    {
        var candidate = MakeCandidate("App.csproj");
        var (service, _, workflow, _, debug, gate) = CreateHarness(new ProjectContext(
            ProjectContextState.SingleProject,
            TempRoot,
            new[] { candidate },
            candidate,
            Array.Empty<string>(),
            null));

        workflow.BuildOutcome = ProjectWorkflowOutcomeKind.Failed;

        var result = await service.StartDebuggingAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(DebugSessionOutcomeKind.BuildFailed, result.Outcome);
        Assert.False(gate.IsDebugHandoffActive);
        Assert.Equal(1, workflow.BuildForHandoffCount);
        Assert.Contains(debug.PreLaunchFailures, f => f.Kind == DebugSessionOutcomeKind.BuildFailed);
        Assert.Equal(DebugSessionState.Failed, debug.Current.State);
    }

    [Fact]
    public async Task StartDebuggingAsync_TargetResolutionFailure_ReturnsUnsupportedLaunchTarget()
    {
        var candidate = MakeCandidate("App.csproj");
        var (service, _, workflow, resolver, debug, gate) = CreateHarness(new ProjectContext(
            ProjectContextState.SingleProject,
            TempRoot,
            new[] { candidate },
            candidate,
            Array.Empty<string>(),
            null));

        resolver.Next = ProjectDebugTargetResolution.Unsupported("bad target");

        var result = await service.StartDebuggingAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(DebugSessionOutcomeKind.UnsupportedLaunchTarget, result.Outcome);
        Assert.Null(debug.LastLaunch);
        Assert.False(gate.IsDebugHandoffActive);
        Assert.Equal(1, workflow.BuildForHandoffCount);
        Assert.Contains(debug.PreLaunchFailures, f => f.Kind == DebugSessionOutcomeKind.UnsupportedLaunchTarget);
    }

    [Fact]
    public async Task StartDebuggingAsync_SuccessfulHandoff_BuildResolveLaunch()
    {
        var candidate = MakeCandidate("App.csproj");
        var dll = Path.Combine(TempRoot, "App.dll");
        File.WriteAllText(dll, "stub");

        var (service, _, workflow, resolver, debug, gate) = CreateHarness(new ProjectContext(
            ProjectContextState.SingleProject,
            TempRoot,
            new[] { candidate },
            candidate,
            Array.Empty<string>(),
            null));

        resolver.Next = ProjectDebugTargetResolution.Success(dll);
        workflow.BuildGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var startTask = service.StartDebuggingAsync();
        await WaitUntilAsync(() => gate.IsDebugHandoffActive);
        var blocked = await gate.TryAcquireWorkflowOperationAsync(ProjectOperationKind.Build);
        Assert.False(blocked.IsSuccess);

        workflow.BuildGate.TrySetResult(true);
        var result = await startTask;

        Assert.True(result.Succeeded);
        Assert.NotNull(debug.LastLaunch);
        Assert.Equal(dll, debug.LastLaunch!.ProgramPath);
        Assert.False(gate.IsDebugHandoffActive);
        Assert.Equal(1, workflow.BuildForHandoffCount);
    }

    [Fact]
    public async Task StartDebuggingAsync_AdapterFailure_ReleasesHandoff()
    {
        var candidate = MakeCandidate("App.csproj");
        var dll = Path.Combine(TempRoot, "launch-fail.dll");
        File.WriteAllText(dll, "stub");

        var (service, _, _, resolver, debug, gate) = CreateHarness(new ProjectContext(
            ProjectContextState.SingleProject,
            TempRoot,
            new[] { candidate },
            candidate,
            Array.Empty<string>(),
            null));

        resolver.Next = ProjectDebugTargetResolution.Success(dll);
        debug.NextStartResult = new DebugSessionOperationResult(
            false,
            DebugSessionOutcomeKind.StartupFailed,
            "adapter failed");

        var result = await service.StartDebuggingAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(DebugSessionOutcomeKind.StartupFailed, result.Outcome);
        Assert.False(gate.IsDebugHandoffActive);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        var deadline = DateTime.UtcNow + timeout.Value;

        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
                return;

            await Task.Delay(20);
        }

        throw new TimeoutException("Timed out waiting for predicate.");
    }
}