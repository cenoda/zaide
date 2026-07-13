using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Zaide.Models;

namespace Zaide.Services;

/// <summary>
/// Observes <see cref="Workspace"/> document lifecycle and content, sending ordered
/// LSP document notifications through the current ready language session.
/// </summary>
public sealed class LanguageDocumentBridge : ILanguageDocumentBridge
{
    private readonly Workspace _workspace;
    private readonly ILanguageSessionService _sessionService;
    private readonly ILogger<LanguageDocumentBridge> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, TrackedDocument> _tracked =
        new(StringComparer.Ordinal);
    private readonly IDisposable _sessionSubscription;

    private CancellationTokenSource _generationCts = new();
    private long _syncGeneration;
    private bool _disposed;

    public LanguageDocumentBridge(
        Workspace workspace,
        ILanguageSessionService sessionService,
        ILogger<LanguageDocumentBridge> logger)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _workspace.DocumentOpened += OnDocumentOpened;
        _workspace.DocumentClosed += OnDocumentClosed;
        _sessionSubscription = _sessionService.WhenChanged.Subscribe(OnSessionChanged);

        foreach (var document in _workspace.Documents)
            TrackDocument(document);

        ObserveTask(ApplySessionSnapshotAsync(_sessionService.Current));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _workspace.DocumentOpened -= OnDocumentOpened;
        _workspace.DocumentClosed -= OnDocumentClosed;
        _sessionSubscription.Dispose();

        _generationCts.Cancel();
        _generationCts.Dispose();

        foreach (var tracked in _tracked.Values)
            tracked.Document.ContentChanged -= tracked.OnContentChanged;

        _tracked.Clear();
        _gate.Dispose();
    }

    private void OnDocumentOpened(object? sender, Document document) => TrackDocument(document);

    private void OnDocumentClosed(object? sender, string path)
    {
        ObserveTask(HandleDocumentClosedAsync(path));
    }

    private void OnSessionChanged(LanguageSessionSnapshot snapshot)
    {
        ObserveTask(ApplySessionSnapshotAsync(snapshot));
    }

    private void TrackDocument(Document document)
    {
        if (!LanguageDocumentSyncPolicy.IsEligible(document))
            return;

        var path = document.FilePath;
        if (_tracked.ContainsKey(path))
            return;

        var tracked = new TrackedDocument(document, LanguageDocumentUri.FromPath(path));
        tracked.OnContentChanged = (_, _) => ObserveTask(HandleContentChangedAsync(path));
        document.ContentChanged += tracked.OnContentChanged;
        _tracked[path] = tracked;

        ObserveTask(TryOpenDocumentAsync(path));
    }

    private async Task ApplySessionSnapshotAsync(LanguageSessionSnapshot snapshot)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
                return;

            if (snapshot.Generation != _syncGeneration)
            {
                _generationCts.Cancel();
                _generationCts.Dispose();
                _generationCts = new CancellationTokenSource();
                _syncGeneration = snapshot.Generation;

                foreach (var tracked in _tracked.Values)
                {
                    tracked.Version = 0;
                    tracked.OpenSentForGeneration = false;
                }
            }
        }
        finally
        {
            _gate.Release();
        }

        if (snapshot.State == LanguageSessionState.Ready)
            await ResyncAllOpenDocumentsAsync(snapshot.Generation).ConfigureAwait(false);
    }

    private async Task HandleDocumentClosedAsync(string path)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed || !_tracked.Remove(path, out var tracked))
                return;

            tracked.Document.ContentChanged -= tracked.OnContentChanged;

            var generation = _syncGeneration;
            var session = _sessionService.TryGetReadySession(generation);
            if (session is null)
                return;

            try
            {
                await session.NotifyDidCloseAsync(tracked.Uri, _generationCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Generation advanced or bridge disposed.
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Debug, new EventId(10010), ex,
                    "didClose failed for {Uri}", tracked.Uri);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task HandleContentChangedAsync(string path)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed || !_tracked.TryGetValue(path, out var tracked))
                return;

            if (!tracked.OpenSentForGeneration || tracked.OpenSentGeneration != _syncGeneration)
                return;

            var generation = _syncGeneration;
            tracked.Version++;
            var nextVersion = tracked.Version;

            var session = _sessionService.TryGetReadySession(generation);
            if (session is null)
                return;

            try
            {
                await session.NotifyDidChangeAsync(
                        tracked.Uri,
                        nextVersion,
                        tracked.Document.Content,
                        _generationCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Generation advanced or bridge disposed.
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Debug, new EventId(10011), ex,
                    "didChange failed for {Uri} version {Version}", tracked.Uri, nextVersion);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task TryOpenDocumentAsync(string path)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed || !_tracked.TryGetValue(path, out var tracked))
                return;

            if (tracked.OpenSentForGeneration && tracked.OpenSentGeneration == _syncGeneration)
                return;

            var snapshot = _sessionService.Current;
            if (snapshot.State != LanguageSessionState.Ready)
                return;

            var generation = _syncGeneration;
            if (snapshot.Generation != generation)
                return;

            tracked.Version = 1;
            tracked.OpenSentForGeneration = true;
            tracked.OpenSentGeneration = generation;

            var session = _sessionService.TryGetReadySession(generation);
            if (session is null)
            {
                tracked.OpenSentForGeneration = false;
                tracked.OpenSentGeneration = 0;
                tracked.Version = 0;
                return;
            }

            try
            {
                await session.NotifyDidOpenAsync(
                        tracked.Uri,
                        1,
                        tracked.Document.Content,
                        _generationCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                tracked.OpenSentForGeneration = false;
                tracked.OpenSentGeneration = 0;
                tracked.Version = 0;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Debug, new EventId(10012), ex,
                    "didOpen failed for {Uri}", tracked.Uri);
                tracked.OpenSentForGeneration = false;
                tracked.OpenSentGeneration = 0;
                tracked.Version = 0;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ResyncAllOpenDocumentsAsync(long generation)
    {
        List<string> paths;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed || generation != _syncGeneration)
                return;

            paths = new List<string>(_tracked.Keys);
        }
        finally
        {
            _gate.Release();
        }

        foreach (var path in paths)
            await TryOpenDocumentAsync(path).ConfigureAwait(false);
    }

    private static void ObserveTask(Task task)
    {
        _ = task.ContinueWith(
            t => { _ = t.Exception; },
            TaskScheduler.Default);
    }

    private sealed class TrackedDocument
    {
        public TrackedDocument(Document document, string uri)
        {
            Document = document;
            Uri = uri;
        }

        public Document Document { get; }
        public string Uri { get; }
        public int Version { get; set; }
        public bool OpenSentForGeneration { get; set; }
        public long OpenSentGeneration { get; set; }
        public EventHandler? OnContentChanged { get; set; }
    }
}
