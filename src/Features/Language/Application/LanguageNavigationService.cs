using System;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Language.Contracts;
using Zaide.App.Composition;
using Zaide.Features.Language.Infrastructure.Lsp;

namespace Zaide.Features.Language.Application;

/// <summary>
/// Owns cancellable Go to Definition requests with generation/version/active-document checks.
/// Never opens tabs or mutates selection — callers navigate only after accepting a live result.
/// </summary>
internal sealed class LanguageNavigationService : ILanguageNavigationService
{
    private readonly global::Zaide.Features.Workspace.Domain.Workspace _workspace;
    private readonly ILanguageSessionService _sessionService;
    private readonly ILanguageDocumentBridge _documentBridge;
    private readonly ILogger<LanguageNavigationService> _logger;
    private readonly Subject<LanguageNavigationSnapshot> _subject = new();
    private readonly object _gate = new();
    private readonly IDisposable _sessionSubscription;

    private LanguageNavigationSnapshot _current = LanguageNavigationSnapshot.Idle;
    private CancellationTokenSource? _requestCts;
    private long _requestId;
    private bool _disposed;

    public LanguageNavigationService(
        global::Zaide.Features.Workspace.Domain.Workspace workspace,
        ILanguageSessionService sessionService,
        ILanguageDocumentBridge documentBridge,
        ILogger<LanguageNavigationService> logger)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _documentBridge = documentBridge ?? throw new ArgumentNullException(nameof(documentBridge));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _workspace.DocumentClosed += OnDocumentClosed;
        _sessionSubscription = _sessionService.WhenChanged.Subscribe(OnSessionChanged);
    }

    /// <inheritdoc />
    public LanguageNavigationSnapshot Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    /// <inheritdoc />
    public IObservable<LanguageNavigationSnapshot> WhenChanged => _subject;

    /// <inheritdoc />
    public void RequestDefinition(string filePath, int caretOffset)
    {
        if (_disposed || string.IsNullOrWhiteSpace(filePath))
            return;

        if (!LanguageDocumentSyncPolicy.IsEligiblePath(filePath))
            return;

        CancelRequestLocked();

        var sessionSnapshot = _sessionService.Current;
        if (sessionSnapshot.State != LanguageSessionState.Ready)
        {
            PublishTerminal(
                LanguageNavigationState.Unavailable,
                sessionSnapshot.Generation,
                filePath,
                caretOffset,
                LanguageNavigationPolicy.UnavailableMessage);
            return;
        }

        var session = _sessionService.TryGetReadySession(sessionSnapshot.Generation);
        if (session is null || !session.Capabilities.DefinitionSupported)
        {
            PublishTerminal(
                LanguageNavigationState.Unavailable,
                sessionSnapshot.Generation,
                filePath,
                caretOffset,
                LanguageNavigationPolicy.UnavailableMessage);
            return;
        }

        var uri = LanguageDocumentUri.FromPath(filePath);
        if (!_documentBridge.TryGetOpenDocument(uri, out var tracked) ||
            tracked.Generation != sessionSnapshot.Generation)
        {
            PublishTerminal(
                LanguageNavigationState.Unavailable,
                sessionSnapshot.Generation,
                filePath,
                caretOffset,
                LanguageNavigationPolicy.UnavailableMessage);
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
                LanguageNavigationState.Unavailable,
                sessionSnapshot.Generation,
                filePath,
                caretOffset,
                LanguageNavigationPolicy.UnavailableMessage);
            return;
        }

        var document = FindDocument(filePath);
        if (document is null)
        {
            PublishTerminal(
                LanguageNavigationState.Unavailable,
                sessionSnapshot.Generation,
                filePath,
                caretOffset,
                LanguageNavigationPolicy.UnavailableMessage);
            return;
        }

        _requestCts = new CancellationTokenSource();
        var requestId = Interlocked.Increment(ref _requestId);
        var requestToken = _requestCts.Token;

        var loading = new LanguageNavigationSnapshot(
            LanguageNavigationState.Loading,
            requestId,
            sessionSnapshot.Generation,
            tracked.DocumentUri,
            filePath,
            tracked.Version,
            caretOffset,
            SelectedIndex: 0,
            Locations: Array.Empty<LanguageLocation>(),
            FeedbackMessage: null);

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

    /// <inheritdoc />
    public void MoveSelection(int delta)
    {
        if (_disposed || delta == 0)
            return;

        LanguageNavigationSnapshot published;
        lock (_gate)
        {
            if (_disposed ||
                _current.State != LanguageNavigationState.Choose ||
                _current.Locations.Count == 0)
            {
                return;
            }

            var count = _current.Locations.Count;
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

        LanguageNavigationSnapshot snapshot;
        lock (_gate)
            snapshot = _current;

        if (snapshot.State != LanguageNavigationState.Choose ||
            snapshot.Locations.Count == 0 ||
            string.IsNullOrWhiteSpace(snapshot.SourceFilePath))
        {
            return null;
        }

        var index = snapshot.SelectedIndex;
        if (index < 0 || index >= snapshot.Locations.Count)
            return null;

        if (!IsSourceStillLive(snapshot))
        {
            Dismiss();
            return null;
        }

        var location = snapshot.Locations[index];
        Dismiss();
        return location;
    }

    /// <inheritdoc />
    public LanguageLocation? TryTakeSingleLocation()
    {
        if (_disposed)
            return null;

        LanguageNavigationSnapshot snapshot;
        lock (_gate)
            snapshot = _current;

        if (!snapshot.IsSingleNavigateReady ||
            string.IsNullOrWhiteSpace(snapshot.SourceFilePath))
        {
            return null;
        }

        if (!IsSourceStillLive(snapshot))
        {
            Dismiss();
            return null;
        }

        var location = snapshot.Locations[0];
        Dismiss();
        return location;
    }

    /// <inheritdoc />
    public void Dismiss()
    {
        if (_disposed)
            return;

        CancelRequestLocked();

        LanguageNavigationSnapshot published;
        lock (_gate)
        {
            if (_current.State == LanguageNavigationState.Idle)
                return;

            published = LanguageNavigationSnapshot.Idle;
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
        CancelRequestLocked();

        lock (_gate)
            _current = LanguageNavigationSnapshot.Idle;

        _subject.OnCompleted();
        _subject.Dispose();
    }

    private bool IsSourceStillLive(LanguageNavigationSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.SourceFilePath))
            return false;

        return LanguageActiveDocumentValidator.TryValidate(
            _workspace,
            _sessionService,
            _documentBridge,
            snapshot.SourceFilePath,
            snapshot.SessionGeneration,
            snapshot.SourceDocumentVersion,
            out _);
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
            if (_current.SourceFilePath is null ||
                !string.Equals(_current.SourceFilePath, path, StringComparison.Ordinal))
            {
                return;
            }
        }

        Dismiss();
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
            var result = await session.RequestDefinitionAsync(
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
                // Stale: must not navigate, open tabs, or update surfaces.
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
                PublishTerminal(
                    LanguageNavigationState.Failed,
                    generation,
                    filePath,
                    caretOffset,
                    LanguageNavigationPolicy.FailedMessage,
                    requestId,
                    tracked);
                return;
            }

            var valid = LanguageLocationOrdering.FilterValid(result.Locations);
            if (valid.Count == 0)
            {
                // Zero results or only invalid URIs/ranges — truthful non-error feedback.
                var message = result.Locations.Count == 0
                    ? LanguageNavigationPolicy.NotFoundMessage
                    : LanguageNavigationPolicy.InvalidMessage;

                PublishTerminal(
                    result.Locations.Count == 0
                        ? LanguageNavigationState.Empty
                        : LanguageNavigationState.Failed,
                    generation,
                    filePath,
                    caretOffset,
                    message,
                    requestId,
                    tracked);
                return;
            }

            if (valid.Count == 1)
            {
                var ready = new LanguageNavigationSnapshot(
                    LanguageNavigationState.Ready,
                    requestId,
                    generation,
                    tracked.DocumentUri,
                    filePath,
                    tracked.Version,
                    caretOffset,
                    SelectedIndex: 0,
                    valid,
                    FeedbackMessage: null);

                PublishLocked(ready);
                return;
            }

            var choose = new LanguageNavigationSnapshot(
                LanguageNavigationState.Choose,
                requestId,
                generation,
                tracked.DocumentUri,
                filePath,
                tracked.Version,
                caretOffset,
                SelectedIndex: 0,
                valid,
                FeedbackMessage: null);

            PublishLocked(choose);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer request or dismiss — no surface mutation.
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Debug, new EventId(10030), ex,
                "Definition request failed for {FilePath}", filePath);

            if (!cancellationToken.IsCancellationRequested)
            {
                PublishTerminal(
                    LanguageNavigationState.Failed,
                    generation,
                    filePath,
                    caretOffset,
                    LanguageNavigationPolicy.FailedMessage,
                    requestId,
                    tracked);
            }
        }
    }

    private void PublishTerminal(
        LanguageNavigationState state,
        long generation,
        string filePath,
        int caretOffset,
        string message,
        long? requestId = null,
        LanguageTrackedDocumentInfo? tracked = null)
    {
        var snapshot = new LanguageNavigationSnapshot(
            state,
            requestId ?? Interlocked.Increment(ref _requestId),
            generation,
            tracked?.DocumentUri ?? LanguageDocumentUri.FromPath(filePath),
            filePath,
            tracked?.Version ?? 0,
            caretOffset,
            SelectedIndex: 0,
            Locations: Array.Empty<LanguageLocation>(),
            message);

        PublishLocked(snapshot);
        // Terminal states do not keep a chooser open; return to idle after feedback.
        PublishLocked(LanguageNavigationSnapshot.Idle);
    }

    private void PublishLocked(LanguageNavigationSnapshot snapshot)
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

    private static void ObserveTask(Task task)
    {
        _ = task.ContinueWith(
            t => { _ = t.Exception; },
            TaskScheduler.Default);
    }
}
