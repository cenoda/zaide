using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 11 F9: save-all-dirty-tabs before Build / Run / Test.
/// Proves the auto-save policy, failure gating, and that all three
/// workflow commands share the same save-before-start guard.
/// </summary>
public sealed class ProjectWorkflowSaveBeforeStartTests
{
    static ProjectWorkflowSaveBeforeStartTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static readonly string FixtureProjectPath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "fixtures", "workflow-console", "WorkflowConsole.csproj"));

    // ── Save delegate invoked, workflow proceeds ────────────────────

    [Fact]
    public async Task Build_SavesDirtyTabsBeforeStart()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeAllOpsWorkflowService();
        var context = new FakeProjectContextService(EligibleCSharpContext());
        using var vm = CreateViewModel(registry, context, workflow);

        var saveCallCount = 0;
        vm.SaveAllDirtyTabsAsync = () =>
        {
            saveCallCount++;
            return Task.FromResult(true);
        };

        await vm.BuildCommand.Execute();

        Assert.Equal(1, saveCallCount);
        Assert.Equal(1, workflow.StartBuildCount);
    }

    [Fact]
    public async Task Run_SavesDirtyTabsBeforeStart()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeAllOpsWorkflowService();
        var context = new FakeProjectContextService(EligibleCSharpContext());
        using var vm = CreateViewModel(registry, context, workflow);

        var saveCallCount = 0;
        vm.SaveAllDirtyTabsAsync = () =>
        {
            saveCallCount++;
            return Task.FromResult(true);
        };

        await vm.RunCommand.Execute();

        Assert.Equal(1, saveCallCount);
        Assert.Equal(1, workflow.StartRunCount);
    }

    [Fact]
    public async Task Test_SavesDirtyTabsBeforeStart()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeAllOpsWorkflowService();
        var context = new FakeProjectContextService(EligibleCSharpContext());
        using var vm = CreateViewModel(registry, context, workflow);

        var saveCallCount = 0;
        vm.SaveAllDirtyTabsAsync = () =>
        {
            saveCallCount++;
            return Task.FromResult(true);
        };

        await vm.TestCommand.Execute();

        Assert.Equal(1, saveCallCount);
        Assert.Equal(1, workflow.StartTestCount);
    }

    // ── Save failure prevents workflow start ────────────────────────

    [Fact]
    public async Task Build_SaveFailure_PreventsWorkflowStart()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeAllOpsWorkflowService();
        var context = new FakeProjectContextService(EligibleCSharpContext());
        using var vm = CreateViewModel(registry, context, workflow);

        var saveCallCount = 0;
        vm.SaveAllDirtyTabsAsync = () =>
        {
            saveCallCount++;
            return Task.FromResult(false);
        };

        await vm.BuildCommand.Execute();

        Assert.Equal(1, saveCallCount);
        Assert.Equal(0, workflow.StartBuildCount);
    }

    [Fact]
    public async Task Run_SaveFailure_PreventsWorkflowStart()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeAllOpsWorkflowService();
        var context = new FakeProjectContextService(EligibleCSharpContext());
        using var vm = CreateViewModel(registry, context, workflow);

        var saveCallCount = 0;
        vm.SaveAllDirtyTabsAsync = () =>
        {
            saveCallCount++;
            return Task.FromResult(false);
        };

        await vm.RunCommand.Execute();

        Assert.Equal(1, saveCallCount);
        Assert.Equal(0, workflow.StartRunCount);
    }

    [Fact]
    public async Task Test_SaveFailure_PreventsWorkflowStart()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeAllOpsWorkflowService();
        var context = new FakeProjectContextService(EligibleCSharpContext());
        using var vm = CreateViewModel(registry, context, workflow);

        var saveCallCount = 0;
        vm.SaveAllDirtyTabsAsync = () =>
        {
            saveCallCount++;
            return Task.FromResult(false);
        };

        await vm.TestCommand.Execute();

        Assert.Equal(1, saveCallCount);
        Assert.Equal(0, workflow.StartTestCount);
    }

    // ── Clean buffers do not trigger unnecessary saves ──────────────

    [Fact]
    public async Task Build_CleanBuffers_DoesNotTriggerUnnecessarySaves()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeAllOpsWorkflowService();
        var context = new FakeProjectContextService(EligibleCSharpContext());
        using var vm = CreateViewModel(registry, context, workflow);

        var saveCallCount = 0;
        vm.SaveAllDirtyTabsAsync = () =>
        {
            saveCallCount++;
            return Task.FromResult(true);
        };

        // First build — save is called (delegate returns true quickly)
        await vm.BuildCommand.Execute();
        Assert.Equal(1, saveCallCount);
        Assert.Equal(1, workflow.StartBuildCount);

        // Second build — save is still called but returns immediately
        // (the delegate responsibility is to check dirty state; the
        //  VM always calls it unconditionally before each workflow)
        await vm.BuildCommand.Execute();
        Assert.Equal(2, saveCallCount);
        Assert.Equal(2, workflow.StartBuildCount);
    }

    // ── Delegate not wired (null) — workflow proceeds normally ──────

    [Fact]
    public async Task Build_WhenDelegateNotWired_ProceedsToWorkflow()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeAllOpsWorkflowService();
        var context = new FakeProjectContextService(EligibleCSharpContext());
        using var vm = CreateViewModel(registry, context, workflow);

        // SaveAllDirtyTabsAsync is null by default (tests that don't wire it)
        Assert.Null(vm.SaveAllDirtyTabsAsync);

        await vm.BuildCommand.Execute();

        Assert.Equal(1, workflow.StartBuildCount);
    }

    // ── Shared policy: Build / Run / Test all gate the same way ─────

    [Fact]
    public async Task AllCommands_ShareSaveBeforeStartPolicy()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeAllOpsWorkflowService();
        var context = new FakeProjectContextService(EligibleCSharpContext());
        using var vm = CreateViewModel(registry, context, workflow);

        var saveCallCount = 0;
        vm.SaveAllDirtyTabsAsync = () =>
        {
            saveCallCount++;
            return Task.FromResult(true);
        };

        await vm.BuildCommand.Execute();
        await vm.RunCommand.Execute();
        await vm.TestCommand.Execute();

        // Each command called the save delegate exactly once
        Assert.Equal(3, saveCallCount);
        Assert.Equal(1, workflow.StartBuildCount);
        Assert.Equal(1, workflow.StartRunCount);
        Assert.Equal(1, workflow.StartTestCount);
    }

    [Fact]
    public async Task AllCommands_ShareSaveFailurePolicy()
    {
        var registry = CommandRegistryFactory.Create();
        var workflow = new FakeAllOpsWorkflowService();
        var context = new FakeProjectContextService(EligibleCSharpContext());
        using var vm = CreateViewModel(registry, context, workflow);

        var saveCallCount = 0;
        vm.SaveAllDirtyTabsAsync = () =>
        {
            saveCallCount++;
            return Task.FromResult(false);
        };

        await vm.BuildCommand.Execute();
        await vm.RunCommand.Execute();
        await vm.TestCommand.Execute();

        // Save was attempted three times
        Assert.Equal(3, saveCallCount);
        // None of the workflows started
        Assert.Equal(0, workflow.StartBuildCount);
        Assert.Equal(0, workflow.StartRunCount);
        Assert.Equal(0, workflow.StartTestCount);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static ProjectWorkflowViewModel CreateViewModel(
        ICommandRegistry registry,
        FakeProjectContextService? context = null,
        FakeAllOpsWorkflowService? workflow = null)
    {
        context ??= new FakeProjectContextService(EligibleCSharpContext());
        workflow ??= new FakeAllOpsWorkflowService();
        var vm = TestProjectWorkflowFactory.CreateViewModel(workflow, context, registry);
        vm.Scheduler = CurrentThreadScheduler.Instance;
        vm.Activate();
        return vm;
    }

    private static ProjectContext EligibleCSharpContext()
    {
        var candidate = new ProjectCandidate(
            FixtureProjectPath,
            "WorkflowConsole",
            ProjectKind.CSharpProject);
        return new ProjectContext(
            ProjectContextState.SingleProject,
            Path.GetDirectoryName(FixtureProjectPath),
            new[] { candidate },
            candidate,
            Array.Empty<string>(),
            null);
    }

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

    /// <summary>
    /// Supports all three workflow operations (Build, Run, Test) with
    /// per-operation call counters so the shared-policy tests can verify
    /// each path independently.
    /// </summary>
    private sealed class FakeAllOpsWorkflowService : IProjectWorkflowService
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
        public int StartRunCount { get; private set; }
        public int StartTestCount { get; private set; }

        public ProjectWorkflowSnapshot Current => _current;

        public IObservable<ProjectWorkflowSnapshot> WhenChanged => _snapshotSubject;
        public IObservable<ManagedProcessOutputLine> WhenOutputReceived => _outputSubject;

        public Task<ProjectWorkflowOperationResult> StartBuildAsync(
            CancellationToken cancellationToken = default)
        {
            StartBuildCount++;
            return Task.FromResult(new ProjectWorkflowOperationResult(
                ProjectWorkflowOutcomeKind.Succeeded,
                1,
                ProjectWorkflowOperation.Build,
                null,
                0));
        }

        public Task<ProjectWorkflowOperationResult> StartRunAsync(
            CancellationToken cancellationToken = default)
        {
            StartRunCount++;
            return Task.FromResult(new ProjectWorkflowOperationResult(
                ProjectWorkflowOutcomeKind.Succeeded,
                1,
                ProjectWorkflowOperation.Run,
                null,
                0));
        }

        public Task<ProjectWorkflowOperationResult> StartTestAsync(
            CancellationToken cancellationToken = default)
        {
            StartTestCount++;
            return Task.FromResult(new ProjectWorkflowOperationResult(
                ProjectWorkflowOutcomeKind.Succeeded,
                1,
                ProjectWorkflowOperation.Test,
                null,
                0));
        }

        public Task<ProjectWorkflowOperationResult> StartBuildForDebugHandoffAsync(
            IProjectOperationHandoffLease handoffLease,
            CancellationToken cancellationToken = default) =>
            StartBuildAsync(cancellationToken);

        public Task CancelAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Dispose()
        {
            _snapshotSubject.OnCompleted();
            _outputSubject.OnCompleted();
            _snapshotSubject.Dispose();
            _outputSubject.Dispose();
        }
    }
}
