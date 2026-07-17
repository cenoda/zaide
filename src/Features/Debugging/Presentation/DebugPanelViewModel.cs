using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI;
using Zaide.Services;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Application;

namespace Zaide.Features.Debugging.Presentation;

/// <summary>
/// Projects debug-session diagnostics, call stack, and variables into the Debug
/// bottom panel. Does not own DAP or process logic.
/// </summary>
public sealed class DebugPanelViewModel : ReactiveObject, IDisposable
{
    private readonly IDebugSessionService _debugSession;
    private readonly CompositeDisposable _subscriptions = new();
    private readonly Subject<Unit> _showDebugRequested = new();
    private DebugSessionState _state = DebugSessionState.Idle;
    private string? _statusMessage;
    private int _projectedDiagnosticCount;
    private DebugSessionState? _lastAnnouncedState;
    private bool _disposed;

    public DebugPanelViewModel(
        IDebugSessionService debugSession,
        DebugStackProjectionViewModel stackProjection)
    {
        _debugSession = debugSession ?? throw new ArgumentNullException(nameof(debugSession));
        StackProjection = stackProjection ?? throw new ArgumentNullException(nameof(stackProjection));
    }

    public DebugStackProjectionViewModel StackProjection { get; }

    public ObservableCollection<DebugConsoleLineViewModel> Lines { get; } = new();

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

    /// <summary>
    /// Raised when the UI should show the Debug bottom panel.
    /// </summary>
    public IObservable<Unit> WhenShowDebugRequested => _showDebugRequested;

    /// <summary>
    /// Starts projecting debug session output. Safe to call once.
    /// </summary>
    public void Activate()
    {
        if (_disposed || _subscriptions.Count > 0)
            return;

        StackProjection.Activate();
        ApplySnapshot(_debugSession.Current, isInitial: true);
        _subscriptions.Add(_debugSession.WhenChanged.Subscribe(snapshot => ApplySnapshot(snapshot)));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _subscriptions.Dispose();
        StackProjection.Dispose();
        _showDebugRequested.OnCompleted();
        _showDebugRequested.Dispose();
    }

    private void ApplySnapshot(DebugSessionSnapshot snapshot, bool isInitial = false)
    {
        var previousState = State;
        State = snapshot.State;
        StatusMessage = snapshot.Failure?.Message;

        if (!isInitial && previousState != snapshot.State)
        {
            if (snapshot.State == DebugSessionState.Starting &&
                previousState is DebugSessionState.Idle or DebugSessionState.Failed)
            {
                _showDebugRequested.OnNext(Unit.Default);
            }

            AppendStateTransition(snapshot);
        }
        else if (isInitial && snapshot.State == DebugSessionState.Starting)
        {
            _showDebugRequested.OnNext(Unit.Default);
            AppendStateTransition(snapshot);
        }

        AppendNewDiagnostics(snapshot.DiagnosticOutput);
        _lastAnnouncedState = snapshot.State;
    }

    private void AppendStateTransition(DebugSessionSnapshot snapshot)
    {
        if (_lastAnnouncedState == snapshot.State)
            return;

        var message = snapshot.State switch
        {
            DebugSessionState.Starting => "Debug session starting.",
            DebugSessionState.Running => "Debuggee running.",
            DebugSessionState.Stopped => FormatStoppedMessage(snapshot),
            DebugSessionState.Stopping => "Stopping debug session.",
            DebugSessionState.Failed => snapshot.Failure is null
                ? "Debug session failed."
                : $"Debug session failed: {snapshot.Failure.Message}",
            DebugSessionState.Idle when _lastAnnouncedState is not null and not DebugSessionState.Idle
                and not DebugSessionState.Unavailable =>
                "Debug session ended.",
            _ => null,
        };

        if (message is not null)
            AppendLine(message, snapshot.Failure is not null ? DebugConsoleLineKind.Error : DebugConsoleLineKind.Info);
    }

    private static string FormatStoppedMessage(DebugSessionSnapshot snapshot)
    {
        var reason = snapshot.StopInfo?.Reason;
        return reason is null or { Length: 0 }
            ? "Debuggee stopped."
            : $"Debuggee stopped ({reason}).";
    }

    private void AppendNewDiagnostics(IReadOnlyList<string> diagnostics)
    {
        while (_projectedDiagnosticCount < diagnostics.Count)
        {
            var text = diagnostics[_projectedDiagnosticCount];
            _projectedDiagnosticCount++;
            var kind = text.StartsWith("[error]", StringComparison.Ordinal)
                ? DebugConsoleLineKind.Error
                : DebugConsoleLineKind.Output;
            AppendLine(text, kind);
        }
    }

    private void AppendLine(string text, DebugConsoleLineKind kind)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Lines.Add(new DebugConsoleLineViewModel(text, kind));
    }
}