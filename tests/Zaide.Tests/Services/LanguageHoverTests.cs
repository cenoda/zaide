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
using Zaide.Features.Workspace.Domain;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 10 M4 tests for active-document hover ownership.
/// </summary>
public sealed class LanguageHoverTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase10-m4-hover-" + Guid.NewGuid().ToString("N"));

    static LanguageHoverTests()
    {
        Directory.CreateDirectory(TempRoot);
    }

    private sealed class ConfigurableSession : ILanguageServerSession
    {
        public long Generation { get; set; } = 1;
        public LanguageServerCapabilities Capabilities { get; set; } = TestLanguageServerSession.DefaultCapabilities;
        public Func<string, int, int, CancellationToken, Task<LanguageServerHoverResult?>>? HoverHandler { get; set; }
        public TaskCompletionSource<bool>? HoverGate { get; set; }
        public int HoverCallCount { get; private set; }

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
            TestLanguageServerSession.EmptyCompletionAsync(documentUri, line, character, cancellationToken);

        public async Task<LanguageServerHoverResult?> RequestHoverAsync(
            string documentUri,
            int line,
            int character,
            CancellationToken cancellationToken = default)
        {
            HoverCallCount++;
            if (HoverGate is not null)
                await HoverGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            if (HoverHandler is not null)
                return await HoverHandler(documentUri, line, character, cancellationToken).ConfigureAwait(false);

            return new LanguageServerHoverResult(null);
        }

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

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

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
        public LanguageHoverService Service { get; }
        public List<LanguageHoverSnapshot> Snapshots { get; } = new();

        public Harness()
        {
            Service = new LanguageHoverService(
                Workspace,
                SessionService,
                Bridge,
                NullLogger<LanguageHoverService>.Instance);
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
    public async Task DwellDelay_ShowsReadyHover()
    {
        using var harness = new Harness();
        var content = "class C { void Greet() {} }";
        var path = harness.OpenCs("hover.cs", content);
        harness.SetReady();
        var offset = content.IndexOf("Greet", StringComparison.Ordinal);

        harness.Session.HoverHandler = (_, _, _, _) =>
            Task.FromResult<LanguageServerHoverResult?>(new LanguageServerHoverResult("void Greet()"));

        harness.Service.Schedule(path, offset);
        await Task.Delay(LanguageHoverTriggerPolicy.DwellDelay + TimeSpan.FromMilliseconds(80));
        await WaitForVisibleAsync(harness);

        Assert.Equal(LanguageHoverState.Ready, harness.Service.Current.State);
        Assert.Contains("Greet", harness.Service.Current.Content);
    }

    [Fact]
    public async Task CaretChange_ReplacesPendingHover()
    {
        using var harness = new Harness();
        var path = harness.OpenCs("replace.cs", "class C { void Greet() {} }");
        harness.SetReady();

        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Session.HoverGate = gate;

        harness.Service.Schedule(path, 10);
        await Task.Delay(LanguageHoverTriggerPolicy.DwellDelay + TimeSpan.FromMilliseconds(40));
        harness.Service.Schedule(path, 20);
        gate.TrySetResult(true);
        await Task.Delay(300);

        Assert.True(harness.Session.HoverCallCount <= 1);
    }

    [Fact]
    public async Task EmptyHover_DoesNotStayVisible()
    {
        using var harness = new Harness();
        var path = harness.OpenCs("empty.cs", "class C {}");
        harness.SetReady();
        harness.Service.Schedule(path, 0);
        await Task.Delay(LanguageHoverTriggerPolicy.DwellDelay + TimeSpan.FromMilliseconds(200));
        Assert.False(harness.Service.Current.IsVisible);
    }

    [Fact]
    public async Task UnsupportedCapability_DoesNotShowHover()
    {
        using var harness = new Harness();
        var path = harness.OpenCs("unsupported.cs", "class C {}");
        harness.SetReady();
        harness.Session.Capabilities = new LanguageServerCapabilities(true, new[] { '.' }, false, true, true, true);
        harness.Service.Schedule(path, 0);
        await Task.Delay(LanguageHoverTriggerPolicy.DwellDelay + TimeSpan.FromMilliseconds(100));
        Assert.False(harness.Service.Current.IsVisible);
    }

    [Fact]
    public async Task StaleVersionResponse_IsDiscarded()
    {
        using var harness = new Harness();
        var content = "class C { void Greet() {} }";
        var path = harness.OpenCs("stale.cs", content);
        var uri = LanguageDocumentUri.FromPath(path);
        harness.SetReady();

        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Session.HoverGate = gate;
        harness.Session.HoverHandler = (_, _, _, _) =>
            Task.FromResult<LanguageServerHoverResult?>(new LanguageServerHoverResult("late"));

        harness.Service.Schedule(path, 10);
        await Task.Delay(LanguageHoverTriggerPolicy.DwellDelay + TimeSpan.FromMilliseconds(20));
        harness.Workspace.Documents.First().Content = content + "\n";
        harness.Bridge.SetVersion(uri, 2);
        gate.TrySetResult(true);
        await Task.Delay(300);

        Assert.False(harness.Service.Current.IsVisible);
    }

    [Fact]
    public async Task Dismiss_ClearsVisibleHover()
    {
        using var harness = new Harness();
        var path = harness.OpenCs("dismiss.cs", "class C {}");
        harness.SetReady();
        harness.Session.HoverHandler = (_, _, _, _) =>
            Task.FromResult<LanguageServerHoverResult?>(new LanguageServerHoverResult("tip"));
        harness.Service.Schedule(path, 0);
        await WaitForVisibleAsync(harness);
        harness.Service.Dismiss();
        Assert.Equal(LanguageHoverState.Idle, harness.Service.Current.State);
    }

    [Fact]
    public void NonBmpPosition_UsesUtf16CharacterIndex()
    {
        var text = "var 🎉 = Greet";
        Assert.True(LspUtf16PositionMapper.TryGetOffset(text, 0, 9, out var offset));
        var (line, character) = LspUtf16PositionMapper.GetPosition(text, offset);
        Assert.Equal(0, line);
        Assert.Equal(9, character);
    }

    private static async Task WaitForVisibleAsync(Harness harness, int timeoutMs = 2500)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (harness.Service.Current.IsVisible)
                return;

            await Task.Delay(20);
        }

        Assert.True(harness.Service.Current.IsVisible);
    }
}
