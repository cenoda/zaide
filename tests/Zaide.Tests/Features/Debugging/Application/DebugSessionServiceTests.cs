using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Zaide.Services;
using Zaide.Features.Debugging.Infrastructure.Dap;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Application;
using Zaide.Tests.Features.Debugging.Infrastructure.Dap;

namespace Zaide.Tests.Features.Debugging.Application;

/// <summary>
/// Phase 12 M1 tests for <see cref="DebugSessionService"/> lifecycle, ordering,
/// events, failures, generation safety, and disposal.
/// </summary>
public sealed class DebugSessionServiceTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase12-m1-debug-" + Guid.NewGuid().ToString("N"));

    static DebugSessionServiceTests()
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

    private sealed class FakeAdapterLocator : IDebugAdapterLocator
    {
        public string? Path { get; set; } = "/fake/netcoredbg";

        public string? Resolve() => Path;
    }

    private sealed class FakeAdapterSessionFactory : IDebugAdapterSessionFactory
    {
        public List<DebugAdapterStartOptions> Starts { get; } = new();
        public List<TestDebugAdapterSession> CreatedSessions { get; } = new();
        public TaskCompletionSource<bool>? StartGate { get; set; }
        public Exception? StartException { get; set; }
        public bool SuppressStoppedEvent { get; set; }
        public TimeSpan? InitializeDelay { get; set; }
        public Exception? LaunchException { get; set; }
        public Exception? SetBreakpointsException { get; set; }
        public Exception? ContinueException { get; set; }
        public Exception? StepException { get; set; }
        public TimeSpan? ContinueDelay { get; set; }
        public string? SetBreakpointsBodyJson { get; set; }
        public Action<TestDebugAdapterSession>? ConfigureSession { get; set; }

        public async Task<IDebugAdapterSession> StartAsync(
            DebugAdapterStartOptions options,
            CancellationToken cancellationToken)
        {
            Starts.Add(options);

            if (StartGate is not null)
                await StartGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            if (StartException is not null)
                throw StartException;

            var session = new TestDebugAdapterSession
            {
                Generation = options.Generation,
                EmitStoppedAfterConfigurationDone = !SuppressStoppedEvent,
                InitializeDelay = InitializeDelay,
                LaunchException = LaunchException,
                SetBreakpointsException = SetBreakpointsException,
                ContinueException = ContinueException,
                StepException = StepException,
                ContinueDelay = ContinueDelay,
                SetBreakpointsBodyJson = SetBreakpointsBodyJson,
            };
            ConfigureSession?.Invoke(session);
            CreatedSessions.Add(session);
            await session.ConnectAsync(cancellationToken).ConfigureAwait(false);
            return session;
        }
    }

    private sealed class SnapshotCollector : IDisposable
    {
        private readonly IDisposable _subscription;
        public List<DebugSessionSnapshot> Snapshots { get; } = new();

        public SnapshotCollector(IDebugSessionService service)
        {
            _subscription = service.WhenChanged.Subscribe(s => Snapshots.Add(s));
        }

        public void Dispose() => _subscription.Dispose();
    }

    private static ProjectCandidate MakeCandidate(string fileName, ProjectKind kind = ProjectKind.CSharpProject)
    {
        var path = Path.GetFullPath(Path.Combine(TempRoot, fileName));
        return new ProjectCandidate(path, Path.GetFileNameWithoutExtension(path), kind);
    }

    private static ProjectContext MakeContext(
        ProjectContextState state,
        ProjectCandidate? selected,
        IReadOnlyList<ProjectCandidate>? candidates = null)
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

    private static DebugLaunchRequest MakeLaunchRequest(
        string dllName = "App.dll",
        string sourceName = "Program.cs",
        int breakpointLine = 1)
    {
        var projectDir = Path.Combine(TempRoot, "fixture");
        Directory.CreateDirectory(projectDir);
        var program = Path.GetFullPath(Path.Combine(projectDir, dllName));
        var source = Path.GetFullPath(Path.Combine(projectDir, sourceName));
        return new DebugLaunchRequest(
            program,
            projectDir,
            StopAtEntry: true,
            new[] { new DebugBreakpointRequest(source, breakpointLine) });
    }

    private static (DebugSessionService Service, FakeProjectContextService Context, FakeAdapterSessionFactory Factory, FakeAdapterLocator Locator)
        CreateHarness(ProjectContext? initial = null)
    {
        var context = new FakeProjectContextService();
        var factory = new FakeAdapterSessionFactory();
        var locator = new FakeAdapterLocator();

        if (initial is not null)
            context.Emit(initial);

        var service = new DebugSessionService(
            context,
            locator,
            factory,
            new DebugSessionTimeoutPolicy(
                initialize: TimeSpan.FromMilliseconds(300),
                launchConfiguration: TimeSpan.FromMilliseconds(300),
                ordinaryRequest: TimeSpan.FromMilliseconds(100),
                disconnect: TimeSpan.FromMilliseconds(50)),
            NullLogger<DebugSessionService>.Instance);

        return (service, context, factory, locator);
    }

    private static async Task<DebugSessionSnapshot> WaitForAsync(
        IDebugSessionService service,
        Func<DebugSessionSnapshot, bool> predicate,
        TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        var deadline = DateTime.UtcNow + timeout.Value;

        while (DateTime.UtcNow < deadline)
        {
            var current = service.Current;
            if (predicate(current))
                return current;

            await Task.Delay(20).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Timed out waiting for debug session state. Last={service.Current.State}, gen={service.Current.Generation}");
    }

    [Fact]
    public async Task StartLaunchAsync_RunsDapOrdering_InitializeLaunchBreakpointsConfigurationDone()
    {
        var candidate = MakeCandidate("Ordering.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using var collector = new SnapshotCollector(service);
        using (service)
        {
            var request = MakeLaunchRequest();
            var result = await service.StartLaunchAsync(request);

            Assert.True(result.Succeeded);
            Assert.Equal(DebugSessionState.Stopped, service.Current.State);
            Assert.Single(factory.CreatedSessions);

            var session = factory.CreatedSessions[0];
            Assert.Equal(
                new[]
                {
                    "connect",
                    "initialize",
                    "launch",
                    $"setBreakpoints:{request.Breakpoints[0].SourcePath}:1",
                    "configurationDone",
                },
                session.CallOrder);
        }
    }

    [Fact]
    public async Task StartLaunchAsync_PublishesStoppedState_WithThreadInfo()
    {
        var candidate = MakeCandidate("Stopped.csproj");
        var (service, context, _, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            var result = await service.StartLaunchAsync(MakeLaunchRequest());

            Assert.True(result.Succeeded);
            Assert.Equal(DebugSessionState.Stopped, service.Current.State);
            Assert.NotNull(service.Current.StopInfo);
            Assert.Equal("entry", service.Current.StopInfo!.Reason);
            Assert.Equal(1, service.Current.StopInfo.ThreadId);
        }
    }

    [Fact]
    public async Task ContinueAsync_FromStopped_TransitionsToRunning()
    {
        var candidate = MakeCandidate("Continue.csproj");
        var (service, context, _, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());
            var result = await service.ContinueAsync(1);

            Assert.True(result.Succeeded);
            var running = await WaitForAsync(service, s => s.State == DebugSessionState.Running);
            Assert.Equal(DebugSessionState.Running, running.State);
            Assert.Null(running.StopInfo);
        }
    }

    [Fact]
    public async Task ContinueAsync_ReturnsRunning_BeforeDeferredContinuedEvent()
    {
        var candidate = MakeCandidate("ContinueGap.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());
            var session = factory.CreatedSessions[0];
            session.DeferExecutionEvents = true;
            session.ExecutionEventDelay = TimeSpan.FromMilliseconds(100);

            var result = await service.ContinueAsync(1);

            Assert.True(result.Succeeded);
            Assert.Equal(DebugSessionState.Running, service.Current.State);
            Assert.Null(service.Current.StopInfo);
        }
    }

    [Fact]
    public async Task PauseAsync_FromRunning_TransitionsToStopped()
    {
        var candidate = MakeCandidate("Pause.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());
            await service.ContinueAsync(1);
            await WaitForAsync(service, s => s.State == DebugSessionState.Running);

            var pause = await service.PauseAsync();
            Assert.True(pause.Succeeded);
            Assert.Equal("pause", factory.CreatedSessions[0].CallOrder[^1]);

            var stopped = await WaitForAsync(service, s => s.State == DebugSessionState.Stopped);
            Assert.Equal(DebugSessionState.Stopped, stopped.State);
        }
    }

    [Fact]
    public async Task StepOverAsync_FromStopped_TransitionsToRunningBeforeNextStop()
    {
        var candidate = MakeCandidate("Step.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());

            var step = await service.StepOverAsync();
            Assert.True(step.Succeeded);
            Assert.Contains("next:1", factory.CreatedSessions[0].CallOrder);
            Assert.Equal(DebugSessionState.Running, service.Current.State);

            var stopped = await WaitForAsync(service, s => s.State == DebugSessionState.Stopped);
            Assert.Equal(DebugSessionState.Stopped, stopped.State);
        }
    }

    [Fact]
    public async Task StopAsync_FromRunning_RecordsStoppedByUserOutcome()
    {
        var candidate = MakeCandidate("StopRunning.csproj");
        var (service, context, _, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());
            await service.ContinueAsync(1);
            await WaitForAsync(service, s => s.State == DebugSessionState.Running);

            var stop = await service.StopAsync();
            Assert.True(stop.Succeeded);
            Assert.Equal(DebugSessionState.Idle, service.Current.State);
            Assert.Equal(DebugSessionOutcomeKind.StoppedByUser, service.Current.LastOutcome);
        }
    }

    [Fact]
    public async Task StopAsync_FromStopped_RecordsStoppedByUserOutcome()
    {
        var candidate = MakeCandidate("StopStopped.csproj");
        var (service, context, _, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());

            var stop = await service.StopAsync();
            Assert.True(stop.Succeeded);
            Assert.Equal(DebugSessionState.Idle, service.Current.State);
            Assert.Equal(DebugSessionOutcomeKind.StoppedByUser, service.Current.LastOutcome);
        }
    }

    [Fact]
    public async Task StepOverAsync_WhenRunning_RejectsWithoutIssuingNext()
    {
        var candidate = MakeCandidate("StepGuard.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());
            await service.ContinueAsync(1);
            await WaitForAsync(service, s => s.State == DebugSessionState.Running);

            var step = await service.StepOverAsync();
            Assert.False(step.Succeeded);
            Assert.DoesNotContain("next:", factory.CreatedSessions[0].CallOrder);
        }
    }

    [Fact]
    public async Task StoppedState_AllowsThreadsStackScopesRequests()
    {
        var candidate = MakeCandidate("Inspect.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());

            var threads = await service.RequestThreadsAsync();
            var stack = await service.RequestStackTraceAsync(1);
            var scopes = await service.RequestScopesAsync(10);

            Assert.NotNull(threads);
            Assert.NotNull(stack);
            Assert.NotNull(scopes);
            Assert.Contains("threads", factory.CreatedSessions[0].CallOrder);
            Assert.Contains("stackTrace:1", factory.CreatedSessions[0].CallOrder);
            Assert.Contains("scopes:10", factory.CreatedSessions[0].CallOrder);
        }
    }

    [Fact]
    public async Task OutputEvent_AppendsDiagnosticOutput()
    {
        var candidate = MakeCandidate("Output.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());
            factory.CreatedSessions[0].SimulateOutput("hello debug");

            var snapshot = await WaitForAsync(
                service,
                s => s.DiagnosticOutput.Any(line => line.Contains("hello debug")));

            Assert.Contains(snapshot.DiagnosticOutput, line => line.Contains("hello debug"));
        }
    }

    [Fact]
    public async Task AdapterProcessExit_PublishesFailed_AndDisconnectsSession()
    {
        var candidate = MakeCandidate("Exit.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());
            factory.CreatedSessions[0].SimulateProcessExit();

            var failed = await WaitForAsync(service, s => s.State == DebugSessionState.Failed);
            Assert.Equal(DebugSessionOutcomeKind.AdapterExited, failed.Failure!.Kind);
            Assert.True(factory.CreatedSessions[0].DisconnectCalled || factory.CreatedSessions[0].ForceKillCalled);
        }
    }

    [Fact]
    public async Task TerminatedEvent_PublishesAdapterExitedFailure()
    {
        var candidate = MakeCandidate("Terminated.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());
            factory.CreatedSessions[0].SimulateTerminated();

            var failed = await WaitForAsync(service, s => s.State == DebugSessionState.Failed);
            Assert.Equal(DebugSessionOutcomeKind.AdapterExited, failed.Failure!.Kind);
        }
    }

    [Fact]
    public async Task StartLaunchAsync_WhenAdapterMissing_ReturnsAdapterUnavailable()
    {
        var candidate = MakeCandidate("MissingAdapter.csproj");
        var (service, context, factory, locator) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        locator.Path = null;
        using (service)
        {
            var result = await service.StartLaunchAsync(MakeLaunchRequest());

            Assert.False(result.Succeeded);
            Assert.Equal(DebugSessionOutcomeKind.AdapterUnavailable, result.Outcome);
            Assert.Equal(DebugSessionState.Failed, service.Current.State);
            Assert.Empty(factory.Starts);
        }
    }

    [Fact]
    public async Task StartLaunchAsync_WhenAlreadyActive_ReturnsRejectedConcurrent()
    {
        var candidate = MakeCandidate("Concurrent.csproj");
        var (service, context, _, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());
            var second = await service.StartLaunchAsync(MakeLaunchRequest());

            Assert.False(second.Succeeded);
            Assert.Equal(DebugSessionOutcomeKind.RejectedConcurrent, second.Outcome);
        }
    }

    [Fact]
    public async Task StartLaunchAsync_WhenContextIneligible_ReturnsRejectedContext()
    {
        var candidate = MakeCandidate("Solution.sln", ProjectKind.Solution);
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.Selected, candidate));
        using (service)
        {
            var result = await service.StartLaunchAsync(MakeLaunchRequest());

            Assert.False(result.Succeeded);
            Assert.Equal(DebugSessionOutcomeKind.RejectedContext, result.Outcome);
            Assert.Empty(factory.Starts);
        }
    }

    [Fact]
    public async Task StartLaunchAsync_WhenStoppedNeverArrives_ReturnsStartupFailed()
    {
        var candidate = MakeCandidate("Timeout.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        factory.SuppressStoppedEvent = true;
        using (service)
        {
            var result = await service.StartLaunchAsync(MakeLaunchRequest());

            Assert.False(result.Succeeded);
            Assert.Equal(DebugSessionOutcomeKind.StartupFailed, result.Outcome);
            Assert.Equal(DebugSessionState.Failed, service.Current.State);
        }
    }

    [Fact]
    public async Task StartLaunchAsync_WhenInitializeTimesOut_ReturnsStartupFailed()
    {
        var candidate = MakeCandidate("InitializeTimeout.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        factory.InitializeDelay = TimeSpan.FromMilliseconds(500); // exceeds the 300ms fake timeout
        using (service)
        {
            var result = await service.StartLaunchAsync(MakeLaunchRequest());

            Assert.False(result.Succeeded);
            Assert.Equal(DebugSessionOutcomeKind.StartupFailed, result.Outcome);
        }
    }

    [Fact]
    public async Task ContextChangeWhileActive_TearsDownSession_AndBumpsGeneration()
    {
        var first = MakeCandidate("First.csproj");
        var second = MakeCandidate("Second.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, first));
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());
            var oldGeneration = service.Current.Generation;
            var oldSession = factory.CreatedSessions[0];

            context.Emit(MakeContext(ProjectContextState.SingleProject, second));

            var idle = await WaitForAsync(service, s => s.State == DebugSessionState.Idle);
            Assert.True(service.Current.Generation > oldGeneration);
            Assert.Equal(DebugSessionState.Idle, idle.State);
            Assert.True(oldSession.DisconnectCalled || oldSession.ForceKillCalled);
        }
    }

    [Fact]
    public async Task StopAsync_IsIdempotent_AndDisconnectsAdapter()
    {
        var candidate = MakeCandidate("Stop.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());
            var session = factory.CreatedSessions[0];

            var first = await service.StopAsync();
            var second = await service.StopAsync();

            Assert.True(first.Succeeded);
            Assert.True(second.Succeeded);
            Assert.Equal(DebugSessionState.Idle, service.Current.State);
            Assert.True(session.DisconnectCalled);
            Assert.True(session.Disposed);
        }
    }

    [Fact]
    public async Task Dispose_DisconnectsActiveSession()
    {
        var candidate = MakeCandidate("Dispose.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        await service.StartLaunchAsync(MakeLaunchRequest());
        var session = factory.CreatedSessions[0];

        service.Dispose();

        Assert.True(session.DisconnectCalled || session.ForceKillCalled);
        Assert.True(session.Disposed);
    }

    [Fact]
    public async Task LateStoppedEvent_FromOldGeneration_IsIgnored()
    {
        var candidate = MakeCandidate("Generation.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());
            var generation = service.Current.Generation;
            var session = factory.CreatedSessions[0];

            context.Emit(MakeContext(ProjectContextState.NoProject, null));
            await WaitForAsync(service, s => s.State == DebugSessionState.Unavailable);

            session.SimulateStopped("breakpoint", 1);
            await Task.Delay(100);

            Assert.NotEqual(DebugSessionState.Stopped, service.Current.State);
        }
    }

    [Theory]
    [InlineData(ProjectContextState.Unloaded)]
    [InlineData(ProjectContextState.NoProject)]
    public void IneligibleContext_PublishesUnavailable(ProjectContextState state)
    {
        var (service, context, _, _) = CreateHarness();
        using (service)
        {
            context.Emit(MakeContext(state, null));
            Assert.Equal(DebugSessionState.Unavailable, service.Current.State);
        }
    }

    [Fact]
    public async Task ReplaceBreakpointsBySourceAsync_WhenStopped_SendsPerSourceReplacement()
    {
        var candidate = MakeCandidate("Breakpoints.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());
            var session = factory.CreatedSessions[0];
            var source = Path.GetFullPath(Path.Combine(TempRoot, "Program.cs"));

            var result = await service.ReplaceBreakpointsBySourceAsync(
                new Dictionary<string, IReadOnlyList<int>>
                {
                    [source] = new[] { 7 },
                });

            Assert.True(result.Succeeded);
            Assert.Contains($"setBreakpoints:{source}:7", session.CallOrder);
        }
    }

    [Fact]
    public async Task ReplaceBreakpointsBySourceAsync_EmptyEnabledSet_SendsEmptyLineList()
    {
        var candidate = MakeCandidate("EmptyBreakpoints.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());
            var session = factory.CreatedSessions[0];
            var source = Path.GetFullPath(Path.Combine(TempRoot, "Removed.cs"));

            var result = await service.ReplaceBreakpointsBySourceAsync(
                new Dictionary<string, IReadOnlyList<int>>
                {
                    [source] = Array.Empty<int>(),
                });

            Assert.True(result.Succeeded);
            Assert.Contains($"setBreakpoints:{source}:", session.CallOrder);
        }
    }

    [Fact]
    public async Task ReplaceBreakpointsBySourceAsync_WhenIdle_NoOps()
    {
        var (service, _, factory, _) = CreateHarness();
        using (service)
        {
            var source = Path.GetFullPath(Path.Combine(TempRoot, "Idle.cs"));
            var result = await service.ReplaceBreakpointsBySourceAsync(
                new Dictionary<string, IReadOnlyList<int>>
                {
                    [source] = new[] { 1 },
                });

            Assert.True(result.Succeeded);
            Assert.Empty(factory.CreatedSessions);
        }
    }

    [Fact]
    public async Task StartLaunchAsync_LaunchException_PublishesStartupFailedAndCleansSession()
    {
        var candidate = MakeCandidate("LaunchFail.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        factory.LaunchException = new InvalidOperationException("launch rejected");
        using (service)
        {
            var result = await service.StartLaunchAsync(MakeLaunchRequest());

            Assert.False(result.Succeeded);
            Assert.Equal(DebugSessionOutcomeKind.StartupFailed, result.Outcome);
            Assert.Equal(DebugSessionState.Failed, service.Current.State);
            Assert.Null(service.Current.AdapterProcessId);
            Assert.Null(service.Current.StopInfo);
            Assert.Empty(service.Current.BreakpointVerifications);
            Assert.True(factory.CreatedSessions[0].ForceKillCalled || factory.CreatedSessions[0].Disposed);
            Assert.Contains(service.Current.DiagnosticOutput, line => line.Contains("[error]"));
        }
    }

    [Fact]
    public async Task ContinueAsync_RequestTimeout_FailsSessionAndAllowsRestart()
    {
        var candidate = MakeCandidate("ContinueTimeout.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        factory.ContinueDelay = TimeSpan.FromMilliseconds(500);
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());
            var failedGeneration = service.Current.Generation;

            var result = await service.ContinueAsync(1);

            Assert.False(result.Succeeded);
            Assert.Equal(DebugSessionOutcomeKind.ProtocolFailed, result.Outcome);
            Assert.Equal(DebugSessionState.Failed, service.Current.State);
            Assert.True(service.Current.Generation > failedGeneration);
            Assert.Null(service.Current.StopInfo);
            Assert.Empty(service.Current.BreakpointVerifications);

            factory.ContinueDelay = null;
            var restart = await service.StartLaunchAsync(MakeLaunchRequest());
            Assert.True(restart.Succeeded);
            Assert.Equal(DebugSessionState.Stopped, service.Current.State);
        }
    }

    [Fact]
    public async Task StepOverAsync_ProtocolError_FailsSession_RetainsDiagnostics()
    {
        var candidate = MakeCandidate("StepFail.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        factory.StepException = new InvalidOperationException("step failed");
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());
            factory.CreatedSessions[0].SimulateOutput("pre-failure output");

            var result = await service.StepOverAsync();

            Assert.False(result.Succeeded);
            Assert.Equal(DebugSessionOutcomeKind.ProtocolFailed, result.Outcome);
            Assert.Equal(DebugSessionState.Failed, service.Current.State);
            Assert.Contains(service.Current.DiagnosticOutput, line => line.Contains("pre-failure output"));
            Assert.Contains(service.Current.DiagnosticOutput, line => line.Contains("[error]"));
        }
    }

    [Fact]
    public async Task SetBreakpoints_ProjectsVerifiedPendingRejectedOutcomes()
    {
        var candidate = MakeCandidate("BpVerify.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        var source = Path.GetFullPath(Path.Combine(TempRoot, "fixture", "Program.cs"));
        factory.SetBreakpointsBodyJson =
            "{\"breakpoints\":[" +
            "{\"line\":1,\"verified\":true}," +
            "{\"line\":2,\"verified\":false}," +
            "{\"line\":3,\"verified\":false,\"message\":\"No code on line\"}" +
            "]}";
        using (service)
        {
            var request = new DebugLaunchRequest(
                Path.Combine(TempRoot, "fixture", "App.dll"),
                Path.Combine(TempRoot, "fixture"),
                StopAtEntry: true,
                new[]
                {
                    new DebugBreakpointRequest(source, 1),
                    new DebugBreakpointRequest(source, 2),
                    new DebugBreakpointRequest(source, 3),
                });

            var result = await service.StartLaunchAsync(request);
            Assert.True(result.Succeeded);

            var verifications = service.Current.BreakpointVerifications;
            Assert.Equal(3, verifications.Count);
            Assert.Equal(DebugBreakpointVerificationState.Verified, verifications[0].State);
            Assert.Equal(DebugBreakpointVerificationState.Pending, verifications[1].State);
            Assert.Equal(DebugBreakpointVerificationState.Rejected, verifications[2].State);
            Assert.Equal("No code on line", verifications[2].Message);
            Assert.Contains(
                service.Current.DiagnosticOutput,
                line => line.Contains("Breakpoint rejected") && line.Contains("No code on line"));
        }
    }

    [Fact]
    public async Task StopDuringStartup_AbandonsAdapter_AllowsRestart()
    {
        var candidate = MakeCandidate("StopDuringStart.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        factory.StartGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (service)
        {
            var startTask = service.StartLaunchAsync(MakeLaunchRequest());
            await WaitForAsync(service, s => s.State == DebugSessionState.Starting);

            var stop = await service.StopAsync();
            Assert.True(stop.Succeeded);
            Assert.Equal(DebugSessionState.Idle, service.Current.State);

            factory.StartGate.TrySetResult(true);
            try
            {
                await startTask;
            }
            catch (OperationCanceledException)
            {
                // Cancelled start is acceptable recovery.
            }

            // Late failure from the abandoned start must not leave a stuck active state.
            await Task.Delay(50);
            Assert.True(
                service.Current.State is DebugSessionState.Idle or DebugSessionState.Failed,
                $"Unexpected state after stop-during-start: {service.Current.State}");

            factory.StartGate = null;
            var restart = await service.StartLaunchAsync(MakeLaunchRequest());
            Assert.True(restart.Succeeded, restart.Message);
            Assert.Equal(DebugSessionState.Stopped, service.Current.State);
        }
    }

    [Fact]
    public async Task StartAfterFailure_SucceedsWithNewGeneration()
    {
        var candidate = MakeCandidate("StartAfterFail.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());
            factory.CreatedSessions[0].SimulateProcessExit();
            await WaitForAsync(service, s => s.State == DebugSessionState.Failed);
            var failedGeneration = service.Current.Generation;

            var restart = await service.StartLaunchAsync(MakeLaunchRequest());
            Assert.True(restart.Succeeded);
            Assert.Equal(DebugSessionState.Stopped, service.Current.State);
            Assert.True(service.Current.Generation > failedGeneration);
            Assert.Equal(2, factory.CreatedSessions.Count);
            Assert.True(factory.CreatedSessions[0].Disposed || factory.CreatedSessions[0].ForceKillCalled);
        }
    }

    [Fact]
    public async Task LateEventAfterRecovery_DoesNotMutateNewSession()
    {
        var candidate = MakeCandidate("StaleGen.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());
            var oldSession = factory.CreatedSessions[0];
            oldSession.SimulateProcessExit();
            await WaitForAsync(service, s => s.State == DebugSessionState.Failed);

            await service.StartLaunchAsync(MakeLaunchRequest());
            var newGeneration = service.Current.Generation;
            Assert.Equal(DebugSessionState.Stopped, service.Current.State);

            oldSession.SimulateStopped("breakpoint", 99);
            oldSession.SimulateOutput("stale output");
            await Task.Delay(80);

            Assert.Equal(DebugSessionState.Stopped, service.Current.State);
            Assert.Equal(newGeneration, service.Current.Generation);
            Assert.DoesNotContain(
                service.Current.DiagnosticOutput,
                line => line.Contains("stale output"));
            Assert.NotEqual(99, service.Current.StopInfo?.ThreadId);
        }
    }

    [Fact]
    public async Task AdapterExited_ClearsLiveData_RetainsDiagnostics_AllowsStart()
    {
        var candidate = MakeCandidate("CrashRecover.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            await service.StartLaunchAsync(MakeLaunchRequest());
            factory.CreatedSessions[0].SimulateOutput("adapter noise");
            factory.CreatedSessions[0].SimulateProcessExit();

            var failed = await WaitForAsync(service, s => s.State == DebugSessionState.Failed);
            Assert.Equal(DebugSessionOutcomeKind.AdapterExited, failed.Failure!.Kind);
            Assert.Null(failed.StopInfo);
            Assert.Null(failed.AdapterProcessId);
            Assert.Empty(failed.BreakpointVerifications);
            Assert.Contains(failed.DiagnosticOutput, line => line.Contains("adapter noise"));
            Assert.Contains(failed.DiagnosticOutput, line => line.Contains("[error]"));
        }
    }

    [Fact]
    public async Task ReportPreLaunchFailure_PublishesFailedAndAllowsStart()
    {
        var candidate = MakeCandidate("PreLaunch.csproj");
        var (service, context, factory, _) = CreateHarness();
        context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            var report = await service.ReportPreLaunchFailureAsync(
                DebugSessionOutcomeKind.BuildFailed,
                "Build failed.");

            Assert.False(report.Succeeded);
            Assert.Equal(DebugSessionState.Failed, service.Current.State);
            Assert.Equal(DebugSessionOutcomeKind.BuildFailed, service.Current.Failure!.Kind);
            Assert.Contains(service.Current.DiagnosticOutput, line => line.Contains("Build failed."));

            var start = await service.StartLaunchAsync(MakeLaunchRequest());
            Assert.True(start.Succeeded);
            Assert.Single(factory.CreatedSessions);
        }
    }
}
