using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.Language.Infrastructure.Lsp;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Language.Contracts;
using Zaide.Features.Language.Application;

namespace Zaide.Tests.Features.Language.Application;

/// <summary>
/// Phase 10 M3 tests for structured diagnostics ownership.
/// </summary>
public sealed class LanguageDiagnosticsServiceTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase10-m3-diag-" + Guid.NewGuid().ToString("N"));

    static LanguageDiagnosticsServiceTests()
    {
        Directory.CreateDirectory(TempRoot);
    }

    // ── Fakes ───────────────────────────────────────────────────────────

    private sealed class RecordingSession : ILanguageServerSession
    {
        public long Generation { get; set; } = 1;
        public int? ProcessId => 1;
        public bool HasExited { get; private set; }

#pragma warning disable CS0067 // Required by ILanguageServerSession; unused in diagnostics fakes.
        public event Action<long>? ProcessExited;
#pragma warning restore CS0067
        public event Action<LanguageServerPublishDiagnostics>? DiagnosticsPublished;

        public Task ShutdownAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ForceKillAsync()
        {
            HasExited = true;
            return Task.CompletedTask;
        }

        public Task NotifyDidOpenAsync(string documentUri, int version, string text,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NotifyDidChangeAsync(string documentUri, int version, string text,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NotifyDidCloseAsync(string documentUri,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public LanguageServerCapabilities Capabilities => TestLanguageServerSession.DefaultCapabilities;

        public Task<LanguageServerCompletionResult?> RequestCompletionAsync(
            string documentUri,
            int line,
            int character,
            CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptyCompletionAsync(documentUri, line, character, cancellationToken);

        public Task<LanguageServerHoverResult?> RequestHoverAsync(
            string documentUri,
            int line,
            int character,
            CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptyHoverAsync(documentUri, line, character, cancellationToken);

        public Task<LanguageServerDefinitionResult?> RequestDefinitionAsync(
            string documentUri,
            int line,
            int character,
            CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptyDefinitionAsync(documentUri, line, character, cancellationToken);

        public Task<LanguageServerSymbolResult?> RequestDocumentSymbolsAsync(
            string documentUri,
            CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptySymbolsAsync(cancellationToken);

        public Task<LanguageServerSymbolResult?> RequestWorkspaceSymbolsAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptySymbolsAsync(cancellationToken);

        public Task<LanguageServerFormattingResult?> RequestFormattingAsync(
            string documentUri,
            CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptyFormattingAsync(cancellationToken);



        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Publish(LanguageServerPublishDiagnostics notification) =>
            DiagnosticsPublished?.Invoke(notification);
    }

    private sealed class FakeSessionService : ILanguageSessionService
    {
        private readonly Subject<LanguageSessionSnapshot> _subject = new();
        private LanguageSessionSnapshot _current = Unavailable(0);
        private RecordingSession? _session;

        public LanguageSessionSnapshot Current => _current;
        public IObservable<LanguageSessionSnapshot> WhenChanged => _subject;

        public void SetSnapshot(LanguageSessionSnapshot snapshot, RecordingSession? session = null)
        {
            _session = session;
            _current = snapshot;
            _subject.OnNext(snapshot);
        }
        public Task<LanguageServerSymbolResult?> RequestWorkspaceSymbolsAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptySymbolsAsync(cancellationToken);

        public Task<LanguageServerFormattingResult?> RequestFormattingAsync(
            string documentUri,
            CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptyFormattingAsync(cancellationToken);



        public ILanguageServerSession? TryGetReadySession(long generation) =>
            _current.State == LanguageSessionState.Ready &&
            _current.Generation == generation
                ? _session
                : null;

        public Task RestartAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Dispose()
        {
            _subject.OnCompleted();
            _subject.Dispose();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static LanguageSessionSnapshot Unavailable(long generation) => new(
            LanguageSessionState.Unavailable,
            generation,
            null, null, null, null);
    }

    private sealed class FakeDocumentBridge : ILanguageDocumentBridge
    {
        private readonly Dictionary<string, LanguageTrackedDocumentInfo> _open =
            new(StringComparer.Ordinal);

        public void SetOpen(string uri, string path, int version, long generation)
        {
            var normalized = LanguageDocumentUri.Normalize(uri);
            _open[normalized] = new LanguageTrackedDocumentInfo(normalized, path, version, generation);
        }

        public void Clear(string uri) =>
            _open.Remove(LanguageDocumentUri.Normalize(uri));

        public void ClearAll() => _open.Clear();

        public bool TryGetOpenDocument(string documentUri, out LanguageTrackedDocumentInfo info) =>
            _open.TryGetValue(LanguageDocumentUri.Normalize(documentUri), out info);

        public void Dispose() { }
    }

    private sealed class Harness : IDisposable
    {
        public global::Zaide.Features.Workspace.Domain.Workspace Workspace { get; } = new();
        public FakeSessionService SessionService { get; } = new();
        public FakeDocumentBridge Bridge { get; } = new();
        public RecordingSession Session { get; } = new() { Generation = 1 };
        public LanguageDiagnosticsService Service { get; }

        public List<LanguageDiagnosticsSnapshot> Snapshots { get; } = new();

        public Harness()
        {
            Service = new LanguageDiagnosticsService(
                Workspace,
                SessionService,
                Bridge,
                NullLogger<LanguageDiagnosticsService>.Instance);
            Service.WhenChanged.Subscribe(s => Snapshots.Add(s));
        }

        public void SetReady(long generation = 1)
        {
            Session.Generation = generation;
            SessionService.SetSnapshot(
                new LanguageSessionSnapshot(
                    LanguageSessionState.Ready,
                    generation,
                    "/tmp/project/Project.csproj",
                    "/tmp/project",
                    42,
                    null),
                Session);
        }

        public void SetState(LanguageSessionState state, long generation, LanguageSessionFailure? failure = null)
        {
            SessionService.SetSnapshot(
                new LanguageSessionSnapshot(
                    state,
                    generation,
                    state is LanguageSessionState.Ready or LanguageSessionState.Loading or LanguageSessionState.Failed
                        ? "/tmp/project/Project.csproj"
                        : null,
                    state is LanguageSessionState.Ready or LanguageSessionState.Loading or LanguageSessionState.Failed
                        ? "/tmp/project"
                        : null,
                    state == LanguageSessionState.Ready ? 42 : null,
                    failure),
                state == LanguageSessionState.Ready ? Session : null);
        }

        public Document OpenCs(string name, string content)
        {
            var path = Path.Combine(TempRoot, name + ".cs");
            return Workspace.OpenDocument(path, content);
        }

        public void TrackOpen(Document document, int version, long generation)
        {
            var uri = LanguageDocumentUri.FromPath(document.FilePath);
            Bridge.SetOpen(uri, document.FilePath, version, generation);
        }

        public void Publish(
            Document document,
            int? version,
            long generation,
            params LanguageServerDiagnosticPayload[] diagnostics)
        {
            var uri = LanguageDocumentUri.FromPath(document.FilePath);
            Session.Publish(new LanguageServerPublishDiagnostics(
                generation,
                uri,
                version,
                diagnostics));
        }

        public static LanguageServerDiagnosticPayload Diag(
            string message,
            int startLine = 0,
            int startChar = 0,
            int endLine = 0,
            int endChar = 1,
            LanguageDiagnosticSeverity severity = LanguageDiagnosticSeverity.Error,
            string? code = "CS1002") =>
            new(severity, message, code, "csharp",
                new LspRange(startLine, startChar, endLine, endChar));

        public void Dispose() => Service.Dispose();
    }

    // ── Tests ───────────────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsUnavailableEmpty()
    {
        using var harness = new Harness();
        Assert.Equal(LanguageSessionState.Unavailable, harness.Service.Current.State);
        Assert.Empty(harness.Service.Current.Diagnostics);
    }

    [Fact]
    public void PublishDiagnostics_ReplacesForUriAndVersion()
    {
        using var harness = new Harness();
        harness.SetReady();
        var doc = harness.OpenCs("replace", "class A { }");
        harness.TrackOpen(doc, version: 1, generation: 1);

        harness.Publish(doc, version: 1, generation: 1,
            Harness.Diag("first", endChar: 5));
        Assert.Single(harness.Service.Current.Diagnostics);
        Assert.Equal("first", harness.Service.Current.Diagnostics[0].Message);

        harness.Publish(doc, version: 1, generation: 1,
            Harness.Diag("second", endChar: 5),
            Harness.Diag("third", startChar: 6, endChar: 7));

        Assert.Equal(2, harness.Service.Current.Diagnostics.Count);
        Assert.Contains(harness.Service.Current.Diagnostics, d => d.Message == "second");
        Assert.Contains(harness.Service.Current.Diagnostics, d => d.Message == "third");
        Assert.DoesNotContain(harness.Service.Current.Diagnostics, d => d.Message == "first");
    }

    [Fact]
    public void PublishEmptyDiagnostics_ClearsUri()
    {
        using var harness = new Harness();
        harness.SetReady();
        var doc = harness.OpenCs("clear", "class A { }");
        harness.TrackOpen(doc, 1, 1);

        harness.Publish(doc, 1, 1, Harness.Diag("err", endChar: 5));
        Assert.Single(harness.Service.Current.Diagnostics);

        harness.Publish(doc, 1, 1);
        Assert.Empty(harness.Service.Current.Diagnostics);
    }

    [Fact]
    public void MultiFile_IndependentUpdates()
    {
        using var harness = new Harness();
        harness.SetReady();
        var a = harness.OpenCs("a", "class A { }");
        var b = harness.OpenCs("b", "class B { }");
        harness.TrackOpen(a, 1, 1);
        harness.TrackOpen(b, 1, 1);

        harness.Publish(a, 1, 1, Harness.Diag("from-a", endChar: 5));
        harness.Publish(b, 1, 1, Harness.Diag("from-b", endChar: 5));

        Assert.Equal(2, harness.Service.Current.Diagnostics.Count);
        Assert.Contains(harness.Service.Current.Diagnostics, d => d.Message == "from-a");
        Assert.Contains(harness.Service.Current.Diagnostics, d => d.Message == "from-b");

        harness.Publish(a, 1, 1);
        Assert.Single(harness.Service.Current.Diagnostics);
        Assert.Equal("from-b", harness.Service.Current.Diagnostics[0].Message);
    }

    [Fact]
    public void InvalidRange_IsDropped()
    {
        using var harness = new Harness();
        harness.SetReady();
        var doc = harness.OpenCs("invalid", "ab");
        harness.TrackOpen(doc, 1, 1);

        harness.Publish(doc, 1, 1,
            Harness.Diag("bad-line", startLine: 99, endLine: 99, endChar: 1),
            Harness.Diag("bad-char", startChar: 50, endChar: 51),
            Harness.Diag("ok", endChar: 1));

        var only = Assert.Single(harness.Service.Current.Diagnostics);
        Assert.Equal("ok", only.Message);
    }

    [Fact]
    public void StaleGeneration_IsIgnored()
    {
        using var harness = new Harness();
        harness.SetReady(generation: 2);
        var doc = harness.OpenCs("stale-gen", "class A { }");
        harness.TrackOpen(doc, 1, 2);

        harness.Publish(doc, 1, generation: 1, Harness.Diag("stale", endChar: 5));
        Assert.Empty(harness.Service.Current.Diagnostics);
    }

    [Fact]
    public void MismatchedVersion_IsIgnored()
    {
        using var harness = new Harness();
        harness.SetReady();
        var doc = harness.OpenCs("stale-ver", "class A { }");
        harness.TrackOpen(doc, version: 3, generation: 1);

        harness.Publish(doc, version: 2, generation: 1, Harness.Diag("old", endChar: 5));
        Assert.Empty(harness.Service.Current.Diagnostics);
    }

    [Fact]
    public void ClosedDocument_IsIgnoredAndClearsOnClose()
    {
        using var harness = new Harness();
        harness.SetReady();
        var doc = harness.OpenCs("closed", "class A { }");
        harness.TrackOpen(doc, 1, 1);

        harness.Publish(doc, 1, 1, Harness.Diag("live", endChar: 5));
        Assert.Single(harness.Service.Current.Diagnostics);

        harness.Bridge.Clear(LanguageDocumentUri.FromPath(doc.FilePath));
        harness.Workspace.CloseDocument(doc.FilePath);

        Assert.Empty(harness.Service.Current.Diagnostics);

        // Publish after close is ignored.
        harness.Publish(doc, 1, 1, Harness.Diag("ghost", endChar: 5));
        Assert.Empty(harness.Service.Current.Diagnostics);
    }

    [Fact]
    public void SessionNotReady_ClearsDiagnosticsAndProjectsState()
    {
        using var harness = new Harness();
        harness.SetReady();
        var doc = harness.OpenCs("fail", "class A { }");
        harness.TrackOpen(doc, 1, 1);
        harness.Publish(doc, 1, 1, Harness.Diag("x", endChar: 5));
        Assert.Single(harness.Service.Current.Diagnostics);

        harness.SetState(
            LanguageSessionState.Failed,
            generation: 2,
            new LanguageSessionFailure(LanguageSessionFailureKind.ServerExited, "exited"));

        Assert.Equal(LanguageSessionState.Failed, harness.Service.Current.State);
        Assert.Empty(harness.Service.Current.Diagnostics);
        Assert.Equal("exited", harness.Service.Current.Failure?.Message);
    }

    [Fact]
    public void LoadingAndUnavailable_ProjectTruthfulStates()
    {
        using var harness = new Harness();

        harness.SetState(LanguageSessionState.Loading, generation: 1);
        Assert.Equal(LanguageSessionState.Loading, harness.Service.Current.State);
        Assert.Empty(harness.Service.Current.Diagnostics);

        harness.SetState(LanguageSessionState.Unavailable, generation: 2);
        Assert.Equal(LanguageSessionState.Unavailable, harness.Service.Current.State);
        Assert.Empty(harness.Service.Current.Diagnostics);
    }

    [Fact]
    public void GenerationChange_ClearsPriorDiagnostics()
    {
        using var harness = new Harness();
        harness.SetReady(1);
        var doc = harness.OpenCs("reload", "class A { }");
        harness.TrackOpen(doc, 1, 1);
        harness.Publish(doc, 1, 1, Harness.Diag("old-gen", endChar: 5));
        Assert.Single(harness.Service.Current.Diagnostics);

        harness.SetReady(2);
        Assert.Empty(harness.Service.Current.Diagnostics);
        Assert.Equal(2, harness.Service.Current.SessionGeneration);
    }

    [Fact]
    public void NonBmpPosition_MapsUtf16SurrogatePair()
    {
        using var harness = new Harness();
        harness.SetReady();
        // "A🎉B" — 🎉 is one scalar / two UTF-16 code units at indices 1-2; B at 3.
        var doc = harness.OpenCs("emoji", "A🎉B");
        harness.TrackOpen(doc, 1, 1);

        // Character covering the emoji: start=1, end=3 (after surrogate pair).
        harness.Publish(doc, 1, 1,
            Harness.Diag("emoji-span", startChar: 1, endChar: 3));

        var only = Assert.Single(harness.Service.Current.Diagnostics);
        Assert.Equal(1, only.StartOffset);
        Assert.Equal(3, only.EndOffset);
        Assert.Equal(0, only.Range.StartLine);
        Assert.Equal(1, only.Range.StartCharacter);
    }

    [Fact]
    public void NullVersion_AcceptedWhenDocumentOpen()
    {
        using var harness = new Harness();
        harness.SetReady();
        var doc = harness.OpenCs("nover", "class A { }");
        harness.TrackOpen(doc, 4, 1);

        harness.Publish(doc, version: null, generation: 1, Harness.Diag("ok", endChar: 5));
        var only = Assert.Single(harness.Service.Current.Diagnostics);
        Assert.Equal(4, only.DocumentVersion);
        Assert.Equal("ok", only.Message);
    }

    [Fact]
    public void MappedOffsets_MatchLspUtf16Mapper()
    {
        Assert.True(LspUtf16PositionMapper.TryGetOffset("a\nb", 1, 0, out var offset));
        Assert.Equal(2, offset);

        Assert.True(LspUtf16PositionMapper.TryGetOffset("A🎉B", 0, 1, out var emojiStart));
        Assert.Equal(1, emojiStart);
        Assert.True(LspUtf16PositionMapper.TryGetOffset("A🎉B", 0, 3, out var afterEmoji));
        Assert.Equal(3, afterEmoji);

        Assert.False(LspUtf16PositionMapper.TryGetOffset("ab", 0, 5, out _));
        Assert.False(LspUtf16PositionMapper.TryGetOffset("ab", -1, 0, out _));

        var (line, character) = LspUtf16PositionMapper.GetPosition("A🎉B", 3);
        Assert.Equal(0, line);
        Assert.Equal(3, character);
    }
}
