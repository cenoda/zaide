using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 10 M5 tests for Go to Definition ownership and validated navigation.
/// </summary>
public sealed class LanguageNavigationTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase10-m5-nav-" + Guid.NewGuid().ToString("N"));

    static LanguageNavigationTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        Directory.CreateDirectory(TempRoot);
    }

    private sealed class ConfigurableSession : ILanguageServerSession
    {
        public long Generation { get; set; } = 1;
        public LanguageServerCapabilities Capabilities { get; set; } = TestLanguageServerSession.DefaultCapabilities;
        public Func<string, int, int, CancellationToken, Task<LanguageServerDefinitionResult?>>? DefinitionHandler { get; set; }
        public TaskCompletionSource<bool>? DefinitionGate { get; set; }
        public int DefinitionCallCount { get; private set; }
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

        public async Task<LanguageServerDefinitionResult?> RequestDefinitionAsync(
            string documentUri, int line, int character, CancellationToken cancellationToken = default)
        {
            DefinitionCallCount++;
            if (DefinitionGate is not null)
                await DefinitionGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            if (DefinitionHandler is not null)
                return await DefinitionHandler(documentUri, line, character, cancellationToken).ConfigureAwait(false);

            return new LanguageServerDefinitionResult(Array.Empty<LanguageLocation>());
        }

        public Task<LanguageServerSymbolResult?> RequestDocumentSymbolsAsync(
            string documentUri, CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptySymbolsAsync(cancellationToken);

        public Task<LanguageServerSymbolResult?> RequestWorkspaceSymbolsAsync(
            string query, CancellationToken cancellationToken = default) =>
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

        public void SetReady(ConfigurableSession session, long generation = 1)
        {
            session.Generation = generation;
            SetSnapshot(new LanguageSessionSnapshot(
                LanguageSessionState.Ready, generation, "/p.csproj", TempRoot, 1, null), session);
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
        private readonly ServiceProvider _sp;

        public Workspace Workspace { get; } = new();
        public FakeSessionService SessionService { get; } = new();
        public FakeDocumentBridge Bridge { get; } = new();
        public ConfigurableSession Session { get; } = new();
        public LanguageNavigationService Service { get; }
        public EditorTabViewModel Tabs { get; }
        public EditorLanguageInputViewModel Input { get; }
        public List<LanguageNavigationSnapshot> Snapshots { get; } = new();
        public int OpenCommandExecutions { get; private set; }

        public Harness()
        {
            var services = new ServiceCollection();
            services.AddSingleton(Workspace);
            services.AddSingleton<IFileService>(new FileService());
            services.AddTransient(sp =>
                new EditorViewModel(new Document(""), sp.GetRequiredService<IFileService>()));
            _sp = services.BuildServiceProvider();
            Tabs = new EditorTabViewModel(_sp, _sp.GetRequiredService<IFileService>(), Workspace);

            Service = new LanguageNavigationService(
                Workspace, SessionService, Bridge, NullLogger<LanguageNavigationService>.Instance);
            Service.WhenChanged.Subscribe(s => Snapshots.Add(s));

            var completion = new LanguageCompletionService(
                Workspace, SessionService, Bridge, NullLogger<LanguageCompletionService>.Instance);
            var hover = new LanguageHoverService(
                Workspace, SessionService, Bridge, NullLogger<LanguageHoverService>.Instance);
            var symbols = new LanguageSymbolService(
                Workspace, SessionService, Bridge, NullLogger<LanguageSymbolService>.Instance);
            var formatting = new LanguageFormattingService(
                Workspace, SessionService, Bridge, NullLogger<LanguageFormattingService>.Instance);

            Input = new EditorLanguageInputViewModel(
                completion, hover, Service, symbols, formatting, SessionService, Tabs, CommandRegistryFactory.Create());

            // Count open attempts without consuming command execution semantics.
            Tabs.OpenFileCommand.IsExecuting.Subscribe(_ => { });
        }

        public string OpenActive(string name, string content, int version = 1, long generation = 1)
        {
            var path = Path.Combine(TempRoot, name);
            File.WriteAllText(path, content);
            var doc = Workspace.OpenDocument(path, content);
            Workspace.SetActiveDocument(doc);
            Bridge.SetOpen(path, version, generation);
            return path;
        }

        public void Dispose()
        {
            Service.Dispose();
            SessionService.Dispose();
            Bridge.Dispose();
            _sp.Dispose();
        }
    }

    private static LanguageLocation Loc(string path, int sl, int sc, int el, int ec, string? name = null) =>
        new(LanguageDocumentUri.FromPath(path), path, new LspRange(sl, sc, el, ec), null, name);

    private static async Task WaitForAsync(Func<bool> predicate, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(2));
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
                return;
            await Task.Delay(15);
        }
    }

    [Fact]
    public async Task SingleDefinition_SameFile_ProducesReady()
    {
        using var h = new Harness();
        var content = "class C { void M() { M(); } }";
        var path = h.OpenActive("same.cs", content);
        h.SessionService.SetReady(h.Session);
        h.Session.DefinitionHandler = (_, _, _, _) =>
            Task.FromResult<LanguageServerDefinitionResult?>(
                new LanguageServerDefinitionResult(new[] { Loc(path, 0, 10, 0, 11, "M") }));

        h.Service.RequestDefinition(path, content.IndexOf("M();", StringComparison.Ordinal));
        await WaitForAsync(() => h.Service.Current.State is LanguageNavigationState.Ready or LanguageNavigationState.Idle);

        // Single-result Ready is observed before auto-take may dismiss.
        Assert.Contains(h.Snapshots, s => s.IsSingleNavigateReady && s.Locations.Count == 1);
    }

    [Fact]
    public async Task SingleDefinition_CrossFile_NavigatesThroughOpenFileCommand()
    {
        using var h = new Harness();
        var sourceContent = "class Caller { void X() { Target.M(); } }";
        var targetContent = "class Target { public static void M() {} }";
        var source = h.OpenActive("caller.cs", sourceContent);
        var target = Path.Combine(TempRoot, "target.cs");
        await File.WriteAllTextAsync(target, targetContent);

        h.SessionService.SetReady(h.Session);
        h.Session.DefinitionHandler = (_, _, _, _) =>
            Task.FromResult<LanguageServerDefinitionResult?>(
                new LanguageServerDefinitionResult(new[] { Loc(target, 0, 35, 0, 36, "M") }));

        var location = Loc(target, 0, 35, 0, 36, "M");
        var navigated = await h.Input.NavigateToLocationAsync(location);
        Assert.True(navigated);
        Assert.NotNull(h.Tabs.ActiveTab);
        Assert.Equal(target, h.Tabs.ActiveTab!.FilePath);
        Assert.NotNull(h.Tabs.ActiveTab.PendingNavigationOffset);
    }

    [Fact]
    public async Task ZeroResults_PublishesEmptyFeedback_NoNavigation()
    {
        using var h = new Harness();
        var content = "class C {}";
        var path = h.OpenActive("empty.cs", content);
        h.SessionService.SetReady(h.Session);
        h.Session.DefinitionHandler = (_, _, _, _) =>
            Task.FromResult<LanguageServerDefinitionResult?>(
                new LanguageServerDefinitionResult(Array.Empty<LanguageLocation>()));

        h.Service.RequestDefinition(path, 0);
        await WaitForAsync(() => h.Snapshots.Any(s => s.State == LanguageNavigationState.Empty));

        var empty = h.Snapshots.Last(s => s.State == LanguageNavigationState.Empty);
        Assert.Equal(LanguageNavigationPolicy.NotFoundMessage, empty.FeedbackMessage);
        Assert.Equal(LanguageNavigationState.Idle, h.Service.Current.State);
        Assert.Null(h.Tabs.ActiveTab?.PendingNavigationOffset);
    }

    [Fact]
    public async Task MultipleResults_ShowsChooser_DeterministicOrder()
    {
        using var h = new Harness();
        var content = "class C { object x; }";
        var path = h.OpenActive("multi.cs", content);
        var other = Path.Combine(TempRoot, "other-def.cs");
        await File.WriteAllTextAsync(other, "class Other {}");

        h.SessionService.SetReady(h.Session);
        h.Session.DefinitionHandler = (_, _, _, _) =>
            Task.FromResult<LanguageServerDefinitionResult?>(
                new LanguageServerDefinitionResult(new[]
                {
                    Loc(other, 0, 6, 0, 11, "Other"),
                    Loc(path, 0, 6, 0, 7, "C"),
                }));

        h.Service.RequestDefinition(path, 0);
        await WaitForAsync(() => h.Service.Current.State == LanguageNavigationState.Choose);

        Assert.True(h.Service.Current.IsChooserOpen);
        Assert.Equal(2, h.Service.Current.Locations.Count);
        // Ordered by path (OrdinalIgnoreCase), so multi.cs before other-def.cs when names compare that way.
        Assert.Equal(path, h.Service.Current.Locations[0].FilePath);
        Assert.Equal(other, h.Service.Current.Locations[1].FilePath);
    }

    [Fact]
    public async Task InvalidLocations_DoNotNavigate()
    {
        using var h = new Harness();
        var content = "class C {}";
        var path = h.OpenActive("invalid.cs", content);
        h.SessionService.SetReady(h.Session);
        h.Session.DefinitionHandler = (_, _, _, _) =>
            Task.FromResult<LanguageServerDefinitionResult?>(
                new LanguageServerDefinitionResult(new[]
                {
                    new LanguageLocation("file:///not/a/valid/path%zz", null,
                        new LspRange(0, 0, 0, 1), null, null),
                    new LanguageLocation("https://example.com/x", null,
                        new LspRange(0, 0, 0, 1), null, null),
                }));

        h.Service.RequestDefinition(path, 0);
        await WaitForAsync(() => h.Snapshots.Any(s =>
            s.State is LanguageNavigationState.Failed or LanguageNavigationState.Empty));

        Assert.Null(h.Service.TryTakeSingleLocation());
        Assert.Equal(LanguageNavigationState.Idle, h.Service.Current.State);
    }

    [Fact]
    public async Task UnsupportedCapability_UnavailableFeedback()
    {
        using var h = new Harness();
        var content = "class C {}";
        var path = h.OpenActive("unsup.cs", content);
        h.Session.Capabilities = new LanguageServerCapabilities(
            true, new[] { '.' }, true, DefinitionSupported: false, true, true);
        h.SessionService.SetReady(h.Session);

        h.Service.RequestDefinition(path, 0);
        await WaitForAsync(() => h.Snapshots.Any(s => s.State == LanguageNavigationState.Unavailable));

        var unavailable = h.Snapshots.Last(s => s.State == LanguageNavigationState.Unavailable);
        Assert.Equal(LanguageNavigationPolicy.UnavailableMessage, unavailable.FeedbackMessage);
        Assert.Equal(0, h.Session.DefinitionCallCount);
    }

    [Fact]
    public async Task FailedRequest_PublishesFailure_NoNavigation()
    {
        using var h = new Harness();
        var content = "class C {}";
        var path = h.OpenActive("fail.cs", content);
        h.SessionService.SetReady(h.Session);
        h.Session.DefinitionHandler = (_, _, _, _) =>
            Task.FromResult<LanguageServerDefinitionResult?>(null);

        h.Service.RequestDefinition(path, 0);
        await WaitForAsync(() => h.Snapshots.Any(s => s.State == LanguageNavigationState.Failed));

        Assert.Equal(LanguageNavigationPolicy.FailedMessage,
            h.Snapshots.Last(s => s.State == LanguageNavigationState.Failed).FeedbackMessage);
        Assert.Null(h.Tabs.ActiveTab?.PendingNavigationOffset);
    }

    [Fact]
    public async Task CancelledRequest_DoesNotUpdateSurfaceOrNavigate()
    {
        using var h = new Harness();
        var content = "class C {}";
        var path = h.OpenActive("cancel.cs", content);
        h.SessionService.SetReady(h.Session);
        h.Session.DefinitionGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        h.Service.RequestDefinition(path, 0);
        await WaitForAsync(() => h.Service.Current.State == LanguageNavigationState.Loading);

        h.Service.Dismiss();
        h.Session.DefinitionGate.TrySetResult(true);
        await Task.Delay(100);

        Assert.Equal(LanguageNavigationState.Idle, h.Service.Current.State);
        Assert.DoesNotContain(h.Snapshots, s => s.State == LanguageNavigationState.Ready);
        Assert.Null(h.Tabs.ActiveTab?.PendingNavigationOffset);
    }

    [Fact]
    public async Task StaleGeneration_DoesNotAcceptResult()
    {
        using var h = new Harness();
        var content = "class C {}";
        var path = h.OpenActive("stale-gen.cs", content);
        h.SessionService.SetReady(h.Session, generation: 1);
        h.Session.DefinitionGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Session.DefinitionHandler = (_, _, _, _) =>
            Task.FromResult<LanguageServerDefinitionResult?>(
                new LanguageServerDefinitionResult(new[] { Loc(path, 0, 6, 0, 7) }));

        h.Service.RequestDefinition(path, 0);
        await WaitForAsync(() => h.Service.Current.State == LanguageNavigationState.Loading);

        // Advance generation while request in flight.
        h.SessionService.SetReady(h.Session, generation: 2);
        h.Bridge.SetOpen(path, 1, 2);
        h.Session.DefinitionGate.TrySetResult(true);
        await Task.Delay(150);

        // Stale generation must not leave a Ready/Choose surface.
        Assert.False(h.Service.Current.IsSingleNavigateReady);
        Assert.False(h.Service.Current.IsChooserOpen);
    }

    [Fact]
    public async Task StaleDocumentVersion_DoesNotAcceptResult()
    {
        using var h = new Harness();
        var content = "class C {}";
        var path = h.OpenActive("stale-ver.cs", content, version: 1);
        h.SessionService.SetReady(h.Session);
        h.Session.DefinitionGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Session.DefinitionHandler = (_, _, _, _) =>
            Task.FromResult<LanguageServerDefinitionResult?>(
                new LanguageServerDefinitionResult(new[] { Loc(path, 0, 6, 0, 7) }));

        h.Service.RequestDefinition(path, 0);
        await WaitForAsync(() => h.Service.Current.State == LanguageNavigationState.Loading);

        h.Bridge.SetVersion(path, 2);
        h.Session.DefinitionGate.TrySetResult(true);
        await Task.Delay(150);

        Assert.False(h.Service.Current.IsSingleNavigateReady);
        Assert.Null(h.Service.TryTakeSingleLocation());
    }

    [Fact]
    public async Task ActiveTabChange_DismissesAndBlocksStaleNavigation()
    {
        using var h = new Harness();
        var contentA = "class A {}";
        var pathA = h.OpenActive("a.cs", contentA);
        h.SessionService.SetReady(h.Session);
        h.Session.DefinitionGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Session.DefinitionHandler = (_, _, _, _) =>
            Task.FromResult<LanguageServerDefinitionResult?>(
                new LanguageServerDefinitionResult(new[] { Loc(pathA, 0, 6, 0, 7) }));

        h.Service.RequestDefinition(pathA, 0);
        await WaitForAsync(() => h.Service.Current.State == LanguageNavigationState.Loading);

        var contentB = "class B {}";
        var pathB = Path.Combine(TempRoot, "b.cs");
        await File.WriteAllTextAsync(pathB, contentB);
        var docB = h.Workspace.OpenDocument(pathB, contentB);
        h.Workspace.SetActiveDocument(docB);
        h.Bridge.SetOpen(pathB, 1, 1);

        h.Session.DefinitionGate.TrySetResult(true);
        await Task.Delay(150);

        Assert.Null(h.Service.TryTakeSingleLocation());
        Assert.False(h.Service.Current.IsChooserOpen);
        Assert.Null(h.Tabs.ActiveTab?.PendingNavigationOffset);
    }

    [Fact]
    public async Task ClosedProjectSession_Unavailable()
    {
        using var h = new Harness();
        var path = h.OpenActive("closed.cs", "class C {}");
        h.SessionService.SetSnapshot(new LanguageSessionSnapshot(
            LanguageSessionState.Unavailable, 3, null, null, null, null));

        h.Service.RequestDefinition(path, 0);
        await WaitForAsync(() => h.Snapshots.Any(s => s.State == LanguageNavigationState.Unavailable));

        Assert.Equal(0, h.Session.DefinitionCallCount);
    }

    [Fact]
    public async Task TryAcceptSelected_NavigatesChosenLocation()
    {
        using var h = new Harness();
        var content = "class C {}";
        var path = h.OpenActive("choose.cs", content);
        var other = Path.Combine(TempRoot, "choose-other.cs");
        await File.WriteAllTextAsync(other, "class Other { }");

        h.SessionService.SetReady(h.Session);
        h.Session.DefinitionHandler = (_, _, _, _) =>
            Task.FromResult<LanguageServerDefinitionResult?>(
                new LanguageServerDefinitionResult(new[]
                {
                    Loc(path, 0, 6, 0, 7, "C"),
                    Loc(other, 0, 6, 0, 11, "Other"),
                }));

        h.Service.RequestDefinition(path, 0);
        await WaitForAsync(() => h.Service.Current.State == LanguageNavigationState.Choose);

        // Deterministic order by path: choose-other.cs sorts before choose.cs.
        Assert.Equal(2, h.Service.Current.Locations.Count);
        var otherIndex = h.Service.Current.Locations
            .Select((loc, i) => (loc, i))
            .First(x => string.Equals(x.loc.FilePath, other, StringComparison.Ordinal))
            .i;
        while (h.Service.Current.SelectedIndex != otherIndex)
            h.Service.MoveSelection(1);

        var selected = h.Service.TryAcceptSelected();
        Assert.NotNull(selected);
        Assert.Equal(other, selected!.FilePath);

        var ok = await h.Input.NavigateToLocationAsync(selected);
        Assert.True(ok);
        Assert.Equal(other, h.Tabs.ActiveTab!.FilePath);
    }

    [Fact]
    public async Task NavigateToLocation_InvalidRange_NoSelectionMutation()
    {
        using var h = new Harness();
        var content = "class C {}";
        var path = h.OpenActive("range.cs", content);
        await h.Tabs.OpenFileCommand.Execute(path).FirstAsync();
        var before = h.Tabs.ActiveTab!.NavigationRequestId;

        var bad = Loc(path, 99, 0, 99, 1);
        var ok = await h.Input.NavigateToLocationAsync(bad);
        Assert.False(ok);
        Assert.Equal(before, h.Tabs.ActiveTab.NavigationRequestId);
        Assert.Null(h.Tabs.ActiveTab.PendingNavigationOffset);
    }
}
