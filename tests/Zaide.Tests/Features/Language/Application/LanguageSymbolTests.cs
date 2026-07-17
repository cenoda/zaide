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
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.Language.Contracts;
using Zaide.Features.Language.Application;

namespace Zaide.Tests.Features.Language.Application;

/// <summary>
/// Phase 10 M5 tests for document and workspace symbol ownership.
/// </summary>
public sealed class LanguageSymbolTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase10-m5-sym-" + Guid.NewGuid().ToString("N"));

    static LanguageSymbolTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        Directory.CreateDirectory(TempRoot);
    }

    private sealed class ConfigurableSession : ILanguageServerSession
    {
        public long Generation { get; set; } = 1;
        public LanguageServerCapabilities Capabilities { get; set; } = TestLanguageServerSession.DefaultCapabilities;
        public Func<string, CancellationToken, Task<LanguageServerSymbolResult?>>? DocumentHandler { get; set; }
        public Func<string, CancellationToken, Task<LanguageServerSymbolResult?>>? WorkspaceHandler { get; set; }
        public TaskCompletionSource<bool>? DocumentGate { get; set; }
        public TaskCompletionSource<bool>? WorkspaceGate { get; set; }
        public int DocumentCallCount { get; private set; }
        public int WorkspaceCallCount { get; private set; }
        public string? LastWorkspaceQuery { get; private set; }

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

        public async Task<LanguageServerSymbolResult?> RequestDocumentSymbolsAsync(
            string documentUri, CancellationToken cancellationToken = default)
        {
            DocumentCallCount++;
            if (DocumentGate is not null)
                await DocumentGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (DocumentHandler is not null)
                return await DocumentHandler(documentUri, cancellationToken).ConfigureAwait(false);
            return new LanguageServerSymbolResult(Array.Empty<LanguageSymbol>());
        }

        public async Task<LanguageServerSymbolResult?> RequestWorkspaceSymbolsAsync(
            string query, CancellationToken cancellationToken = default)
        {
            WorkspaceCallCount++;
            LastWorkspaceQuery = query;
            if (WorkspaceGate is not null)
                await WorkspaceGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (WorkspaceHandler is not null)
                return await WorkspaceHandler(query, cancellationToken).ConfigureAwait(false);
            return new LanguageServerSymbolResult(Array.Empty<LanguageSymbol>());
        }

        public Task<LanguageServerFormattingResult?> RequestFormattingAsync(
            string documentUri, CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptyFormattingAsync(cancellationToken);
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

        public void SetState(LanguageSessionState state, long generation = 1)
        {
            _session = null;
            _current = new LanguageSessionSnapshot(state, generation, null, null, null, null);
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

        public global::Zaide.Features.Workspace.Domain.Workspace Workspace { get; } = new();
        public FakeSessionService SessionService { get; } = new();
        public FakeDocumentBridge Bridge { get; } = new();
        public ConfigurableSession Session { get; } = new();
        public LanguageSymbolService Service { get; }
        public EditorTabViewModel Tabs { get; }
        public EditorLanguageInputViewModel Input { get; }
        public List<LanguageSymbolSnapshot> Snapshots { get; } = new();

        public Harness()
        {
            var services = new ServiceCollection();
            services.AddSingleton(Workspace);
            services.AddSingleton<IFileService>(new FileService());
            services.AddTransient(sp =>
                new EditorViewModel(new Document(""), sp.GetRequiredService<IFileService>()));
            _sp = services.BuildServiceProvider();
            Tabs = new EditorTabViewModel(_sp, _sp.GetRequiredService<IFileService>(), Workspace);

            Service = new LanguageSymbolService(
                Workspace, SessionService, Bridge, NullLogger<LanguageSymbolService>.Instance);
            Service.WhenChanged.Subscribe(s => Snapshots.Add(s));

            var completion = new LanguageCompletionService(
                Workspace, SessionService, Bridge, NullLogger<LanguageCompletionService>.Instance);
            var hover = new LanguageHoverService(
                Workspace, SessionService, Bridge, NullLogger<LanguageHoverService>.Instance);
            var navigation = new LanguageNavigationService(
                Workspace, SessionService, Bridge, NullLogger<LanguageNavigationService>.Instance);
            var formatting = new LanguageFormattingService(
                Workspace, SessionService, Bridge, NullLogger<LanguageFormattingService>.Instance);

            Input = new EditorLanguageInputViewModel(
                completion, hover, navigation, Service, formatting, SessionService, Tabs, CommandRegistryFactory.Create());
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

    private static LanguageSymbol Sym(
        string name,
        string path,
        int line,
        int kind = 5,
        int depth = 0,
        IReadOnlyList<LanguageSymbol>? children = null) =>
        new(
            name,
            kind,
            Detail: null,
            ContainerName: null,
            new LanguageLocation(
                LanguageDocumentUri.FromPath(path),
                path,
                new LspRange(line, 0, line, name.Length),
                null,
                name),
            children ?? Array.Empty<LanguageSymbol>(),
            depth);

    private static async Task WaitForAsync(Func<bool> predicate, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(3));
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
                return;
            await Task.Delay(15);
        }
    }

    [Fact]
    public async Task DocumentSymbols_ActiveDocumentOnly_OrderedAndFlattened()
    {
        using var h = new Harness();
        var path = h.OpenActive("doc.cs", "class Z { void A(){} void B(){} }");
        h.SessionService.SetReady(h.Session);
        h.Session.DocumentHandler = (_, _) =>
            Task.FromResult<LanguageServerSymbolResult?>(new LanguageServerSymbolResult(new[]
            {
                Sym("Z", path, 0, children: new[]
                {
                    Sym("B", path, 0, depth: 1),
                    Sym("A", path, 0, depth: 1),
                }),
            }));

        h.Service.RequestDocumentSymbols(path);
        await WaitForAsync(() => h.Service.Current.State == LanguageSymbolState.Ready);

        Assert.Equal(LanguageSymbolScope.Document, h.Service.Current.Scope);
        Assert.Equal(path, h.Service.Current.FilePath);
        // Flattened: Z, then children ordered A then B.
        Assert.Equal(new[] { "Z", "A", "B" }, h.Service.Current.Symbols.Select(s => s.Name).ToArray());
        Assert.Equal(0, h.Service.Current.Symbols[0].Depth);
        Assert.Equal(1, h.Service.Current.Symbols[1].Depth);
    }

    [Fact]
    public async Task DocumentSymbols_InactiveDocument_RejectedAsUnavailable()
    {
        using var h = new Harness();
        var pathA = h.OpenActive("a.cs", "class A {}");
        var pathB = Path.Combine(TempRoot, "b.cs");
        await File.WriteAllTextAsync(pathB, "class B {}");
        // OpenDocument activates pathB; re-activate pathA so pathB is tracked but inactive.
        h.Workspace.OpenDocument(pathB, "class B {}");
        h.Bridge.SetOpen(pathB, 1, 1);
        var docA = h.Workspace.Documents.First(d =>
            string.Equals(d.FilePath, pathA, StringComparison.Ordinal));
        h.Workspace.SetActiveDocument(docA);
        h.SessionService.SetReady(h.Session);

        // Request for non-active document.
        h.Service.RequestDocumentSymbols(pathB);
        await WaitForAsync(() => h.Snapshots.Any(s => s.State == LanguageSymbolState.Unavailable));

        Assert.Equal(0, h.Session.DocumentCallCount);
        Assert.Equal(LanguageSymbolState.Idle, h.Service.Current.State);
    }

    [Fact]
    public async Task WorkspaceSymbols_QueryCancellationAndReplacement()
    {
        using var h = new Harness();
        h.OpenActive("ws.cs", "class C {}");
        h.SessionService.SetReady(h.Session);
        h.Session.WorkspaceGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        h.Service.RequestWorkspaceSymbols("first");
        await WaitForAsync(() => h.Service.Current.State == LanguageSymbolState.Loading);

        // Replace query before debounce/request completes.
        h.Session.WorkspaceGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Service.SetWorkspaceQuery("second");
        await Task.Delay(LanguageSymbolPolicy.WorkspaceQueryDebounce + TimeSpan.FromMilliseconds(100));

        // Only the latest query should remain live.
        Assert.Equal("second", h.Service.Current.Query);
        h.Session.WorkspaceHandler = (q, _) =>
            Task.FromResult<LanguageServerSymbolResult?>(new LanguageServerSymbolResult(new[]
            {
                Sym("Match", Path.Combine(TempRoot, "ws.cs"), 0),
            }));
        h.Session.WorkspaceGate.TrySetResult(true);
        await WaitForAsync(() => h.Service.Current.State == LanguageSymbolState.Ready);

        Assert.Equal("second", h.Session.LastWorkspaceQuery);
        Assert.Equal(LanguageSymbolState.Ready, h.Service.Current.State);
        Assert.Single(h.Service.Current.Symbols);
    }

    [Fact]
    public async Task WorkspaceSymbols_EmptyAndUnavailable()
    {
        using var h = new Harness();
        h.OpenActive("empty-ws.cs", "class C {}");
        h.SessionService.SetReady(h.Session);
        h.Session.WorkspaceHandler = (_, _) =>
            Task.FromResult<LanguageServerSymbolResult?>(
                new LanguageServerSymbolResult(Array.Empty<LanguageSymbol>()));

        h.Service.RequestWorkspaceSymbols("Nope");
        await WaitForAsync(
            () => h.Service.Current.State == LanguageSymbolState.Empty,
            TimeSpan.FromSeconds(3));

        Assert.Equal(LanguageSymbolPolicy.WorkspaceEmptyMessage, h.Service.Current.FeedbackMessage);

        h.Session.Capabilities = new LanguageServerCapabilities(
            true, new[] { '.' }, true, true, true, WorkspaceSymbolSupported: false);
        h.SessionService.SetReady(h.Session);
        h.Service.RequestWorkspaceSymbols("x");
        await WaitForAsync(() => h.Snapshots.Any(s =>
            s.State == LanguageSymbolState.Unavailable &&
            s.Scope == LanguageSymbolScope.Workspace));
    }

    [Fact]
    public async Task WorkspaceSymbols_FailureDoesNotLeaveStaleSurface()
    {
        using var h = new Harness();
        h.OpenActive("fail-ws.cs", "class C {}");
        h.SessionService.SetReady(h.Session);
        h.Session.WorkspaceHandler = (_, _) =>
            Task.FromResult<LanguageServerSymbolResult?>(null);

        h.Service.RequestWorkspaceSymbols("x");
        await WaitForAsync(() => h.Snapshots.Any(s => s.State == LanguageSymbolState.Failed));

        Assert.Equal(LanguageSymbolState.Idle, h.Service.Current.State);
    }

    [Fact]
    public async Task DocumentSymbols_StaleVersionDoesNotUpdateSurface()
    {
        using var h = new Harness();
        var path = h.OpenActive("stale.cs", "class C {}", version: 1);
        h.SessionService.SetReady(h.Session);
        h.Session.DocumentGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Session.DocumentHandler = (_, _) =>
            Task.FromResult<LanguageServerSymbolResult?>(new LanguageServerSymbolResult(new[]
            {
                Sym("C", path, 0),
            }));

        h.Service.RequestDocumentSymbols(path);
        await WaitForAsync(() => h.Service.Current.State == LanguageSymbolState.Loading);

        h.Bridge.SetVersion(path, 2);
        h.Session.DocumentGate.TrySetResult(true);
        await Task.Delay(150);

        // Stale response must not install Ready symbols; surface must collapse to Idle.
        Assert.Equal(LanguageSymbolState.Idle, h.Service.Current.State);
        Assert.False(h.Service.Current.IsSurfaceOpen);
        Assert.DoesNotContain(h.Snapshots, s => s.State == LanguageSymbolState.Ready && s.Symbols.Count > 0);
    }

    [Fact]
    public async Task DocumentSymbols_ActiveTabChange_DismissesSurface()
    {
        using var h = new Harness();
        var path = h.OpenActive("tab.cs", "class C {}");
        h.SessionService.SetReady(h.Session);
        h.Session.DocumentHandler = (_, _) =>
            Task.FromResult<LanguageServerSymbolResult?>(new LanguageServerSymbolResult(new[]
            {
                Sym("C", path, 0),
            }));

        h.Service.RequestDocumentSymbols(path);
        await WaitForAsync(() => h.Service.Current.State == LanguageSymbolState.Ready);

        // Production dismiss on tab switch comes from EditorLanguageInputViewModel.
        h.Input.ActiveDocumentId = "/other/doc.cs";

        Assert.Equal(LanguageSymbolState.Idle, h.Service.Current.State);
        Assert.False(h.Service.Current.IsSurfaceOpen);
        Assert.Null(h.Service.TryAcceptSelected());
    }

    [Fact]
    public async Task SymbolSelection_NavigatesThroughValidatedPath()
    {
        using var h = new Harness();
        var path = h.OpenActive("nav-sym.cs", "class Hello { }");
        h.SessionService.SetReady(h.Session);
        h.Session.DocumentHandler = (_, _) =>
            Task.FromResult<LanguageServerSymbolResult?>(new LanguageServerSymbolResult(new[]
            {
                Sym("Hello", path, 0),
            }));

        h.Service.RequestDocumentSymbols(path);
        await WaitForAsync(() => h.Service.Current.State == LanguageSymbolState.Ready);

        var location = h.Service.TryAcceptSelected();
        Assert.NotNull(location);
        var ok = await h.Input.NavigateToLocationAsync(location!);
        Assert.True(ok);
        Assert.Equal(path, h.Tabs.ActiveTab!.FilePath);
        Assert.NotNull(h.Tabs.ActiveTab.PendingNavigationOffset);
    }

    [Fact]
    public async Task DocumentSymbols_EmptyState_Feedback()
    {
        using var h = new Harness();
        var path = h.OpenActive("empty-doc.cs", "// empty");
        h.SessionService.SetReady(h.Session);
        h.Session.DocumentHandler = (_, _) =>
            Task.FromResult<LanguageServerSymbolResult?>(
                new LanguageServerSymbolResult(Array.Empty<LanguageSymbol>()));

        h.Service.RequestDocumentSymbols(path);
        await WaitForAsync(() => h.Service.Current.State == LanguageSymbolState.Empty);

        Assert.Equal(LanguageSymbolPolicy.DocumentEmptyMessage, h.Service.Current.FeedbackMessage);
        Assert.True(h.Service.Current.IsSurfaceOpen);
    }

    [Fact]
    public void Ordering_IsDeterministicByNameThenRange()
    {
        var path = Path.Combine(TempRoot, "order.cs");
        var symbols = new[]
        {
            Sym("Beta", path, 2),
            Sym("Alpha", path, 5),
            Sym("Alpha", path, 1),
        };

        var ordered = LanguageServerSymbolParser.OrderSiblings(symbols);
        Assert.Equal(new[] { "Alpha", "Alpha", "Beta" }, ordered.Select(s => s.Name).ToArray());
        Assert.Equal(1, ordered[0].Location!.Range.StartLine);
        Assert.Equal(5, ordered[1].Location!.Range.StartLine);
    }

    [Fact]
    public async Task MoveSelection_WrapsAroundBoundaries()
    {
        using var h = new Harness();
        var path = h.OpenActive("wrap.cs", "class A {} class B {} class C {}");
        h.SessionService.SetReady(h.Session);
        h.Session.DocumentHandler = (_, _) =>
            Task.FromResult<LanguageServerSymbolResult?>(new LanguageServerSymbolResult(new[]
            {
                Sym("A", path, 0),
                Sym("B", path, 1),
                Sym("C", path, 2),
            }));

        h.Service.RequestDocumentSymbols(path);
        await WaitForAsync(() => h.Service.Current.State == LanguageSymbolState.Ready);

        Assert.Equal(0, h.Service.Current.SelectedIndex);

        h.Service.MoveSelection(1);
        Assert.Equal(1, h.Service.Current.SelectedIndex);

        h.Service.MoveSelection(1);
        Assert.Equal(2, h.Service.Current.SelectedIndex);

        // Wrap forward.
        h.Service.MoveSelection(1);
        Assert.Equal(0, h.Service.Current.SelectedIndex);

        // Wrap backward.
        h.Service.MoveSelection(-1);
        Assert.Equal(2, h.Service.Current.SelectedIndex);

        h.Service.MoveSelection(-1);
        Assert.Equal(1, h.Service.Current.SelectedIndex);

        h.Service.MoveSelection(-1);
        Assert.Equal(0, h.Service.Current.SelectedIndex);
    }

    [Fact]
    public async Task DocumentSymbols_CapabilityUnsupported_UnavailableThenIdle()
    {
        using var h = new Harness();
        var path = h.OpenActive("nosup.cs", "class C {}");
        h.Session.Capabilities = new LanguageServerCapabilities(
            true, new[] { '.' }, true, true, DocumentSymbolSupported: false, WorkspaceSymbolSupported: true);
        h.SessionService.SetReady(h.Session);

        h.Service.RequestDocumentSymbols(path);
        await WaitForAsync(() => h.Snapshots.Any(s =>
            s.State == LanguageSymbolState.Unavailable && s.Scope == LanguageSymbolScope.Document));

        Assert.Equal(LanguageSymbolState.Idle, h.Service.Current.State);
        Assert.Equal(0, h.Session.DocumentCallCount);
    }

    [Fact]
    public async Task DocumentSymbols_RequestFailure_FailedThenIdle()
    {
        using var h = new Harness();
        var path = h.OpenActive("fail-doc.cs", "class C {}");
        h.SessionService.SetReady(h.Session);
        h.Session.DocumentHandler = (_, _) =>
            Task.FromResult<LanguageServerSymbolResult?>(null);

        h.Service.RequestDocumentSymbols(path);
        await WaitForAsync(() => h.Snapshots.Any(s => s.State == LanguageSymbolState.Failed));

        Assert.Equal(LanguageSymbolState.Idle, h.Service.Current.State);
    }

    [Fact]
    public async Task DocumentSymbols_StaleGenerationDoesNotInstallReady()
    {
        using var h = new Harness();
        var path = h.OpenActive("gen.cs", "class C {}", version: 1);
        h.SessionService.SetReady(h.Session, generation: 1);
        h.Session.DocumentGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Session.DocumentHandler = (_, _) =>
            Task.FromResult<LanguageServerSymbolResult?>(new LanguageServerSymbolResult(new[]
            {
                Sym("C", path, 0),
            }));

        h.Service.RequestDocumentSymbols(path);
        await WaitForAsync(() => h.Service.Current.State == LanguageSymbolState.Loading);

        // Advance generation before response arrives.
        h.SessionService.SetReady(h.Session, generation: 2);
        h.Session.DocumentGate.TrySetResult(true);
        await Task.Delay(150);

        Assert.NotEqual(LanguageSymbolState.Ready, h.Service.Current.State);
        Assert.DoesNotContain(h.Snapshots, s => s.State == LanguageSymbolState.Ready && s.Symbols.Count > 0);
    }

    [Fact]
    public async Task DocumentSymbols_DocumentClosed_DismissesSurface()
    {
        using var h = new Harness();
        var path = h.OpenActive("close-me.cs", "class C {}");
        h.SessionService.SetReady(h.Session);
        h.Session.DocumentHandler = (_, _) =>
            Task.FromResult<LanguageServerSymbolResult?>(new LanguageServerSymbolResult(new[]
            {
                Sym("C", path, 0),
            }));

        h.Service.RequestDocumentSymbols(path);
        await WaitForAsync(() => h.Service.Current.State == LanguageSymbolState.Ready);

        h.Workspace.CloseDocument(path);
        await Task.Delay(50);

        Assert.Equal(LanguageSymbolState.Idle, h.Service.Current.State);
    }

    [Fact]
    public async Task SessionLeavesReady_DismissesSymbolSurface()
    {
        using var h = new Harness();
        var path = h.OpenActive("session-die.cs", "class C {}");
        h.SessionService.SetReady(h.Session);
        h.Session.DocumentHandler = (_, _) =>
            Task.FromResult<LanguageServerSymbolResult?>(new LanguageServerSymbolResult(new[]
            {
                Sym("C", path, 0),
            }));

        h.Service.RequestDocumentSymbols(path);
        await WaitForAsync(() => h.Service.Current.State == LanguageSymbolState.Ready);

        h.SessionService.SetState(LanguageSessionState.Cancelled);
        await Task.Delay(50);

        Assert.Equal(LanguageSymbolState.Idle, h.Service.Current.State);
    }

    [Fact]
    public async Task WorkspaceSymbols_InvalidLocationsFilteredOut()
    {
        using var h = new Harness();
        var path = h.OpenActive("loc-ws.cs", "class C {}");
        h.SessionService.SetReady(h.Session);
        var nullLoc = new LanguageSymbol("NullLoc", 5, null, null, null,
            Array.Empty<LanguageSymbol>(), 0);
        var noFilePath = new LanguageSymbol("NoPath", 5, null, null,
            new LanguageLocation("file:///x", null, new LspRange(0, 0, 0, 3), null, "NoPath"),
            Array.Empty<LanguageSymbol>(), 0);
        var valid = Sym("Valid", path, 1);

        h.Session.WorkspaceHandler = (_, _) =>
            Task.FromResult<LanguageServerSymbolResult?>(new LanguageServerSymbolResult(new LanguageSymbol[]
            {
                nullLoc,
                noFilePath,
                valid,
            }));

        h.Service.RequestWorkspaceSymbols("test");
        await WaitForAsync(() => h.Service.Current.State == LanguageSymbolState.Ready);

        Assert.Single(h.Service.Current.Symbols);
        Assert.Equal("Valid", h.Service.Current.Symbols[0].Name);
    }

    [Fact]
    public async Task WorkspaceSymbols_AllInvalidLocations_Empty()
    {
        using var h = new Harness();
        var path = h.OpenActive("all-bad.cs", "class C {}");
        h.SessionService.SetReady(h.Session);
        var nullLoc = new LanguageSymbol("NullLoc", 5, null, null, null,
            Array.Empty<LanguageSymbol>(), 0);

        h.Session.WorkspaceHandler = (_, _) =>
            Task.FromResult<LanguageServerSymbolResult?>(new LanguageServerSymbolResult(new[]
            {
                nullLoc,
            }));

        h.Service.RequestWorkspaceSymbols("test");
        await WaitForAsync(() => h.Service.Current.State == LanguageSymbolState.Empty);

        Assert.Equal(LanguageSymbolPolicy.WorkspaceEmptyMessage, h.Service.Current.FeedbackMessage);
    }
}
