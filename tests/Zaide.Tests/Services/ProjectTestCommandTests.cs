using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 11 M5 tests for <c>project.test</c> registration, CanExecute matrix,
/// and workflow invocation.
/// </summary>
public sealed class ProjectTestCommandTests
{
    static ProjectTestCommandTests()
    {
        ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static readonly string FixtureProjectPath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "fixtures", "workflow-tests-pass", "WorkflowTestsPass.csproj"));

    private static readonly string FixtureSolutionPath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..",
            "Zaide.slnx"));

    [Fact]
    public void TestCommand_IsRegisteredWithMetadata()
    {
        var registry = CommandRegistryFactory.Create();
        using var vm = CreateViewModel(registry);

        var test = registry.GetById("project.test");

        Assert.NotNull(test);
        Assert.Equal("Run Tests", test!.DisplayName);
        Assert.Equal("Project", test.Category);
        Assert.Empty(test.DefaultGestures);
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
    public void Test_CanExecute_ReflectsEligibilityForCSharpProject(
        ProjectContextState state,
        bool expectedWhenIdle)
    {
        var registry = CommandRegistryFactory.Create();
        var context = new FakeProjectContextService(EligibleCSharpContext(state));
        using var vm = CreateViewModel(registry, context);

        Assert.Equal(expectedWhenIdle, vm.TestCommand.CanExecute.FirstAsync().Wait());
    }

    [Theory]
    [InlineData(ProjectContextState.SingleProject, true)]
    [InlineData(ProjectContextState.Selected, true)]
    public void Test_CanExecute_IsTrueForSolutionTarget(ProjectContextState state, bool expected)
    {
        var registry = CommandRegistryFactory.Create();
        var context = new FakeProjectContextService(EligibleSolutionContext(state));
        using var vm = CreateViewModel(registry, context);

        Assert.Equal(expected, vm.TestCommand.CanExecute.FirstAsync().Wait());
    }

    [Fact]
    public void Test_CanExecute_IsFalseWhileOperationActive()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeProjectWorkflowService();
        var context = new FakeProjectContextService(EligibleCSharpContext(ProjectContextState.SingleProject));
        using var vm = CreateViewModel(registry, context, workflow);

        workflow.Emit(RunningSnapshot());

        Assert.False(vm.TestCommand.CanExecute.FirstAsync().Wait());
        Assert.True(vm.CancelCommand.CanExecute.FirstAsync().Wait());
    }

    [Fact]
    public async Task Test_InvokesStartTestAsync()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeProjectWorkflowService();
        var context = new FakeProjectContextService(EligibleCSharpContext(ProjectContextState.SingleProject));
        using var vm = CreateViewModel(registry, context, workflow);

        await vm.TestCommand.Execute();

        Assert.Equal(1, workflow.StartTestCount);
    }

    [Fact]
    public async Task RegistryExecute_RespectsCanExecute_ForRejectedConcurrent()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeProjectWorkflowService
        {
            StartTestResult = new ProjectWorkflowOperationResult(
                ProjectWorkflowOutcomeKind.RejectedConcurrent,
                1,
                ProjectWorkflowOperation.Test,
                FixtureProjectPath,
                null),
        };
        var context = new FakeProjectContextService(EligibleCSharpContext(ProjectContextState.SingleProject));
        using var vm = CreateViewModel(registry, context, workflow);
        workflow.Emit(RunningSnapshot());

        Assert.False(registry.Execute("project.test"));
        Assert.Equal(0, workflow.StartTestCount);
    }

    [Fact]
    public async Task Cancel_InvokesCancelAsyncWhileTesting()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeProjectWorkflowService();
        var context = new FakeProjectContextService(EligibleCSharpContext(ProjectContextState.SingleProject));
        using var vm = CreateViewModel(registry, context, workflow);
        workflow.Emit(RunningSnapshot(ProjectWorkflowOperation.Test));

        await vm.CancelCommand.Execute();

        Assert.Equal(1, workflow.CancelCount);
    }

    [Fact]
    public async Task Test_ShowOutputRequested_WhenStartingBeforeCompletion()
    {
        var registry = CommandRegistryFactory.Create();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workflow = new FakeProjectWorkflowService
        {
            EmitStartingOnStartTest = true,
            TestCompletionGate = gate,
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

        var testComplete = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var executeSub = vm.TestCommand.Execute().Subscribe(
            _ => testComplete.TrySetResult(true),
            ex => testComplete.TrySetException(ex));

        await Task.Yield();

        Assert.Equal(1, showCount);
        Assert.True(showBeforeComplete);

        gate.SetResult(true);
        await testComplete.Task;
        executeSub.Dispose();
    }

    [Fact]
    public async Task Test_ShowTestResultsRequested_WhenStartingBeforeCompletion()
    {
        var registry = CommandRegistryFactory.Create();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workflow = new FakeProjectWorkflowService
        {
            EmitStartingOnStartTest = true,
            TestCompletionGate = gate,
        };
        var context = new FakeProjectContextService(EligibleCSharpContext(ProjectContextState.SingleProject));
        using var vm = CreateViewModel(registry, context, workflow);

        var showCount = 0;
        using var _ = vm.WhenShowTestResultsRequested.Subscribe(_ => showCount++);

        var testComplete = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var executeSub = vm.TestCommand.Execute().Subscribe(
            _ => testComplete.TrySetResult(true),
            ex => testComplete.TrySetException(ex));

        await Task.Yield();
        Assert.Equal(1, showCount);

        gate.SetResult(true);
        await testComplete.Task;
        executeSub.Dispose();
    }

    [Fact]
    public async Task Test_ShowTestResultsRequested_NotRaisedOnRejectedContext()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeProjectWorkflowService
        {
            StartTestResult = new ProjectWorkflowOperationResult(
                ProjectWorkflowOutcomeKind.RejectedContext,
                0,
                ProjectWorkflowOperation.Test,
                null,
                null),
        };
        var context = new FakeProjectContextService(EligibleCSharpContext(ProjectContextState.SingleProject));
        using var vm = CreateViewModel(registry, context, workflow);

        var showCount = 0;
        using var _ = vm.WhenShowTestResultsRequested.Subscribe(_ => showCount++);

        await vm.TestCommand.Execute();

        Assert.Equal(0, showCount);
    }

    [Fact]
    public void WorkflowTestsPassFixture_ExistsForSmoke()
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
            "WorkflowTestsPass",
            ProjectKind.CSharpProject);
        return new ProjectContext(
            state,
            Path.GetDirectoryName(FixtureProjectPath),
            new[] { candidate },
            candidate,
            Array.Empty<string>(),
            null);
    }

    private static ProjectContext EligibleSolutionContext(ProjectContextState state)
    {
        var candidate = new ProjectCandidate(
            FixtureSolutionPath,
            "Zaide",
            ProjectKind.SolutionX);
        return new ProjectContext(
            state,
            Path.GetDirectoryName(FixtureSolutionPath),
            new[] { candidate },
            candidate,
            Array.Empty<string>(),
            null);
    }

    private static ProjectWorkflowSnapshot RunningSnapshot(
        ProjectWorkflowOperation operation = ProjectWorkflowOperation.Build) =>
        new(
            ProjectWorkflowOperationState.Running,
            1,
            operation,
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

        public int StartTestCount { get; private set; }
        public int CancelCount { get; private set; }

        public ProjectWorkflowOperationResult? StartTestResult { get; set; }

        public bool EmitStartingOnStartTest { get; set; }

        public TaskCompletionSource<bool>? TestCompletionGate { get; set; }

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

        public Task<ProjectWorkflowOperationResult> StartRunAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task<ProjectWorkflowOperationResult> StartTestAsync(
            CancellationToken cancellationToken = default)
        {
            StartTestCount++;

            if (StartTestResult?.Outcome is ProjectWorkflowOutcomeKind.RejectedConcurrent
                or ProjectWorkflowOutcomeKind.RejectedContext)
            {
                return StartTestResult;
            }

            if (EmitStartingOnStartTest)
            {
                Emit(new ProjectWorkflowSnapshot(
                    ProjectWorkflowOperationState.Starting,
                    1,
                    ProjectWorkflowOperation.Test,
                    null,
                    FixtureProjectPath,
                    null,
                    Array.Empty<ManagedProcessOutputLine>()));
            }

            if (TestCompletionGate is not null)
                await TestCompletionGate.Task.ConfigureAwait(false);

            return StartTestResult ?? new ProjectWorkflowOperationResult(
                ProjectWorkflowOutcomeKind.Succeeded,
                1,
                ProjectWorkflowOperation.Test,
                FixtureProjectPath,
                0);
        }

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
