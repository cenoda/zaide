using System;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Language.Contracts;

namespace Zaide.Features.Language.Application;

/// <summary>
/// Owns cancellable whole-document <c>textDocument/formatting</c> with
/// generation/version/active-document validation. Never mutates text.
/// </summary>
public sealed class LanguageFormattingService : ILanguageFormattingService
{
    private readonly global::Zaide.Features.Workspace.Domain.Workspace _workspace;
    private readonly ILanguageSessionService _sessionService;
    private readonly ILanguageDocumentBridge _documentBridge;
    private readonly ILogger<LanguageFormattingService> _logger;
    private readonly Subject<LanguageFormattingSnapshot> _subject = new();
    private readonly object _gate = new();
    private readonly IDisposable _sessionSubscription;

    private LanguageFormattingSnapshot _current = LanguageFormattingSnapshot.Idle;
    private CancellationTokenSource? _requestCts;
    private long _requestId;
    private bool _disposed;

    public LanguageFormattingService(
        global::Zaide.Features.Workspace.Domain.Workspace workspace,
        ILanguageSessionService sessionService,
        ILanguageDocumentBridge documentBridge,
        ILogger<LanguageFormattingService> logger)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _documentBridge = documentBridge ?? throw new ArgumentNullException(nameof(documentBridge));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _workspace.DocumentClosed += OnDocumentClosed;
        _sessionSubscription = _sessionService.WhenChanged.Subscribe(OnSessionChanged);
    }

    /// <inheritdoc />
    public LanguageFormattingSnapshot Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    /// <inheritdoc />
    public IObservable<LanguageFormattingSnapshot> WhenChanged => _subject;

    /// <inheritdoc />
    public async Task<LanguageFormattingOutcome> FormatDocumentAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (_disposed || string.IsNullOrWhiteSpace(filePath))
        {
            return Terminal(
                LanguageFormattingOutcomeKind.Unavailable,
                LanguageFormattingPolicy.UnavailableMessage);
        }

        if (!LanguageDocumentSyncPolicy.IsEligiblePath(filePath))
        {
            return Terminal(
                LanguageFormattingOutcomeKind.Unavailable,
                LanguageFormattingPolicy.UnavailableMessage);
        }

        CancelRequestLocked();

        var sessionSnapshot = _sessionService.Current;
        if (sessionSnapshot.State != LanguageSessionState.Ready)
        {
            return PublishTerminalOutcome(
                LanguageFormattingState.Unavailable,
                LanguageFormattingOutcomeKind.Unavailable,
                sessionSnapshot.Generation,
                filePath,
                LanguageFormattingPolicy.UnavailableMessage);
        }

        var session = _sessionService.TryGetReadySession(sessionSnapshot.Generation);
        if (session is null)
        {
            return PublishTerminalOutcome(
                LanguageFormattingState.Unavailable,
                LanguageFormattingOutcomeKind.Unavailable,
                sessionSnapshot.Generation,
                filePath,
                LanguageFormattingPolicy.UnavailableMessage);
        }

        if (!session.Capabilities.DocumentFormattingSupported)
        {
            return PublishTerminalOutcome(
                LanguageFormattingState.Unsupported,
                LanguageFormattingOutcomeKind.Unsupported,
                sessionSnapshot.Generation,
                filePath,
                LanguageFormattingPolicy.UnsupportedMessage);
        }

        var uri = LanguageDocumentUri.FromPath(filePath);
        if (!_documentBridge.TryGetOpenDocument(uri, out var tracked) ||
            tracked.Generation != sessionSnapshot.Generation)
        {
            return PublishTerminalOutcome(
                LanguageFormattingState.Unavailable,
                LanguageFormattingOutcomeKind.Unavailable,
                sessionSnapshot.Generation,
                filePath,
                LanguageFormattingPolicy.UnavailableMessage);
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
            return PublishTerminalOutcome(
                LanguageFormattingState.Unavailable,
                LanguageFormattingOutcomeKind.Unavailable,
                sessionSnapshot.Generation,
                filePath,
                LanguageFormattingPolicy.UnavailableMessage);
        }

        var document = FindDocument(filePath);
        if (document is null)
        {
            return PublishTerminalOutcome(
                LanguageFormattingState.Unavailable,
                LanguageFormattingOutcomeKind.Unavailable,
                sessionSnapshot.Generation,
                filePath,
                LanguageFormattingPolicy.UnavailableMessage);
        }

        var sourceText = document.Content;
        _requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var requestToken = _requestCts.Token;
        var requestId = Interlocked.Increment(ref _requestId);

        var loading = new LanguageFormattingSnapshot(
            LanguageFormattingState.Loading,
            requestId,
            sessionSnapshot.Generation,
            tracked.DocumentUri,
            filePath,
            tracked.Version,
            Array.Empty<LanguageTextEdit>(),
            FormattedText: null,
            FeedbackMessage: null);
        PublishLocked(loading);

        try
        {
            var result = await session.RequestFormattingAsync(
                    tracked.DocumentUri,
                    requestToken)
                .ConfigureAwait(false);

            if (requestToken.IsCancellationRequested || cancellationToken.IsCancellationRequested)
            {
                return PublishTerminalOutcome(
                    LanguageFormattingState.Cancelled,
                    LanguageFormattingOutcomeKind.Cancelled,
                    sessionSnapshot.Generation,
                    filePath,
                    LanguageFormattingPolicy.CancelledMessage,
                    requestId,
                    tracked);
            }

            if (!LanguageActiveDocumentValidator.TryValidate(
                    _workspace,
                    _sessionService,
                    _documentBridge,
                    filePath,
                    sessionSnapshot.Generation,
                    tracked.Version,
                    out _))
            {
                return PublishTerminalOutcome(
                    LanguageFormattingState.Stale,
                    LanguageFormattingOutcomeKind.Stale,
                    sessionSnapshot.Generation,
                    filePath,
                    LanguageFormattingPolicy.CancelledMessage,
                    requestId,
                    tracked);
            }

            var currentSession = _sessionService.Current;
            if (currentSession.State != LanguageSessionState.Ready ||
                currentSession.Generation != sessionSnapshot.Generation)
            {
                return PublishTerminalOutcome(
                    LanguageFormattingState.Stale,
                    LanguageFormattingOutcomeKind.Stale,
                    sessionSnapshot.Generation,
                    filePath,
                    LanguageFormattingPolicy.CancelledMessage,
                    requestId,
                    tracked);
            }

            lock (_gate)
            {
                if (_disposed || _current.RequestId != requestId)
                {
                    return LanguageFormattingOutcome.Terminal(
                        LanguageFormattingOutcomeKind.Cancelled,
                        LanguageFormattingPolicy.CancelledMessage);
                }
            }

            // Source text must still match the document we formatted against.
            if (!string.Equals(document.Content, sourceText, StringComparison.Ordinal))
            {
                return PublishTerminalOutcome(
                    LanguageFormattingState.Stale,
                    LanguageFormattingOutcomeKind.Stale,
                    sessionSnapshot.Generation,
                    filePath,
                    LanguageFormattingPolicy.CancelledMessage,
                    requestId,
                    tracked);
            }

            if (result is null)
            {
                return PublishTerminalOutcome(
                    LanguageFormattingState.Failed,
                    LanguageFormattingOutcomeKind.Failed,
                    sessionSnapshot.Generation,
                    filePath,
                    LanguageFormattingPolicy.FailedMessage,
                    requestId,
                    tracked);
            }

            if (!LanguageFormattingEditApplier.TryApply(
                    sourceText,
                    result.Edits,
                    out var formattedText))
            {
                return PublishTerminalOutcome(
                    LanguageFormattingState.Invalid,
                    LanguageFormattingOutcomeKind.Invalid,
                    sessionSnapshot.Generation,
                    filePath,
                    LanguageFormattingPolicy.InvalidMessage,
                    requestId,
                    tracked);
            }

            if (result.Edits.Count == 0 ||
                string.Equals(formattedText, sourceText, StringComparison.Ordinal))
            {
                var noEdits = new LanguageFormattingSnapshot(
                    LanguageFormattingState.NoEdits,
                    requestId,
                    sessionSnapshot.Generation,
                    tracked.DocumentUri,
                    filePath,
                    tracked.Version,
                    result.Edits,
                    sourceText,
                    LanguageFormattingPolicy.NoEditsMessage);
                PublishLocked(noEdits);
                PublishLocked(LanguageFormattingSnapshot.Idle);
                return new LanguageFormattingOutcome(
                    LanguageFormattingOutcomeKind.NoEdits,
                    sourceText,
                    result.Edits,
                    LanguageFormattingPolicy.NoEditsMessage);
            }

            var ready = new LanguageFormattingSnapshot(
                LanguageFormattingState.Ready,
                requestId,
                sessionSnapshot.Generation,
                tracked.DocumentUri,
                filePath,
                tracked.Version,
                result.Edits,
                formattedText,
                LanguageFormattingPolicy.AppliedMessage);
            PublishLocked(ready);
            PublishLocked(LanguageFormattingSnapshot.Idle);
            return new LanguageFormattingOutcome(
                LanguageFormattingOutcomeKind.Applied,
                formattedText,
                result.Edits,
                LanguageFormattingPolicy.AppliedMessage);
        }
        catch (OperationCanceledException)
        {
            return PublishTerminalOutcome(
                LanguageFormattingState.Cancelled,
                LanguageFormattingOutcomeKind.Cancelled,
                sessionSnapshot.Generation,
                filePath,
                LanguageFormattingPolicy.CancelledMessage,
                requestId,
                tracked);
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Debug, new EventId(10040), ex,
                "Formatting request failed for {FilePath}", filePath);

            return PublishTerminalOutcome(
                LanguageFormattingState.Failed,
                LanguageFormattingOutcomeKind.Failed,
                sessionSnapshot.Generation,
                filePath,
                LanguageFormattingPolicy.FailedMessage,
                requestId,
                tracked);
        }
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
            _current = LanguageFormattingSnapshot.Idle;

        _subject.OnCompleted();
        _subject.Dispose();
    }

    private LanguageFormattingOutcome PublishTerminalOutcome(
        LanguageFormattingState state,
        LanguageFormattingOutcomeKind kind,
        long generation,
        string filePath,
        string message,
        long? requestId = null,
        LanguageTrackedDocumentInfo? tracked = null)
    {
        var snapshot = new LanguageFormattingSnapshot(
            state,
            requestId ?? Interlocked.Increment(ref _requestId),
            generation,
            tracked?.DocumentUri ?? LanguageDocumentUri.FromPath(filePath),
            filePath,
            tracked?.Version ?? 0,
            Array.Empty<LanguageTextEdit>(),
            FormattedText: null,
            message);

        PublishLocked(snapshot);
        PublishLocked(LanguageFormattingSnapshot.Idle);
        return LanguageFormattingOutcome.Terminal(kind, message);
    }

    private static LanguageFormattingOutcome Terminal(
        LanguageFormattingOutcomeKind kind,
        string message) =>
        LanguageFormattingOutcome.Terminal(kind, message);

    private void PublishLocked(LanguageFormattingSnapshot snapshot)
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _current = snapshot;
        }

        _subject.OnNext(snapshot);
    }

    private void OnSessionChanged(LanguageSessionSnapshot snapshot)
    {
        if (_disposed)
            return;

        if (snapshot.State == LanguageSessionState.Ready)
            return;

        CancelRequestLocked();
        PublishLocked(LanguageFormattingSnapshot.Idle);
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

        CancelRequestLocked();
        PublishLocked(LanguageFormattingSnapshot.Idle);
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
}
