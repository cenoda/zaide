using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using Zaide.Services;

namespace Zaide.ViewModels;

/// <summary>
/// Projects stopped-state threads, call stack, scopes, and first-level variables
/// from live DAP inspection requests. Does not own adapter or process logic.
/// </summary>
public sealed class DebugStackProjectionViewModel : ReactiveObject, IDisposable
{
    private readonly IDebugSessionService _debugSession;
    private readonly CompositeDisposable _subscriptions = new();
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private long _stoppedLoadToken;
    private long _threadSelectionToken;
    private long _frameSelectionToken;
    private long _scopeSelectionToken;
    private DebugProjectionState _callStackState = DebugProjectionState.Unavailable;
    private DebugProjectionState _variablesState = DebugProjectionState.Unavailable;
    private string _callStackStatusText = "Call stack is unavailable without an active debug session.";
    private string _variablesStatusText = "Variables are unavailable without an active debug session.";
    private DebugThreadViewModel? _selectedThread;
    private DebugStackFrameViewModel? _selectedFrame;
    private DebugScopeViewModel? _selectedScope;
    private bool _disposed;

    public DebugStackProjectionViewModel(IDebugSessionService debugSession)
    {
        _debugSession = debugSession ?? throw new ArgumentNullException(nameof(debugSession));

        SelectThreadCommand = ReactiveCommand.Create<DebugThreadViewModel>(SelectThread);
        SelectFrameCommand = ReactiveCommand.Create<DebugStackFrameViewModel>(SelectFrame);
        SelectScopeCommand = ReactiveCommand.Create<DebugScopeViewModel>(SelectScope);
    }

    public ObservableCollection<DebugThreadViewModel> Threads { get; } = new();

    public ObservableCollection<DebugStackFrameViewModel> Frames { get; } = new();

    public ObservableCollection<DebugScopeViewModel> Scopes { get; } = new();

    public ObservableCollection<DebugVariableViewModel> Variables { get; } = new();

    public DebugProjectionState CallStackState
    {
        get => _callStackState;
        private set => this.RaiseAndSetIfChanged(ref _callStackState, value);
    }

    public DebugProjectionState VariablesState
    {
        get => _variablesState;
        private set => this.RaiseAndSetIfChanged(ref _variablesState, value);
    }

    public string CallStackStatusText
    {
        get => _callStackStatusText;
        private set => this.RaiseAndSetIfChanged(ref _callStackStatusText, value);
    }

    public string VariablesStatusText
    {
        get => _variablesStatusText;
        private set => this.RaiseAndSetIfChanged(ref _variablesStatusText, value);
    }

    public DebugThreadViewModel? SelectedThread
    {
        get => _selectedThread;
        private set => this.RaiseAndSetIfChanged(ref _selectedThread, value);
    }

    public DebugStackFrameViewModel? SelectedFrame
    {
        get => _selectedFrame;
        private set => this.RaiseAndSetIfChanged(ref _selectedFrame, value);
    }

    public DebugScopeViewModel? SelectedScope
    {
        get => _selectedScope;
        private set => this.RaiseAndSetIfChanged(ref _selectedScope, value);
    }

    public ReactiveCommand<DebugThreadViewModel, Unit> SelectThreadCommand { get; }

    public ReactiveCommand<DebugStackFrameViewModel, Unit> SelectFrameCommand { get; }

    public ReactiveCommand<DebugScopeViewModel, Unit> SelectScopeCommand { get; }

    /// <summary>
    /// Starts projecting stopped-state inspection data. Safe to call once.
    /// </summary>
    public void Activate()
    {
        if (_disposed || _subscriptions.Count > 0)
            return;

        ApplySnapshot(_debugSession.Current);
        _subscriptions.Add(_debugSession.WhenChanged.Subscribe(ApplySnapshot));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _subscriptions.Dispose();
        _loadGate.Dispose();
    }

    private void ApplySnapshot(DebugSessionSnapshot snapshot)
    {
        if (snapshot.State == DebugSessionState.Stopped)
        {
            ObserveTask(LoadStoppedStateAsync(snapshot));
            return;
        }

        ClearProjection(ResolveUnavailableStatus(snapshot));
    }

    private void SelectThread(DebugThreadViewModel thread)
    {
        if (thread is null || ReferenceEquals(SelectedThread, thread))
            return;

        SelectedThread = thread;
        Interlocked.Increment(ref _threadSelectionToken);
        ObserveTask(LoadFramesForSelectedThreadAsync(_threadSelectionToken));
    }

    private void SelectFrame(DebugStackFrameViewModel frame)
    {
        if (frame is null || ReferenceEquals(SelectedFrame, frame))
            return;

        SelectedFrame = frame;
        Interlocked.Increment(ref _frameSelectionToken);
        ObserveTask(LoadScopesForSelectedFrameAsync(_frameSelectionToken));
    }

    private void SelectScope(DebugScopeViewModel scope)
    {
        if (scope is null || ReferenceEquals(SelectedScope, scope))
            return;

        SelectedScope = scope;
        Interlocked.Increment(ref _scopeSelectionToken);
        ObserveTask(LoadVariablesForSelectedScopeAsync(_scopeSelectionToken));
    }

    private async Task LoadStoppedStateAsync(DebugSessionSnapshot snapshot)
    {
        var loadToken = Interlocked.Increment(ref _stoppedLoadToken);

        await _loadGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed || !IsStoppedSnapshotCurrent(snapshot, loadToken))
                return;

            SetCallStackLoading();
            SetVariablesUnavailable("Select a stopped frame to inspect variables.");
            ClearThreadFrameScopeCollections();
        }
        finally
        {
            _loadGate.Release();
        }

        IReadOnlyList<DapThreadInfo> threads;
        try
        {
            var response = await _debugSession.RequestThreadsAsync().ConfigureAwait(false);
            if (!IsStoppedSnapshotCurrent(snapshot, loadToken))
                return;

            threads = DapInspectionParser.ParseThreads(response);
        }
        catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException)
        {
            if (!IsStoppedSnapshotCurrent(snapshot, loadToken))
                return;

            SetCallStackError("Call stack request failed.");
            SetVariablesUnavailable("Variables are unavailable after a call stack error.");
            return;
        }

        if (!IsStoppedSnapshotCurrent(snapshot, loadToken))
            return;

        if (threads.Count == 0)
        {
            SetCallStackEmpty("Stopped, but the adapter returned no threads.");
            SetVariablesUnavailable("Variables are unavailable without a call stack frame.");
            return;
        }

        ReplaceThreads(threads);
        var preferredThreadId = snapshot.StopInfo?.ThreadId;
        var initialThread = threads.FirstOrDefault(thread => thread.Id == preferredThreadId) ?? threads[0];
        SelectedThread = FindThread(initialThread.Id);
        Interlocked.Increment(ref _threadSelectionToken);
        await LoadFramesForSelectedThreadAsync(_threadSelectionToken, snapshot, loadToken).ConfigureAwait(false);
    }

    private async Task LoadFramesForSelectedThreadAsync(long selectionToken)
    {
        var snapshot = _debugSession.Current;
        if (snapshot.State != DebugSessionState.Stopped)
            return;

        await LoadFramesForSelectedThreadAsync(selectionToken, snapshot, _stoppedLoadToken)
            .ConfigureAwait(false);
    }

    private async Task LoadFramesForSelectedThreadAsync(
        long selectionToken,
        DebugSessionSnapshot snapshot,
        long loadToken)
    {
        var thread = SelectedThread;
        if (thread is null)
            return;

        await _loadGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed ||
                selectionToken != _threadSelectionToken ||
                !IsStoppedSnapshotCurrent(snapshot, loadToken))
            {
                return;
            }

            SetCallStackLoading();
            ClearFrameScopeVariableCollections();
            SetVariablesUnavailable("Loading scopes for the selected frame.");
        }
        finally
        {
            _loadGate.Release();
        }

        IReadOnlyList<DapStackFrameInfo> frames;
        try
        {
            var response = await _debugSession.RequestStackTraceAsync(thread.Id).ConfigureAwait(false);
            if (selectionToken != _threadSelectionToken ||
                !IsStoppedSnapshotCurrent(snapshot, loadToken))
            {
                return;
            }

            frames = DapInspectionParser.ParseStackFrames(response);
        }
        catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException)
        {
            if (selectionToken != _threadSelectionToken ||
                !IsStoppedSnapshotCurrent(snapshot, loadToken))
            {
                return;
            }

            SetCallStackError("Call stack request failed.");
            SetVariablesUnavailable("Variables are unavailable after a call stack error.");
            return;
        }

        if (selectionToken != _threadSelectionToken ||
            !IsStoppedSnapshotCurrent(snapshot, loadToken))
        {
            return;
        }

        if (frames.Count == 0)
        {
            SetCallStackEmpty("Stopped, but the adapter returned no stack frames.");
            SetVariablesUnavailable("Variables are unavailable without a call stack frame.");
            return;
        }

        ReplaceFrames(frames);
        SelectedFrame = Frames[0];
        Interlocked.Increment(ref _frameSelectionToken);
        await LoadScopesForSelectedFrameAsync(_frameSelectionToken, snapshot, loadToken)
            .ConfigureAwait(false);
    }

    private async Task LoadScopesForSelectedFrameAsync(long selectionToken)
    {
        var snapshot = _debugSession.Current;
        if (snapshot.State != DebugSessionState.Stopped)
            return;

        await LoadScopesForSelectedFrameAsync(selectionToken, snapshot, _stoppedLoadToken)
            .ConfigureAwait(false);
    }

    private async Task LoadScopesForSelectedFrameAsync(
        long selectionToken,
        DebugSessionSnapshot snapshot,
        long loadToken)
    {
        var frame = SelectedFrame;
        if (frame is null)
            return;

        await _loadGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed || !IsFrameSelectionCurrent(selectionToken, frame.Id, snapshot, loadToken))
                return;

            SetVariablesLoading();
            ClearScopeVariableCollections();
        }
        finally
        {
            _loadGate.Release();
        }

        IReadOnlyList<DapScopeInfo> scopes;
        try
        {
            var response = await _debugSession.RequestScopesAsync(frame.Id).ConfigureAwait(false);
            if (!IsFrameSelectionCurrent(selectionToken, frame.Id, snapshot, loadToken))
                return;

            scopes = DapInspectionParser.ParseScopes(response);
        }
        catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException)
        {
            if (!IsFrameSelectionCurrent(selectionToken, frame.Id, snapshot, loadToken))
                return;

            SetVariablesError("Scope request failed.");
            return;
        }

        if (!IsFrameSelectionCurrent(selectionToken, frame.Id, snapshot, loadToken))
            return;

        if (scopes.Count == 0)
        {
            SetVariablesEmpty("The selected frame has no scopes.");
            return;
        }

        ReplaceScopes(scopes);
        SelectedScope = Scopes[0];
        Interlocked.Increment(ref _scopeSelectionToken);
        await LoadVariablesForSelectedScopeAsync(_scopeSelectionToken, snapshot, loadToken)
            .ConfigureAwait(false);
    }

    private async Task LoadVariablesForSelectedScopeAsync(long selectionToken)
    {
        var snapshot = _debugSession.Current;
        if (snapshot.State != DebugSessionState.Stopped)
            return;

        await LoadVariablesForSelectedScopeAsync(selectionToken, snapshot, _stoppedLoadToken)
            .ConfigureAwait(false);
    }

    private async Task LoadVariablesForSelectedScopeAsync(
        long selectionToken,
        DebugSessionSnapshot snapshot,
        long loadToken)
    {
        var scope = SelectedScope;
        if (scope is null)
            return;

        await _loadGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed ||
                !IsScopeSelectionCurrent(selectionToken, scope.VariablesReference, snapshot, loadToken))
                return;

            SetVariablesLoading();
            Variables.Clear();
        }
        finally
        {
            _loadGate.Release();
        }

        IReadOnlyList<DapVariableInfo> variables;
        try
        {
            var response = await _debugSession
                .RequestVariablesAsync(scope.VariablesReference)
                .ConfigureAwait(false);

            if (!IsScopeSelectionCurrent(selectionToken, scope.VariablesReference, snapshot, loadToken))
                return;

            variables = DapInspectionParser.ParseVariables(response);
        }
        catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException)
        {
            if (!IsScopeSelectionCurrent(selectionToken, scope.VariablesReference, snapshot, loadToken))
                return;

            SetVariablesError("Variable request failed.");
            return;
        }

        if (!IsScopeSelectionCurrent(selectionToken, scope.VariablesReference, snapshot, loadToken))
            return;

        if (variables.Count == 0)
        {
            SetVariablesEmpty($"Scope '{scope.Name}' has no variables.");
            return;
        }

        ReplaceVariables(variables);
        VariablesState = DebugProjectionState.Ready;
        VariablesStatusText = string.Empty;
    }

    private bool IsStoppedSnapshotCurrent(DebugSessionSnapshot snapshot, long loadToken)
    {
        if (_disposed || loadToken != _stoppedLoadToken)
            return false;

        var current = _debugSession.Current;
        return current.State == DebugSessionState.Stopped &&
               current.Generation == snapshot.Generation;
    }

    private bool IsFrameSelectionCurrent(
        long selectionToken,
        int frameId,
        DebugSessionSnapshot snapshot,
        long loadToken) =>
        selectionToken == _frameSelectionToken &&
        SelectedFrame?.Id == frameId &&
        IsStoppedSnapshotCurrent(snapshot, loadToken);

    private bool IsScopeSelectionCurrent(
        long selectionToken,
        int variablesReference,
        DebugSessionSnapshot snapshot,
        long loadToken) =>
        selectionToken == _scopeSelectionToken &&
        SelectedScope?.VariablesReference == variablesReference &&
        IsStoppedSnapshotCurrent(snapshot, loadToken);

    private void ClearProjection((string CallStack, string Variables) status)
    {
        Interlocked.Increment(ref _stoppedLoadToken);
        Interlocked.Increment(ref _threadSelectionToken);
        Interlocked.Increment(ref _frameSelectionToken);
        Interlocked.Increment(ref _scopeSelectionToken);

        Threads.Clear();
        Frames.Clear();
        Scopes.Clear();
        Variables.Clear();
        SelectedThread = null;
        SelectedFrame = null;
        SelectedScope = null;
        CallStackState = DebugProjectionState.Unavailable;
        VariablesState = DebugProjectionState.Unavailable;
        CallStackStatusText = status.CallStack;
        VariablesStatusText = status.Variables;
    }

    private static (string CallStack, string Variables) ResolveUnavailableStatus(DebugSessionSnapshot snapshot)
    {
        return snapshot.State switch
        {
            DebugSessionState.Running => (
                "Call stack is unavailable while the debuggee is running.",
                "Variables are unavailable while the debuggee is running."),
            DebugSessionState.Starting or DebugSessionState.Stopping => (
                "Call stack is unavailable during session transitions.",
                "Variables are unavailable during session transitions."),
            DebugSessionState.Failed => (
                "Call stack is unavailable after a debug session failure.",
                "Variables are unavailable after a debug session failure."),
            DebugSessionState.Idle or DebugSessionState.Unavailable => (
                "Call stack is unavailable without an active debug session.",
                "Variables are unavailable without an active debug session."),
            _ => (
                "Call stack is unavailable.",
                "Variables are unavailable."),
        };
    }

    private void SetCallStackLoading()
    {
        CallStackState = DebugProjectionState.Loading;
        CallStackStatusText = "Loading call stack.";
    }

    private void SetCallStackEmpty(string message)
    {
        Frames.Clear();
        SelectedFrame = null;
        CallStackState = DebugProjectionState.Empty;
        CallStackStatusText = message;
    }

    private void SetCallStackError(string message)
    {
        Frames.Clear();
        SelectedFrame = null;
        CallStackState = DebugProjectionState.Error;
        CallStackStatusText = message;
    }

    private void SetVariablesLoading()
    {
        VariablesState = DebugProjectionState.Loading;
        VariablesStatusText = "Loading variables.";
    }

    private void SetVariablesUnavailable(string message)
    {
        Scopes.Clear();
        Variables.Clear();
        SelectedScope = null;
        VariablesState = DebugProjectionState.Unavailable;
        VariablesStatusText = message;
    }

    private void SetVariablesEmpty(string message)
    {
        Variables.Clear();
        VariablesState = DebugProjectionState.Empty;
        VariablesStatusText = message;
    }

    private void SetVariablesError(string message)
    {
        Variables.Clear();
        VariablesState = DebugProjectionState.Error;
        VariablesStatusText = message;
    }

    private void ClearThreadFrameScopeCollections()
    {
        Threads.Clear();
        Frames.Clear();
        Scopes.Clear();
        Variables.Clear();
        SelectedThread = null;
        SelectedFrame = null;
        SelectedScope = null;
    }

    private void ClearFrameScopeVariableCollections()
    {
        Frames.Clear();
        Scopes.Clear();
        Variables.Clear();
        SelectedFrame = null;
        SelectedScope = null;
    }

    private void ClearScopeVariableCollections()
    {
        Scopes.Clear();
        Variables.Clear();
        SelectedScope = null;
    }

    private void ReplaceThreads(IReadOnlyList<DapThreadInfo> threads)
    {
        Threads.Clear();
        foreach (var thread in threads)
            Threads.Add(new DebugThreadViewModel(thread.Id, thread.Name));
    }

    private void ReplaceFrames(IReadOnlyList<DapStackFrameInfo> frames)
    {
        Frames.Clear();
        foreach (var frame in frames)
        {
            Frames.Add(new DebugStackFrameViewModel(
                frame.Id,
                frame.Name,
                frame.SourcePath,
                frame.Line));
        }

        CallStackState = DebugProjectionState.Ready;
        CallStackStatusText = string.Empty;
    }

    private void ReplaceScopes(IReadOnlyList<DapScopeInfo> scopes)
    {
        Scopes.Clear();
        foreach (var scope in scopes)
            Scopes.Add(new DebugScopeViewModel(scope.Name, scope.VariablesReference));
    }

    private void ReplaceVariables(IReadOnlyList<DapVariableInfo> variables)
    {
        Variables.Clear();
        foreach (var variable in variables)
        {
            Variables.Add(new DebugVariableViewModel(
                variable.Name,
                variable.Value,
                variable.Type));
        }
    }

    private DebugThreadViewModel? FindThread(int threadId) =>
        Threads.FirstOrDefault(thread => thread.Id == threadId);

    private static void ObserveTask(Task task)
    {
        _ = task.ContinueWith(
            t => { _ = t.Exception; },
            TaskScheduler.Default);
    }
}