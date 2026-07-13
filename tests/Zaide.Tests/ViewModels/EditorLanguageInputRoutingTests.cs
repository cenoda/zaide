using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.Tests.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

/// <summary>
/// Phase 10 M4 editor input-routing and commit presentation tests.
/// </summary>
public sealed class EditorLanguageInputRoutingTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase10-m4-input-" + Guid.NewGuid().ToString("N"));

    static EditorLanguageInputRoutingTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        Directory.CreateDirectory(TempRoot);
    }

    private sealed class RecordingEditor : IEditorLanguageOperations
    {
        public string Text { get; set; } = string.Empty;
        public int CaretOffset { get; set; }
        public int SelectionStart { get; private set; }
        public int SelectionLength { get; private set; }

        public string GetText() => Text;
        public void SetText(string text) => Text = text;
        public void SetSelection(int offset, int length)
        {
            SelectionStart = offset;
            SelectionLength = length;
        }

        public int GetSelectionOffset() => SelectionStart;
        public int GetSelectionLength() => SelectionLength;
        public int GetCaretOffset() => CaretOffset;

        public char? GetCharBeforeCaret() =>
            CaretOffset > 0 && CaretOffset <= Text.Length ? Text[CaretOffset - 1] : null;

        public void ReplaceRange(int start, int length, string newText) =>
            Text = Text[..start] + newText + Text[(start + length)..];

        public int ReplaceAllMatches(string query, string replacement, bool caseSensitive) => 0;
    }

    private sealed class ConfigurableSession : ILanguageServerSession
    {
        public long Generation { get; set; } = 1;
        public LanguageServerCapabilities Capabilities { get; set; } = TestLanguageServerSession.DefaultCapabilities;
        public Func<CancellationToken, Task<LanguageServerCompletionResult?>>? CompletionHandler { get; set; }

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
            string documentUri,
            int line,
            int character,
            CancellationToken cancellationToken = default) =>
            CompletionHandler is null
                ? TestLanguageServerSession.EmptyCompletionAsync(documentUri, line, character, cancellationToken)
                : CompletionHandler(cancellationToken);

        public Task<LanguageServerHoverResult?> RequestHoverAsync(
            string documentUri,
            int line,
            int character,
            CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptyHoverAsync(documentUri, line, character, cancellationToken);
    }

    private sealed class FakeSessionService : ILanguageSessionService
    {
        private readonly Subject<LanguageSessionSnapshot> _subject = new();
        private LanguageSessionSnapshot _current = new(LanguageSessionState.Unavailable, 0, null, null, null, null);
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
    }

    private sealed class FakeDocumentBridge : ILanguageDocumentBridge
    {
        private readonly Dictionary<string, LanguageTrackedDocumentInfo> _open = new(StringComparer.Ordinal);

        public void Track(string path, int version, long generation)
        {
            var uri = LanguageDocumentUri.FromPath(path);
            _open[uri] = new LanguageTrackedDocumentInfo(uri, path, version, generation);
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
        public LanguageCompletionService Completion { get; }
        public LanguageHoverService Hover { get; }
        public EditorLanguageInputViewModel Input { get; }
        public RecordingEditor Editor { get; } = new();

        public Harness()
        {
            Completion = new LanguageCompletionService(
                Workspace, SessionService, Bridge, NullLogger<LanguageCompletionService>.Instance);
            Hover = new LanguageHoverService(
                Workspace, SessionService, Bridge, NullLogger<LanguageHoverService>.Instance);
            Input = new EditorLanguageInputViewModel(
                Completion, Hover, CommandRegistryFactory.Create());
        }

        public string OpenActive(string name, string content)
        {
            var path = Path.Combine(TempRoot, name);
            var doc = Workspace.OpenDocument(path, content);
            Workspace.SetActiveDocument(doc);
            Bridge.Track(path, 1, 1);
            Editor.Text = content;
            Input.ActiveEditor = Editor;
            Input.ActiveDocumentId = path;
            return path;
        }

        public void Dispose()
        {
            Completion.Dispose();
            Hover.Dispose();
            SessionService.Dispose();
            Bridge.Dispose();
        }
    }

    [Fact]
    public async Task Commit_AppliesOnlyToActiveDocument()
    {
        using var harness = new Harness();
        var content = "class C { void M() { Con } }";
        var path = harness.OpenActive("commit.cs", content);
        harness.SessionService.SetReady(harness.Session);
        harness.Editor.CaretOffset = content.IndexOf("Con", StringComparison.Ordinal) + 3;

        harness.Session.CompletionHandler = _ =>
            Task.FromResult<LanguageServerCompletionResult?>(new LanguageServerCompletionResult(
                new[] { new LanguageServerCompletionItem("Console", "Console", null, null, null, null) }));

        harness.Input.TriggerSuggestCommand.Execute().Subscribe();
        await WaitForCompletionReadyAsync(harness);
        harness.Input.CompletionCommitCommand.Execute().Subscribe();

        Assert.Equal("class C { void M() { Console } }", harness.Editor.Text);
        Assert.Equal(path, harness.Input.ActiveDocumentId);
    }

    [Fact]
    public async Task StaleCompletionCommit_DoesNotMutateAfterTabSwitch()
    {
        using var harness = new Harness();
        var contentA = "class A { void M() { Con } }";
        var pathA = harness.OpenActive("a.cs", contentA);
        harness.SessionService.SetReady(harness.Session);
        harness.Editor.CaretOffset = contentA.IndexOf("Con", StringComparison.Ordinal) + 3;

        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Session.CompletionHandler = async ct =>
        {
            await gate.Task.WaitAsync(ct).ConfigureAwait(false);
            return new LanguageServerCompletionResult(
                new[] { new LanguageServerCompletionItem("Console", "Console", null, null, null, null) });
        };

        harness.Input.TriggerSuggestCommand.Execute().Subscribe();

        var pathB = Path.Combine(TempRoot, "b.cs");
        var contentB = "class B { int x = 1; }";
        harness.Workspace.OpenDocument(pathB, contentB);
        harness.Bridge.Track(pathB, 1, 1);
        harness.Input.ActiveDocumentId = pathB;
        harness.Editor.Text = contentB;
        harness.Editor.CaretOffset = contentB.Length;

        gate.TrySetResult(true);
        await Task.Delay(300);

        Assert.Equal(contentB, harness.Editor.Text);
        Assert.False(harness.Completion.Current.IsPopupOpen);
    }

    [Fact]
    public void ActiveDocumentChange_DismissesTransientState()
    {
        using var harness = new Harness();
        var path = harness.OpenActive("dismiss.cs", "class C {}");
        harness.SessionService.SetReady(harness.Session);
        harness.Completion.RequestExplicit(path, 0);
        harness.Input.ActiveDocumentId = Path.Combine(TempRoot, "other.cs");
        Assert.Equal(LanguageCompletionState.Idle, harness.Completion.Current.State);
        Assert.Equal(LanguageHoverState.Idle, harness.Hover.Current.State);
    }

    [Fact]
    public void TriggerSuggest_IsRegisteredWithCtrlSpace()
    {
        var registry = CommandRegistryFactory.Create();
        _ = new EditorLanguageInputViewModel(
            new LanguageCompletionService(
                new Workspace(),
                new FakeSessionService(),
                new FakeDocumentBridge(),
                NullLogger<LanguageCompletionService>.Instance),
            new LanguageHoverService(
                new Workspace(),
                new FakeSessionService(),
                new FakeDocumentBridge(),
                NullLogger<LanguageHoverService>.Instance),
            registry);

        var descriptor = registry.GetById(LanguageCompletionTriggerPolicy.ExplicitCommandId);
        Assert.NotNull(descriptor);
        Assert.Equal(LanguageCompletionTriggerPolicy.ExplicitDefaultGestures, descriptor!.DefaultGestures);
    }

    private static async Task WaitForCompletionReadyAsync(Harness harness)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (harness.Completion.Current.State == LanguageCompletionState.Ready)
                return;

            await Task.Delay(20);
        }

        Assert.Equal(LanguageCompletionState.Ready, harness.Completion.Current.State);
    }
}
