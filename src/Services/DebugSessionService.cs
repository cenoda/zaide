using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Zaide.Services;

/// <summary>
/// Singleton debug-session owner. Manages one DAP adapter session, generation-safe
/// teardown, and immutable snapshot publication.
/// </summary>
public sealed class DebugSessionService : IDebugSessionService
{
    private static readonly IReadOnlyList<string> EmptyDiagnostics = Array.Empty<string>();

    private static readonly DebugSessionSnapshot InitialSnapshot = new(
        DebugSessionState.Idle,
        Generation: 0,
        ProgramPath: null,
        WorkingDirectory: null,
        AdapterProcessId: null,
        StopInfo: null,
        Failure: null,
        DiagnosticOutput: EmptyDiagnostics);

    private readonly IProjectContextService _projectContext;
    private readonly IDebugAdapterLocator _adapterLocator;
    private readonly IDebugAdapterSessionFactory _sessionFactory;
    private readonly DebugSessionTimeoutPolicy _timeoutPolicy;
    private readonly ILogger<DebugSessionService> _logger;
    private readonly Subject<DebugSessionSnapshot> _subject = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<string> _diagnosticOutput = new();
    private readonly IDisposable _contextSubscription;

    private volatile DebugSessionSnapshot _current = InitialSnapshot;
    private long _generation;
    private CancellationTokenSource? _sessionCts;
    private IDebugAdapterSession? _activeSession;
    private bool _disposed;

    public DebugSessionService(
        IProjectContextService projectContext,
        IDebugAdapterLocator adapterLocator,
        IDebugAdapterSessionFactory sessionFactory,
        DebugSessionTimeoutPolicy timeoutPolicy,
        ILogger<DebugSessionService> logger)
    {
        _projectContext = projectContext ?? throw new ArgumentNullException(nameof(projectContext));
        _adapterLocator = adapterLocator ?? throw new ArgumentNullException(nameof(adapterLocator));
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _timeoutPolicy = timeoutPolicy ?? throw new ArgumentNullException(nameof(timeoutPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _contextSubscription = _projectContext.WhenChanged.Subscribe(OnProjectContextChanged);
        PublishInitialSnapshot(_projectContext.Current);
    }

    /// <inheritdoc />
    public DebugSessionSnapshot Current => _current;

    /// <inheritdoc />
    public IObservable<DebugSessionSnapshot> WhenChanged => _subject;

    /// <inheritdoc />
    public async Task<DebugSessionOperationResult> StartLaunchAsync(
        DebugLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsDebugEligible(_projectContext.Current))
        {
            return new DebugSessionOperationResult(
                false,
                DebugSessionOutcomeKind.RejectedContext,
                "The selected project is not eligible for debugging.");
        }

        if (!IsStartAllowed(_current.State))
        {
            return new DebugSessionOperationResult(
                false,
                DebugSessionOutcomeKind.RejectedConcurrent,
                "A debug session is already active.");
        }

        var adapterPath = _adapterLocator.Resolve();
        if (adapterPath is null)
        {
            await PublishIfCurrentGenerationAsync(
                _generation,
                new DebugSessionSnapshot(
                    DebugSessionState.Failed,
                    _generation,
                    ProgramPath: null,
                    WorkingDirectory: null,
                    AdapterProcessId: null,
                    StopInfo: null,
                    new DebugSessionFailure(
                        DebugSessionOutcomeKind.AdapterUnavailable,
                        DebugAdapterLocator.UnavailableMessage),
                    EmptyDiagnostics))
                .ConfigureAwait(false);

            return new DebugSessionOperationResult(
                false,
                DebugSessionOutcomeKind.AdapterUnavailable,
                DebugAdapterLocator.UnavailableMessage);
        }

        var programPath = Path.GetFullPath(request.ProgramPath);
        var workingDirectory = Path.GetFullPath(request.WorkingDirectory);

        long generation;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!IsDebugEligible(_projectContext.Current))
            {
                return new DebugSessionOperationResult(
                    false,
                    DebugSessionOutcomeKind.RejectedContext,
                    "The selected project is not eligible for debugging.");
            }

            if (!IsStartAllowed(_current.State))
            {
                return new DebugSessionOperationResult(
                    false,
                    DebugSessionOutcomeKind.RejectedConcurrent,
                    "A debug session is already active.");
            }

            _generation++;
            generation = _generation;
            _diagnosticOutput.Clear();
            PublishLocked(BuildStartingSnapshot(generation, programPath, workingDirectory));
        }
        finally
        {
            _gate.Release();
        }

        _sessionCts?.Cancel();
        _sessionCts?.Dispose();
        _sessionCts = new CancellationTokenSource();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _sessionCts.Token);
        var sessionToken = linked.Token;

        IDebugAdapterSession? session = null;
        try
        {
            session = await _sessionFactory.StartAsync(
                new DebugAdapterStartOptions(generation, adapterPath),
                sessionToken).ConfigureAwait(false);

            await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (_disposed || generation != _generation)
                {
                    await AbandonSessionAsync(session).ConfigureAwait(false);
                    return new DebugSessionOperationResult(
                        false,
                        DebugSessionOutcomeKind.Cancelled,
                        "Debug session start was superseded.");
                }

                AttachSessionHandlers(session, generation);
                _activeSession = session;
                PublishLocked(BuildStartingSnapshot(
                    generation,
                    programPath,
                    workingDirectory,
                    session.ProcessId));
            }
            finally
            {
                _gate.Release();
            }

            await WithTimeoutAsync(
                token => session.InitializeAsync(token),
                _timeoutPolicy.Initialize,
                sessionToken).ConfigureAwait(false);
            await WithTimeoutAsync(
                token => session.LaunchAsync(programPath, workingDirectory, request.StopAtEntry, token),
                _timeoutPolicy.LaunchConfiguration,
                sessionToken).ConfigureAwait(false);

            var breakpointsBySource = request.Breakpoints
                .GroupBy(bp => Path.GetFullPath(bp.SourcePath))
                .ToDictionary(group => group.Key, group => group.Select(bp => bp.Line).ToArray());

            foreach (var (sourcePath, lines) in breakpointsBySource)
            {
                await WithTimeoutAsync(
                    token => session.SetBreakpointsAsync(sourcePath, lines, token),
                    _timeoutPolicy.LaunchConfiguration,
                    sessionToken).ConfigureAwait(false);
            }

            var stoppedTcs = new TaskCompletionSource<DapStoppedEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            void OnInitialStopped(DapStoppedEvent stoppedEvent)
            {
                if (stoppedEvent.Generation == generation)
                    stoppedTcs.TrySetResult(stoppedEvent);
            }

            session.Stopped += OnInitialStopped;
            try
            {
                await WithTimeoutAsync(
                    token => session.ConfigurationDoneAsync(token),
                    _timeoutPolicy.LaunchConfiguration,
                    sessionToken).ConfigureAwait(false);
                using var stoppedTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(sessionToken);
                stoppedTimeoutCts.CancelAfter(_timeoutPolicy.LaunchConfiguration);
                var stoppedEvent = await stoppedTcs.Task
                    .WaitAsync(stoppedTimeoutCts.Token)
                    .ConfigureAwait(false);

                await PublishIfCurrentGenerationAsync(
                    generation,
                    BuildStoppedSnapshot(
                        generation,
                        programPath,
                        workingDirectory,
                        session.ProcessId,
                        stoppedEvent))
                    .ConfigureAwait(false);
            }
            finally
            {
                session.Stopped -= OnInitialStopped;
            }

            return new DebugSessionOperationResult(true, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await HandleStartupFailureAsync(
                generation,
                programPath,
                workingDirectory,
                session,
                DebugSessionOutcomeKind.Cancelled,
                "Debug session start was cancelled.")
                .ConfigureAwait(false);
            throw;
        }
        catch (OperationCanceledException)
        {
            await HandleStartupFailureAsync(
                generation,
                programPath,
                workingDirectory,
                session,
                DebugSessionOutcomeKind.StartupFailed,
                "Debug session start timed out.")
                .ConfigureAwait(false);

            return new DebugSessionOperationResult(
                false,
                DebugSessionOutcomeKind.StartupFailed,
                "Debug session start timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Debug session start failed for generation {Generation}", generation);

            await HandleStartupFailureAsync(
                generation,
                programPath,
                workingDirectory,
                session,
                DebugSessionOutcomeKind.StartupFailed,
                "Debug session failed to start.")
                .ConfigureAwait(false);

            return new DebugSessionOperationResult(
                false,
                DebugSessionOutcomeKind.StartupFailed,
                "Debug session failed to start.");
        }
    }

    /// <inheritdoc />
    public async Task<DebugSessionOperationResult> StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disposed)
                return new DebugSessionOperationResult(true, null, null);

            if (!IsActiveState(_current.State))
                return new DebugSessionOperationResult(true, null, null);

            PublishLocked(BuildStoppingSnapshot(_current));
        }
        finally
        {
            _gate.Release();
        }

        await TearDownActiveSessionAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (_disposed)
                return new DebugSessionOperationResult(true, null, null);

            PublishLocked(BuildIdleSnapshot(_generation));
        }
        finally
        {
            _gate.Release();
        }

        return new DebugSessionOperationResult(true, null, null);
    }

    /// <inheritdoc />
    public async Task<DebugSessionOperationResult> ContinueAsync(
        int threadId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = _current;
        var session = _activeSession;

        if (snapshot.State != DebugSessionState.Stopped ||
            session is null ||
            session.Generation != snapshot.Generation)
        {
            return new DebugSessionOperationResult(
                false,
                DebugSessionOutcomeKind.ProtocolFailed,
                "Continue is only available while the debuggee is stopped.");
        }

        try
        {
            await WithTimeoutAsync(
                token => session.ContinueAsync(threadId, token),
                _timeoutPolicy.OrdinaryRequest,
                cancellationToken).ConfigureAwait(false);
            return new DebugSessionOperationResult(true, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Debug continue failed for generation {Generation}", snapshot.Generation);
            return new DebugSessionOperationResult(
                false,
                DebugSessionOutcomeKind.ProtocolFailed,
                "Debug continue failed.");
        }
    }

    /// <inheritdoc />
    public async Task<JsonElement?> RequestThreadsAsync(CancellationToken cancellationToken = default)
    {
        var session = RequireStoppedSession();
        return await WithTimeoutAsync(
            token => session.RequestThreadsAsync(token),
            _timeoutPolicy.OrdinaryRequest,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<JsonElement?> RequestStackTraceAsync(
        int threadId,
        CancellationToken cancellationToken = default)
    {
        var session = RequireStoppedSession();
        return await WithTimeoutAsync(
            token => session.RequestStackTraceAsync(threadId, token),
            _timeoutPolicy.OrdinaryRequest,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<JsonElement?> RequestScopesAsync(
        int frameId,
        CancellationToken cancellationToken = default)
    {
        var session = RequireStoppedSession();
        return await WithTimeoutAsync(
            token => session.RequestScopesAsync(frameId, token),
            _timeoutPolicy.OrdinaryRequest,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _generation++;
        _contextSubscription.Dispose();

        _sessionCts?.Cancel();
        _sessionCts?.Dispose();
        _sessionCts = null;

        try
        {
            TearDownActiveSessionAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Debug session dispose teardown encountered an error.");
        }

        _subject.OnCompleted();
        _subject.Dispose();
        _gate.Dispose();
    }

    private void OnProjectContextChanged(ProjectContext context)
    {
        if (_disposed)
            return;

        ObserveTask(ReconcileContextAsync(context));
    }

    private async Task ReconcileContextAsync(ProjectContext context)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
                return;

            _generation++;

            if (IsActiveState(_current.State))
            {
                var stoppingSnapshot = BuildStoppingSnapshot(_current with { Generation = _generation });
                PublishLocked(stoppingSnapshot);
            }
            else
            {
                PublishLocked(BuildContextSnapshot(context, _generation));
            }
        }
        finally
        {
            _gate.Release();
        }

        if (IsActiveState(_current.State))
        {
            await TearDownActiveSessionAsync(CancellationToken.None).ConfigureAwait(false);

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_disposed)
                    return;

                PublishLocked(BuildContextSnapshot(context, _generation));
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    private async Task HandleStartupFailureAsync(
        long generation,
        string programPath,
        string workingDirectory,
        IDebugAdapterSession? session,
        DebugSessionOutcomeKind outcome,
        string message)
    {
        if (session is not null)
            await AbandonSessionAsync(session).ConfigureAwait(false);

        await PublishIfCurrentGenerationAsync(
            generation,
            new DebugSessionSnapshot(
                DebugSessionState.Failed,
                generation,
                programPath,
                workingDirectory,
                session?.ProcessId,
                StopInfo: null,
                new DebugSessionFailure(outcome, message),
                CopyDiagnostics(session)))
            .ConfigureAwait(false);
    }

    private async Task TearDownActiveSessionAsync(CancellationToken cancellationToken)
    {
        _sessionCts?.Cancel();
        _sessionCts?.Dispose();
        _sessionCts = null;

        var session = _activeSession;
        _activeSession = null;

        if (session is null)
            return;

        DetachSessionHandlers(session);

        try
        {
            await session.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception disconnectEx)
        {
            _logger.LogDebug(disconnectEx, "Graceful debug disconnect failed; force-killing adapter.");
            try
            {
                await session.ForceKillAsync().ConfigureAwait(false);
            }
            catch (Exception killEx)
            {
                _logger.LogError(killEx, "Force-kill also failed for debug adapter session.");
            }
        }

        await session.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task AbandonSessionAsync(IDebugAdapterSession session)
    {
        try
        {
            await session.ForceKillAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best effort.
        }

        await session.DisposeAsync().ConfigureAwait(false);
    }

    private void AttachSessionHandlers(IDebugAdapterSession session, long generation)
    {
        session.Stopped += OnSessionStopped;
        session.Continued += OnSessionContinued;
        session.Output += OnSessionOutput;
        session.Terminated += OnSessionTerminated;
        session.Exited += OnSessionExited;
        session.ProcessExited += OnSessionProcessExited;
    }

    private void DetachSessionHandlers(IDebugAdapterSession session)
    {
        session.Stopped -= OnSessionStopped;
        session.Continued -= OnSessionContinued;
        session.Output -= OnSessionOutput;
        session.Terminated -= OnSessionTerminated;
        session.Exited -= OnSessionExited;
        session.ProcessExited -= OnSessionProcessExited;
    }

    private void OnSessionStopped(DapStoppedEvent stoppedEvent) =>
        ObserveTask(HandleSessionStoppedAsync(stoppedEvent));

    private async Task HandleSessionStoppedAsync(DapStoppedEvent stoppedEvent)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed || stoppedEvent.Generation != _generation)
                return;

            var current = _current;
            if (_activeSession is null || _activeSession.Generation != stoppedEvent.Generation)
                return;

            PublishLocked(current with
            {
                State = DebugSessionState.Stopped,
                StopInfo = new DapStoppedInfo(stoppedEvent.Reason, stoppedEvent.ThreadId),
            });
        }
        finally
        {
            _gate.Release();
        }
    }

    private void OnSessionContinued(DapContinuedEvent continuedEvent) =>
        ObserveTask(HandleSessionContinuedAsync(continuedEvent));

    private async Task HandleSessionContinuedAsync(DapContinuedEvent continuedEvent)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed || continuedEvent.Generation != _generation)
                return;

            var current = _current;
            if (_activeSession is null || _activeSession.Generation != continuedEvent.Generation)
                return;

            PublishLocked(current with
            {
                State = DebugSessionState.Running,
                StopInfo = null,
            });
        }
        finally
        {
            _gate.Release();
        }
    }

    private void OnSessionOutput(DapOutputEvent outputEvent) =>
        ObserveTask(HandleSessionOutputAsync(outputEvent));

    private async Task HandleSessionOutputAsync(DapOutputEvent outputEvent)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed || outputEvent.Generation != _generation)
                return;

            AppendDiagnostic(outputEvent.Output);

            if (_activeSession is null || _activeSession.Generation != outputEvent.Generation)
                return;

            PublishLocked(_current with { DiagnosticOutput = CopyDiagnosticsLocked() });
        }
        finally
        {
            _gate.Release();
        }
    }

    private void OnSessionTerminated(long generation) =>
        ObserveTask(HandleSessionEndedAsync(generation, "Debug adapter reported terminated."));

    private void OnSessionExited(DapExitedEvent exitedEvent) =>
        ObserveTask(HandleSessionEndedAsync(exitedEvent.Generation, "Debug adapter reported exited."));

    private void OnSessionProcessExited(long generation) =>
        ObserveTask(HandleSessionEndedAsync(generation, "Debug adapter process exited unexpectedly."));

    private async Task HandleSessionEndedAsync(long endedGeneration, string message)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed || endedGeneration != _generation)
                return;

            if (_activeSession is null || _activeSession.Generation != endedGeneration)
                return;

            _generation++;
            var newGeneration = _generation;
            var current = _current;

            PublishLocked(new DebugSessionSnapshot(
                DebugSessionState.Failed,
                newGeneration,
                current.ProgramPath,
                current.WorkingDirectory,
                current.AdapterProcessId,
                StopInfo: null,
                new DebugSessionFailure(DebugSessionOutcomeKind.AdapterExited, message),
                CopyDiagnosticsLocked()));
        }
        finally
        {
            _gate.Release();
        }

        await TearDownActiveSessionAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private IDebugAdapterSession RequireStoppedSession()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var snapshot = _current;
        var session = _activeSession;

        if (snapshot.State != DebugSessionState.Stopped ||
            session is null ||
            session.Generation != snapshot.Generation)
        {
            throw new InvalidOperationException("Stopped-state DAP requests require an active stopped session.");
        }

        return session;
    }

    private async Task PublishIfCurrentGenerationAsync(long generation, DebugSessionSnapshot snapshot)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed || generation != _generation)
                return;

            PublishLocked(snapshot);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void PublishInitialSnapshot(ProjectContext context)
    {
        PublishLocked(BuildContextSnapshot(context, _generation));
    }

    private void PublishLocked(DebugSessionSnapshot snapshot)
    {
        _current = snapshot;
        _subject.OnNext(snapshot);
    }

    private void AppendDiagnostic(string line)
    {
        if (!string.IsNullOrEmpty(line))
            _diagnosticOutput.Add(line);
    }

    private void AppendStderrDiagnostics(IDebugAdapterSession? session)
    {
        if (session is null)
            return;

        foreach (var line in session.StderrLines)
            AppendDiagnostic(line);
    }

    private IReadOnlyList<string> CopyDiagnosticsLocked() => _diagnosticOutput.ToArray();

    private IReadOnlyList<string> CopyDiagnostics(IDebugAdapterSession? session)
    {
        AppendStderrDiagnostics(session);
        return CopyDiagnosticsLocked();
    }

    private static bool IsStartAllowed(DebugSessionState state) =>
        state is DebugSessionState.Idle or DebugSessionState.Failed;

    private static bool IsActiveState(DebugSessionState state) =>
        state is DebugSessionState.Starting
            or DebugSessionState.Running
            or DebugSessionState.Stopped
            or DebugSessionState.Stopping;

    private static bool IsDebugEligible(ProjectContext context) =>
        ProjectTargetResolver.IsEligible(context) &&
        context.SelectedProject?.Kind == ProjectKind.CSharpProject;

    private static DebugSessionSnapshot BuildContextSnapshot(ProjectContext context, long generation)
    {
        if (!IsDebugEligible(context))
        {
            return new DebugSessionSnapshot(
                DebugSessionState.Unavailable,
                generation,
                ProgramPath: null,
                WorkingDirectory: null,
                AdapterProcessId: null,
                StopInfo: null,
                Failure: null,
                DiagnosticOutput: EmptyDiagnostics);
        }

        return new DebugSessionSnapshot(
            DebugSessionState.Idle,
            generation,
            ProgramPath: null,
            WorkingDirectory: null,
            AdapterProcessId: null,
            StopInfo: null,
            Failure: null,
            DiagnosticOutput: EmptyDiagnostics);
    }

    private DebugSessionSnapshot BuildStartingSnapshot(
        long generation,
        string programPath,
        string workingDirectory,
        int? adapterProcessId = null) =>
        new(
            DebugSessionState.Starting,
            generation,
            programPath,
            workingDirectory,
            adapterProcessId,
            StopInfo: null,
            Failure: null,
            CopyDiagnosticsLocked());

    private DebugSessionSnapshot BuildStoppedSnapshot(
        long generation,
        string programPath,
        string workingDirectory,
        int? adapterProcessId,
        DapStoppedEvent stoppedEvent)
    {
        AppendStderrDiagnostics(_activeSession);
        return new DebugSessionSnapshot(
            DebugSessionState.Stopped,
            generation,
            programPath,
            workingDirectory,
            adapterProcessId,
            new DapStoppedInfo(stoppedEvent.Reason, stoppedEvent.ThreadId),
            Failure: null,
            CopyDiagnosticsLocked());
    }

    private DebugSessionSnapshot BuildStoppingSnapshot(DebugSessionSnapshot current) =>
        current with { State = DebugSessionState.Stopping, StopInfo = null };

    private DebugSessionSnapshot BuildIdleSnapshot(long generation) =>
        new(
            DebugSessionState.Idle,
            generation,
            ProgramPath: null,
            WorkingDirectory: null,
            AdapterProcessId: null,
            StopInfo: null,
            Failure: null,
            CopyDiagnosticsLocked());

    private static async Task WithTimeoutAsync(
        Func<CancellationToken, Task> action,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        await action(timeoutCts.Token).ConfigureAwait(false);
    }

    private static async Task<T> WithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> action,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        return await action(timeoutCts.Token).ConfigureAwait(false);
    }

    private static void ObserveTask(Task task)
    {
        _ = task.ContinueWith(
            t => { _ = t.Exception; },
            TaskScheduler.Default);
    }
}
