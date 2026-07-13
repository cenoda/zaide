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

    public ReactiveCommand<Unit, Unit> BuildCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    /// <summary>
    /// Raised when a build starts successfully so hosts can reveal the Output panel.
    /// </summary>
    public IObservable<Unit> WhenShowOutputRequested => _showOutputRequested;

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

        var canCancel = _workflow.WhenChanged
            .StartWith(_workflow.Current)
            .Select(snapshot =>
                snapshot.State is ProjectWorkflowOperationState.Starting
                    or ProjectWorkflowOperationState.Running);

        CancelCommand = ReactiveCommand.CreateFromTask(ExecuteCancelAsync, canCancel);

        commandRegistry?.Register(new CommandDescriptor(
            "project.build", "Build", "Project", new[] { "Ctrl+Shift+B" }, BuildCommand));
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

        ApplySnapshot(_outputService.Current);

        _subscriptions.Add(
            _outputService.WhenChanged
                .ObserveOn(Scheduler)
                .Subscribe(ApplySnapshot));
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
    }

    private async Task ExecuteBuildAsync()
    {
        var result = await _workflow.StartBuildAsync().ConfigureAwait(true);
        if (result.Outcome is ProjectWorkflowOutcomeKind.RejectedConcurrent
            or ProjectWorkflowOutcomeKind.RejectedContext)
        {
            return;
        }

        _showOutputRequested.OnNext(Unit.Default);
    }

    private Task ExecuteCancelAsync() => _workflow.CancelAsync();

    private void ApplySnapshot(ProjectOutputSnapshot snapshot)
    {
        State = snapshot.State;
        LastOutcome = snapshot.LastOutcome;
        IsOperationActive = snapshot.State is ProjectWorkflowOperationState.Starting
            or ProjectWorkflowOperationState.Running;
        StatusMessage = ProjectWorkflowStatusPolicy.MapOutputStatusMessage(snapshot);

        Lines.Clear();
        foreach (var line in snapshot.Lines)
            Lines.Add(new OutputLineViewModel(line));
    }
}
