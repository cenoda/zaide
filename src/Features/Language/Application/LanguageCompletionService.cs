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

namespace Zaide.Features.Language.Application;

/// <summary>
/// Owns cancellable active-document completion requests and deterministic selection/commit state.
/// </summary>
public sealed class LanguageCompletionService : ILanguageCompletionService
{
    private readonly global::Zaide.Features.Workspace.Domain.Workspace _workspace;
    private readonly ILanguageSessionService _sessionService;
    private readonly ILanguageDocumentBridge _documentBridge;
    private readonly ILogger<LanguageCompletionService> _logger;
    private readonly Subject<LanguageCompletionSnapshot> _subject = new();
    private readonly object _gate = new();
    private readonly IDisposable _sessionSubscription;

    private LanguageCompletionSnapshot _current = LanguageCompletionSnapshot.Idle;
    private CancellationTokenSource? _requestCts;
    private CancellationTokenSource? _debounceCts;
    private long _requestId;
    private bool _disposed;

    public LanguageCompletionService(
        global::Zaide.Features.Workspace.Domain.Workspace workspace,
        ILanguageSessionService sessionService,
        ILanguageDocumentBridge documentBridge,
        ILogger<LanguageCompletionService> logger)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _documentBridge = documentBridge ?? throw new ArgumentNullException(nameof(documentBridge));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _workspace.DocumentClosed += OnDocumentClosed;
        _sessionSubscription = _sessionService.WhenChanged.Subscribe(OnSessionChanged);
    }

    /// <inheritdoc />
    public LanguageCompletionSnapshot Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    /// <inheritdoc />
    public IObservable<LanguageCompletionSnapshot> WhenChanged => _subject;

    /// <inheritdoc />
    public void RequestExplicit(string filePath, int caretOffset)
    {
        if (_disposed)
            return;

        CancelDebounceLocked();
        BeginRequestLocked(filePath, caretOffset);
    }

    /// <inheritdoc />
    public void RequestAutomatic(string filePath, int caretOffset, char triggerCharacter)
    {
        if (_disposed)
            return;

        var session = _sessionService.Current;
        if (session.State != LanguageSessionState.Ready)
            return;

        var readySession = _sessionService.TryGetReadySession(session.Generation);
        if (readySession is null)
            return;

        if (!readySession.Capabilities.CompletionSupported)
            return;

        if (!IsSupportedTrigger(readySession.Capabilities, triggerCharacter))
            return;

        CancelDebounceLocked();
        _debounceCts = new CancellationTokenSource();
        var debounceToken = _debounceCts.Token;
        ObserveTask(DebouncedAutomaticAsync(filePath, caretOffset, debounceToken));
    }

    /// <inheritdoc />
    public void MoveSelection(int delta)
    {
        if (_disposed || delta == 0)
            return;

        LanguageCompletionSnapshot published;

        lock (_gate)
        {
            if (_disposed || _current.State != LanguageCompletionState.Ready || _current.Items.Count == 0)
                return;

            var count = _current.Items.Count;
            var next = (_current.SelectedIndex + delta) % count;
            if (next < 0)
                next += count;

            published = _current with { SelectedIndex = next };
            _current = published;
        }

        _subject.OnNext(published);
    }

    /// <inheritdoc />
    public LanguageCompletionCommit? TryCommitSelected()
    {
        if (_disposed)
            return null;

        LanguageCompletionSnapshot snapshot;
        lock (_gate)
        {
            snapshot = _current;
        }

        if (snapshot.State != LanguageCompletionState.Ready ||
            snapshot.Items.Count == 0 ||
            string.IsNullOrWhiteSpace(snapshot.FilePath))
        {
            return null;
        }

        var index = snapshot.SelectedIndex;
        if (index < 0 || index >= snapshot.Items.Count)
            return null;

        if (!LanguageActiveDocumentValidator.TryValidate(
                _workspace,
                _sessionService,
                _documentBridge,
                snapshot.FilePath,
                snapshot.SessionGeneration,
                snapshot.DocumentVersion,
                out _))
        {
            Dismiss();
            return null;
        }

        var item = snapshot.Items[index];
        var commit = new LanguageCompletionCommit(
            snapshot.RequestId,
            snapshot.SessionGeneration,
            snapshot.FilePath,
            snapshot.DocumentUri!,
            snapshot.DocumentVersion,
            snapshot.CaretOffset,
            item.ReplaceStartOffset,
            item.ReplaceLength,
            item.InsertText);

        Dismiss();
        return commit;
    }

    /// <inheritdoc />
    public void Dismiss()
    {
        if (_disposed)
            return;

        CancelDebounceLocked();
        CancelRequestLocked();

        LanguageCompletionSnapshot published;
        lock (_gate)
        {
            if (_current.State == LanguageCompletionState.Idle)
                return;

            published = LanguageCompletionSnapshot.Idle;
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
            _current = LanguageCompletionSnapshot.Idle;

        _subject.OnCompleted();
        _subject.Dispose();
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
            if (_current.FilePath is null ||
                !string.Equals(_current.FilePath, path, StringComparison.Ordinal))
            {
                return;
            }
        }

        Dismiss();
    }

    private async Task DebouncedAutomaticAsync(
        string filePath,
        int caretOffset,
        CancellationToken debounceToken)
    {
        try
        {
            await Task.Delay(LanguageCompletionTriggerPolicy.AutomaticDebounce, debounceToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        BeginRequestLocked(filePath, caretOffset);
    }

    private void BeginRequestLocked(string filePath, int caretOffset)
    {
        if (_disposed || string.IsNullOrWhiteSpace(filePath))
            return;

        if (!LanguageDocumentSyncPolicy.IsEligiblePath(filePath))
            return;

        var sessionSnapshot = _sessionService.Current;
        if (sessionSnapshot.State != LanguageSessionState.Ready)
        {
            PublishUnavailable(sessionSnapshot.Generation, filePath, caretOffset, "Language session is not ready.");
            return;
        }

        var session = _sessionService.TryGetReadySession(sessionSnapshot.Generation);
        if (session is null)
        {
            PublishUnavailable(sessionSnapshot.Generation, filePath, caretOffset, "Language session is not ready.");
            return;
        }

        if (!session.Capabilities.CompletionSupported)
        {
            PublishUnavailable(sessionSnapshot.Generation, filePath, caretOffset, "Completion is not supported.");
            return;
        }

        var uri = LanguageDocumentUri.FromPath(filePath);
        if (!_documentBridge.TryGetOpenDocument(uri, out var tracked))
        {
            PublishUnavailable(sessionSnapshot.Generation, filePath, caretOffset, "Document is not synchronized.");
            return;
        }

        if (tracked.Generation != sessionSnapshot.Generation)
        {
            PublishUnavailable(sessionSnapshot.Generation, filePath, caretOffset, "Document is not synchronized.");
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
            Dismiss();
            return;
        }

        var document = FindDocument(filePath);
        if (document is null)
        {
            Dismiss();
            return;
        }

        CancelRequestLocked();
        _requestCts = new CancellationTokenSource();
        var requestId = Interlocked.Increment(ref _requestId);
        var requestToken = _requestCts.Token;

        var loading = new LanguageCompletionSnapshot(
            LanguageCompletionState.Loading,
            requestId,
            sessionSnapshot.Generation,
            tracked.DocumentUri,
            filePath,
            tracked.Version,
            caretOffset,
            SelectedIndex: 0,
            Items: Array.Empty<LanguageCompletionItem>(),
            FailureMessage: null);

        PublishLocked(loading);
        ObserveTask(ExecuteRequestAsync(
            requestId,
            sessionSnapshot.Generation,
            tracked,
            filePath,
            document.Content,
            caretOffset,
            session,
            requestToken));
    }

    private async Task ExecuteRequestAsync(
        long requestId,
        long generation,
        LanguageTrackedDocumentInfo tracked,
        string filePath,
        string documentText,
        int caretOffset,
        ILanguageServerSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            var (line, character) = LspUtf16PositionMapper.GetPosition(documentText, caretOffset);
            var result = await session.RequestCompletionAsync(
                    tracked.DocumentUri,
                    line,
                    character,
                    cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
                return;

            if (!LanguageActiveDocumentValidator.TryValidate(
                    _workspace,
                    _sessionService,
                    _documentBridge,
                    filePath,
                    generation,
                    tracked.Version,
                    out _))
            {
                return;
            }

            var currentSession = _sessionService.Current;
            if (currentSession.State != LanguageSessionState.Ready ||
                currentSession.Generation != generation)
            {
                return;
            }

            lock (_gate)
            {
                if (_disposed || _current.RequestId != requestId)
                    return;
            }

            if (result is null)
            {
                PublishFailure(requestId, generation, tracked, filePath, caretOffset, "Completion request failed.");
                return;
            }

            var mapped = LanguageCompletionItemMapper.MapItems(documentText, caretOffset, result.Items);
            if (mapped.Count == 0)
            {
                PublishEmpty(requestId, generation, tracked, filePath, caretOffset);
                return;
            }

            var ready = new LanguageCompletionSnapshot(
                LanguageCompletionState.Ready,
                requestId,
                generation,
                tracked.DocumentUri,
                filePath,
                tracked.Version,
                caretOffset,
                SelectedIndex: 0,
                mapped,
                FailureMessage: null);

            PublishLocked(ready);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer request or dismiss.
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Debug, new EventId(10020), ex,
                "Completion request failed for {FilePath}", filePath);

            if (!cancellationToken.IsCancellationRequested)
            {
                PublishFailure(requestId, generation, tracked, filePath, caretOffset, ex.Message);
            }
        }
    }

    private void PublishUnavailable(long generation, string filePath, int caretOffset, string message)
    {
        var snapshot = new LanguageCompletionSnapshot(
            LanguageCompletionState.Unavailable,
            Interlocked.Increment(ref _requestId),
            generation,
            LanguageDocumentUri.FromPath(filePath),
            filePath,
            DocumentVersion: 0,
            caretOffset,
            SelectedIndex: 0,
            Items: Array.Empty<LanguageCompletionItem>(),
            message);

        PublishLocked(snapshot);

        // Unavailable never opens a popup — return to idle immediately.
        PublishLocked(LanguageCompletionSnapshot.Idle);
    }

    private void PublishEmpty(
        long requestId,
        long generation,
        LanguageTrackedDocumentInfo tracked,
        string filePath,
        int caretOffset)
    {
        var empty = new LanguageCompletionSnapshot(
            LanguageCompletionState.Empty,
            requestId,
            generation,
            tracked.DocumentUri,
            filePath,
            tracked.Version,
            caretOffset,
            SelectedIndex: 0,
            Items: Array.Empty<LanguageCompletionItem>(),
            FailureMessage: null);

        PublishLocked(empty);
        PublishLocked(LanguageCompletionSnapshot.Idle);
    }

    private void PublishFailure(
        long requestId,
        long generation,
        LanguageTrackedDocumentInfo tracked,
        string filePath,
        int caretOffset,
        string message)
    {
        var failed = new LanguageCompletionSnapshot(
            LanguageCompletionState.Failed,
            requestId,
            generation,
            tracked.DocumentUri,
            filePath,
            tracked.Version,
            caretOffset,
            SelectedIndex: 0,
            Items: Array.Empty<LanguageCompletionItem>(),
            message);

        PublishLocked(failed);
        PublishLocked(LanguageCompletionSnapshot.Idle);
    }

    private void PublishLocked(LanguageCompletionSnapshot snapshot)
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _current = snapshot;
        }

        _subject.OnNext(snapshot);
    }

    private Document? FindDocument(string filePath)
    {
        foreach (var document in _workspace.Documents)
        {
            if (string.Equals(document.FilePath, filePath, StringComparison.Ordinal))
                return document;
        }

        return null;
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

    private static bool IsSupportedTrigger(LanguageServerCapabilities capabilities, char triggerCharacter)
    {
        foreach (var supported in capabilities.CompletionTriggerCharacters)
        {
            if (supported == triggerCharacter)
                return true;
        }

        return false;
    }

    private static void ObserveTask(Task task)
    {
        _ = task.ContinueWith(
            t => { _ = t.Exception; },
            TaskScheduler.Default);
    }
}
