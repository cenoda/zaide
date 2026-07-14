using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 11 M4 tests for <c>project.run</c> registration, CanExecute matrix,
/// and workflow invocation.
/// </summary>
public sealed class ProjectRunCommandTests
{
    static ProjectRunCommandTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static readonly string FixtureProjectPath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "fixtures", "workflow-console", "WorkflowConsole.csproj"));

    private static readonly string FixtureSolutionPath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..",
            "Zaide.slnx"));

    [Fact]
    public void RunCommand_IsRegisteredWithMetadata()
    {
        var registry = CommandRegistryFactory.Create();
        using var vm = CreateViewModel(registry);

        var run = registry.GetById("project.run");

        Assert.NotNull(run);
        Assert.Equal("Run", run!.DisplayName);
        Assert.Equal("Project", run.Category);
        Assert.Equal(new[] { "Ctrl+F5" }, run.DefaultGestures);
    }

    [Theory]
    [InlineData(ProjectContextState.Unloaded, false)]
    [InlineData(ProjectContextState.Loading, false)]
    [InlineData(ProjectContextState.NoProject, false)]
    [InlineData(ProjectContextState.Unsupported, false)]
    [InlineData(ProjectContextState.Ambiguous, false)]
    [InlineData(ProjectContextState.Failed, false)]
    [InlineData(ProjectContextState.SingleProject, true)]
    [InlineData(ProjectContextState.Selected, true)]
    public void Run_CanExecute_ReflectsEligibilityForCSharpProject(
        ProjectContextState state,
        bool expectedWhenIdle)
    {
        var registry = CommandRegistryFactory.Create();
        var context = new FakeProjectContextService(EligibleCSharpContext(state));
        using var vm = CreateViewModel(registry, context);

        Assert.Equal(expectedWhenIdle, vm.RunCommand.CanExecute.FirstAsync().Wait());
    }

    [Fact]
    public void Run_CanExecute_IsFalseForSolutionTarget()
    {
        var registry = CommandRegistryFactory.Create();
        var context = new FakeProjectContextService(EligibleSolutionContext());
        using var vm = CreateViewModel(registry, context);

        Assert.False(vm.RunCommand.CanExecute.FirstAsync().Wait());
    }

    [Fact]
    public void Run_CanExecute_IsFalseWhileOperationActive()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeProjectWorkflowService();
        var context = new FakeProjectContextService(EligibleCSharpContext(ProjectContextState.SingleProject));
        using var vm = CreateViewModel(registry, context, workflow);

        workflow.Emit(RunningSnapshot());

        Assert.False(vm.RunCommand.CanExecute.FirstAsync().Wait());
        Assert.True(vm.CancelCommand.CanExecute.FirstAsync().Wait());
    }

    [Fact]
    public async Task Run_InvokesStartRunAsync()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeProjectWorkflowService();
        var context = new FakeProjectContextService(EligibleCSharpContext(ProjectContextState.SingleProject));
        using var vm = CreateViewModel(registry, context, workflow);

        await vm.RunCommand.Execute();

        Assert.Equal(1, workflow.StartRunCount);
    }

    [Fact]
    public async Task RegistryExecute_RespectsCanExecute_ForRejectedConcurrent()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeProjectWorkflowService
        {
            StartRunResult = new ProjectWorkflowOperationResult(
                ProjectWorkflowOutcomeKind.RejectedConcurrent,
                1,
                ProjectWorkflowOperation.Run,
                FixtureProjectPath,
                null),
        };
        var context = new FakeProjectContextService(EligibleCSharpContext(ProjectContextState.SingleProject));
        using var vm = CreateViewModel(registry, context, workflow);
        workflow.Emit(RunningSnapshot());

        Assert.False(registry.Execute("project.run"));
        Assert.Equal(0, workflow.StartRunCount);
    }

    [Fact]
    public async Task Cancel_InvokesCancelAsyncWhileRunning()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeProjectWorkflowService();
        var context = new FakeProjectContextService(EligibleCSharpContext(ProjectContextState.SingleProject));
        using var vm = CreateViewModel(registry, context, workflow);
        workflow.Emit(RunningSnapshot());

        await vm.CancelCommand.Execute();

        Assert.Equal(1, workflow.CancelCount);
    }

    [Fact]
    public async Task Run_ShowOutputRequested_WhenStartingBeforeCompletion()
    {
        var registry = CommandRegistryFactory.Create();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workflow = new FakeProjectWorkflowService
        {
            EmitStartingOnStartRun = true,
            RunCompletionGate = gate,
        };
        var context = new FakeProjectContextService(EligibleCSharpContext(ProjectContextState.SingleProject));
        using var vm = CreateViewModel(registry, context, workflow);

        var showCount = 0;
        var showBeforeComplete = false;
        using var _ = vm.WhenShowOutputRequested.Subscribe(_ =>
        {
            showCount++;
            showBeforeComplete = !gate.Task.IsCompleted;
        });

        var runComplete = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var executeSub = vm.RunCommand.Execute().Subscribe(
            _ => runComplete.TrySetResult(true),
            ex => runComplete.TrySetException(ex));

        await Task.Yield();

        Assert.Equal(1, showCount);
        Assert.True(showBeforeComplete);

        gate.SetResult(true);
        await runComplete.Task;
        executeSub.Dispose();
    }

    [Fact]
    public async Task Run_ShowOutputRequested_NotRaisedOnRejectedContext()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeProjectWorkflowService
        {
            StartRunResult = new ProjectWorkflowOperationResult(
                ProjectWorkflowOutcomeKind.RejectedContext,
                0,
                ProjectWorkflowOperation.Run,
                null,
                null),
        };
        var context = new FakeProjectContextService(EligibleCSharpContext(ProjectContextState.SingleProject));
        using var vm = CreateViewModel(registry, context, workflow);

        var showCount = 0;
        using var _ = vm.WhenShowOutputRequested.Subscribe(_ => showCount++);

        await vm.RunCommand.Execute();

        Assert.Equal(0, showCount);
    }

    [Fact]
    public async Task Run_RejectedContext_ForSolutionTarget()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeProjectWorkflowService();
        var context = new FakeProjectContextService(EligibleSolutionContext());
        using var vm = CreateViewModel(registry, context, workflow);

        // CanExecute is false, so registry.Execute should return false
        Assert.False(vm.RunCommand.CanExecute.FirstAsync().Wait());
        Assert.False(registry.Execute("project.run"));
        Assert.Equal(0, workflow.StartRunCount);
    }

    [Fact]
    public void WorkflowConsoleFixture_ExistsForSmoke()
    {
        Assert.True(File.Exists(FixtureProjectPath), FixtureProjectPath);
    }

    private static ProjectWorkflowViewModel CreateViewModel(
        ICommandRegistry registry,
        FakeProjectContextService? context = null,
        FakeProjectWorkflowService? workflow = null)
    {
        context ??= new FakeProjectContextService(EligibleCSharpContext(ProjectContextState.SingleProject));
        workflow ??= new FakeProjectWorkflowService();
        var vm = TestProjectWorkflowFactory.CreateViewModel(workflow, context, registry);
        vm.Scheduler = CurrentThreadScheduler.Instance;
        vm.Activate();
        return vm;
    }

    private static ProjectContext EligibleCSharpContext(ProjectContextState state)
    {
        var candidate = new ProjectCandidate(
            FixtureProjectPath,
            "WorkflowConsole",
            ProjectKind.CSharpProject);
        return new ProjectContext(
            state,
            Path.GetDirectoryName(FixtureProjectPath),
            new[] { candidate },
            candidate,
            Array.Empty<string>(),
            null);
    }

    private static ProjectContext EligibleSolutionContext()
    {
        var candidate = new ProjectCandidate(
            FixtureSolutionPath,
            "Zaide",
            ProjectKind.SolutionX);
        return new ProjectContext(
            ProjectContextState.SingleProject,
            Path.GetDirectoryName(FixtureSolutionPath),
            new[] { candidate },
            candidate,
            Array.Empty<string>(),
            null);
    }

    private static ProjectWorkflowSnapshot RunningSnapshot() =>
        new(
            ProjectWorkflowOperationState.Running,
            1,
            ProjectWorkflowOperation.Run,
            null,
            FixtureProjectPath,
            42,
            Array.Empty<ManagedProcessOutputLine>());

    private sealed class FakeProjectContextService : IProjectContextService
    {
        private readonly Subject<ProjectContext> _subject = new();
        private ProjectContext _current;

        public FakeProjectContextService(ProjectContext initial)
        {
            _current = initial;
            _subject.OnNext(initial);
        }

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
    }

    private sealed class FakeProjectWorkflowService : IProjectWorkflowService
    {
        private readonly Subject<ProjectWorkflowSnapshot> _snapshotSubject = new();
        private readonly Subject<ManagedProcessOutputLine> _outputSubject = new();
        private ProjectWorkflowSnapshot _current = new(
            ProjectWorkflowOperationState.Idle,
            0,
            null,
            null,
            null,
            null,
            Array.Empty<ManagedProcessOutputLine>());

        public int StartRunCount { get; private set; }
        public int CancelCount { get; private set; }

        public ProjectWorkflowOperationResult? StartRunResult { get; set; }

        public bool EmitStartingOnStartRun { get; set; }

        public TaskCompletionSource<bool>? RunCompletionGate { get; set; }

        public ProjectWorkflowSnapshot Current => _current;

        public IObservable<ProjectWorkflowSnapshot> WhenChanged => _snapshotSubject;

        public IObservable<ManagedProcessOutputLine> WhenOutputReceived => _outputSubject;

        public void Emit(ProjectWorkflowSnapshot snapshot)
        {
            _current = snapshot;
            _snapshotSubject.OnNext(snapshot);
        }

        public Task<ProjectWorkflowOperationResult> StartBuildAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task<ProjectWorkflowOperationResult> StartRunAsync(
            CancellationToken cancellationToken = default)
        {
            StartRunCount++;

            if (StartRunResult?.Outcome is ProjectWorkflowOutcomeKind.RejectedConcurrent
                or ProjectWorkflowOutcomeKind.RejectedContext)
            {
                return StartRunResult;
            }

            if (EmitStartingOnStartRun)
            {
                Emit(new ProjectWorkflowSnapshot(
                    ProjectWorkflowOperationState.Starting,
                    1,
                    ProjectWorkflowOperation.Run,
                    null,
                    FixtureProjectPath,
                    null,
                    Array.Empty<ManagedProcessOutputLine>()));
            }

            if (RunCompletionGate is not null)
                await RunCompletionGate.Task.ConfigureAwait(false);

            return StartRunResult ?? new ProjectWorkflowOperationResult(
                ProjectWorkflowOutcomeKind.Succeeded,
                1,
                ProjectWorkflowOperation.Run,
                FixtureProjectPath,
                0);
        }

        public Task<ProjectWorkflowOperationResult> StartTestAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProjectWorkflowOperationResult> StartBuildForDebugHandoffAsync(
            IProjectOperationHandoffLease handoffLease,
            CancellationToken cancellationToken = default) =>
            StartBuildAsync(cancellationToken);

        public Task CancelAsync(CancellationToken cancellationToken = default)
        {
            CancelCount++;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _snapshotSubject.OnCompleted();
            _outputSubject.OnCompleted();
            _snapshotSubject.Dispose();
            _outputSubject.Dispose();
        }
    }
}
