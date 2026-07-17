using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Infrastructure;
using Zaide.Features.Language.Contracts;
using Zaide.Features.Debugging.Contracts;

namespace Zaide.Tests.Features.ProjectSystem.DI;

/// <summary>
/// Phase 11 F10 tests for explicit projection-service disposal on app exit.
/// </summary>
public sealed class ProjectWorkflowProjectionShutdownTests
{
    static ProjectWorkflowProjectionShutdownTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        Program.ConfigureServices(services);
        services.AddSingleton<IScheduler>(_ => CurrentThreadScheduler.Instance);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void DisposeServicesOnExit_DisposesAllThreeProjectionServices()
    {
        var order = new List<string>();
        var provider = BuildRecordingProvider(order);

        App.DisposeServicesOnExit(provider);

        Assert.Contains("workflow", order);
        Assert.Contains("output", order);
        Assert.Contains("buildDiagnostics", order);
        Assert.Contains("testResults", order);
    }

    [Fact]
    public void DisposeServicesOnExit_KillsRunnerBeforeLanguageDispose()
    {
        var order = new List<string>();
        var runner = new RecordingManagedProcessRunner(order);
        var provider = BuildRecordingProvider(order, runner);

        App.DisposeServicesOnExit(provider);

        var debugIndex = order.IndexOf("debugSession");
        var workflowIndex = order.IndexOf("workflow");
        var languageIndex = order.IndexOf("languageSession");
        Assert.True(debugIndex >= 0);
        Assert.True(workflowIndex >= 0);
        Assert.True(languageIndex >= 0);
        Assert.True(runner.KillCalled);
        Assert.True(debugIndex < workflowIndex);
        Assert.True(workflowIndex < languageIndex);
    }

    [Fact]
    public void DisposeServicesOnExit_OrdersWorkflowBeforeProjectionsBeforeLanguage()
    {
        var order = new List<string>();
        var provider = BuildRecordingProvider(order);

        App.DisposeServicesOnExit(provider);

        static int IndexOf(List<string> items, string name)
        {
            var index = items.IndexOf(name);
            Assert.True(index >= 0, $"Expected dispose marker '{name}' was not recorded.");
            return index;
        }

        var debugSession = IndexOf(order, "debugSession");
        var debugPanel = IndexOf(order, "debugPanel");
        var debugCurrentLocation = IndexOf(order, "debugCurrentLocation");
        var editorBreakpoint = IndexOf(order, "editorBreakpoint");
        var debugSessionViewModel = IndexOf(order, "debugSessionViewModel");
        var workflow = IndexOf(order, "workflow");
        var output = IndexOf(order, "output");
        var buildDiagnostics = IndexOf(order, "buildDiagnostics");
        var testResults = IndexOf(order, "testResults");
        var languageSession = IndexOf(order, "languageSession");

        Assert.True(debugSession < debugPanel);
        Assert.True(debugPanel < debugCurrentLocation);
        Assert.True(debugCurrentLocation < editorBreakpoint);
        Assert.True(editorBreakpoint < debugSessionViewModel);
        Assert.True(debugSessionViewModel < workflow);
        Assert.True(workflow < output);
        Assert.True(output < buildDiagnostics);
        Assert.True(buildDiagnostics < testResults);
        Assert.True(testResults < languageSession);
    }

    [Fact]
    public void ProductionContainer_ProjectionSubjectsCompleteOnExitDispose()
    {
        using var provider = BuildProvider();

        var buildCompleted = false;
        var testCompleted = false;
        using var buildSub = provider.GetRequiredService<IBuildDiagnosticsService>()
            .WhenChanged
            .Subscribe(_ => { }, () => buildCompleted = true);
        using var testSub = provider.GetRequiredService<ITestResultsService>()
            .WhenChanged
            .Subscribe(_ => { }, () => testCompleted = true);

        App.DisposeServicesOnExit(provider);

        Assert.True(buildCompleted);
        Assert.True(testCompleted);
    }

    [Fact]
    public void ProjectOutputService_Dispose_ReleasesWorkflowSubscription()
    {
        var workflow = new EmittingWorkflowService();
        var output = new ProjectOutputService(workflow);

        workflow.Emit(RunningSnapshot());
        Assert.Equal(ProjectWorkflowOperationState.Running, output.Current.State);

        output.Dispose();

        workflow.Emit(IdleSnapshot());
        Assert.Equal(ProjectWorkflowOperationState.Running, output.Current.State);
    }

    [Fact]
    public void BuildDiagnosticsService_Dispose_CompletesSubjectAndReleasesSubscription()
    {
        var workflow = new EmittingWorkflowService();
        var service = new BuildDiagnosticsService(workflow);

        var completed = false;
        using var sub = service.WhenChanged.Subscribe(_ => { }, () => completed = true);

        workflow.Publish(StartingBuildSnapshot());
        workflow.Publish(BuildCompleteSnapshot());
        var snapshotBeforeDispose = service.Current;
        Assert.NotEmpty(snapshotBeforeDispose.Diagnostics);

        service.Dispose();

        Assert.True(completed);
        Assert.Equal(BuildDiagnosticsSnapshot.Empty, service.Current);

        workflow.Publish(StartingBuildSnapshot(generation: 2));
        workflow.Publish(BuildCompleteSnapshot(generation: 2));
        Assert.Equal(BuildDiagnosticsSnapshot.Empty, service.Current);
        Assert.Equal(0, service.Current.BuildGeneration);
    }

    [Fact]
    public void TestResultsService_Dispose_CompletesSubjectAndReleasesSubscription()
    {
        var workflow = new EmittingWorkflowService();
        var service = new TestResultsService(workflow);

        var completed = false;
        using var sub = service.WhenChanged.Subscribe(_ => { }, () => completed = true);

        workflow.Publish(StartingTestSnapshot());
        workflow.Publish(TestCompleteSnapshot());
        var snapshotBeforeDispose = service.Current;
        Assert.NotNull(snapshotBeforeDispose.Summary);

        service.Dispose();

        Assert.True(completed);
        Assert.Equal(TestResultsSnapshot.Empty, service.Current);

        workflow.Publish(StartingTestSnapshot(generation: 2));
        workflow.Publish(TestCompleteSnapshot(generation: 2));
        Assert.Equal(TestResultsSnapshot.Empty, service.Current);
        Assert.Equal(0, service.Current.Generation);
    }

    [Theory]
    [InlineData("output")]
    [InlineData("buildDiagnostics")]
    [InlineData("testResults")]
    public void ProjectionDispose_CanBeCalledTwice(string serviceKind)
    {
        var workflow = new EmittingWorkflowService();
        IDisposable service = serviceKind switch
        {
            "output" => new ProjectOutputService(workflow),
            "buildDiagnostics" => new BuildDiagnosticsService(workflow),
            "testResults" => new TestResultsService(workflow),
            _ => throw new ArgumentOutOfRangeException(nameof(serviceKind)),
        };

        service.Dispose();
        var exception = Record.Exception(() => service.Dispose());

        Assert.Null(exception);
    }

    [Fact]
    public void ConfigureServices_ResolvesOneSharedProjectOutputServiceSingleton()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<IProjectOutputService>();
        var second = provider.GetRequiredService<IProjectOutputService>();

        Assert.Same(first, second);
    }

    [Fact]
    public void ConfigureServices_WorkflowOwnsSingleManagedProcessRunner()
    {
        using var provider = BuildProvider();

        var runnerFromDi = provider.GetRequiredService<IManagedProcessRunner>();
        var workflow = (ProjectWorkflowService)provider.GetRequiredService<IProjectWorkflowService>();

        Assert.Same(runnerFromDi, provider.GetRequiredService<IManagedProcessRunner>());
        Assert.NotNull(runnerFromDi);
        Assert.NotNull(workflow);
    }

    private static RecordingServiceProvider BuildRecordingProvider(
        List<string> order,
        RecordingManagedProcessRunner? runner = null)
    {
        runner ??= new RecordingManagedProcessRunner(order);
        return new RecordingServiceProvider(order, runner);
    }

    private sealed class RecordingServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services;

        public RecordingServiceProvider(List<string> order, RecordingManagedProcessRunner runner)
        {
            _services = new Dictionary<Type, object>
            {
                [typeof(IDebugSessionService)] = CreateRecordingDisposable<IDebugSessionService>(order, "debugSession"),
                [typeof(DebugPanelViewModel)] = new RecordingDisposeMarker(order, "debugPanel"),
                [typeof(DebugCurrentLocationViewModel)] = new RecordingDisposeMarker(order, "debugCurrentLocation"),
                [typeof(EditorBreakpointViewModel)] = new RecordingDisposeMarker(order, "editorBreakpoint"),
                [typeof(DebugSessionViewModel)] = new RecordingDisposeMarker(order, "debugSessionViewModel"),
                [typeof(IProjectWorkflowService)] = new RecordingWorkflowService(order, runner),
                [typeof(IProjectOutputService)] = CreateRecordingDisposable<IProjectOutputService>(order, "output"),
                [typeof(IBuildDiagnosticsService)] = CreateRecordingDisposable<IBuildDiagnosticsService>(order, "buildDiagnostics"),
                [typeof(ITestResultsService)] = CreateRecordingDisposable<ITestResultsService>(order, "testResults"),
                [typeof(ILanguageFormattingService)] = CreateRecordingDisposable<ILanguageFormattingService>(order, "languageFormatting"),
                [typeof(ILanguageNavigationService)] = CreateRecordingDisposable<ILanguageNavigationService>(order, "languageNavigation"),
                [typeof(ILanguageSymbolService)] = CreateRecordingDisposable<ILanguageSymbolService>(order, "languageSymbol"),
                [typeof(ILanguageCompletionService)] = CreateRecordingDisposable<ILanguageCompletionService>(order, "languageCompletion"),
                [typeof(ILanguageHoverService)] = CreateRecordingDisposable<ILanguageHoverService>(order, "languageHover"),
                [typeof(ILanguageDiagnosticsService)] = CreateRecordingDisposable<ILanguageDiagnosticsService>(order, "languageDiagnostics"),
                [typeof(ILanguageDocumentBridge)] = CreateRecordingDisposable<ILanguageDocumentBridge>(order, "languageBridge"),
                [typeof(ILanguageSessionService)] = CreateRecordingSessionService(order),
                [typeof(IProjectContextService)] = CreateRecordingDisposable<IProjectContextService>(order, "projectContext"),
                [typeof(ITerminalHost)] = CreateRecordingDisposable<ITerminalHost>(order, "terminalHost"),
            };
        }

        public object? GetService(Type serviceType) =>
            _services.TryGetValue(serviceType, out var service) ? service : null;
    }

    private static T CreateRecordingDisposable<T>(List<string> order, string name)
        where T : class, IDisposable
    {
        var mock = new Mock<T>();
        mock.Setup(service => service.Dispose()).Callback(() => order.Add(name));
        return mock.Object;
    }

    private sealed class RecordingDisposeMarker : IDisposable
    {
        private readonly List<string> _order;
        private readonly string _name;

        public RecordingDisposeMarker(List<string> order, string name)
        {
            _order = order;
            _name = name;
        }

        public void Dispose() => _order.Add(_name);
    }

    private static ILanguageSessionService CreateRecordingSessionService(List<string> order)
    {
        var mock = new Mock<ILanguageSessionService>();
        mock.Setup(service => service.Dispose()).Callback(() => order.Add("languageSession"));
        mock.Setup(service => service.DisposeAsync()).Returns(ValueTask.CompletedTask);
        return mock.Object;
    }

    private sealed class RecordingManagedProcessRunner : IManagedProcessRunner
    {
        private readonly List<string> _order;

        public RecordingManagedProcessRunner(List<string> order) => _order = order;

        public bool KillCalled { get; private set; }
        public bool Disposed { get; private set; }
        public bool IsRunning => false;
        public int? ProcessId => null;

#pragma warning disable CS0067 // Events required by IManagedProcessRunner; never raised in this fake
        public event Action<ManagedProcessOutputLine>? OutputReceived;
        public event Action? ProcessStarted;
#pragma warning restore CS0067

        public Task<ManagedProcessRunResult> RunAsync(
            ManagedProcessStartRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ManagedProcessRunResult(0, false, StartupFailed: false));

        public Task KillAsync()
        {
            KillCalled = true;
            _order.Add("runner-kill");
            return Task.CompletedTask;
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class RecordingWorkflowService : IProjectWorkflowService
    {
        private readonly List<string> _order;
        private readonly RecordingManagedProcessRunner _runner;

        public RecordingWorkflowService(List<string> order, RecordingManagedProcessRunner runner)
        {
            _order = order;
            _runner = runner;
        }

        public ProjectWorkflowSnapshot Current { get; } = new(
            ProjectWorkflowOperationState.Idle,
            Generation: 0,
            ActiveOperation: null,
            LastOutcome: null,
            TargetFilePath: null,
            ProcessId: null,
            OutputLines: Array.Empty<ManagedProcessOutputLine>());

        public IObservable<ProjectWorkflowSnapshot> WhenChanged =>
            new Subject<ProjectWorkflowSnapshot>();

        public IObservable<ManagedProcessOutputLine> WhenOutputReceived =>
            new Subject<ManagedProcessOutputLine>();

        public Task<ProjectWorkflowOperationResult> StartBuildAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProjectWorkflowOperationResult> StartRunAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProjectWorkflowOperationResult> StartTestAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProjectWorkflowOperationResult> StartBuildForDebugHandoffAsync(
            IProjectOperationHandoffLease handoffLease,
            CancellationToken cancellationToken = default) =>
            StartBuildAsync(cancellationToken);

        public Task CancelAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Dispose()
        {
            _runner.KillAsync().GetAwaiter().GetResult();
            _runner.Dispose();
            _order.Add("workflow");
        }
    }

    private sealed class EmittingWorkflowService : IProjectWorkflowService
    {
        private readonly Subject<ProjectWorkflowSnapshot> _snapshotSubject = new();
        private readonly Subject<ManagedProcessOutputLine> _outputSubject = new();

        public ProjectWorkflowSnapshot Current { get; private set; } = IdleSnapshot();

        public IObservable<ProjectWorkflowSnapshot> WhenChanged => _snapshotSubject;

        public IObservable<ManagedProcessOutputLine> WhenOutputReceived => _outputSubject;

        public void Emit(ProjectWorkflowSnapshot snapshot)
        {
            Current = snapshot;
            _snapshotSubject.OnNext(snapshot);
        }

        public void Publish(ProjectWorkflowSnapshot snapshot) => Emit(snapshot);

        public Task<ProjectWorkflowOperationResult> StartBuildAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProjectWorkflowOperationResult> StartRunAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProjectWorkflowOperationResult> StartTestAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProjectWorkflowOperationResult> StartBuildForDebugHandoffAsync(
            IProjectOperationHandoffLease handoffLease,
            CancellationToken cancellationToken = default) =>
            StartBuildAsync(cancellationToken);

        public Task CancelAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Dispose()
        {
            _snapshotSubject.OnCompleted();
            _snapshotSubject.Dispose();
            _outputSubject.OnCompleted();
            _outputSubject.Dispose();
        }
    }

    private static ProjectWorkflowSnapshot IdleSnapshot(long generation = 0) => new(
        ProjectWorkflowOperationState.Idle,
        generation,
        ActiveOperation: null,
        LastOutcome: null,
        TargetFilePath: null,
        ProcessId: null,
        OutputLines: Array.Empty<ManagedProcessOutputLine>());

    private static ProjectWorkflowSnapshot RunningSnapshot(long generation = 1) => new(
        ProjectWorkflowOperationState.Running,
        generation,
        ProjectWorkflowOperation.Build,
        LastOutcome: null,
        TargetFilePath: "/tmp/app.csproj",
        ProcessId: 42,
        OutputLines: Array.Empty<ManagedProcessOutputLine>());

    private static ProjectWorkflowSnapshot StartingBuildSnapshot(long generation = 1)
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "app.csproj");
        return new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            generation,
            ProjectWorkflowOperation.Build,
            LastOutcome: null,
            TargetFilePath: projectPath,
            ProcessId: null,
            OutputLines: Array.Empty<ManagedProcessOutputLine>());
    }

    private static ProjectWorkflowSnapshot StartingTestSnapshot(long generation = 1)
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "app.csproj");
        return new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            generation,
            ProjectWorkflowOperation.Test,
            LastOutcome: null,
            TargetFilePath: projectPath,
            ProcessId: null,
            OutputLines: Array.Empty<ManagedProcessOutputLine>());
    }

    private static ProjectWorkflowSnapshot BuildCompleteSnapshot(long generation = 1)
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "app.csproj");
        var sourcePath = Path.Combine(Path.GetTempPath(), "Program.cs");
        return new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Idle,
            generation,
            ActiveOperation: null,
            LastOutcome: ProjectWorkflowOutcomeKind.Failed,
            TargetFilePath: projectPath,
            ProcessId: null,
            OutputLines: new[]
            {
                new ManagedProcessOutputLine(
                    generation,
                    ProcessStreamKind.StdErr,
                    $"{sourcePath}(1,2): error CS1002: ; expected",
                    DateTimeOffset.UtcNow),
            });
    }

    private static ProjectWorkflowSnapshot TestCompleteSnapshot(long generation = 1)
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "app.csproj");
        return new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Idle,
            generation,
            ActiveOperation: null,
            LastOutcome: ProjectWorkflowOutcomeKind.Failed,
            TargetFilePath: projectPath,
            ProcessId: null,
            OutputLines: new[]
            {
                new ManagedProcessOutputLine(
                    generation,
                    ProcessStreamKind.StdOut,
                    "Failed!  - Failed:     1, Passed:     0, Skipped:     0, Total:     1",
                    DateTimeOffset.UtcNow),
                new ManagedProcessOutputLine(
                    generation,
                    ProcessStreamKind.StdOut,
                    "  Failed Test.Namespace.Case [1 ms]",
                    DateTimeOffset.UtcNow),
            },
            LastOperation: ProjectWorkflowOperation.Test);
    }
}
