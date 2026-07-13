using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 11 M1 tests for <see cref="ProjectWorkflowService"/> admission control,
/// cancellation, generation safety, context-change cancel, and dispose kill.
/// </summary>
public sealed class ProjectWorkflowServiceTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase11-workflow-" + Guid.NewGuid().ToString("N"));

    static ProjectWorkflowServiceTests()
    {
        Directory.CreateDirectory(TempRoot);
    }

    private sealed class FakeProjectContextService : IProjectContextService
    {
        private readonly Subject<ProjectContext> _subject = new();
        private ProjectContext _current = Unloaded();

        public ProjectContext Current => _current;

        public IObservable<ProjectContext> WhenChanged => _subject;

        public void Emit(ProjectContext context)
        {
            _current = context;
            _subject.OnNext(context);
        }

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
            _subject.OnCompleted();
            _subject.Dispose();
        }

        private static ProjectContext Unloaded() => new(
            ProjectContextState.Unloaded,
            WorkspaceRoot: null,
            Candidates: Array.Empty<ProjectCandidate>(),
            SelectedProject: null,
            UnsupportedFiles: Array.Empty<string>(),
            ErrorMessage: null);
    }

    private sealed class FakeManagedProcessRunner : IManagedProcessRunner
    {
        public TaskCompletionSource<bool>? RunGate { get; set; }
        public bool KillCalled { get; private set; }
        public bool Disposed { get; private set; }
        public ManagedProcessStartRequest? LastRequest { get; private set; }
        public int? SimulatedExitCode { get; set; } = 0;
        public bool SimulateStartupFailed { get; set; }
        public bool SimulateCancelOnWait { get; set; }

        public bool IsRunning { get; private set; }
        public int? ProcessId => IsRunning ? 9001 : null;

        public event Action<ManagedProcessOutputLine>? OutputReceived;

        public async Task<ManagedProcessRunResult> RunAsync(
            ManagedProcessStartRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            IsRunning = true;

            OutputReceived?.Invoke(
                new ManagedProcessOutputLine(
                    request.Generation,
                    ProcessStreamKind.StdOut,
                    "line-1",
                    DateTimeOffset.UtcNow));

            if (SimulateStartupFailed)
            {
                IsRunning = false;
                return new ManagedProcessRunResult(null, false, StartupFailed: true);
            }

            try
            {
                if (RunGate is not null)
                    await RunGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                IsRunning = false;
                return new ManagedProcessRunResult(null, WasCancelled: true, StartupFailed: false);
            }

            if (SimulateCancelOnWait || cancellationToken.IsCancellationRequested)
            {
                IsRunning = false;
                return new ManagedProcessRunResult(null, WasCancelled: true, StartupFailed: false);
            }

            IsRunning = false;
            return new ManagedProcessRunResult(SimulatedExitCode, false, StartupFailed: false);
        }

        public Task KillAsync()
        {
            KillCalled = true;
            IsRunning = false;
            RunGate?.TrySetResult(true);
            return Task.CompletedTask;
        }

        public void SimulateOutput(ManagedProcessOutputLine line) =>
            OutputReceived?.Invoke(line);

        public void Dispose()
        {
            Disposed = true;
            KillCalled = true;
            IsRunning = false;
            RunGate?.TrySetResult(true);
        }
    }

    private static ProjectCandidate MakeCandidate(string fileName, ProjectKind kind = ProjectKind.CSharpProject)
    {
        var path = Path.GetFullPath(Path.Combine(TempRoot, fileName));
        return new ProjectCandidate(path, Path.GetFileNameWithoutExtension(path), kind);
    }

    private static ProjectContext MakeContext(
        ProjectContextState state,
        ProjectCandidate? selected,
        ProjectCandidate[]? candidates = null)
    {
        var list = candidates ?? (selected is not null ? new[] { selected } : Array.Empty<ProjectCandidate>());
        return new ProjectContext(
            state,
            WorkspaceRoot: TempRoot,
            list,
            selected,
            UnsupportedFiles: Array.Empty<string>(),
            ErrorMessage: state == ProjectContextState.Failed ? "discovery failed" : null);
    }

    private static (ProjectWorkflowService Service, FakeProjectContextService Context, FakeManagedProcessRunner Runner)
        CreateHarness(ProjectContext? initial = null)
    {
        var context = new FakeProjectContextService();
        var runner = new FakeManagedProcessRunner();
        var service = new ProjectWorkflowService(
            context,
            runner,
            NullLogger<ProjectWorkflowService>.Instance);

        if (initial is not null)
            context.Emit(initial);

        return (service, context, runner);
    }

    [Fact]
    public async Task StartBuildAsync_EligibleContext_SucceedsAndCapturesOutput()
    {
        var candidate = MakeCandidate("Build.csproj");
        var (service, _, runner) = CreateHarness(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            var result = await service.StartBuildAsync();

            Assert.Equal(ProjectWorkflowOutcomeKind.Succeeded, result.Outcome);
            Assert.Equal(candidate.FilePath, result.TargetFilePath);
            Assert.Equal(0, result.ExitCode);
            Assert.Equal($"build \"{candidate.FilePath}\"", runner.LastRequest!.Arguments);
            Assert.Single(service.Current.OutputLines);
            Assert.Equal(ProjectWorkflowOperationState.Idle, service.Current.State);
            Assert.Equal(ProjectWorkflowOutcomeKind.Succeeded, service.Current.LastOutcome);
        }
    }

    [Fact]
    public async Task StartBuildAsync_IneligibleContext_ReturnsRejectedContext()
    {
        var (service, _, _) = CreateHarness(MakeContext(ProjectContextState.NoProject, null));
        using (service)
        {
            var result = await service.StartBuildAsync();

            Assert.Equal(ProjectWorkflowOutcomeKind.RejectedContext, result.Outcome);
            Assert.Equal(ProjectWorkflowOperationState.Idle, service.Current.State);
        }
    }

    [Fact]
    public async Task StartRunAsync_Solution_ReturnsRejectedContext()
    {
        var candidate = MakeCandidate("App.sln", ProjectKind.Solution);
        var (service, _, _) = CreateHarness(MakeContext(ProjectContextState.Selected, candidate, new[] { candidate }));
        using (service)
        {
            var result = await service.StartRunAsync();

            Assert.Equal(ProjectWorkflowOutcomeKind.RejectedContext, result.Outcome);
        }
    }

    [Fact]
    public async Task StartBuildAsync_WhileBusy_ReturnsRejectedConcurrent()
    {
        var candidate = MakeCandidate("Busy.csproj");
        var (service, _, runner) = CreateHarness(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            runner.RunGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var first = service.StartBuildAsync();
            await WaitUntilAsync(() => service.Current.State == ProjectWorkflowOperationState.Running);

            var second = await service.StartBuildAsync();

            Assert.Equal(ProjectWorkflowOutcomeKind.RejectedConcurrent, second.Outcome);

            runner.RunGate.TrySetResult(true);
            await first;
        }
    }

    [Fact]
    public async Task CancelAsync_ActiveOperation_ReturnsCancelled()
    {
        var candidate = MakeCandidate("Cancel.csproj");
        var (service, _, runner) = CreateHarness(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            runner.RunGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var buildTask = service.StartBuildAsync();
            await WaitUntilAsync(() => service.Current.State == ProjectWorkflowOperationState.Running);

            await service.CancelAsync();
            var result = await buildTask;

            Assert.Equal(ProjectWorkflowOutcomeKind.Cancelled, result.Outcome);
            Assert.True(runner.KillCalled || !runner.IsRunning);
            Assert.Equal(ProjectWorkflowOutcomeKind.Cancelled, service.Current.LastOutcome);
        }
    }

    [Fact]
    public async Task ContextChangeAwayFromTarget_CancelsActiveOperation()
    {
        var first = MakeCandidate("First.csproj");
        var second = MakeCandidate("Second.csproj");
        var (service, context, runner) = CreateHarness(MakeContext(ProjectContextState.SingleProject, first));
        using (service)
        {
            runner.RunGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var buildTask = service.StartBuildAsync();
            await WaitUntilAsync(() => service.Current.State == ProjectWorkflowOperationState.Running);

            context.Emit(MakeContext(ProjectContextState.Selected, second, new[] { first, second }));

            var result = await buildTask;

            Assert.Equal(ProjectWorkflowOutcomeKind.Cancelled, result.Outcome);
            Assert.Equal(ProjectWorkflowOutcomeKind.Cancelled, service.Current.LastOutcome);
        }
    }

    [Fact]
    public async Task StaleOutputFromOldGeneration_IsIgnoredBySnapshot()
    {
        var candidate = MakeCandidate("Generation.csproj");
        var (service, _, runner) = CreateHarness(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            runner.RunGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var first = service.StartBuildAsync();
            await WaitUntilAsync(() => service.Current.State == ProjectWorkflowOperationState.Running);
            var firstGeneration = service.Current.Generation;

            runner.SimulateOutput(
                new ManagedProcessOutputLine(
                    firstGeneration,
                    ProcessStreamKind.StdErr,
                    "stale-for-first",
                    DateTimeOffset.UtcNow));
            runner.SimulateOutput(
                new ManagedProcessOutputLine(
                    firstGeneration + 99,
                    ProcessStreamKind.StdErr,
                    "stale-ignored",
                    DateTimeOffset.UtcNow));

            runner.RunGate.TrySetResult(true);
            await first;

            Assert.Contains(
                service.Current.OutputLines,
                line => line.Text == "stale-for-first");
            Assert.DoesNotContain(
                service.Current.OutputLines,
                line => line.Text == "stale-ignored");
        }
    }

    [Fact]
    public async Task NonZeroExit_ReturnsFailed()
    {
        var candidate = MakeCandidate("Fail.csproj");
        var (service, _, runner) = CreateHarness(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            runner.SimulatedExitCode = 1;

            var result = await service.StartTestAsync();

            Assert.Equal(ProjectWorkflowOutcomeKind.Failed, result.Outcome);
            Assert.Equal(1, result.ExitCode);
        }
    }

    [Fact]
    public async Task StartupFailed_ReturnsStartupFailed()
    {
        var candidate = MakeCandidate("Startup.csproj");
        var (service, _, runner) = CreateHarness(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            runner.SimulateStartupFailed = true;

            var result = await service.StartBuildAsync();

            Assert.Equal(ProjectWorkflowOutcomeKind.StartupFailed, result.Outcome);
        }
    }

    [Fact]
    public async Task Dispose_KillsRunnerAndReturnsToIdle()
    {
        var candidate = MakeCandidate("Dispose.csproj");
        var (service, _, runner) = CreateHarness(MakeContext(ProjectContextState.SingleProject, candidate));
        runner.RunGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var buildTask = service.StartBuildAsync();
        await WaitUntilAsync(() => service.Current.State == ProjectWorkflowOperationState.Running);

        service.Dispose();
        var result = await buildTask;

        Assert.Equal(ProjectWorkflowOutcomeKind.Cancelled, result.Outcome);
        Assert.True(runner.Disposed);
        Assert.Equal(ProjectWorkflowOperationState.Idle, service.Current.State);
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

        throw new TimeoutException("Timed out waiting for workflow state.");
    }
}
