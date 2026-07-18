using System;
using System.IO;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.Language.Contracts;
using Zaide.App.Composition;
using Zaide.Features.Language.Infrastructure.Lsp;

namespace Zaide.Features.Language.Application;

/// <summary>
/// Singleton language-session owner. Reacts to <see cref="IProjectContextService"/>
/// snapshots, launches csharp-ls, and manages generation-safe teardown.
/// </summary>
internal sealed class LanguageSessionService : ILanguageSessionService
{
    private static readonly LanguageSessionSnapshot InitialSnapshot = new(
        LanguageSessionState.Unavailable,
        Generation: 0,
        ProjectFilePath: null,
        WorkspaceFolderPath: null,
        ServerProcessId: null,
        Failure: null);

    private readonly IProjectContextService _projectContext;
    private readonly ILanguageServerBinaryLocator _binaryLocator;
    private readonly ILanguageServerSessionFactory _sessionFactory;
    private readonly ILogger<LanguageSessionService> _logger;
    private readonly Subject<LanguageSessionSnapshot> _subject = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IDisposable _contextSubscription;

    private volatile LanguageSessionSnapshot _current = InitialSnapshot;
    private long _generation;
    private CancellationTokenSource? _sessionCts;
    private ILanguageServerSession? _activeSession;
    private bool _disposed;

    public LanguageSessionService(
        IProjectContextService projectContext,
        ILanguageServerBinaryLocator binaryLocator,
        ILanguageServerSessionFactory sessionFactory,
        ILogger<LanguageSessionService> logger)
    {
        _projectContext = projectContext ?? throw new ArgumentNullException(nameof(projectContext));
        _binaryLocator = binaryLocator ?? throw new ArgumentNullException(nameof(binaryLocator));
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _contextSubscription = _projectContext.WhenChanged.Subscribe(OnProjectContextChanged);
        ObserveTask(ReconcileAsync(_projectContext.Current, bumpGeneration: true));
    }

    /// <inheritdoc />
    public LanguageSessionSnapshot Current => _current;

    /// <inheritdoc />
    public IObservable<LanguageSessionSnapshot> WhenChanged => _subject;

    /// <inheritdoc />
    public ILanguageServerSession? TryGetReadySession(long generation)
    {
        if (_disposed)
            return null;

        var snapshot = _current;
        if (snapshot.State != LanguageSessionState.Ready || snapshot.Generation != generation)
            return null;

        var session = _activeSession;
        return session is not null && session.Generation == generation ? session : null;
    }

    /// <inheritdoc />
    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        ProjectContext context;
        long generation;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            context = _projectContext.Current;
            _generation++;
            generation = _generation;

            await TearDownSessionLockedAsync().ConfigureAwait(false);
            PublishLocked(BuildInterimSnapshot(context, generation));
        }
        finally
        {
            _gate.Release();
        }

        if (!IsEligible(context))
            return;

        await StartSessionAsync(context, generation, cancellationToken).ConfigureAwait(false);
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

        if (_activeSession is not null)
        {
            _activeSession.ProcessExited -= OnSessionProcessExited;
            var session = _activeSession;
            _activeSession = null;

            try
            {
                // Offload to the thread pool so the continuation never posts back
                // to a captured SynchronizationContext, avoiding a potential deadlock.
                Task.Run(async () =>
                {
                    await session.ForceKillAsync().ConfigureAwait(false);
                    await session.DisposeAsync().ConfigureAwait(false);
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Debug, new EventId(10004), ex,
                    "Language session dispose teardown encountered an error.");
            }
        }

        _subject.OnCompleted();
        _subject.Dispose();
        _gate.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        _generation++;
        _contextSubscription.Dispose();

        _sessionCts?.Cancel();
        _sessionCts?.Dispose();
        _sessionCts = null;

        if (_activeSession is not null)
        {
            _activeSession.ProcessExited -= OnSessionProcessExited;
            var session = _activeSession;
            _activeSession = null;

            try
            {
                await session.ForceKillAsync().ConfigureAwait(false);
                await session.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Debug, new EventId(10004), ex,
                    "Language session async-dispose teardown encountered an error.");
            }
        }

        _subject.OnCompleted();
        _subject.Dispose();
        _gate.Dispose();
    }

    private void OnProjectContextChanged(ProjectContext context)
    {
        if (_disposed)
            return;

        ObserveTask(ReconcileAsync(context, bumpGeneration: true));
    }

    private async Task ReconcileAsync(ProjectContext context, bool bumpGeneration)
    {
        long generation;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
                return;

            if (bumpGeneration)
                _generation++;

            generation = _generation;

            await TearDownSessionLockedAsync().ConfigureAwait(false);
            PublishLocked(BuildInterimSnapshot(context, generation));
        }
        finally
        {
            _gate.Release();
        }

        if (!IsEligible(context))
            return;

        await StartSessionAsync(context, generation, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task StartSessionAsync(
        ProjectContext context,
        long generation,
        CancellationToken outerCancellationToken)
    {
        var candidate = context.SelectedProject!;
        var workspaceFolder = Path.GetDirectoryName(candidate.FilePath)!;

        var serverPath = _binaryLocator.Resolve();
        if (serverPath is null)
        {
            await PublishIfCurrentGenerationAsync(
                generation,
                new LanguageSessionSnapshot(
                    LanguageSessionState.Failed,
                    generation,
                    candidate.FilePath,
                    workspaceFolder,
                    ServerProcessId: null,
                    new LanguageSessionFailure(
                        LanguageSessionFailureKind.MissingServerBinary,
                        "csharp-ls was not found. Install with: dotnet tool install -g csharp-ls")))
                .ConfigureAwait(false);
            return;
        }

        _sessionCts?.Cancel();
        _sessionCts?.Dispose();
        _sessionCts = new CancellationTokenSource();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            outerCancellationToken,
            _sessionCts.Token);
        var cancellationToken = linked.Token;

        try
        {
            var options = new LanguageServerStartOptions(
                generation,
                serverPath,
                candidate.FilePath,
                workspaceFolder,
                candidate.Kind);

            var session = await _sessionFactory.StartAsync(options, cancellationToken)
                .ConfigureAwait(false);

            await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (_disposed || generation != _generation)
                {
                    session.ProcessExited -= OnSessionProcessExited;
                    try
                    {
                        await session.ShutdownAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    catch
                    {
                        await session.ForceKillAsync().ConfigureAwait(false);
                    }

                    await session.DisposeAsync().ConfigureAwait(false);
                    return;
                }

                _activeSession = session;
                session.ProcessExited += OnSessionProcessExited;

                PublishLocked(new LanguageSessionSnapshot(
                    LanguageSessionState.Ready,
                    generation,
                    candidate.FilePath,
                    workspaceFolder,
                    session.ProcessId,
                    Failure: null));
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            await PublishIfCurrentGenerationAsync(
                generation,
                BuildCancelledSnapshot(candidate, generation))
                .ConfigureAwait(false);

            if (outerCancellationToken.IsCancellationRequested)
                throw;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.Log(LogLevel.Error, new EventId(10001), ex,
                "Language server process failed to start for generation {Generation}", generation);

            await PublishIfCurrentGenerationAsync(
                generation,
                new LanguageSessionSnapshot(
                    LanguageSessionState.Failed,
                    generation,
                    candidate.FilePath,
                    workspaceFolder,
                    ServerProcessId: null,
                    new LanguageSessionFailure(
                        LanguageSessionFailureKind.ProcessStartFailed,
                        "Language server process failed to start.")))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, new EventId(10001), ex,
                "Language session start failed for generation {Generation}", generation);

            await PublishIfCurrentGenerationAsync(
                generation,
                new LanguageSessionSnapshot(
                    LanguageSessionState.Failed,
                    generation,
                    candidate.FilePath,
                    workspaceFolder,
                    ServerProcessId: null,
                    new LanguageSessionFailure(
                        LanguageSessionFailureKind.InitializeFailed,
                        "Language server failed to start.")))
                .ConfigureAwait(false);
        }
    }

    private void OnSessionProcessExited(long exitedGeneration)
    {
        ObserveTask(HandleProcessExitedAsync(exitedGeneration));
    }

    private async Task HandleProcessExitedAsync(long exitedGeneration)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed || exitedGeneration != _generation)
                return;

            if (_activeSession is null || _activeSession.Generation != exitedGeneration)
                return;

            _generation++;
            var newGeneration = _generation;

            await TearDownSessionLockedAsync().ConfigureAwait(false);

            var context = _projectContext.Current;
            var candidate = context.SelectedProject;

            PublishLocked(new LanguageSessionSnapshot(
                LanguageSessionState.Failed,
                newGeneration,
                candidate?.FilePath,
                candidate is not null ? Path.GetDirectoryName(candidate.FilePath) : null,
                ServerProcessId: null,
                new LanguageSessionFailure(
                    LanguageSessionFailureKind.ServerExited,
                    "Language server process exited unexpectedly.")));

            _logger.Log(LogLevel.Warning, new EventId(10002),
                "Language server process exited for generation {Generation}", exitedGeneration);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task TearDownSessionLockedAsync()
    {
        _sessionCts?.Cancel();
        _sessionCts?.Dispose();
        _sessionCts = null;

        var session = _activeSession;
        _activeSession = null;

        if (session is null)
            return;

        session.ProcessExited -= OnSessionProcessExited;

        try
        {
            await session.ShutdownAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception shutdownEx)
        {
            _logger.Log(LogLevel.Debug, new EventId(10003), shutdownEx,
                "Graceful language-server shutdown failed; force-killing.");
            try
            {
                await session.ForceKillAsync().ConfigureAwait(false);
            }
            catch (Exception killEx)
            {
                _logger.Log(LogLevel.Error, new EventId(10005), killEx,
                    "Force-kill also failed for language session.");
                PublishLocked(new LanguageSessionSnapshot(
                    LanguageSessionState.Failed,
                    _generation,
                    ProjectFilePath: null,
                    WorkspaceFolderPath: null,
                    ServerProcessId: null,
                    new LanguageSessionFailure(
                        LanguageSessionFailureKind.ShutdownFailed,
                        "Language server could not be shut down or killed.")));
            }
        }

        await session.DisposeAsync().ConfigureAwait(false);
    }

    private async Task PublishIfCurrentGenerationAsync(long generation, LanguageSessionSnapshot snapshot)
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

    private void PublishLocked(LanguageSessionSnapshot snapshot)
    {
        _current = snapshot;
        _subject.OnNext(snapshot);
    }

    private static bool IsEligible(ProjectContext context) =>
        context.SelectedProject is not null &&
        context.State is ProjectContextState.SingleProject or ProjectContextState.Selected;

    private static LanguageSessionSnapshot BuildInterimSnapshot(ProjectContext context, long generation)
    {
        if (!IsEligible(context))
        {
            return new LanguageSessionSnapshot(
                MapContextToSessionState(context.State),
                generation,
                ProjectFilePath: null,
                WorkspaceFolderPath: null,
                ServerProcessId: null,
                Failure: null);
        }

        var candidate = context.SelectedProject!;
        return new LanguageSessionSnapshot(
            LanguageSessionState.Loading,
            generation,
            candidate.FilePath,
            Path.GetDirectoryName(candidate.FilePath),
            ServerProcessId: null,
            Failure: null);
    }

    private static LanguageSessionSnapshot BuildCancelledSnapshot(
        ProjectCandidate candidate,
        long generation)
    {
        return new LanguageSessionSnapshot(
            LanguageSessionState.Cancelled,
            generation,
            candidate.FilePath,
            Path.GetDirectoryName(candidate.FilePath),
            ServerProcessId: null,
            Failure: null);
    }

    private static LanguageSessionState MapContextToSessionState(ProjectContextState state) =>
        state == ProjectContextState.Loading
            ? LanguageSessionState.Loading
            : LanguageSessionState.Unavailable;

    private static void ObserveTask(Task task)
    {
        _ = task.ContinueWith(
            t => { _ = t.Exception; },
            TaskScheduler.Default);
    }
}
