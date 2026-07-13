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
/// Phase 11 M2 tests for <c>project.build</c> and <c>project.cancel</c> registration,
/// CanExecute matrix, and workflow invocation.
/// </summary>
public sealed class ProjectBuildCommandTests
{
    static ProjectBuildCommandTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static readonly string FixtureProjectPath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "fixtures", "workflow-console", "WorkflowConsole.csproj"));

    [Fact]
    public void BuildAndCancel_AreRegisteredWithMetadata()
    {
        var registry = CommandRegistryFactory.Create();
        using var vm = CreateViewModel(registry);

        var build = registry.GetById("project.build");
        var cancel = registry.GetById("project.cancel");

        Assert.NotNull(build);
        Assert.Equal("Build", build!.DisplayName);
        Assert.Equal("Project", build.Category);
        Assert.Equal(new[] { "Ctrl+Shift+B" }, build.DefaultGestures);

        Assert.NotNull(cancel);
        Assert.Equal("Cancel Build/Run/Test", cancel!.DisplayName);
        Assert.Equal("Project", cancel.Category);
        Assert.Empty(cancel.DefaultGestures);
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
    public void Build_CanExecute_ReflectsEligibilityAndBusyState(
        ProjectContextState state,
        bool expectedWhenIdle)
    {
        var registry = CommandRegistryFactory.Create();
        var context = new FakeProjectContextService(EligibleContext(state));
        using var vm = CreateViewModel(registry, context);

        Assert.Equal(expectedWhenIdle, vm.BuildCommand.CanExecute.FirstAsync().Wait());
    }

    [Fact]
    public void Build_CanExecute_IsFalseWhileOperationActive()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeProjectWorkflowService();
        var context = new FakeProjectContextService(EligibleContext(ProjectContextState.SingleProject));
        using var vm = CreateViewModel(registry, context, workflow);

        workflow.Emit(RunningSnapshot());

        Assert.False(vm.BuildCommand.CanExecute.FirstAsync().Wait());
        Assert.True(vm.CancelCommand.CanExecute.FirstAsync().Wait());
    }

    [Fact]
    public async Task Build_InvokesStartBuildAsync()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeProjectWorkflowService();
        var context = new FakeProjectContextService(EligibleContext(ProjectContextState.SingleProject));
        using var vm = CreateViewModel(registry, context, workflow);

        await vm.BuildCommand.Execute();

        Assert.Equal(1, workflow.StartBuildCount);
    }

    [Fact]
    public async Task RegistryExecute_RespectsCanExecute_ForRejectedConcurrent()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeProjectWorkflowService
        {
            StartBuildResult = new ProjectWorkflowOperationResult(
                ProjectWorkflowOutcomeKind.RejectedConcurrent,
                1,
                ProjectWorkflowOperation.Build,
                FixtureProjectPath,
                null),
        };
        var context = new FakeProjectContextService(EligibleContext(ProjectContextState.SingleProject));
        using var vm = CreateViewModel(registry, context, workflow);
        workflow.Emit(RunningSnapshot());

        Assert.False(registry.Execute("project.build"));
        Assert.Equal(0, workflow.StartBuildCount);
    }

    [Fact]
    public async Task Cancel_InvokesCancelAsyncWhileRunning()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeProjectWorkflowService();
        var context = new FakeProjectContextService(EligibleContext(ProjectContextState.SingleProject));
        using var vm = CreateViewModel(registry, context, workflow);
        workflow.Emit(RunningSnapshot());

        await vm.CancelCommand.Execute();

        Assert.Equal(1, workflow.CancelCount);
    }

    [Fact]
    public async Task Build_ShowOutputRequested_WhenStartingBeforeCompletion()
    {
        var registry = CommandRegistryFactory.Create();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workflow = new FakeProjectWorkflowService
        {
            EmitStartingOnStartBuild = true,
            BuildCompletionGate = gate,
        };
        var context = new FakeProjectContextService(EligibleContext(ProjectContextState.SingleProject));
        using var vm = CreateViewModel(registry, context, workflow);

        var showCount = 0;
        var showBeforeComplete = false;
        using var _ = vm.WhenShowOutputRequested.Subscribe(_ =>
        {
            showCount++;
            showBeforeComplete = !gate.Task.IsCompleted;
        });

        var buildComplete = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var executeSub = vm.BuildCommand.Execute().Subscribe(
            _ => buildComplete.TrySetResult(true),
            ex => buildComplete.TrySetException(ex));

        await Task.Yield();

        Assert.Equal(1, showCount);
        Assert.True(showBeforeComplete);

        gate.SetResult(true);
        await buildComplete.Task;
        executeSub.Dispose();
    }

    [Fact]
    public async Task Build_ShowOutputRequested_NotRaisedOnRejectedContext()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeProjectWorkflowService
        {
            StartBuildResult = new ProjectWorkflowOperationResult(
                ProjectWorkflowOutcomeKind.RejectedContext,
                0,
                ProjectWorkflowOperation.Build,
                null,
                null),
        };
        var context = new FakeProjectContextService(EligibleContext(ProjectContextState.SingleProject));
        using var vm = CreateViewModel(registry, context, workflow);

        var showCount = 0;
        using var _ = vm.WhenShowOutputRequested.Subscribe(_ => showCount++);

        await vm.BuildCommand.Execute();

        Assert.Equal(0, showCount);
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
        context ??= new FakeProjectContextService(EligibleContext(ProjectContextState.SingleProject));
        workflow ??= new FakeProjectWorkflowService();
        var output = new ProjectOutputService(workflow);
        var vm = new ProjectWorkflowViewModel(workflow, output, context, registry);
        vm.Scheduler = CurrentThreadScheduler.Instance;
        vm.Activate();
        return vm;
    }

    private static ProjectContext EligibleContext(ProjectContextState state)
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

    private static ProjectWorkflowSnapshot RunningSnapshot() =>
        new(
            ProjectWorkflowOperationState.Running,
            1,
            ProjectWorkflowOperation.Build,
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

        public int StartBuildCount { get; private set; }
        public int CancelCount { get; private set; }

        public ProjectWorkflowOperationResult? StartBuildResult { get; set; }

        public bool EmitStartingOnStartBuild { get; set; }

        public TaskCompletionSource<bool>? BuildCompletionGate { get; set; }

        public ProjectWorkflowSnapshot Current => _current;

        public IObservable<ProjectWorkflowSnapshot> WhenChanged => _snapshotSubject;

        public IObservable<ManagedProcessOutputLine> WhenOutputReceived => _outputSubject;

        public void Emit(ProjectWorkflowSnapshot snapshot)
        {
            _current = snapshot;
            _snapshotSubject.OnNext(snapshot);
        }

        public async Task<ProjectWorkflowOperationResult> StartBuildAsync(
            CancellationToken cancellationToken = default)
        {
            StartBuildCount++;

            if (StartBuildResult?.Outcome is ProjectWorkflowOutcomeKind.RejectedConcurrent
                or ProjectWorkflowOutcomeKind.RejectedContext)
            {
                return StartBuildResult;
            }

            if (EmitStartingOnStartBuild)
            {
                Emit(new ProjectWorkflowSnapshot(
                    ProjectWorkflowOperationState.Starting,
                    1,
                    ProjectWorkflowOperation.Build,
                    null,
                    FixtureProjectPath,
                    null,
                    Array.Empty<ManagedProcessOutputLine>()));
            }

            if (BuildCompletionGate is not null)
                await BuildCompletionGate.Task.ConfigureAwait(false);

            return StartBuildResult ?? new ProjectWorkflowOperationResult(
                ProjectWorkflowOutcomeKind.Succeeded,
                1,
                ProjectWorkflowOperation.Build,
                null,
                0);
        }

        public Task<ProjectWorkflowOperationResult> StartRunAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProjectWorkflowOperationResult> StartTestAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

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
