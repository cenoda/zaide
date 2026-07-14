using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Zaide.Services;

namespace Zaide.ViewModels;

/// <summary>
/// Registers debug execution commands and dispatches F5 start/continue without
/// owning DAP or process logic.
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

    public DebugSessionViewModel(
        IProjectDebugLaunchService debugLaunch,
        IDebugSessionService debugSession,
        ICommandRegistry? commandRegistry = null)
    {
        _debugLaunch = debugLaunch ?? throw new ArgumentNullException(nameof(debugLaunch));
        _debugSession = debugSession ?? throw new ArgumentNullException(nameof(debugSession));

        var canStartOrContinue = _debugSession.WhenChanged
            .StartWith(_debugSession.Current)
            .Select(snapshot => snapshot.State is DebugSessionState.Idle
                or DebugSessionState.Failed
                or DebugSessionState.Unavailable
                or DebugSessionState.Stopped);

        StartOrContinueCommand = ReactiveCommand.CreateFromTask(
            ExecuteStartOrContinueAsync,
            canStartOrContinue);

        commandRegistry?.Register(new CommandDescriptor(
            "debug.startOrContinue",
            "Start Debugging / Continue",
            "Debug",
            new[] { "F5" },
            StartOrContinueCommand));
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