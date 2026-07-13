using System;
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
using Zaide.Tests.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

/// <summary>
/// Phase 10 M7 command availability and capability gating tests.
/// </summary>
public sealed class LanguageCommandAvailabilityTests
{
    static LanguageCommandAvailabilityTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    [Fact]
    public void TriggerSuggest_DisabledWhenSessionUnavailable()
    {
        using var harness = CreateHarness(LanguageSessionState.Unavailable, TestLanguageServerSession.DefaultCapabilities);
        harness.Input.ActiveEditor = harness.Editor;
        harness.Input.ActiveDocumentId = harness.Path;

        Assert.False(harness.Input.TriggerSuggestCommand.CanExecute.FirstAsync().Wait());
    }

    [Fact]
    public void TriggerSuggest_DisabledWhenCompletionUnsupported()
    {
        var caps = TestLanguageServerSession.DefaultCapabilities with { CompletionSupported = false };
        using var harness = CreateHarness(LanguageSessionState.Ready, caps);
        harness.Input.ActiveEditor = harness.Editor;
        harness.Input.ActiveDocumentId = harness.Path;

        Assert.False(harness.Input.TriggerSuggestCommand.CanExecute.FirstAsync().Wait());
    }

    [Fact]
    public void TriggerSuggest_EnabledWhenReadyAndSupported()
    {
        using var harness = CreateHarness(LanguageSessionState.Ready, TestLanguageServerSession.DefaultCapabilities);
        harness.Input.ActiveEditor = harness.Editor;
        harness.Input.ActiveDocumentId = harness.Path;

        Assert.True(harness.Input.TriggerSuggestCommand.CanExecute.FirstAsync().Wait());
    }

    [Fact]
    public void WorkspaceSymbol_EnabledWithoutActiveDocument_WhenSessionReady()
    {
        using var harness = CreateHarness(LanguageSessionState.Ready, TestLanguageServerSession.DefaultCapabilities);

        Assert.True(harness.Input.WorkspaceSymbolCommand.CanExecute.FirstAsync().Wait());
    }

    [Fact]
    public void FormatDocument_DisabledWhenFormattingUnsupported()
    {
        var caps = TestLanguageServerSession.DefaultCapabilities with { DocumentFormattingSupported = false };
        using var harness = CreateHarness(LanguageSessionState.Ready, caps);
        harness.Input.ActiveEditor = harness.Editor;
        harness.Input.ActiveDocumentId = harness.Path;

        Assert.False(harness.Input.FormatDocumentCommand.CanExecute.FirstAsync().Wait());
    }

    private static Harness CreateHarness(LanguageSessionState state, LanguageServerCapabilities capabilities)
    {
        var workspace = new Workspace();
        var session = new FakeSessionService();
        var bridge = new FakeDocumentBridge();
        var services = new ServiceCollection()
            .AddSingleton(workspace)
            .AddSingleton<IFileService>(new FileService())
            .AddTransient(sp => new EditorViewModel(new Document(""), sp.GetRequiredService<IFileService>()))
            .BuildServiceProvider();
        var tabs = new EditorTabViewModel(services, services.GetRequiredService<IFileService>(), workspace);
        var path = "/tmp/sample.cs";

        var completion = new LanguageCompletionService(
            workspace, session, bridge, NullLogger<LanguageCompletionService>.Instance);
        var hover = new LanguageHoverService(
            workspace, session, bridge, NullLogger<LanguageHoverService>.Instance);
        var navigation = new LanguageNavigationService(
            workspace, session, bridge, NullLogger<LanguageNavigationService>.Instance);
        var symbols = new LanguageSymbolService(
            workspace, session, bridge, NullLogger<LanguageSymbolService>.Instance);
        var formatting = new LanguageFormattingService(
            workspace, session, bridge, NullLogger<LanguageFormattingService>.Instance);
        var input = new EditorLanguageInputViewModel(
            completion, hover, navigation, symbols, formatting, session, tabs, CommandRegistryFactory.Create());

        session.SetReady(1, new ConfigurableSession { Capabilities = capabilities });
        session.Emit(new LanguageSessionSnapshot(state, 1, "/p.csproj", "/p", 1, null));

        return new Harness(
            path,
            input,
            new RecordingEditor(),
            completion,
            hover,
            navigation,
            symbols,
            formatting,
            session,
            bridge,
            services);
    }

    private sealed class Harness : IDisposable
    {
        public string Path { get; }
        public EditorLanguageInputViewModel Input { get; }
        public RecordingEditor Editor { get; }
        private readonly LanguageCompletionService _completion;
        private readonly LanguageHoverService _hover;
        private readonly LanguageNavigationService _navigation;
        private readonly LanguageSymbolService _symbols;
        private readonly LanguageFormattingService _formatting;
        private readonly FakeSessionService _session;
        private readonly FakeDocumentBridge _bridge;
        private readonly ServiceProvider _services;

        public Harness(
            string path,
            EditorLanguageInputViewModel input,
            RecordingEditor editor,
            LanguageCompletionService completion,
            LanguageHoverService hover,
            LanguageNavigationService navigation,
            LanguageSymbolService symbols,
            LanguageFormattingService formatting,
            FakeSessionService session,
            FakeDocumentBridge bridge,
            ServiceProvider services)
        {
            Path = path;
            Input = input;
            Editor = editor;
            _completion = completion;
            _hover = hover;
            _navigation = navigation;
            _symbols = symbols;
            _formatting = formatting;
            _session = session;
            _bridge = bridge;
            _services = services;
        }

        public void Dispose()
        {
            _completion.Dispose();
            _hover.Dispose();
            _navigation.Dispose();
            _symbols.Dispose();
            _formatting.Dispose();
            _session.Dispose();
            _bridge.Dispose();
            _services.Dispose();
        }
    }

    private sealed class RecordingEditor : IEditorLanguageOperations
    {
        public string GetText() => string.Empty;
        public void SetText(string text) { }
        public void SetSelection(int offset, int length) { }
        public int GetSelectionOffset() => 0;
        public int GetSelectionLength() => 0;
        public int GetCaretOffset() => 0;
        public char? GetCharBeforeCaret() => null;
        public void ReplaceRange(int start, int length, string newText) { }
        public int ReplaceAllMatches(string query, string replacement, bool caseSensitive) => 0;
        public bool ApplyFormattedDocument(string formattedText) => true;
        public bool TryUndo() => false;
    }

    private sealed class FakeSessionService : ILanguageSessionService
    {
        private readonly Subject<LanguageSessionSnapshot> _subject = new();
        private LanguageSessionSnapshot _current = new(LanguageSessionState.Unavailable, 0, null, null, null, null);
        private ConfigurableSession? _session;

        public LanguageSessionSnapshot Current => _current;
        public IObservable<LanguageSessionSnapshot> WhenChanged => _subject;

        public void SetReady(long generation, ConfigurableSession session)
        {
            _session = session;
            _session.Generation = generation;
        }

        public void Emit(LanguageSessionSnapshot snapshot)
        {
            _current = snapshot;
            _subject.OnNext(snapshot);
        }

        public ILanguageServerSession? TryGetReadySession(long generation) =>
            _current.State == LanguageSessionState.Ready && _current.Generation == generation
                ? _session
                : null;

        public Task RestartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() => _subject.Dispose();
    }

    private sealed class FakeDocumentBridge : ILanguageDocumentBridge
    {
        public bool TryGetOpenDocument(string documentUri, out LanguageTrackedDocumentInfo info)
        {
            info = default;
            return false;
        }

        public void Dispose() { }
    }

    private sealed class ConfigurableSession : ILanguageServerSession
    {
        public long Generation { get; set; } = 1;
        public LanguageServerCapabilities Capabilities { get; set; } = TestLanguageServerSession.DefaultCapabilities;
        public int? ProcessId => 1;
        public bool HasExited => false;

#pragma warning disable CS0067
        public event Action<long>? ProcessExited;
        public event Action<LanguageServerPublishDiagnostics>? DiagnosticsPublished;
#pragma warning restore CS0067

        public Task ShutdownAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ForceKillAsync() => Task.CompletedTask;
        public Task NotifyDidOpenAsync(string documentUri, int version, string text, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyDidChangeAsync(string documentUri, int version, string text, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyDidCloseAsync(string documentUri, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task<LanguageServerCompletionResult?> RequestCompletionAsync(string documentUri, int line, int character, CancellationToken cancellationToken = default) => Task.FromResult<LanguageServerCompletionResult?>(null);
        public Task<LanguageServerHoverResult?> RequestHoverAsync(string documentUri, int line, int character, CancellationToken cancellationToken = default) => Task.FromResult<LanguageServerHoverResult?>(null);
        public Task<LanguageServerDefinitionResult?> RequestDefinitionAsync(string documentUri, int line, int character, CancellationToken cancellationToken = default) => Task.FromResult<LanguageServerDefinitionResult?>(null);
        public Task<LanguageServerSymbolResult?> RequestDocumentSymbolsAsync(string documentUri, CancellationToken cancellationToken = default) => Task.FromResult<LanguageServerSymbolResult?>(null);
        public Task<LanguageServerSymbolResult?> RequestWorkspaceSymbolsAsync(string query, CancellationToken cancellationToken = default) => Task.FromResult<LanguageServerSymbolResult?>(null);
        public Task<LanguageServerFormattingResult?> RequestFormattingAsync(string documentUri, CancellationToken cancellationToken = default) => Task.FromResult<LanguageServerFormattingResult?>(null);
    }
}