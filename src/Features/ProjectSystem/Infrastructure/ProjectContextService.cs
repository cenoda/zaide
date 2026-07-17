using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Features.ProjectSystem.Infrastructure;

/// <summary>
/// Singleton service that owns the authoritative <see cref="ProjectContext"/>
/// for the currently opened workspace.
///
/// <para>Lifecycle methods accept cancellation. Cancellation is never
/// represented as <see cref="ProjectContextState.Failed"/> — a cancelled
/// operation restores the last stable snapshot (or <see cref="ProjectContextState.Unloaded"/>
/// if no prior stable snapshot exists).</para>
///
/// <para>Overlapping <see cref="LoadAsync"/> and <see cref="ReloadAsync"/> calls
/// are protected by a monotonically increasing request sequence. Only the newest
/// request may publish a terminal or restoration snapshot; stale completions and
/// stale cancellations emit nothing.</para>
/// </summary>
public sealed class ProjectContextService : IProjectContextService
{
    private readonly IProjectDiscovery _discovery;
    private readonly ILogger<ProjectContextService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Subject<ProjectContext> _subject = new();
    private readonly global::Zaide.Features.Workspace.Domain.Workspace? _workspace;
    private readonly EventHandler _workspaceChangedHandler;
    private bool _disposed;

    // Sequence management for overlapping requests.
    // _nextSequence is incremented at the start of each LoadAsync/ReloadAsync/UnloadAsync.
    // _currentOwner is the sequence of the request currently allowed to publish.
    // A request is "stale" when its captured sequence != _currentOwner.
    private long _nextSequence;
    private long _currentOwner;

    private volatile ProjectContext _current;

    private static readonly ProjectContext UnloadedContext = new(
        ProjectContextState.Unloaded,
        WorkspaceRoot: null,
        Candidates: Array.Empty<ProjectCandidate>(),
        SelectedProject: null,
        UnsupportedFiles: Array.Empty<string>(),
        ErrorMessage: null);

    /// <summary>
    /// M2-compatible constructor: builds the service without wiring it to a
    /// <see cref="global::Zaide.Features.Workspace.Domain.Workspace"/>. Used by unit tests that exercise the lifecycle
    /// API directly. The service never subscribes to workspace events here.
    /// </summary>
    public ProjectContextService(
        IProjectDiscovery discovery,
        ILogger<ProjectContextService> logger)
        : this(workspace: null, discovery, logger)
    {
    }

    /// <summary>
    /// Production constructor. Subscribes to <see cref="global::Zaide.Features.Workspace.Domain.Workspace.WorkspaceFolderChanged"/>
    /// with a stored delegate and immediately reconciles a non-null
    /// <see cref="global::Zaide.Features.Workspace.Domain.Workspace.WorkspacePath"/> by starting one load. A null startup
    /// path remains <see cref="ProjectContextState.Unloaded"/> and emits nothing.
    /// </summary>
    public ProjectContextService(
        global::Zaide.Features.Workspace.Domain.Workspace? workspace,
        IProjectDiscovery discovery,
        ILogger<ProjectContextService> logger)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _current = UnloadedContext;
        _workspace = workspace;
        _workspaceChangedHandler = OnWorkspaceFolderChanged;

        if (_workspace is not null)
        {
            _workspace.WorkspaceFolderChanged += _workspaceChangedHandler;
            if (_workspace.WorkspacePath is not null)
            {
                // Startup reconciliation: a workspace was already open when the
                // service was constructed (e.g. resolved from DI after startup).
                ReconcileFromWorkspace();
            }
        }
    }

    /// <inheritdoc />
    public ProjectContext Current => _current;

    /// <inheritdoc />
    public IObservable<ProjectContext> WhenChanged => _subject;

    /// <inheritdoc />
    public async Task LoadAsync(string workspaceRoot, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        cancellationToken.ThrowIfCancellationRequested();

        // Phase 1: acquire gate, increment sequence, save stable snapshot, emit Loading.
        long sequence;
        ProjectContext? stableBeforeLoad;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            sequence = _nextSequence++;
            _currentOwner = sequence;

            // Save the last non-Loading snapshot as the stable snapshot for this
            // overlapping request sequence.
            stableBeforeLoad = _current.State != ProjectContextState.Loading
                ? _current
                : null;

            // Emit one Loading snapshot.
            _current = new ProjectContext(
                ProjectContextState.Loading,
                workspaceRoot,
                Array.Empty<ProjectCandidate>(),
                SelectedProject: null,
                UnsupportedFiles: Array.Empty<string>(),
                ErrorMessage: null);
            _subject.OnNext(_current);
        }
        finally
        {
            _gate.Release();
        }

        // Phase 2: perform discovery without holding the gate.
        ProjectDiscoveryResult result;
        try
        {
            result = await _discovery.DiscoverAsync(workspaceRoot, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Phase 3a: restoration on cancellation.
            await RestoreOnCancellationAsync(sequence, stableBeforeLoad).ConfigureAwait(false);
            _logger.Log(LogLevel.Debug, new EventId(8302),
                "LoadAsync cancelled for root: {Root}", workspaceRoot);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Log(LogLevel.Error, new EventId(8301), ex,
                "Unexpected discovery exception for root: {Root}", workspaceRoot);
            throw;
        }

        // Phase 3b: publish terminal snapshot.
        await PublishTerminalAsync(sequence, result, workspaceRoot).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        cancellationToken.ThrowIfCancellationRequested();

        string? root;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            root = _current.WorkspaceRoot;
        }
        finally
        {
            _gate.Release();
        }

        if (root is null)
        {
            // Emit Failed with InvalidRoot semantics synchronously.
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _current = new ProjectContext(
                    ProjectContextState.Failed,
                    WorkspaceRoot: null,
                    Candidates: Array.Empty<ProjectCandidate>(),
                    SelectedProject: null,
                    UnsupportedFiles: Array.Empty<string>(),
                    ErrorMessage: "Cannot reload: no workspace root is loaded.");
                _subject.OnNext(_current);
            }
            finally
            {
                _gate.Release();
            }
            return;
        }

        await LoadAsync(root, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UnloadAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        cancellationToken.ThrowIfCancellationRequested();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Invalidate all in-flight request sequences.
            _nextSequence++;
            _currentOwner = _nextSequence;

            _current = UnloadedContext;
            _subject.OnNext(_current);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void SelectProject(ProjectCandidate? candidate)
    {
        _gate.Wait(CancellationToken.None);
        try
        {
            var snapshot = _current;
            var state = snapshot.State;

            if (candidate is null)
            {
                // Null clears an ambiguous selection: return to Ambiguous
                // from Selected (originally chosen from ambiguous candidates).
                if (state == ProjectContextState.Selected)
                {
                    // Determine if we came from ambiguous (multiple candidates).
                    // Emit Ambiguous to let the user pick again.
                    _current = snapshot with
                    {
                        SelectedProject = null,
                        State = ProjectContextState.Ambiguous,
                    };
                    _subject.OnNext(_current);
                    return;
                }

                // Null on an already-Ambiguous state is a no-op (already null selection).
                if (state == ProjectContextState.Ambiguous)
                    return;

                // A null selection with exactly one candidate preserves SingleProject.
                // A null selection with zero candidates preserves NoProject or Unsupported.
                // For all other states, null is a no-op.
                return;
            }

            // Validate candidate by ordinal FilePath identity.
            var match = FindCandidate(snapshot.Candidates, candidate.FilePath);
            if (match is null)
            {
                _logger.Log(LogLevel.Warning, new EventId(8303),
                    "Rejected stale/foreign selection: {Path}", candidate.FilePath);
                return;
            }

            // If the match is already the selected candidate, no duplicate emission.
            if (snapshot.SelectedProject is not null &&
                string.Equals(snapshot.SelectedProject.FilePath, match.FilePath, StringComparison.Ordinal))
            {
                return;
            }

            _current = snapshot with
            {
                SelectedProject = match,
                State = ProjectContextState.Selected,
            };
            _subject.OnNext(_current);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        // Deterministic unsubscription: remove the exact stored handler so the
        // workspace can no longer drive discovery or state emissions.
        if (_workspace is not null)
        {
            _workspace.WorkspaceFolderChanged -= _workspaceChangedHandler;
        }

        _subject.OnCompleted();
        _subject.Dispose();
        _gate.Dispose();
    }

    /// <summary>
    /// Stored <see cref="global::Zaide.Features.Workspace.Domain.Workspace.WorkspaceFolderChanged"/> handler. Routing
    /// table: a non-null <see cref="global::Zaide.Features.Workspace.Domain.Workspace.WorkspacePath"/> starts
    /// <see cref="LoadAsync"/>; a null path starts <see cref="UnloadAsync"/>.
    /// Every started task is observed — no unobserved fire-and-forget work.
    /// </summary>
    private void OnWorkspaceFolderChanged(object? sender, EventArgs e)
    {
        if (_disposed)
            return;
        ReconcileFromWorkspace();
    }

    private void ReconcileFromWorkspace()
    {
        if (_disposed || _workspace is null)
            return;

        var path = _workspace.WorkspacePath;
        var task = path is not null
            ? LoadAsync(path)
            : UnloadAsync();
        ObserveTask(task);
    }

    /// <summary>
    /// Observes a fire-and-forget task started from a workspace event so an
    /// unobserved exception does not crash the process. <see cref="LoadAsync"/>
    /// and <see cref="UnloadAsync"/> already log expected failures (event ID
    /// 8301) and cancellation (event ID 8302); the handler only marks the task
    /// observed.
    /// </summary>
    private static void ObserveTask(Task task)
    {
        _ = task.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default);
    }

    /// <summary>
    /// Restores the last stable snapshot if this sequence is still the owner.
    /// Called when <see cref="OperationCanceledException"/> is caught after
    /// a <see cref="ProjectContextState.Loading"/> emission.
    /// </summary>
    private async Task RestoreOnCancellationAsync(long sequence, ProjectContext? stableBeforeLoad)
    {
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (sequence != _currentOwner)
            {
                // Stale cancellation — emit nothing.
                return;
            }

            _current = stableBeforeLoad ?? UnloadedContext;
            _subject.OnNext(_current);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Publishes a terminal snapshot from a discovery result, but only if this
    /// request sequence is still the owner.
    /// </summary>
    private async Task PublishTerminalAsync(long sequence, ProjectDiscoveryResult result, string workspaceRoot)
    {
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (sequence != _currentOwner)
            {
                // Stale completion — emit nothing.
                return;
            }

            _current = MapResult(result, workspaceRoot);
            _subject.OnNext(_current);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static ProjectCandidate? FindCandidate(IReadOnlyList<ProjectCandidate> candidates, string filePath)
    {
        foreach (var c in candidates)
        {
            if (string.Equals(c.FilePath, filePath, StringComparison.Ordinal))
                return c;
        }
        return null;
    }

    private static ProjectContext MapResult(ProjectDiscoveryResult result, string workspaceRoot)
    {
        if (result.Failure is not null)
        {
            return new ProjectContext(
                ProjectContextState.Failed,
                workspaceRoot,
                Array.Empty<ProjectCandidate>(),
                SelectedProject: null,
                Array.Empty<string>(),
                result.Failure.Message);
        }

        if (result.SupportedCandidates.Count == 0 && result.UnsupportedFiles.Count == 0)
        {
            return new ProjectContext(
                ProjectContextState.NoProject,
                workspaceRoot,
                Array.Empty<ProjectCandidate>(),
                SelectedProject: null,
                Array.Empty<string>(),
                ErrorMessage: null);
        }

        if (result.SupportedCandidates.Count == 0 && result.UnsupportedFiles.Count > 0)
        {
            return new ProjectContext(
                ProjectContextState.Unsupported,
                workspaceRoot,
                Array.Empty<ProjectCandidate>(),
                SelectedProject: null,
                result.UnsupportedFiles,
                ErrorMessage: null);
        }

        if (result.SupportedCandidates.Count == 1)
        {
            var candidate = result.SupportedCandidates[0];
            return new ProjectContext(
                ProjectContextState.SingleProject,
                workspaceRoot,
                result.SupportedCandidates,
                candidate,
                result.UnsupportedFiles,
                ErrorMessage: null);
        }

        // Multiple supported candidates — Ambiguous with no selection.
        return new ProjectContext(
            ProjectContextState.Ambiguous,
            workspaceRoot,
            result.SupportedCandidates,
            SelectedProject: null,
            result.UnsupportedFiles,
            ErrorMessage: null);
    }
}
