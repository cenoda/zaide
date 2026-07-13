using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 10 M6 tests for whole-document formatting ownership and edit validation.
/// </summary>
public sealed class LanguageFormattingTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase10-m6-fmt-" + Guid.NewGuid().ToString("N"));

    static LanguageFormattingTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        Directory.CreateDirectory(TempRoot);
    }

    private sealed class ConfigurableSession : ILanguageServerSession
    {
        public long Generation { get; set; } = 1;
        public LanguageServerCapabilities Capabilities { get; set; } =
            TestLanguageServerSession.DefaultCapabilities;
        public Func<string, CancellationToken, Task<LanguageServerFormattingResult?>>? FormattingHandler { get; set; }
        public TaskCompletionSource<bool>? FormattingGate { get; set; }
        public int FormattingCallCount { get; private set; }
        public int? ProcessId => 1;
        public bool HasExited { get; private set; }

#pragma warning disable CS0067
        public event Action<long>? ProcessExited;
        public event Action<LanguageServerPublishDiagnostics>? DiagnosticsPublished;
#pragma warning restore CS0067

        public Task ShutdownAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ForceKillAsync() { HasExited = true; return Task.CompletedTask; }
        public Task NotifyDidOpenAsync(string documentUri, int version, string text, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyDidChangeAsync(string documentUri, int version, string text, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyDidCloseAsync(string documentUri, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<LanguageServerCompletionResult?> RequestCompletionAsync(
            string documentUri, int line, int character, CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptyCompletionAsync(documentUri, line, character, cancellationToken);

        public Task<LanguageServerHoverResult?> RequestHoverAsync(
            string documentUri, int line, int character, CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptyHoverAsync(documentUri, line, character, cancellationToken);

        public Task<LanguageServerDefinitionResult?> RequestDefinitionAsync(
            string documentUri, int line, int character, CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptyDefinitionAsync(documentUri, line, character, cancellationToken);

        public Task<LanguageServerSymbolResult?> RequestDocumentSymbolsAsync(
            string documentUri, CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptySymbolsAsync(cancellationToken);

        public Task<LanguageServerSymbolResult?> RequestWorkspaceSymbolsAsync(
            string query, CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptySymbolsAsync(cancellationToken);

        public async Task<LanguageServerFormattingResult?> RequestFormattingAsync(
            string documentUri, CancellationToken cancellationToken = default)
        {
            FormattingCallCount++;
            if (FormattingGate is not null)
                await FormattingGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            if (FormattingHandler is not null)
                return await FormattingHandler(documentUri, cancellationToken).ConfigureAwait(false);

            return LanguageServerFormattingResult.Empty;
        }
    }

    private sealed class FakeSessionService : ILanguageSessionService
    {
        private readonly Subject<LanguageSessionSnapshot> _subject = new();
        private LanguageSessionSnapshot _current = Unavailable(0);
        private ConfigurableSession? _session;

        public LanguageSessionSnapshot Current => _current;
        public IObservable<LanguageSessionSnapshot> WhenChanged => _subject;

        public void SetReady(ConfigurableSession session, long generation = 1)
        {
            _session = session;
            session.Generation = generation;
            _current = new LanguageSessionSnapshot(
                LanguageSessionState.Ready, generation, "/p.csproj", TempRoot, 1, null);
            _subject.OnNext(_current);
        }

        public void SetUnavailable(long generation = 0)
        {
            _session = null;
            _current = Unavailable(generation);
            _subject.OnNext(_current);
        }

        public ILanguageServerSession? TryGetReadySession(long generation) =>
            _current.State == LanguageSessionState.Ready && _current.Generation == generation
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
            LanguageSessionState.Unavailable, generation, null, null, null, null);
    }

    private sealed class FakeDocumentBridge : ILanguageDocumentBridge
    {
        private readonly Dictionary<string, LanguageTrackedDocumentInfo> _open = new(StringComparer.Ordinal);

        public void SetOpen(string path, int version, long generation)
        {
            var uri = LanguageDocumentUri.FromPath(path);
            _open[uri] = new LanguageTrackedDocumentInfo(uri, path, version, generation);
        }

        public void SetVersion(string path, int version)
        {
            var uri = LanguageDocumentUri.FromPath(path);
            if (_open.TryGetValue(uri, out var info))
                _open[uri] = info with { Version = version };
        }

        public bool TryGetOpenDocument(string documentUri, out LanguageTrackedDocumentInfo info) =>
            _open.TryGetValue(LanguageDocumentUri.Normalize(documentUri), out info);

        public void Dispose() { }
    }

    private sealed class Harness : IDisposable
    {
        public Workspace Workspace { get; } = new();
        public FakeSessionService SessionService { get; } = new();
        public FakeDocumentBridge Bridge { get; } = new();
        public ConfigurableSession Session { get; } = new();
        public LanguageFormattingService Service { get; }
        public List<LanguageFormattingSnapshot> Snapshots { get; } = new();

        public Harness()
        {
            Service = new LanguageFormattingService(
                Workspace, SessionService, Bridge, NullLogger<LanguageFormattingService>.Instance);
            Service.WhenChanged.Subscribe(s => Snapshots.Add(s));
        }

        public (string Path, Document Document) OpenActive(
            string name, string content, int version = 1, long generation = 1)
        {
            var path = Path.Combine(TempRoot, Guid.NewGuid().ToString("N") + "-" + name);
            File.WriteAllText(path, content);
            var doc = Workspace.OpenDocument(path, content);
            Workspace.SetActiveDocument(doc);
            Bridge.SetOpen(path, version, generation);
            return (path, doc);
        }

        public void Dispose()
        {
            Service.Dispose();
            SessionService.Dispose();
            Bridge.Dispose();
        }
    }

    private static LanguageTextEdit Edit(int sl, int sc, int el, int ec, string text) =>
        new(new LspRange(sl, sc, el, ec), text);

    [Fact]
    public void EditApplier_NoEdits_ReturnsSourceUnchanged()
    {
        var source = "class C { }";
        Assert.True(LanguageFormattingEditApplier.TryApply(source, Array.Empty<LanguageTextEdit>(), out var result));
        Assert.Equal(source, result);
    }

    [Fact]
    public void EditApplier_ValidSingleEdit_Applies()
    {
        var source = "a b c";
        var edits = new[] { Edit(0, 2, 0, 3, "B") };
        Assert.True(LanguageFormattingEditApplier.TryApply(source, edits, out var result));
        Assert.Equal("a B c", result);
    }

    [Fact]
    public void EditApplier_MultiEdit_AppliesInReverseOffsetOrder()
    {
        var source = "xxAAyyBBzz";
        // Replace AA and BB (non-overlapping)
        var edits = new[]
        {
            Edit(0, 2, 0, 4, "aa"),
            Edit(0, 6, 0, 8, "bb"),
        };
        Assert.True(LanguageFormattingEditApplier.TryApply(source, edits, out var result));
        Assert.Equal("xxaayybbzz", result);
    }

    [Fact]
    public void EditApplier_OverlappingEdits_Rejected()
    {
        var source = "abcdef";
        var edits = new[]
        {
            Edit(0, 1, 0, 4, "X"),
            Edit(0, 2, 0, 5, "Y"),
        };
        Assert.False(LanguageFormattingEditApplier.TryApply(source, edits, out var result));
        Assert.Equal(source, result);
    }

    [Fact]
    public void EditApplier_OutOfRange_Rejected()
    {
        var source = "ab";
        var edits = new[] { Edit(0, 0, 0, 99, "X") };
        Assert.False(LanguageFormattingEditApplier.TryApply(source, edits, out _));
    }

    [Fact]
    public void EditApplier_NonBmp_Utf16Positions()
    {
        // 🎉 is two UTF-16 code units
        var source = "a🎉b";
        Assert.Equal(4, source.Length);
        // Replace 🎉 (characters 1..3) with "X"
        var edits = new[] { Edit(0, 1, 0, 3, "X") };
        Assert.True(LanguageFormattingEditApplier.TryApply(source, edits, out var result));
        Assert.Equal("aXb", result);
    }

    [Fact]
    public async Task Format_NoEdits_AcceptedWithoutMutationSignal()
    {
        using var h = new Harness();
        var (path, doc) = h.OpenActive("tidy.cs", "class C { }");
        var original = doc.Content;
        h.SessionService.SetReady(h.Session);
        h.Session.FormattingHandler = (_, _) =>
            Task.FromResult<LanguageServerFormattingResult?>(LanguageServerFormattingResult.Empty);

        var outcome = await h.Service.FormatDocumentAsync(path);

        Assert.Equal(LanguageFormattingOutcomeKind.NoEdits, outcome.Kind);
        Assert.True(outcome.IsAccepted);
        Assert.False(outcome.HasTextChange);
        Assert.Equal(original, doc.Content);
        Assert.False(doc.IsDirty);
        Assert.Equal(1, h.Session.FormattingCallCount);
        Assert.Contains(h.Snapshots, s => s.State == LanguageFormattingState.NoEdits);
    }

    [Fact]
    public async Task Format_ValidEdits_ProducesFormattedText()
    {
        using var h = new Harness();
        var (path, doc) = h.OpenActive("messy.cs", "class C{int x;}");
        h.SessionService.SetReady(h.Session);
        h.Session.FormattingHandler = (_, _) =>
            Task.FromResult<LanguageServerFormattingResult?>(
                new LanguageServerFormattingResult(new[]
                {
                    Edit(0, 0, 0, "class C{int x;}".Length, "class C\n{\n    int x;\n}\n"),
                }));

        var outcome = await h.Service.FormatDocumentAsync(path);

        Assert.Equal(LanguageFormattingOutcomeKind.Applied, outcome.Kind);
        Assert.True(outcome.HasTextChange);
        Assert.Equal("class C\n{\n    int x;\n}\n", outcome.FormattedText);
        // Service never mutates Document — callers apply.
        Assert.Equal("class C{int x;}", doc.Content);
    }

    [Fact]
    public async Task Format_InvalidEdits_RejectedWithoutTextChange()
    {
        using var h = new Harness();
        var (path, doc) = h.OpenActive("bad.cs", "abc");
        h.SessionService.SetReady(h.Session);
        h.Session.FormattingHandler = (_, _) =>
            Task.FromResult<LanguageServerFormattingResult?>(
                new LanguageServerFormattingResult(new[] { Edit(0, 0, 0, 99, "X") }));

        var outcome = await h.Service.FormatDocumentAsync(path);

        Assert.Equal(LanguageFormattingOutcomeKind.Invalid, outcome.Kind);
        Assert.False(outcome.IsAccepted);
        Assert.Equal("abc", doc.Content);
        Assert.Equal(LanguageFormattingPolicy.InvalidMessage, outcome.FeedbackMessage);
    }

    [Fact]
    public async Task Format_OverlappingEdits_Rejected()
    {
        using var h = new Harness();
        var (path, _) = h.OpenActive("overlap.cs", "abcdef");
        h.SessionService.SetReady(h.Session);
        h.Session.FormattingHandler = (_, _) =>
            Task.FromResult<LanguageServerFormattingResult?>(
                new LanguageServerFormattingResult(new[]
                {
                    Edit(0, 0, 0, 4, "X"),
                    Edit(0, 2, 0, 6, "Y"),
                }));

        var outcome = await h.Service.FormatDocumentAsync(path);
        Assert.Equal(LanguageFormattingOutcomeKind.Invalid, outcome.Kind);
    }

    [Fact]
    public async Task Format_UnsupportedCapability_NoRequest()
    {
        using var h = new Harness();
        var (path, _) = h.OpenActive("unsup.cs", "class C {}");
        h.Session.Capabilities = TestLanguageServerSession.DefaultCapabilities with
        {
            DocumentFormattingSupported = false,
        };
        h.SessionService.SetReady(h.Session);

        var outcome = await h.Service.FormatDocumentAsync(path);

        Assert.Equal(LanguageFormattingOutcomeKind.Unsupported, outcome.Kind);
        Assert.Equal(0, h.Session.FormattingCallCount);
        Assert.Equal(LanguageFormattingPolicy.UnsupportedMessage, outcome.FeedbackMessage);
    }

    [Fact]
    public async Task Format_SessionNotReady_Unavailable()
    {
        using var h = new Harness();
        var (path, _) = h.OpenActive("nr.cs", "class C {}");
        h.SessionService.SetUnavailable();

        var outcome = await h.Service.FormatDocumentAsync(path);

        Assert.Equal(LanguageFormattingOutcomeKind.Unavailable, outcome.Kind);
        Assert.Equal(0, h.Session.FormattingCallCount);
    }

    [Fact]
    public async Task Format_FailedNullResult_FailedOutcome()
    {
        using var h = new Harness();
        var (path, _) = h.OpenActive("fail.cs", "class C {}");
        h.SessionService.SetReady(h.Session);
        h.Session.FormattingHandler = (_, _) =>
            Task.FromResult<LanguageServerFormattingResult?>(null);

        var outcome = await h.Service.FormatDocumentAsync(path);

        Assert.Equal(LanguageFormattingOutcomeKind.Failed, outcome.Kind);
        Assert.Equal(LanguageFormattingPolicy.FailedMessage, outcome.FeedbackMessage);
    }

    [Fact]
    public async Task Format_Cancelled_ReturnsCancelled()
    {
        using var h = new Harness();
        var (path, _) = h.OpenActive("cancel.cs", "class C {}");
        h.SessionService.SetReady(h.Session);
        h.Session.FormattingGate = new TaskCompletionSource<bool>();

        using var cts = new CancellationTokenSource();
        var task = h.Service.FormatDocumentAsync(path, cts.Token);
        cts.Cancel();
        h.Session.FormattingGate.TrySetResult(true);

        var outcome = await task;
        Assert.Equal(LanguageFormattingOutcomeKind.Cancelled, outcome.Kind);
    }

    [Fact]
    public async Task Format_StaleVersion_Rejected()
    {
        using var h = new Harness();
        var (path, _) = h.OpenActive("stale.cs", "class C {}", version: 1);
        h.SessionService.SetReady(h.Session);
        h.Session.FormattingGate = new TaskCompletionSource<bool>();

        var task = h.Service.FormatDocumentAsync(path);
        for (var i = 0; i < 50 && h.Session.FormattingCallCount == 0; i++)
            await Task.Delay(10);
        h.Bridge.SetVersion(path, 2); // version advanced while in flight
        h.Session.FormattingGate.TrySetResult(true);

        var outcome = await task;
        Assert.Equal(LanguageFormattingOutcomeKind.Stale, outcome.Kind);
    }

    [Fact]
    public async Task Format_ActiveTabSwitch_Rejected()
    {
        using var h = new Harness();
        var (pathA, docA) = h.OpenActive("a.cs", "class A {}");
        var pathB = Path.Combine(TempRoot, Guid.NewGuid().ToString("N") + "-b.cs");
        File.WriteAllText(pathB, "class B {}");
        var docB = h.Workspace.OpenDocument(pathB, "class B {}");
        // OpenDocument activates B; restore A as active before formatting.
        h.Workspace.SetActiveDocument(docA);

        h.SessionService.SetReady(h.Session);
        h.Session.FormattingGate = new TaskCompletionSource<bool>();

        var task = h.Service.FormatDocumentAsync(pathA);
        // Wait until the request is in-flight so early validation has passed.
        for (var i = 0; i < 50 && h.Session.FormattingCallCount == 0; i++)
            await Task.Delay(10);
        Assert.True(h.Session.FormattingCallCount >= 1, "formatting request should start");
        h.Workspace.SetActiveDocument(docB);
        h.Session.FormattingGate.TrySetResult(true);

        var outcome = await task;
        Assert.Equal(LanguageFormattingOutcomeKind.Stale, outcome.Kind);
    }

    [Fact]
    public async Task Format_ContentChangedDuringRequest_Stale()
    {
        using var h = new Harness();
        var (path, doc) = h.OpenActive("chg.cs", "class C {}");
        h.SessionService.SetReady(h.Session);
        h.Session.FormattingGate = new TaskCompletionSource<bool>();

        var task = h.Service.FormatDocumentAsync(path);
        for (var i = 0; i < 50 && h.Session.FormattingCallCount == 0; i++)
            await Task.Delay(10);
        doc.Content = "class C { int x; }";
        h.Session.FormattingGate.TrySetResult(true);

        var outcome = await task;
        Assert.Equal(LanguageFormattingOutcomeKind.Stale, outcome.Kind);
        Assert.Equal("class C { int x; }", doc.Content);
    }

    [Fact]
    public async Task Format_InactivePath_Unavailable()
    {
        using var h = new Harness();
        var (pathA, docA) = h.OpenActive("a.cs", "class A {}");
        var pathB = Path.Combine(TempRoot, Guid.NewGuid().ToString("N") + "-other.cs");
        File.WriteAllText(pathB, "class B {}");
        h.Workspace.OpenDocument(pathB, "class B {}");
        h.Bridge.SetOpen(pathB, 1, 1);
        h.Workspace.SetActiveDocument(docA); // keep A active
        h.SessionService.SetReady(h.Session);

        // pathB is open but not active
        var outcome = await h.Service.FormatDocumentAsync(pathB);
        Assert.Equal(LanguageFormattingOutcomeKind.Unavailable, outcome.Kind);
        Assert.Equal(0, h.Session.FormattingCallCount);
        Assert.Equal(pathA, h.Workspace.ActiveDocument?.FilePath);
    }

    [Fact]
    public void CommandId_AndDefaultGesture_AreLocked()
    {
        Assert.Equal("editor.formatDocument", LanguageFormattingPolicy.FormatDocumentCommandId);
        Assert.Equal(new[] { "Ctrl+Shift+I" }, LanguageFormattingPolicy.FormatDocumentDefaultGestures);
    }
}
