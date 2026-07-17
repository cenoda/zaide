using System;
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
/// Owns cancellable active-document hover requests with bounded dwell scheduling.
/// </summary>
public sealed class LanguageHoverService : ILanguageHoverService
{
    private readonly global::Zaide.Features.Workspace.Domain.Workspace _workspace;
    private readonly ILanguageSessionService _sessionService;
    private readonly ILanguageDocumentBridge _documentBridge;
    private readonly ILogger<LanguageHoverService> _logger;
    private readonly Subject<LanguageHoverSnapshot> _subject = new();
    private readonly object _gate = new();
    private readonly IDisposable _sessionSubscription;

    private LanguageHoverSnapshot _current = LanguageHoverSnapshot.Idle;
    private CancellationTokenSource? _scheduleCts;
    private CancellationTokenSource? _requestCts;
    private long _requestId;
    private bool _disposed;

    public LanguageHoverService(
        global::Zaide.Features.Workspace.Domain.Workspace workspace,
        ILanguageSessionService sessionService,
        ILanguageDocumentBridge documentBridge,
        ILogger<LanguageHoverService> logger)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _documentBridge = documentBridge ?? throw new ArgumentNullException(nameof(documentBridge));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _workspace.DocumentClosed += OnDocumentClosed;
        _sessionSubscription = _sessionService.WhenChanged.Subscribe(OnSessionChanged);
    }

    /// <inheritdoc />
    public LanguageHoverSnapshot Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    /// <inheritdoc />
    public IObservable<LanguageHoverSnapshot> WhenChanged => _subject;

    /// <inheritdoc />
    public void Schedule(string filePath, int caretOffset)
    {
        if (_disposed || string.IsNullOrWhiteSpace(filePath))
            return;

        if (!LanguageDocumentSyncPolicy.IsEligiblePath(filePath))
            return;

        CancelScheduleLocked();
        CancelRequestLocked();
        PublishLocked(LanguageHoverSnapshot.Idle);

        _scheduleCts = new CancellationTokenSource();
        var scheduleToken = _scheduleCts.Token;
        ObserveTask(ScheduleAsync(filePath, caretOffset, scheduleToken));
    }

    /// <inheritdoc />
    public void Dismiss()
    {
        if (_disposed)
            return;

        CancelScheduleLocked();
        CancelRequestLocked();

        LanguageHoverSnapshot published;
        lock (_gate)
        {
            if (_current.State == LanguageHoverState.Idle)
                return;

            published = LanguageHoverSnapshot.Idle;
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
        CancelScheduleLocked();
        CancelRequestLocked();

        lock (_gate)
            _current = LanguageHoverSnapshot.Idle;

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

    private async Task ScheduleAsync(string filePath, int caretOffset, CancellationToken scheduleToken)
    {
        try
        {
            await Task.Delay(LanguageHoverTriggerPolicy.DwellDelay, scheduleToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        BeginRequest(filePath, caretOffset);
    }

    private void BeginRequest(string filePath, int caretOffset)
    {
        if (_disposed)
            return;

        var sessionSnapshot = _sessionService.Current;
        if (sessionSnapshot.State != LanguageSessionState.Ready)
        {
            PublishUnavailable(sessionSnapshot.Generation, filePath, caretOffset);
            return;
        }

        var session = _sessionService.TryGetReadySession(sessionSnapshot.Generation);
        if (session is null)
        {
            PublishUnavailable(sessionSnapshot.Generation, filePath, caretOffset);
            return;
        }

        if (!session.Capabilities.HoverSupported)
        {
            PublishUnavailable(sessionSnapshot.Generation, filePath, caretOffset);
            return;
        }

        var uri = LanguageDocumentUri.FromPath(filePath);
        if (!_documentBridge.TryGetOpenDocument(uri, out var tracked))
        {
            PublishUnavailable(sessionSnapshot.Generation, filePath, caretOffset);
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

        var loading = new LanguageHoverSnapshot(
            LanguageHoverState.Loading,
            requestId,
            sessionSnapshot.Generation,
            tracked.DocumentUri,
            filePath,
            tracked.Version,
            caretOffset,
            Content: null,
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
            var result = await session.RequestHoverAsync(
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
                PublishFailure(requestId, generation, tracked, filePath, caretOffset, "Hover request failed.");
                return;
            }

            if (string.IsNullOrWhiteSpace(result.Content))
            {
                PublishEmpty(requestId, generation, tracked, filePath, caretOffset);
                return;
            }

            var ready = new LanguageHoverSnapshot(
                LanguageHoverState.Ready,
                requestId,
                generation,
                tracked.DocumentUri,
                filePath,
                tracked.Version,
                caretOffset,
                result.Content,
                FailureMessage: null);

            PublishLocked(ready);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer request or dismiss.
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Debug, new EventId(10021), ex,
                "Hover request failed for {FilePath}", filePath);

            if (!cancellationToken.IsCancellationRequested)
            {
                PublishFailure(requestId, generation, tracked, filePath, caretOffset, ex.Message);
            }
        }
    }

    private void PublishUnavailable(long generation, string filePath, int caretOffset)
    {
        var snapshot = new LanguageHoverSnapshot(
            LanguageHoverState.Unavailable,
            Interlocked.Increment(ref _requestId),
            generation,
            LanguageDocumentUri.FromPath(filePath),
            filePath,
            DocumentVersion: 0,
            caretOffset,
            Content: null,
            FailureMessage: "Hover is not available.");

        PublishLocked(snapshot);
        PublishLocked(LanguageHoverSnapshot.Idle);
    }

    private void PublishEmpty(
        long requestId,
        long generation,
        LanguageTrackedDocumentInfo tracked,
        string filePath,
        int caretOffset)
    {
        var empty = new LanguageHoverSnapshot(
            LanguageHoverState.Empty,
            requestId,
            generation,
            tracked.DocumentUri,
            filePath,
            tracked.Version,
            caretOffset,
            Content: null,
            FailureMessage: null);

        PublishLocked(empty);
        PublishLocked(LanguageHoverSnapshot.Idle);
    }

    private void PublishFailure(
        long requestId,
        long generation,
        LanguageTrackedDocumentInfo tracked,
        string filePath,
        int caretOffset,
        string message)
    {
        var failed = new LanguageHoverSnapshot(
            LanguageHoverState.Failed,
            requestId,
            generation,
            tracked.DocumentUri,
            filePath,
            tracked.Version,
            caretOffset,
            Content: null,
            message);

        PublishLocked(failed);
        PublishLocked(LanguageHoverSnapshot.Idle);
    }

    private void PublishLocked(LanguageHoverSnapshot snapshot)
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

    private void CancelScheduleLocked()
    {
        _scheduleCts?.Cancel();
        _scheduleCts?.Dispose();
        _scheduleCts = null;
    }

    private static void ObserveTask(Task task)
    {
        _ = task.ContinueWith(
            t => { _ = t.Exception; },
            TaskScheduler.Default);
    }
}
