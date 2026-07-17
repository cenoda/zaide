using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Zaide.Services;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Application;

namespace Zaide.Features.Debugging.Presentation;

/// <summary>
/// Registers debug execution commands and dispatches start/continue/pause/stop/step
/// without owning DAP or process logic.
/// </summary>
public sealed class DebugSessionViewModel : ReactiveObject, IDisposable
{
    private readonly IProjectDebugLaunchService _debugLaunch;
    private readonly IDebugSessionService _debugSession;
    private readonly CompositeDisposable _subscriptions = new();
    private DebugSessionState _state = DebugSessionState.Idle;
    private string? _statusMessage;
    private bool _disposed;

    /// <summary>
    /// Delegate that saves every dirty open editor tab before debug start.
    /// Set by the composition root after construction.
    /// </summary>
    internal Func<Task<bool>>? SaveAllDirtyTabsAsync { get; set; }

    public DebugSessionState State
    {
        get => _state;
        private set => this.RaiseAndSetIfChanged(ref _state, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public ReactiveCommand<Unit, Unit> StartOrContinueCommand { get; }
    public ReactiveCommand<Unit, Unit> PauseCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> StepOverCommand { get; }
    public ReactiveCommand<Unit, Unit> StepIntoCommand { get; }
    public ReactiveCommand<Unit, Unit> StepOutCommand { get; }

    public DebugSessionViewModel(
        IProjectDebugLaunchService debugLaunch,
        IDebugSessionService debugSession,
        ICommandRegistry? commandRegistry = null)
    {
        _debugLaunch = debugLaunch ?? throw new ArgumentNullException(nameof(debugLaunch));
        _debugSession = debugSession ?? throw new ArgumentNullException(nameof(debugSession));

        var sessionChanges = _debugSession.WhenChanged.StartWith(_debugSession.Current);

        var canStartOrContinue = sessionChanges
            .Select(snapshot => snapshot.State is DebugSessionState.Idle
                or DebugSessionState.Failed
                or DebugSessionState.Unavailable
                or DebugSessionState.Stopped);

        var canPause = sessionChanges
            .Select(snapshot => snapshot.State == DebugSessionState.Running);

        var canStop = sessionChanges
            .Select(snapshot => snapshot.State is DebugSessionState.Starting
                or DebugSessionState.Running
                or DebugSessionState.Stopped);

        var canStep = sessionChanges
            .Select(snapshot => snapshot.State == DebugSessionState.Stopped &&
                                snapshot.StopInfo?.ThreadId is not null);

        StartOrContinueCommand = ReactiveCommand.CreateFromTask(
            ExecuteStartOrContinueAsync,
            canStartOrContinue);
        PauseCommand = ReactiveCommand.CreateFromTask(ExecutePauseAsync, canPause);
        StopCommand = ReactiveCommand.CreateFromTask(ExecuteStopAsync, canStop);
        StepOverCommand = ReactiveCommand.CreateFromTask(ExecuteStepOverAsync, canStep);
        StepIntoCommand = ReactiveCommand.CreateFromTask(ExecuteStepIntoAsync, canStep);
        StepOutCommand = ReactiveCommand.CreateFromTask(ExecuteStepOutAsync, canStep);

        commandRegistry?.Register(new CommandDescriptor(
            "debug.startOrContinue",
            "Start Debugging / Continue",
            "Debug",
            new[] { "F5" },
            StartOrContinueCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "debug.pause",
            "Pause",
            "Debug",
            Array.Empty<string>(),
            PauseCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "debug.stop",
            "Stop Debugging",
            "Debug",
            new[] { "Shift+F5" },
            StopCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "debug.stepOver",
            "Step Over",
            "Debug",
            new[] { "F10" },
            StepOverCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "debug.stepInto",
            "Step Into",
            "Debug",
            new[] { "F11" },
            StepIntoCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "debug.stepOut",
            "Step Out",
            "Debug",
            new[] { "Shift+F11" },
            StepOutCommand));
    }

    /// <summary>
    /// Starts projecting debug session state. Safe to call once.
    /// </summary>
    public void Activate()
    {
        if (_disposed || _subscriptions.Count > 0)
            return;

        ApplySnapshot(_debugSession.Current);
        _subscriptions.Add(
            _debugSession.WhenChanged.Subscribe(ApplySnapshot));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _subscriptions.Dispose();
    }

    private async Task ExecuteStartOrContinueAsync()
    {
        var snapshot = _debugSession.Current;
        if (snapshot.State == DebugSessionState.Stopped)
        {
            var threadId = snapshot.StopInfo?.ThreadId;
            if (threadId is null)
            {
                StatusMessage = "Continue is unavailable without a stopped thread.";
                return;
            }

            var result = await _debugSession.ContinueAsync(threadId.Value).ConfigureAwait(false);
            if (!result.Succeeded)
                StatusMessage = result.Message;
            return;
        }

        if (!await EnsureDirtyTabsSavedAsync())
            return;

        var start = await _debugLaunch.StartDebuggingAsync().ConfigureAwait(false);
        if (!start.Succeeded)
            StatusMessage = start.Message;
    }

    private async Task ExecutePauseAsync()
    {
        var result = await _debugSession.PauseAsync().ConfigureAwait(false);
        if (!result.Succeeded)
            StatusMessage = result.Message;
    }

    private async Task ExecuteStopAsync()
    {
        var result = await _debugSession.StopAsync().ConfigureAwait(false);
        if (!result.Succeeded)
            StatusMessage = result.Message;
    }

    private async Task ExecuteStepOverAsync()
    {
        var result = await _debugSession.StepOverAsync().ConfigureAwait(false);
        if (!result.Succeeded)
            StatusMessage = result.Message;
    }

    private async Task ExecuteStepIntoAsync()
    {
        var result = await _debugSession.StepIntoAsync().ConfigureAwait(false);
        if (!result.Succeeded)
            StatusMessage = result.Message;
    }

    private async Task ExecuteStepOutAsync()
    {
        var result = await _debugSession.StepOutAsync().ConfigureAwait(false);
        if (!result.Succeeded)
            StatusMessage = result.Message;
    }

    private async Task<bool> EnsureDirtyTabsSavedAsync()
    {
        if (SaveAllDirtyTabsAsync is null)
            return true;

        return await SaveAllDirtyTabsAsync().ConfigureAwait(false);
    }

    private void ApplySnapshot(DebugSessionSnapshot snapshot)
    {
        State = snapshot.State;
        if (snapshot.Failure is not null)
            StatusMessage = snapshot.Failure.Message;
        else if (snapshot.State == DebugSessionState.Idle)
            StatusMessage = null;
    }
}