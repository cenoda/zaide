using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using Microsoft.Extensions.Logging;
using Zaide.Models;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Language.Contracts;
using Zaide.Services;

namespace Zaide.Features.Language.Application;

/// <summary>
/// Owns structured per-document diagnostics from the live language session.
/// Validates generation, open-document tracking, version, and utf-16 ranges.
/// </summary>
public sealed class LanguageDiagnosticsService : ILanguageDiagnosticsService
{
    private readonly global::Zaide.Features.Workspace.Domain.Workspace _workspace;
    private readonly ILanguageSessionService _sessionService;
    private readonly ILanguageDocumentBridge _documentBridge;
    private readonly ILogger<LanguageDiagnosticsService> _logger;
    private readonly Subject<LanguageDiagnosticsSnapshot> _subject = new();
    private readonly object _gate = new();
    private readonly Dictionary<string, DocumentDiagnosticsBucket> _byUri =
        new(StringComparer.Ordinal);
    private readonly IDisposable _sessionSubscription;

    private LanguageDiagnosticsSnapshot _current = LanguageDiagnosticsSnapshot.Empty;
    private ILanguageServerSession? _attachedSession;
    private long _attachedGeneration;
    private bool _disposed;

    public LanguageDiagnosticsService(
        global::Zaide.Features.Workspace.Domain.Workspace workspace,
        ILanguageSessionService sessionService,
        ILanguageDocumentBridge documentBridge,
        ILogger<LanguageDiagnosticsService> logger)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _documentBridge = documentBridge ?? throw new ArgumentNullException(nameof(documentBridge));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _workspace.DocumentClosed += OnDocumentClosed;
        _sessionSubscription = _sessionService.WhenChanged.Subscribe(OnSessionChanged);
        ApplySessionSnapshot(_sessionService.Current);
    }

    /// <inheritdoc />
    public LanguageDiagnosticsSnapshot Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    /// <inheritdoc />
    public IObservable<LanguageDiagnosticsSnapshot> WhenChanged => _subject;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _workspace.DocumentClosed -= OnDocumentClosed;
        _sessionSubscription.Dispose();
        DetachSessionLocked();

        lock (_gate)
        {
            _byUri.Clear();
            _current = LanguageDiagnosticsSnapshot.Empty;
        }

        _subject.OnCompleted();
        _subject.Dispose();
    }

    private void OnSessionChanged(LanguageSessionSnapshot snapshot) =>
        ApplySessionSnapshot(snapshot);

    private void ApplySessionSnapshot(LanguageSessionSnapshot snapshot)
    {
        if (_disposed)
            return;

        LanguageDiagnosticsSnapshot published;

        lock (_gate)
        {
            if (_disposed)
                return;

            if (snapshot.Generation != _attachedGeneration ||
                snapshot.State != LanguageSessionState.Ready)
            {
                _byUri.Clear();
                DetachSessionLocked();
            }

            if (snapshot.State == LanguageSessionState.Ready)
            {
                var session = _sessionService.TryGetReadySession(snapshot.Generation);
                if (session is not null &&
                    (_attachedSession is null ||
                     !ReferenceEquals(_attachedSession, session) ||
                     _attachedGeneration != snapshot.Generation))
                {
                    DetachSessionLocked();
                    _attachedSession = session;
                    _attachedGeneration = snapshot.Generation;
                    session.DiagnosticsPublished += OnDiagnosticsPublished;
                }
            }

            published = BuildSnapshotLocked(snapshot);
            _current = published;
        }

        _subject.OnNext(published);
    }

    private void OnDiagnosticsPublished(LanguageServerPublishDiagnostics notification)
    {
        if (_disposed)
            return;

        LanguageDiagnosticsSnapshot? published = null;

        lock (_gate)
        {
            if (_disposed)
                return;

            var sessionSnapshot = _sessionService.Current;
            if (sessionSnapshot.State != LanguageSessionState.Ready ||
                sessionSnapshot.Generation != notification.Generation ||
                notification.Generation != _attachedGeneration)
            {
                return;
            }

            var normalizedUri = LanguageDocumentUri.Normalize(notification.DocumentUri);

            if (!_documentBridge.TryGetOpenDocument(normalizedUri, out var tracked) ||
                tracked.Generation != notification.Generation)
            {
                // Closed / never-opened document for this generation.
                return;
            }

            if (notification.Version is int publishedVersion &&
                publishedVersion != tracked.Version)
            {
                // Stale or future version relative to the tracked document.
                return;
            }

            var document = FindDocumentByPath(tracked.FilePath);
            if (document is null)
                return;

            var accepted = new List<LanguageDiagnostic>(notification.Diagnostics.Count);
            foreach (var raw in notification.Diagnostics)
            {
                if (!LspUtf16PositionMapper.TryMapRange(
                        document.Content,
                        raw.Range,
                        out var startOffset,
                        out var endOffset))
                {
                    continue;
                }

                accepted.Add(new LanguageDiagnostic(
                    tracked.DocumentUri,
                    tracked.FilePath,
                    tracked.Version,
                    notification.Generation,
                    raw.Severity,
                    raw.Message,
                    raw.Code,
                    raw.Source,
                    raw.Range,
                    startOffset,
                    endOffset));
            }

            // Replace — never append — for this URI/version.
            _byUri[normalizedUri] = new DocumentDiagnosticsBucket(
                tracked.DocumentUri,
                tracked.FilePath,
                tracked.Version,
                notification.Generation,
                accepted);

            published = BuildSnapshotLocked(sessionSnapshot);
            _current = published;
        }

        if (published is not null)
            _subject.OnNext(published);
    }

    private void OnDocumentClosed(object? sender, string path)
    {
        if (_disposed || string.IsNullOrWhiteSpace(path))
            return;

        var uri = LanguageDocumentUri.FromPath(path);
        LanguageDiagnosticsSnapshot? published = null;

        lock (_gate)
        {
            if (_disposed)
                return;

            if (!_byUri.Remove(uri))
                return;

            published = BuildSnapshotLocked(_sessionService.Current);
            _current = published;
        }

        if (published is not null)
            _subject.OnNext(published);
    }

    private void DetachSessionLocked()
    {
        if (_attachedSession is not null)
        {
            _attachedSession.DiagnosticsPublished -= OnDiagnosticsPublished;
            _attachedSession = null;
        }

        _attachedGeneration = 0;
    }

    private LanguageDiagnosticsSnapshot BuildSnapshotLocked(LanguageSessionSnapshot session)
    {
        // Drop buckets that no longer match the live generation.
        if (session.State != LanguageSessionState.Ready)
        {
            _byUri.Clear();
        }
        else
        {
            var staleKeys = _byUri
                .Where(pair => pair.Value.SessionGeneration != session.Generation)
                .Select(pair => pair.Key)
                .ToList();
            foreach (var key in staleKeys)
                _byUri.Remove(key);
        }

        var diagnostics = _byUri.Values
            .SelectMany(bucket => bucket.Diagnostics)
            .OrderBy(d => d.FilePath, StringComparer.Ordinal)
            .ThenBy(d => d.Range.StartLine)
            .ThenBy(d => d.Range.StartCharacter)
            .ThenBy(d => d.Severity)
            .ThenBy(d => d.Message, StringComparer.Ordinal)
            .ToList();

        return new LanguageDiagnosticsSnapshot(
            session.State,
            session.Generation,
            session.Failure,
            diagnostics);
    }

    private Document? FindDocumentByPath(string filePath)
    {
        foreach (var document in _workspace.Documents)
        {
            if (string.Equals(document.FilePath, filePath, StringComparison.Ordinal))
                return document;
        }

        return null;
    }

    private sealed class DocumentDiagnosticsBucket
    {
        public DocumentDiagnosticsBucket(
            string documentUri,
            string filePath,
            int version,
            long sessionGeneration,
            IReadOnlyList<LanguageDiagnostic> diagnostics)
        {
            DocumentUri = documentUri;
            FilePath = filePath;
            Version = version;
            SessionGeneration = sessionGeneration;
            Diagnostics = diagnostics;
        }

        public string DocumentUri { get; }
        public string FilePath { get; }
        public int Version { get; }
        public long SessionGeneration { get; }
        public IReadOnlyList<LanguageDiagnostic> Diagnostics { get; }
    }
}
