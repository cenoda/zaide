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
    private readonly IProjectOperationGate _operationGate;
    private readonly IDebugSessionService _debugSession;
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

    /// <summary>
    /// Delegate that saves every dirty open editor tab before Build, Run, or
    /// Test. Set by the composition root after construction. When null (tests
    /// that don't wire it), the save guard is skipped so existing command
    /// tests are unaffected.
    /// </summary>
    internal Func<Task<bool>>? SaveAllDirtyTabsAsync { get; set; }

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
        IProjectOperationGate operationGate,
        IDebugSessionService debugSession,
        ICommandRegistry? commandRegistry = null)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        _outputService = outputService ?? throw new ArgumentNullException(nameof(outputService));
        _projectContext = projectContext ?? throw new ArgumentNullException(nameof(projectContext));
        _operationGate = operationGate ?? throw new ArgumentNullException(nameof(operationGate));
        _debugSession = debugSession ?? throw new ArgumentNullException(nameof(debugSession));

        var canBuild = Observable.CombineLatest(
            _projectContext.WhenChanged.StartWith(_projectContext.Current),
            _workflow.WhenChanged.StartWith(_workflow.Current),
            _debugSession.WhenChanged.StartWith(_debugSession.Current),
            (context, snapshot, debug) =>
                ProjectTargetResolver.IsEligible(context) &&
                snapshot.State is not ProjectWorkflowOperationState.Starting
                    and not ProjectWorkflowOperationState.Running &&
                !IsWorkflowBlockedByDebug(debug) &&
                !_operationGate.IsDebugHandoffActive);

        BuildCommand = ReactiveCommand.CreateFromTask(ExecuteBuildAsync, canBuild);

        var canRun = Observable.CombineLatest(
            _projectContext.WhenChanged.StartWith(_projectContext.Current),
            _workflow.WhenChanged.StartWith(_workflow.Current),
            _debugSession.WhenChanged.StartWith(_debugSession.Current),
            (context, snapshot, debug) =>
                ProjectTargetResolver.IsEligible(context) &&
                context.SelectedProject!.Kind == ProjectKind.CSharpProject &&
                snapshot.State is not ProjectWorkflowOperationState.Starting
                    and not ProjectWorkflowOperationState.Running &&
                !IsWorkflowBlockedByDebug(debug) &&
                !_operationGate.IsDebugHandoffActive);

        RunCommand = ReactiveCommand.CreateFromTask(ExecuteRunAsync, canRun);

        var canTest = Observable.CombineLatest(
            _projectContext.WhenChanged.StartWith(_projectContext.Current),
            _workflow.WhenChanged.StartWith(_workflow.Current),
            _debugSession.WhenChanged.StartWith(_debugSession.Current),
            (context, snapshot, debug) =>
                ProjectTargetResolver.IsEligible(context) &&
                snapshot.State is not ProjectWorkflowOperationState.Starting
                    and not ProjectWorkflowOperationState.Running &&
                !IsWorkflowBlockedByDebug(debug) &&
                !_operationGate.IsDebugHandoffActive);

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
            "project.cancel",
            "Cancel Build/Run/Test",
            "Project",
            new[] { "Ctrl+F2" },
            CancelCommand));

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

    private async Task ExecuteBuildAsync()
    {
        if (!await EnsureDirtyTabsSavedAsync())
            return;
        await _workflow.StartBuildAsync();
    }

    private async Task ExecuteRunAsync()
    {
        if (!await EnsureDirtyTabsSavedAsync())
            return;
        await _workflow.StartRunAsync();
    }

    private async Task ExecuteTestAsync()
    {
        if (!await EnsureDirtyTabsSavedAsync())
            return;
        await _workflow.StartTestAsync();
    }

    /// <summary>
    /// Saves all dirty editor tabs (via the delegate wired by the composition
    /// root). Returns true when every dirty tab was saved or the delegate is
    /// not configured. Returns false when a save failed, which prevents the
    /// workflow from starting.
    /// </summary>
    private async Task<bool> EnsureDirtyTabsSavedAsync()
    {
        if (SaveAllDirtyTabsAsync is null)
            return true;

        return await SaveAllDirtyTabsAsync();
    }

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

    private static bool IsWorkflowBlockedByDebug(DebugSessionSnapshot snapshot) =>
        snapshot.State is DebugSessionState.Starting
            or DebugSessionState.Running
            or DebugSessionState.Stopped
            or DebugSessionState.Stopping;
}
