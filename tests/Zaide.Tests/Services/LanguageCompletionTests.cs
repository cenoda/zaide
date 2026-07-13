using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 10 M4 tests for active-document completion ownership.
/// </summary>
public sealed class LanguageCompletionTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase10-m4-completion-" + Guid.NewGuid().ToString("N"));

    static LanguageCompletionTests()
    {
        Directory.CreateDirectory(TempRoot);
    }

    private sealed class ConfigurableSession : ILanguageServerSession
    {
        public long Generation { get; set; } = 1;
        public LanguageServerCapabilities Capabilities { get; set; } = TestLanguageServerSession.DefaultCapabilities;
        public Func<string, int, int, CancellationToken, Task<LanguageServerCompletionResult?>>? CompletionHandler { get; set; }
        public TaskCompletionSource<bool>? CompletionGate { get; set; }
        public int CompletionCallCount { get; private set; }

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

        public async Task<LanguageServerCompletionResult?> RequestCompletionAsync(
            string documentUri,
            int line,
            int character,
            CancellationToken cancellationToken = default)
        {
            CompletionCallCount++;
            if (CompletionGate is not null)
                await CompletionGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            if (CompletionHandler is not null)
                return await CompletionHandler(documentUri, line, character, cancellationToken).ConfigureAwait(false);

            return new LanguageServerCompletionResult(Array.Empty<LanguageServerCompletionItem>());
        }

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
        private LanguageSessionSnapshot _current = Unavailable(0);
        private ConfigurableSession? _session;

        public LanguageSessionSnapshot Current => _current;
        public IObservable<LanguageSessionSnapshot> WhenChanged => _subject;

        public void SetSnapshot(LanguageSessionSnapshot snapshot, ConfigurableSession? session = null)
        {
            _session = session;
            _current = snapshot;
            _subject.OnNext(snapshot);
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

        private static LanguageSessionSnapshot Unavailable(long generation) => new(
            LanguageSessionState.Unavailable, generation, null, null, null, null);
    }

    private sealed class FakeDocumentBridge : ILanguageDocumentBridge
    {
        private readonly Dictionary<string, LanguageTrackedDocumentInfo> _open =
            new(StringComparer.Ordinal);

        public void SetOpen(string uri, string path, int version, long generation) =>
            _open[LanguageDocumentUri.Normalize(uri)] =
                new LanguageTrackedDocumentInfo(LanguageDocumentUri.Normalize(uri), path, version, generation);

        public void SetVersion(string uri, int version)
        {
            var key = LanguageDocumentUri.Normalize(uri);
            if (_open.TryGetValue(key, out var info))
                _open[key] = info with { Version = version };
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
        public ConfigurableSession Session { get; } = new() { Generation = 1 };
        public LanguageCompletionService Service { get; }
        public List<LanguageCompletionSnapshot> Snapshots { get; } = new();

        public Harness()
        {
            Service = new LanguageCompletionService(
                Workspace,
                SessionService,
                Bridge,
                NullLogger<LanguageCompletionService>.Instance);
            Service.WhenChanged.Subscribe(s => Snapshots.Add(s));
        }

        public string OpenCs(string name, string content)
        {
            var path = Path.Combine(TempRoot, name);
            var doc = Workspace.OpenDocument(path, content);
            Workspace.SetActiveDocument(doc);
            var uri = LanguageDocumentUri.FromPath(path);
            Bridge.SetOpen(uri, path, 1, 1);
            return path;
        }

        public void SetReady(long generation = 1)
        {
            Session.Generation = generation;
            SessionService.SetSnapshot(
                new LanguageSessionSnapshot(
                    LanguageSessionState.Ready,
                    generation,
                    Path.Combine(TempRoot, "Project.csproj"),
                    TempRoot,
                    1,
                    null),
                Session);
        }

        public void Dispose() => Service.Dispose();
    }

    [Fact]
    public async Task ExplicitTrigger_ReturnsReadyItems()
    {
        using var harness = new Harness();
        var content = "class C { void M() { Con } }";
        var path = harness.OpenCs("explicit.cs", content);
        harness.SetReady();

        harness.Session.CompletionHandler = (_, _, _, _) =>
            Task.FromResult<LanguageServerCompletionResult?>(new LanguageServerCompletionResult(
                new[]
                {
                    new LanguageServerCompletionItem("Console", "Console", null, null, null, null),
                }));

        var caret = content.IndexOf("Con", StringComparison.Ordinal) + 3;
        harness.Service.RequestExplicit(path, caret);
        await WaitForStateAsync(harness, LanguageCompletionState.Ready);

        var ready = harness.Service.Current;
        Assert.Equal(LanguageCompletionState.Ready, ready.State);
        Assert.Single(ready.Items);
        Assert.Equal("Console", ready.Items[0].Label);
    }

    [Fact]
    public async Task AutomaticTrigger_OnlyForAdvertisedCharacters()
    {
        using var harness = new Harness();
        var path = harness.OpenCs("auto.cs", "class C { void M() { } }");
        harness.SetReady();
        harness.Session.Capabilities = new LanguageServerCapabilities(true, new[] { '.' }, true);

        harness.Session.CompletionHandler = (_, _, _, _) =>
            Task.FromResult<LanguageServerCompletionResult?>(new LanguageServerCompletionResult(
                new[] { new LanguageServerCompletionItem("WriteLine", "WriteLine", null, null, null, null) }));

        const int caret = 18;
        harness.Service.RequestAutomatic(path, caret, 'x');
        await Task.Delay(LanguageCompletionTriggerPolicy.AutomaticDebounce + TimeSpan.FromMilliseconds(80));
        Assert.Equal(LanguageCompletionState.Idle, harness.Service.Current.State);
        Assert.Equal(0, harness.Session.CompletionCallCount);

        harness.Service.RequestAutomatic(path, caret, '.');
        await WaitForStateAsync(harness, LanguageCompletionState.Ready);
        Assert.Equal(1, harness.Session.CompletionCallCount);
    }

    [Fact]
    public async Task AutomaticTrigger_DebounceCancelsEarlierRequest()
    {
        using var harness = new Harness();
        var path = harness.OpenCs("debounce.cs", "class C { void M() { } }");
        harness.SetReady();

        harness.Session.CompletionHandler = (_, _, _, _) =>
            Task.FromResult<LanguageServerCompletionResult?>(new LanguageServerCompletionResult(
                new[] { new LanguageServerCompletionItem("Item", "Item", null, null, null, null) }));

        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Session.CompletionGate = gate;
        harness.Service.RequestAutomatic(path, 18, '.');
        await Task.Delay(50);
        harness.Service.RequestAutomatic(path, 18, '.');
        gate.TrySetResult(true);
        await WaitForStateAsync(harness, LanguageCompletionState.Ready, timeoutMs: 3000);

        Assert.Equal(1, harness.Session.CompletionCallCount);
    }

    [Fact]
    public async Task EmptyResult_DoesNotKeepPopupOpen()
    {
        using var harness = new Harness();
        var path = harness.OpenCs("empty.cs", "class C {}");
        harness.SetReady();
        harness.Service.RequestExplicit(path, 0);
        await WaitForStateAsync(harness, LanguageCompletionState.Idle, timeoutMs: 2000);
        Assert.False(harness.Service.Current.IsPopupOpen);
    }

    [Fact]
    public async Task UnsupportedCapability_DoesNotOpenPopup()
    {
        using var harness = new Harness();
        var path = harness.OpenCs("unsupported.cs", "class C {}");
        harness.SetReady();
        harness.Session.Capabilities = LanguageServerCapabilities.None;
        harness.Service.RequestExplicit(path, 0);
        await Task.Delay(100);
        Assert.False(harness.Service.Current.IsPopupOpen);
    }

    [Fact]
    public async Task ServerFailure_DoesNotOpenPopup()
    {
        using var harness = new Harness();
        var path = harness.OpenCs("fail.cs", "class C {}");
        harness.SetReady();
        harness.Session.CompletionHandler = (_, _, _, _) =>
            throw new InvalidOperationException("boom");
        harness.Service.RequestExplicit(path, 0);
        await WaitForStateAsync(harness, LanguageCompletionState.Idle);
        Assert.False(harness.Service.Current.IsPopupOpen);
    }

    [Fact]
    public async Task StaleVersionResponse_IsDiscarded()
    {
        using var harness = new Harness();
        var content = "class C { void M() { Con } }";
        var path = harness.OpenCs("stale-version.cs", content);
        var uri = LanguageDocumentUri.FromPath(path);
        harness.SetReady();

        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Session.CompletionGate = gate;
        var caret = content.IndexOf("Con", StringComparison.Ordinal) + 3;
        harness.Service.RequestExplicit(path, caret);

        harness.Workspace.Documents.First().Content = content + "\n";
        harness.Bridge.SetVersion(uri, 2);
        gate.TrySetResult(true);
        await Task.Delay(300);

        Assert.False(harness.Service.Current.IsPopupOpen);
    }

    [Fact]
    public async Task ActiveTabSwitch_DismissesPopup()
    {
        using var harness = new Harness();
        var pathA = harness.OpenCs("a.cs", "class A {}");
        var pathB = Path.Combine(TempRoot, "b.cs");
        harness.SetReady();

        harness.Session.CompletionHandler = (_, _, _, _) =>
            Task.FromResult<LanguageServerCompletionResult?>(new LanguageServerCompletionResult(
                new[] { new LanguageServerCompletionItem("A", "A", null, null, null, null) }));

        harness.Service.RequestExplicit(pathA, 0);
        await WaitForStateAsync(harness, LanguageCompletionState.Ready);

        var docB = harness.Workspace.OpenDocument(pathB, "class B {}");
        harness.Workspace.SetActiveDocument(docB);
        harness.Bridge.SetOpen(LanguageDocumentUri.FromPath(pathB), pathB, 1, 1);
        harness.Service.RequestExplicit(pathB, 0);
        await Task.Delay(200);

        Assert.NotEqual(pathA, harness.Service.Current.FilePath);
    }

    [Fact]
    public void SelectionMovement_WrapsAndCommitReplacesText()
    {
        using var harness = new Harness();
        var content = "class C { void M() { } }";
        var path = harness.OpenCs("commit.cs", content);
        harness.SetReady();

        harness.Session.CompletionHandler = (_, _, _, _) =>
            Task.FromResult<LanguageServerCompletionResult?>(new LanguageServerCompletionResult(
                new[]
                {
                    new LanguageServerCompletionItem("Alpha", "Alpha", null, null, null, null),
                    new LanguageServerCompletionItem("Beta", "Beta", null, null, null, null),
                }));

        harness.Service.RequestExplicit(path, content.Length);
        SpinWait.SpinUntil(() => harness.Service.Current.State == LanguageCompletionState.Ready, 2000);

        harness.Service.MoveSelection(1);
        Assert.Equal(1, harness.Service.Current.SelectedIndex);

        var commit = harness.Service.TryCommitSelected();
        Assert.NotNull(commit);
        Assert.Equal("Beta", commit!.InsertText);
    }

    [Fact]
    public void NonBmpPosition_MapsUtf16Offsets()
    {
        var text = "var 🎉 = Con";
        Assert.True(LspUtf16PositionMapper.TryGetOffset(text, 0, 8, out var caret));
        var prefixStart = LanguageCompletionItemMapper.FindIdentifierPrefixStart(text, caret);
        Assert.Equal(8, prefixStart);
    }

    [Fact]
    public async Task SessionUnavailable_DismissesOnContextChange()
    {
        using var harness = new Harness();
        var path = harness.OpenCs("ctx.cs", "class C {}");
        harness.SetReady();

        harness.Session.CompletionHandler = (_, _, _, _) =>
            Task.FromResult<LanguageServerCompletionResult?>(new LanguageServerCompletionResult(
                new[] { new LanguageServerCompletionItem("X", "X", null, null, null, null) }));

        harness.Service.RequestExplicit(path, 0);
        await WaitForStateAsync(harness, LanguageCompletionState.Ready);

        harness.SessionService.SetSnapshot(
            new LanguageSessionSnapshot(LanguageSessionState.Unavailable, 2, null, null, null, null));
        await Task.Delay(50);
        Assert.Equal(LanguageCompletionState.Idle, harness.Service.Current.State);
    }

    private static async Task WaitForStateAsync(
        Harness harness,
        LanguageCompletionState state,
        int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (harness.Service.Current.State == state)
                return;

            await Task.Delay(20);
        }

        Assert.Equal(state, harness.Service.Current.State);
    }
}
