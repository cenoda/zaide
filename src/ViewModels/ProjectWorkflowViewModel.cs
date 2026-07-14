using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.Services;

namespace Zaide.ViewModels;

/// <summary>
/// Registers project workflow commands and projects structured output for the
/// Output surface. Does not start processes directly.
/// </summary>
public sealed class ProjectWorkflowViewModel : ReactiveObject, IDisposable
{
    private readonly IProjectWorkflowService _workflow;
    private readonly IProjectOutputService _outputService;
    private readonly IProjectContextService _projectContext;
    private readonly CompositeDisposable _subscriptions = new();
    private readonly Subject<Unit> _showOutputRequested = new();
    private ProjectWorkflowOperationState _state = ProjectWorkflowOperationState.Idle;
    private ProjectWorkflowOutcomeKind? _lastOutcome;
    private string? _statusMessage;
    private bool _isOperationActive;
    private string _cancelAutomationName = "Cancel build";
    private bool _disposed;

    /// <summary>
    /// Scheduler for output projection. Internal so tests can substitute a
    /// deterministic scheduler without a constructor parameter.
    /// </summary>
    internal System.Reactive.Concurrency.IScheduler Scheduler { get; set; }
        = AvaloniaScheduler.Instance;

    public ObservableCollection<OutputLineViewModel> Lines { get; } = new();

    public ProjectWorkflowOperationState State
    {
        get => _state;
        private set => this.RaiseAndSetIfChanged(ref _state, value);
    }

    public ProjectWorkflowOutcomeKind? LastOutcome
    {
        get => _lastOutcome;
        private set => this.RaiseAndSetIfChanged(ref _lastOutcome, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public bool IsOperationActive
    {
        get => _isOperationActive;
        private set => this.RaiseAndSetIfChanged(ref _isOperationActive, value);
    }

    /// <summary>
    /// Screen-reader name for Cancel on Output and Test Results (Build / Run / Test).
    /// </summary>
    public string CancelAutomationName
    {
        get => _cancelAutomationName;
        private set => this.RaiseAndSetIfChanged(ref _cancelAutomationName, value);
    }

    public ReactiveCommand<Unit, Unit> BuildCommand { get; }

    public ReactiveCommand<Unit, Unit> RunCommand { get; }

    public ReactiveCommand<Unit, Unit> TestCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    /// <summary>
    /// Raised when Build, Run, or Test enters
    /// <see cref="ProjectWorkflowOperationState.Starting"/> so hosts can reveal
    /// the Output panel before stdout streams.
    /// </summary>
    public IObservable<Unit> WhenShowOutputRequested => _showOutputRequested;

    private readonly Subject<Unit> _showTestResultsRequested = new();

    /// <summary>
    /// Raised when a test enters <see cref="ProjectWorkflowOperationState.Starting"/>
    /// so hosts can reveal the Test Results panel before completion.
    /// </summary>
    public IObservable<Unit> WhenShowTestResultsRequested => _showTestResultsRequested;

    public ProjectWorkflowViewModel(
        IProjectWorkflowService workflow,
        IProjectOutputService outputService,
        IProjectContextService projectContext,
        ICommandRegistry? commandRegistry = null)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        _outputService = outputService ?? throw new ArgumentNullException(nameof(outputService));
        _projectContext = projectContext ?? throw new ArgumentNullException(nameof(projectContext));

        var canBuild = Observable.CombineLatest(
            _projectContext.WhenChanged.StartWith(_projectContext.Current),
            _workflow.WhenChanged.StartWith(_workflow.Current),
            (context, snapshot) =>
                ProjectTargetResolver.IsEligible(context) &&
                snapshot.State is not ProjectWorkflowOperationState.Starting
                    and not ProjectWorkflowOperationState.Running);

        BuildCommand = ReactiveCommand.CreateFromTask(ExecuteBuildAsync, canBuild);

        var canRun = Observable.CombineLatest(
            _projectContext.WhenChanged.StartWith(_projectContext.Current),
            _workflow.WhenChanged.StartWith(_workflow.Current),
            (context, snapshot) =>
                ProjectTargetResolver.IsEligible(context) &&
                context.SelectedProject!.Kind == ProjectKind.CSharpProject &&
                snapshot.State is not ProjectWorkflowOperationState.Starting
                    and not ProjectWorkflowOperationState.Running);

        RunCommand = ReactiveCommand.CreateFromTask(ExecuteRunAsync, canRun);

        var canTest = Observable.CombineLatest(
            _projectContext.WhenChanged.StartWith(_projectContext.Current),
            _workflow.WhenChanged.StartWith(_workflow.Current),
            (context, snapshot) =>
                ProjectTargetResolver.IsEligible(context) &&
                snapshot.State is not ProjectWorkflowOperationState.Starting
                    and not ProjectWorkflowOperationState.Running);

        TestCommand = ReactiveCommand.CreateFromTask(ExecuteTestAsync, canTest);

        var canCancel = _workflow.WhenChanged
            .StartWith(_workflow.Current)
            .Select(snapshot =>
                snapshot.State is ProjectWorkflowOperationState.Starting
                    or ProjectWorkflowOperationState.Running);

        CancelCommand = ReactiveCommand.CreateFromTask(ExecuteCancelAsync, canCancel);

        commandRegistry?.Register(new CommandDescriptor(
            "project.build", "Build", "Project", new[] { "Ctrl+Shift+B" }, BuildCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "project.run", "Run", "Project", new[] { "Ctrl+F5" }, RunCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "project.test", "Run Tests", "Project", Array.Empty<string>(), TestCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "project.cancel", "Cancel Build/Run/Test", "Project", Array.Empty<string>(), CancelCommand));

    }

    /// <summary>
    /// Starts projecting structured output. Safe to call once; subsequent calls are no-ops.
    /// </summary>
    public void Activate()
    {
        if (_disposed || _subscriptions.Count > 0)
            return;

        ApplyStateOnly(_outputService.Current);

        _subscriptions.Add(
            _outputService.WhenChanged
                .ObserveOn(Scheduler)
                .Subscribe(ApplyStateOnly));

        _subscriptions.Add(
            _outputService.WhenLineReceived
                .ObserveOn(Scheduler)
                .Subscribe(OnLineReceived));

        _subscriptions.Add(
            _workflow.WhenChanged
                .Where(snapshot =>
                    snapshot.State == ProjectWorkflowOperationState.Starting &&
                    snapshot.ActiveOperation is ProjectWorkflowOperation.Build
                        or ProjectWorkflowOperation.Run
                        or ProjectWorkflowOperation.Test)
                .ObserveOn(Scheduler)
                .Subscribe(_ => _showOutputRequested.OnNext(Unit.Default)));

        _subscriptions.Add(
            _workflow.WhenChanged
                .Where(snapshot =>
                    snapshot.State == ProjectWorkflowOperationState.Starting &&
                    snapshot.ActiveOperation == ProjectWorkflowOperation.Test)
                .ObserveOn(Scheduler)
                .Subscribe(_ => _showTestResultsRequested.OnNext(Unit.Default)));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _subscriptions.Dispose();
        _showOutputRequested.OnCompleted();
        _showOutputRequested.Dispose();
        _showTestResultsRequested.OnCompleted();
        _showTestResultsRequested.Dispose();
    }

    private Task ExecuteBuildAsync() =>
        _workflow.StartBuildAsync();

    private Task ExecuteRunAsync() =>
        _workflow.StartRunAsync();

    private Task ExecuteTestAsync() =>
        _workflow.StartTestAsync();

    private Task ExecuteCancelAsync() => _workflow.CancelAsync();

    private void ApplyStateOnly(ProjectOutputSnapshot snapshot)
    {
        State = snapshot.State;
        LastOutcome = snapshot.LastOutcome;
        IsOperationActive = snapshot.State is ProjectWorkflowOperationState.Starting
            or ProjectWorkflowOperationState.Running;
        StatusMessage = ProjectWorkflowStatusPolicy.MapOutputStatusMessage(snapshot);
        CancelAutomationName = ProjectWorkflowStatusPolicy.MapCancelAutomationName(snapshot);

        if (snapshot.State == ProjectWorkflowOperationState.Starting)
            Lines.Clear();
    }

    private void OnLineReceived(ManagedProcessOutputLine line)
    {
        Lines.Add(new OutputLineViewModel(line));
    }
}
