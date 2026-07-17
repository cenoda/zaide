using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Zaide.Models;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Language.Contracts;
using Zaide.Services;
using Zaide.Features.Language.Infrastructure.Lsp;

namespace Zaide.Features.Language.Application;

/// <summary>
/// Owns cancellable document and workspace symbol requests with stale-result protection.
/// </summary>
public sealed class LanguageSymbolService : ILanguageSymbolService
{
    private readonly global::Zaide.Features.Workspace.Domain.Workspace _workspace;
    private readonly ILanguageSessionService _sessionService;
    private readonly ILanguageDocumentBridge _documentBridge;
    private readonly ILogger<LanguageSymbolService> _logger;
    private readonly Subject<LanguageSymbolSnapshot> _subject = new();
    private readonly object _gate = new();
    private readonly IDisposable _sessionSubscription;

    private LanguageSymbolSnapshot _current = LanguageSymbolSnapshot.Idle;
    private CancellationTokenSource? _requestCts;
    private CancellationTokenSource? _debounceCts;
    private long _requestId;
    private bool _disposed;

    public LanguageSymbolService(
        global::Zaide.Features.Workspace.Domain.Workspace workspace,
        ILanguageSessionService sessionService,
        ILanguageDocumentBridge documentBridge,
        ILogger<LanguageSymbolService> logger)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _documentBridge = documentBridge ?? throw new ArgumentNullException(nameof(documentBridge));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _workspace.DocumentClosed += OnDocumentClosed;
        _sessionSubscription = _sessionService.WhenChanged.Subscribe(OnSessionChanged);
    }

    /// <inheritdoc />
    public LanguageSymbolSnapshot Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    /// <inheritdoc />
    public IObservable<LanguageSymbolSnapshot> WhenChanged => _subject;

    /// <inheritdoc />
    public void RequestDocumentSymbols(string filePath)
    {
        if (_disposed || string.IsNullOrWhiteSpace(filePath))
            return;

        if (!LanguageDocumentSyncPolicy.IsEligiblePath(filePath))
            return;

        CancelDebounceLocked();
        CancelRequestLocked();

        var sessionSnapshot = _sessionService.Current;
        if (sessionSnapshot.State != LanguageSessionState.Ready)
        {
            PublishTerminal(
                LanguageSymbolScope.Document,
                LanguageSymbolState.Unavailable,
                sessionSnapshot.Generation,
                filePath,
                query: string.Empty,
                LanguageSymbolPolicy.DocumentUnavailableMessage);
            return;
        }

        var session = _sessionService.TryGetReadySession(sessionSnapshot.Generation);
        if (session is null || !session.Capabilities.DocumentSymbolSupported)
        {
            PublishTerminal(
                LanguageSymbolScope.Document,
                LanguageSymbolState.Unavailable,
                sessionSnapshot.Generation,
                filePath,
                query: string.Empty,
                LanguageSymbolPolicy.DocumentUnavailableMessage);
            return;
        }

        var uri = LanguageDocumentUri.FromPath(filePath);
        if (!_documentBridge.TryGetOpenDocument(uri, out var tracked) ||
            tracked.Generation != sessionSnapshot.Generation)
        {
            PublishTerminal(
                LanguageSymbolScope.Document,
                LanguageSymbolState.Unavailable,
                sessionSnapshot.Generation,
                filePath,
                query: string.Empty,
                LanguageSymbolPolicy.DocumentUnavailableMessage);
            return;
        }

        if (!LanguageActiveDocumentValidator.TryValidate(
                _workspace,
                _sessionService,
                _documentBridge,
                filePath,
                sessionSnapshot.Generation,
                tracked.Version,
                out tracked))
        {
            PublishTerminal(
                LanguageSymbolScope.Document,
                LanguageSymbolState.Unavailable,
                sessionSnapshot.Generation,
                filePath,
                query: string.Empty,
                LanguageSymbolPolicy.DocumentUnavailableMessage);
            return;
        }

        _requestCts = new CancellationTokenSource();
        var requestId = Interlocked.Increment(ref _requestId);
        var requestToken = _requestCts.Token;

        var loading = new LanguageSymbolSnapshot(
            LanguageSymbolState.Loading,
            LanguageSymbolScope.Document,
            requestId,
            sessionSnapshot.Generation,
            tracked.DocumentUri,
            filePath,
            tracked.Version,
            Query: string.Empty,
            SelectedIndex: 0,
            Symbols: Array.Empty<LanguageSymbol>(),
            FeedbackMessage: null);

        PublishLocked(loading);
        ObserveTask(ExecuteDocumentRequestAsync(
            requestId,
            sessionSnapshot.Generation,
            tracked,
            filePath,
            session,
            requestToken));
    }

    /// <inheritdoc />
    public void RequestWorkspaceSymbols(string query)
    {
        if (_disposed)
            return;

        query ??= string.Empty;
        CancelDebounceLocked();
        CancelRequestLocked();

        var sessionSnapshot = _sessionService.Current;
        if (sessionSnapshot.State != LanguageSessionState.Ready)
        {
            PublishTerminal(
                LanguageSymbolScope.Workspace,
                LanguageSymbolState.Unavailable,
                sessionSnapshot.Generation,
                filePath: null,
                query,
                LanguageSymbolPolicy.WorkspaceUnavailableMessage);
            return;
        }

        var session = _sessionService.TryGetReadySession(sessionSnapshot.Generation);
        if (session is null || !session.Capabilities.WorkspaceSymbolSupported)
        {
            PublishTerminal(
                LanguageSymbolScope.Workspace,
                LanguageSymbolState.Unavailable,
                sessionSnapshot.Generation,
                filePath: null,
                query,
                LanguageSymbolPolicy.WorkspaceUnavailableMessage);
            return;
        }

        var requestId = Interlocked.Increment(ref _requestId);
        var loading = new LanguageSymbolSnapshot(
            LanguageSymbolState.Loading,
            LanguageSymbolScope.Workspace,
            requestId,
            sessionSnapshot.Generation,
            DocumentUri: null,
            FilePath: null,
            DocumentVersion: 0,
            query,
            SelectedIndex: 0,
            Symbols: Array.Empty<LanguageSymbol>(),
            FeedbackMessage: null);

        PublishLocked(loading);

        _debounceCts = new CancellationTokenSource();
        var debounceToken = _debounceCts.Token;
        ObserveTask(DebouncedWorkspaceAsync(requestId, sessionSnapshot.Generation, query, session, debounceToken));
    }

    /// <inheritdoc />
    public void SetWorkspaceQuery(string query)
    {
        if (_disposed)
            return;

        LanguageSymbolSnapshot current;
        lock (_gate)
            current = _current;

        if (current.Scope != LanguageSymbolScope.Workspace)
            return;

        RequestWorkspaceSymbols(query ?? string.Empty);
    }

    /// <inheritdoc />
    public void MoveSelection(int delta)
    {
        if (_disposed || delta == 0)
            return;

        LanguageSymbolSnapshot published;
        lock (_gate)
        {
            if (_disposed ||
                _current.State != LanguageSymbolState.Ready ||
                _current.Symbols.Count == 0)
            {
                return;
            }

            var count = _current.Symbols.Count;
            var next = (_current.SelectedIndex + delta) % count;
            if (next < 0)
                next += count;

            published = _current with { SelectedIndex = next };
            _current = published;
        }

        _subject.OnNext(published);
    }

    /// <inheritdoc />
    public LanguageLocation? TryAcceptSelected()
    {
        if (_disposed)
            return null;

        LanguageSymbolSnapshot snapshot;
        lock (_gate)
            snapshot = _current;

        if (snapshot.State != LanguageSymbolState.Ready ||
            snapshot.Symbols.Count == 0)
        {
            return null;
        }

        var index = snapshot.SelectedIndex;
        if (index < 0 || index >= snapshot.Symbols.Count)
            return null;

        if (!IsSnapshotStillLive(snapshot))
        {
            Dismiss();
            return null;
        }

        var symbol = snapshot.Symbols[index];
        var location = symbol.Location;
        if (location is null ||
            string.IsNullOrWhiteSpace(location.FilePath) ||
            string.IsNullOrWhiteSpace(location.DocumentUri))
        {
            return null;
        }

        Dismiss();
        return location;
    }

    /// <inheritdoc />
    public void Dismiss()
    {
        if (_disposed)
            return;

        CancelDebounceLocked();
        CancelRequestLocked();

        LanguageSymbolSnapshot published;
        lock (_gate)
        {
            if (_current.State == LanguageSymbolState.Idle &&
                _current.Scope == LanguageSymbolScope.None)
            {
                return;
            }

            published = LanguageSymbolSnapshot.Idle;
            _current = published;
        }

        _subject.OnNext(published);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _workspace.DocumentClosed -= OnDocumentClosed;
        _sessionSubscription.Dispose();
        CancelDebounceLocked();
        CancelRequestLocked();

        lock (_gate)
            _current = LanguageSymbolSnapshot.Idle;

        _subject.OnCompleted();
        _subject.Dispose();
    }

    private bool IsSnapshotStillLive(LanguageSymbolSnapshot snapshot)
    {
        var session = _sessionService.Current;
        if (session.State != LanguageSessionState.Ready ||
            session.Generation != snapshot.SessionGeneration)
        {
            return false;
        }

        if (snapshot.Scope == LanguageSymbolScope.Document)
        {
            if (string.IsNullOrWhiteSpace(snapshot.FilePath))
                return false;

            return LanguageActiveDocumentValidator.TryValidate(
                _workspace,
                _sessionService,
                _documentBridge,
                snapshot.FilePath,
                snapshot.SessionGeneration,
                snapshot.DocumentVersion,
                out _);
        }

        // Workspace symbols: generation must still match; document is not required.
        return snapshot.Scope == LanguageSymbolScope.Workspace;
    }

    private void OnSessionChanged(LanguageSessionSnapshot snapshot)
    {
        if (_disposed)
            return;

        if (snapshot.State == LanguageSessionState.Ready)
            return;

        Dismiss();
    }

    private void OnDocumentClosed(object? sender, string path)
    {
        if (_disposed)
            return;

        lock (_gate)
        {
            if (_current.Scope != LanguageSymbolScope.Document ||
                _current.FilePath is null ||
                !string.Equals(_current.FilePath, path, StringComparison.Ordinal))
            {
                return;
            }
        }

        Dismiss();
    }

    private async Task DebouncedWorkspaceAsync(
        long requestId,
        long generation,
        string query,
        ILanguageServerSession session,
        CancellationToken debounceToken)
    {
        try
        {
            await Task.Delay(LanguageSymbolPolicy.WorkspaceQueryDebounce, debounceToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_disposed)
            return;

        lock (_gate)
        {
            if (_current.RequestId != requestId ||
                _current.Scope != LanguageSymbolScope.Workspace)
            {
                return;
            }
        }

        CancelRequestLocked();
        _requestCts = new CancellationTokenSource();
        var requestToken = _requestCts.Token;
        ObserveTask(ExecuteWorkspaceRequestAsync(requestId, generation, query, session, requestToken));
    }

    private async Task ExecuteDocumentRequestAsync(
        long requestId,
        long generation,
        LanguageTrackedDocumentInfo tracked,
        string filePath,
        ILanguageServerSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await session.RequestDocumentSymbolsAsync(
                    tracked.DocumentUri,
                    cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                DismissStaleIfCurrentLocked(requestId);
                return;
            }

            if (!LanguageActiveDocumentValidator.TryValidate(
                    _workspace,
                    _sessionService,
                    _documentBridge,
                    filePath,
                    generation,
                    tracked.Version,
                    out _))
            {
                // Stale — must not alter the symbol surface.
                DismissStaleIfCurrentLocked(requestId);
                return;
            }

            var currentSession = _sessionService.Current;
            if (currentSession.State != LanguageSessionState.Ready ||
                currentSession.Generation != generation)
            {
                DismissStaleIfCurrentLocked(requestId);
                return;
            }

            lock (_gate)
            {
                if (_disposed || _current.RequestId != requestId)
                    return;
            }

            if (result is null)
            {
                PublishTerminal(
                    LanguageSymbolScope.Document,
                    LanguageSymbolState.Failed,
                    generation,
                    filePath,
                    query: string.Empty,
                    LanguageSymbolPolicy.DocumentFailedMessage,
                    requestId,
                    tracked);
                return;
            }

            var bound = LanguageServerSymbolParser.BindDocumentUri(
                result.Symbols,
                tracked.DocumentUri,
                filePath);
            var flat = LanguageServerSymbolParser.Flatten(bound);

            if (flat.Count == 0)
            {
                var empty = new LanguageSymbolSnapshot(
                    LanguageSymbolState.Empty,
                    LanguageSymbolScope.Document,
                    requestId,
                    generation,
                    tracked.DocumentUri,
                    filePath,
                    tracked.Version,
                    Query: string.Empty,
                    SelectedIndex: 0,
                    Symbols: Array.Empty<LanguageSymbol>(),
                    LanguageSymbolPolicy.DocumentEmptyMessage);

                PublishLocked(empty);
                return;
            }

            var ready = new LanguageSymbolSnapshot(
                LanguageSymbolState.Ready,
                LanguageSymbolScope.Document,
                requestId,
                generation,
                tracked.DocumentUri,
                filePath,
                tracked.Version,
                Query: string.Empty,
                SelectedIndex: 0,
                flat,
                FeedbackMessage: null);

            PublishLocked(ready);
        }
        catch (OperationCanceledException)
        {
            DismissStaleIfCurrentLocked(requestId);
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Debug, new EventId(10031), ex,
                "Document symbol request failed for {FilePath}", filePath);

            if (!cancellationToken.IsCancellationRequested)
            {
                PublishTerminal(
                    LanguageSymbolScope.Document,
                    LanguageSymbolState.Failed,
                    generation,
                    filePath,
                    query: string.Empty,
                    LanguageSymbolPolicy.DocumentFailedMessage,
                    requestId,
                    tracked);
            }
            else
            {
                DismissStaleIfCurrentLocked(requestId);
            }
        }
    }

    private async Task ExecuteWorkspaceRequestAsync(
        long requestId,
        long generation,
        string query,
        ILanguageServerSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await session.RequestWorkspaceSymbolsAsync(query, cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                DismissStaleIfCurrentLocked(requestId);
                return;
            }

            var currentSession = _sessionService.Current;
            if (currentSession.State != LanguageSessionState.Ready ||
                currentSession.Generation != generation)
            {
                DismissStaleIfCurrentLocked(requestId);
                return;
            }

            lock (_gate)
            {
                if (_disposed ||
                    _current.RequestId != requestId ||
                    _current.Scope != LanguageSymbolScope.Workspace)
                {
                    return;
                }
            }

            if (result is null)
            {
                PublishTerminal(
                    LanguageSymbolScope.Workspace,
                    LanguageSymbolState.Failed,
                    generation,
                    filePath: null,
                    query,
                    LanguageSymbolPolicy.WorkspaceFailedMessage,
                    requestId);
                return;
            }

            // Filter to valid navigable locations; order deterministically.
            var ordered = LanguageServerSymbolParser.OrderSiblings(result.Symbols);
            var navigable = new List<LanguageSymbol>();
            foreach (var symbol in ordered)
            {
                if (symbol.Location is null ||
                    string.IsNullOrWhiteSpace(symbol.Location.FilePath))
                {
                    continue;
                }

                navigable.Add(symbol with { Children = Array.Empty<LanguageSymbol>(), Depth = 0 });
            }

            if (navigable.Count == 0)
            {
                var empty = new LanguageSymbolSnapshot(
                    LanguageSymbolState.Empty,
                    LanguageSymbolScope.Workspace,
                    requestId,
                    generation,
                    DocumentUri: null,
                    FilePath: null,
                    DocumentVersion: 0,
                    query,
                    SelectedIndex: 0,
                    Symbols: Array.Empty<LanguageSymbol>(),
                    LanguageSymbolPolicy.WorkspaceEmptyMessage);

                PublishLocked(empty);
                return;
            }

            var ready = new LanguageSymbolSnapshot(
                LanguageSymbolState.Ready,
                LanguageSymbolScope.Workspace,
                requestId,
                generation,
                DocumentUri: null,
                FilePath: null,
                DocumentVersion: 0,
                query,
                SelectedIndex: 0,
                navigable,
                FeedbackMessage: null);

            PublishLocked(ready);
        }
        catch (OperationCanceledException)
        {
            DismissStaleIfCurrentLocked(requestId);
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Debug, new EventId(10032), ex,
                "Workspace symbol request failed for query {Query}", query);

            if (!cancellationToken.IsCancellationRequested)
            {
                PublishTerminal(
                    LanguageSymbolScope.Workspace,
                    LanguageSymbolState.Failed,
                    generation,
                    filePath: null,
                    query,
                    LanguageSymbolPolicy.WorkspaceFailedMessage,
                    requestId);
            }
            else
            {
                DismissStaleIfCurrentLocked(requestId);
            }
        }
    }

    private void DismissStaleIfCurrentLocked(long requestId)
    {
        LanguageSymbolScope scope;
        long generation;
        string? filePath;
        string query;

        lock (_gate)
        {
            if (_disposed || _current.RequestId != requestId)
                return;

            scope = _current.Scope;
            generation = _current.SessionGeneration;
            filePath = _current.FilePath;
            query = _current.Query;
        }

        PublishTerminal(
            scope,
            LanguageSymbolState.Cancelled,
            generation,
            filePath,
            query,
            "Request was cancelled or superseded.",
            requestId);
    }

    private void PublishTerminal(
        LanguageSymbolScope scope,
        LanguageSymbolState state,
        long generation,
        string? filePath,
        string query,
        string message,
        long? requestId = null,
        LanguageTrackedDocumentInfo? tracked = null)
    {
        var snapshot = new LanguageSymbolSnapshot(
            state,
            scope,
            requestId ?? Interlocked.Increment(ref _requestId),
            generation,
            tracked?.DocumentUri ?? (filePath is null ? null : LanguageDocumentUri.FromPath(filePath)),
            filePath,
            tracked?.Version ?? 0,
            query,
            SelectedIndex: 0,
            Symbols: Array.Empty<LanguageSymbol>(),
            message);

        PublishLocked(snapshot);

        // Unavailable/failed/cancelled collapse to idle so stale surfaces cannot linger.
        if (state is LanguageSymbolState.Unavailable or LanguageSymbolState.Failed or LanguageSymbolState.Cancelled)
            PublishLocked(LanguageSymbolSnapshot.Idle);
    }

    private void PublishLocked(LanguageSymbolSnapshot snapshot)
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _current = snapshot;
        }

        _subject.OnNext(snapshot);
    }

    private void CancelRequestLocked()
    {
        _requestCts?.Cancel();
        _requestCts?.Dispose();
        _requestCts = null;
    }

    private void CancelDebounceLocked()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;
    }

    private static void ObserveTask(Task task)
    {
        _ = task.ContinueWith(
            t => { _ = t.Exception; },
            TaskScheduler.Default);
    }
}
