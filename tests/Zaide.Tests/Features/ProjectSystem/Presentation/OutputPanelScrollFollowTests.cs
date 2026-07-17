using System;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Splat;
using Xunit;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Views;
using Zaide.Tests;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Infrastructure;
using Zaide.Features.ProjectSystem.Presentation;
using Zaide.Tests.Features.ProjectSystem;

namespace Zaide.Tests.Features.ProjectSystem.Presentation;

/// <summary>
/// Phase 11 F11 tests for <see cref="OutputPanel"/> scroll-follow behavior.
/// Tests cover construction, activation, and ViewModel interaction.
/// Full end-to-end scroll-follow (with ScrollViewer) requires a windowing
/// platform and is not exercised here.
/// </summary>
public sealed class OutputPanelScrollFollowTests
{
    static OutputPanelScrollFollowTests()
    {
        EnsureApplication();
    }

    [Fact]
    public void Panel_Constructs_WithoutThrowing()
    {
        var workflow = new RecordingWorkflowService();
        using var output = new ProjectOutputService(workflow);
        var context = new FakeIdleProjectContext();
        using var vm = TestProjectWorkflowFactory.CreateViewModel(workflow, context);
        vm.Scheduler = CurrentThreadScheduler.Instance;

        var panel = new OutputPanel();
        panel.ViewModel = vm;

        Assert.NotNull(panel);
    }

    [Fact]
    public void Panel_ViewModelBinding_DoesNotThrow()
    {
        var workflow = new RecordingWorkflowService();
        using var output = new ProjectOutputService(workflow);
        var context = new FakeIdleProjectContext();
        using var vm = TestProjectWorkflowFactory.CreateViewModel(workflow, context);
        vm.Scheduler = CurrentThreadScheduler.Instance;
        vm.Activate();

        var panel = new OutputPanel();
        panel.ViewModel = vm;

        // Emit a starting snapshot — this triggers Lines.Clear()
        var exception = Record.Exception(() =>
        {
            workflow.Emit(new ProjectWorkflowSnapshot(
                ProjectWorkflowOperationState.Starting,
                1,
                ProjectWorkflowOperation.Build,
                null,
                "/tmp/app.csproj",
                null,
                Array.Empty<ManagedProcessOutputLine>()));

            // Add lines via the workflow output stream
            for (var i = 0; i < 10; i++)
            {
                workflow.EmitLine(new ManagedProcessOutputLine(
                    1,
                    ProcessStreamKind.StdOut,
                    $"line-{i}",
                    DateTimeOffset.UtcNow));
            }
        });

        Assert.Null(exception);
        Assert.Equal(10, vm.Lines.Count);
        Assert.Contains("line-5", vm.Lines[5].Text);
    }

    [Fact]
    public void LinesCollection_TriggersCollectionChanged_OnAdd()
    {
        var workflow = new RecordingWorkflowService();
        using var output = new ProjectOutputService(workflow);
        var context = new FakeIdleProjectContext();
        using var vm = TestProjectWorkflowFactory.CreateViewModel(workflow, context);
        vm.Scheduler = CurrentThreadScheduler.Instance;
        vm.Activate();

        // Verify the ViewModel's Lines collection triggers CollectionChanged
        // (which the OutputPanel subscribes to for scroll-follow).
        workflow.Emit(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            1,
            ProjectWorkflowOperation.Build,
            null,
                "/tmp/app.csproj",
            null,
                Array.Empty<ManagedProcessOutputLine>()));

        var changedCount = 0;
        vm.Lines.CollectionChanged += (_, _) => changedCount++;

        workflow.EmitLine(new ManagedProcessOutputLine(
            1, ProcessStreamKind.StdOut, "hello", DateTimeOffset.UtcNow));

        Assert.Equal(1, changedCount);
    }

    private static void EnsureApplication()
    {
        if (Application.Current is not null)
            return;

        var app = new App();
        app.Initialize();
        SetupReactiveUi();
    }

    private static void SetupReactiveUi()
    {
        Locator.CurrentMutable.Register(
            () => new AvaloniaActivationForViewFetcher(),
            typeof(IActivationForViewFetcher));
    }

    private sealed class RecordingWorkflowService : IProjectWorkflowService
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

        public ProjectWorkflowSnapshot Current => _current;

        public IObservable<ProjectWorkflowSnapshot> WhenChanged => _snapshotSubject;

        public IObservable<ManagedProcessOutputLine> WhenOutputReceived => _outputSubject;

        public void Emit(ProjectWorkflowSnapshot snapshot)
        {
            _current = snapshot;
            _snapshotSubject.OnNext(snapshot);
        }

        public void EmitLine(ManagedProcessOutputLine line)
        {
            _outputSubject.OnNext(line);
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

        public Task<ProjectWorkflowOperationResult> StartBuildForDebugHandoffAsync(
            IProjectOperationHandoffLease handoffLease,
            CancellationToken cancellationToken = default) =>
            StartBuildAsync(cancellationToken);

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
