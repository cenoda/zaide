using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 11 M2 tests for structured output projection and generation boundaries.
/// </summary>
public sealed class ProjectOutputServiceTests
{
    static ProjectOutputServiceTests()
    {
        ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    [Fact]
    public void ProjectOutputService_MapsWorkflowSnapshot()
    {
        var workflow = new RecordingWorkflowService();
        using var output = new ProjectOutputService(workflow);
        var line = new ManagedProcessOutputLine(
            1,
            ProcessStreamKind.StdOut,
            "hello",
            DateTimeOffset.UtcNow);

        workflow.Emit(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Running,
            1,
            ProjectWorkflowOperation.Build,
            null,
            "/tmp/app.csproj",
            99,
            new[] { line }));

        Assert.Equal(1, output.Current.Generation);
        Assert.Equal(ProjectWorkflowOperationState.Running, output.Current.State);
        Assert.Single(output.Current.Lines);
        Assert.Equal("hello", output.Current.Lines[0].Text);
    }

    [Fact]
    public void ProjectOutputService_WhenChanged_EmitsMappedSnapshots()
    {
        var workflow = new RecordingWorkflowService();
        using var output = new ProjectOutputService(workflow);
        var seen = new List<ProjectOutputSnapshot>();

        using var sub = output.WhenChanged.Subscribe(seen.Add);

        workflow.Emit(IdleSnapshot(0));
        workflow.Emit(StartingSnapshot(1));

        Assert.Equal(2, seen.Count);
        Assert.Equal(1, seen[1].Generation);
        Assert.Empty(seen[1].Lines);
    }

    [Fact]
    public void ViewModel_ReplacesLinesOnNewGeneration()
    {
        var workflow = new RecordingWorkflowService();
        using var output = new ProjectOutputService(workflow);
        var context = new FakeIdleProjectContext();
        using var vm = new ProjectWorkflowViewModel(workflow, output, context);
        vm.Scheduler = CurrentThreadScheduler.Instance;
        vm.Activate();

        workflow.Emit(RunningWithLine(1, "gen-1"));
        Assert.Single(vm.Lines);
        Assert.Contains("gen-1", vm.Lines[0].Text);

        workflow.Emit(StartingSnapshot(2));
        workflow.Emit(RunningWithLine(2, "gen-2"));

        Assert.Single(vm.Lines);
        Assert.Contains("gen-2", vm.Lines[0].Text);
        Assert.DoesNotContain("gen-1", vm.Lines[0].Text);
    }

    [Fact]
    public void ViewModel_CancelPath_SurfacesCancelledStatus()
    {
        var workflow = new RecordingWorkflowService();
        using var output = new ProjectOutputService(workflow);
        var context = new FakeIdleProjectContext();
        using var vm = new ProjectWorkflowViewModel(workflow, output, context);
        vm.Scheduler = CurrentThreadScheduler.Instance;
        vm.Activate();

        workflow.Emit(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Idle,
            1,
            null,
            ProjectWorkflowOutcomeKind.Cancelled,
            "/tmp/app.csproj",
            null,
            Array.Empty<ManagedProcessOutputLine>()));

        Assert.Equal(ProjectWorkflowOutcomeKind.Cancelled, vm.LastOutcome);
        Assert.Equal("Build cancelled.", vm.StatusMessage);
        Assert.False(vm.IsOperationActive);
    }

    [Fact]
    public void ViewModel_StreamsStdErrWithTag()
    {
        var workflow = new RecordingWorkflowService();
        using var output = new ProjectOutputService(workflow);
        var context = new FakeIdleProjectContext();
        using var vm = new ProjectWorkflowViewModel(workflow, output, context);
        vm.Scheduler = CurrentThreadScheduler.Instance;
        vm.Activate();

        workflow.Emit(RunningWithLine(1, "warn", ProcessStreamKind.StdErr));

        Assert.Equal("stderr", vm.Lines[0].StreamTag);
        Assert.Contains("warn", vm.Lines[0].DisplayText);
    }

    private static ProjectWorkflowSnapshot IdleSnapshot(long generation) =>
        new(
            ProjectWorkflowOperationState.Idle,
            generation,
            null,
            null,
            null,
            null,
            Array.Empty<ManagedProcessOutputLine>());

    private static ProjectWorkflowSnapshot StartingSnapshot(long generation) =>
        new(
            ProjectWorkflowOperationState.Starting,
            generation,
            ProjectWorkflowOperation.Build,
            null,
            "/tmp/app.csproj",
            null,
            Array.Empty<ManagedProcessOutputLine>());

    private static ProjectWorkflowSnapshot RunningWithLine(
        long generation,
        string text,
        ProcessStreamKind stream = ProcessStreamKind.StdOut) =>
        new(
            ProjectWorkflowOperationState.Running,
            generation,
            ProjectWorkflowOperation.Build,
            null,
            "/tmp/app.csproj",
            12,
            new[]
            {
                new ManagedProcessOutputLine(
                    generation,
                    stream,
                    text,
                    DateTimeOffset.UtcNow),
            });

    private sealed class RecordingWorkflowService : IProjectWorkflowService
    {
        private readonly Subject<ProjectWorkflowSnapshot> _snapshotSubject = new();
        private readonly Subject<ManagedProcessOutputLine> _outputSubject = new();
        private ProjectWorkflowSnapshot _current = IdleSnapshot(0);

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

        public Task<ProjectWorkflowOperationResult> StartTestAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task CancelAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Dispose()
        {
            _snapshotSubject.OnCompleted();
            _outputSubject.OnCompleted();
            _snapshotSubject.Dispose();
            _outputSubject.Dispose();
        }
    }

    private sealed class FakeIdleProjectContext : IProjectContextService
    {
        private readonly Subject<ProjectContext> _subject = new();
        private readonly ProjectContext _current = new(
            ProjectContextState.Unloaded,
            null,
            Array.Empty<ProjectCandidate>(),
            null,
            Array.Empty<string>(),
            null);

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
}
