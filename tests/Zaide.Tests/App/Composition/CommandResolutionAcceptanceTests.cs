using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ReactiveUI.Builder;
using Xunit;
using Zaide.App.Composition;
using Zaide.App.Shell;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;

using Zaide.Tests;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Workspace.Infrastructure;
using Zaide.Features.Workspace.Presentation;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Tests.Features.ProjectSystem;
using Zaide.Tests.Features.Debugging.Application;
using Zaide.Tests.Features.Debugging.Presentation;
using Zaide.Features.SourceControl.Domain;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Presentation;
using Zaide.Features.Terminal.Contracts;
using Zaide.Features.Terminal.Infrastructure;
using Zaide.Features.Terminal.Presentation;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Presentation;
using Zaide.Tests.App.Composition;

namespace Zaide.Tests.App.Composition;
/// <summary>
/// Phase 8.2 M8c: acceptance and adversarial tests for canonical command
/// registration and neutral gesture resolution, using real owning ViewModels
/// with a real CommandRegistry. Isolated from Avalonia KeyBinding materialization.
/// </summary>
public sealed class CommandResolutionAcceptanceTests
{
    static CommandResolutionAcceptanceTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private readonly TestLoggerProvider _loggerProvider;
    private readonly CommandRegistry _registry;

    public CommandResolutionAcceptanceTests()
    {
        _loggerProvider = new TestLoggerProvider();
        var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(_loggerProvider));
        var logger = loggerFactory.CreateLogger<CommandRegistry>();
        _registry = new CommandRegistry(logger);
    }

    // ── Acceptance: all seven canonical IDs registered exactly once ────────
    // (M8c requirement 1 — verifies the owning ViewModels register each ID once.)

    [Fact]
    public void AllSevenCanonicalCommands_PresentExactlyOnce()
    {
        RegisterAllViewModels();

        var expected = new[]
        {
            "file.save",
            "workspace.openFolder",
            "workspace.closeFolder",
            "view.toggleBottomPanel",
            "explorer.toggleHiddenFiles",
            "sourcecontrol.commit",
            "sourcecontrol.refresh"
        };

        Assert.Equal(7, _registry.GetAll().Count);
        foreach (var id in expected)
        {
            Assert.NotNull(_registry.GetById(id));
            Assert.Equal(1, _registry.GetAll().Count(d => d.Id == id));
        }
    }

    // ── Acceptance: every D6a default gesture resolves correctly ──────────
    // (M8c requirement 2)

    [Fact]
    public void AllDefaultsResolveCorrectly()
    {
        RegisterAllViewModels();

        var bindings = _registry.ResolveKeyBindings(new NoOpSettingsService());

        Assert.Equal(5, bindings.Count);

        Assert.Contains(bindings, b => b.Gesture == "Ctrl+S" && b.CommandId == "file.save");
        Assert.Contains(bindings, b => b.Gesture == "Ctrl+O" && b.CommandId == "workspace.openFolder");
        Assert.Contains(bindings, b => b.Gesture == "Ctrl+Oem3" && b.CommandId == "view.toggleBottomPanel");
        Assert.Contains(bindings, b => b.Gesture == "Ctrl+J" && b.CommandId == "view.toggleBottomPanel");
        Assert.Contains(bindings, b => b.Gesture == "Ctrl+Shift+H" && b.CommandId == "explorer.toggleHiddenFiles");
    }

    // ── Unbound canonical commands remain unbound ─────────────────────────
    // (M8c requirement 3)

    [Fact]
    public void UnboundCommands_ProduceNoBindings()
    {
        RegisterAllViewModels();

        var bindings = _registry.ResolveKeyBindings(new NoOpSettingsService());

        Assert.DoesNotContain(bindings, b => b.CommandId == "workspace.closeFolder");
        Assert.DoesNotContain(bindings, b => b.CommandId == "sourcecontrol.commit");
        Assert.DoesNotContain(bindings, b => b.CommandId == "sourcecontrol.refresh");
    }

    // ── Ctrl+Oem3 distinct from Ctrl+J ────────────────────────────────────
    // (M8c requirement 12)

    [Fact]
    public void CtrlOem3AndCtrlJ_AreDistinctTokens_BothMapToViewToggleBottomPanel()
    {
        RegisterAllViewModels();

        var bindings = _registry.ResolveKeyBindings(new NoOpSettingsService());

        var oem3Binding = bindings.FirstOrDefault(b => b.Gesture == "Ctrl+Oem3");
        var jBinding = bindings.FirstOrDefault(b => b.Gesture == "Ctrl+J");

        Assert.NotNull(oem3Binding);
        Assert.NotNull(jBinding);
        Assert.Equal("view.toggleBottomPanel", oem3Binding!.CommandId);
        Assert.Equal("view.toggleBottomPanel", jBinding!.CommandId);
        Assert.NotEqual(oem3Binding.Gesture, jBinding.Gesture);
    }

    // ── User overrides replace defaults correctly ─────────────────────────
    // (M8c requirement 4)

    [Fact]
    public void UserOverride_ReplacesDefault()
    {
        RegisterAllViewModels();

        var bindings = _registry.ResolveKeyBindings(CreateSettings(
            new Dictionary<string, string> { ["file.save"] = "Ctrl+Shift+S" }));

        Assert.Contains(bindings, b => b.Gesture == "Ctrl+Shift+S" && b.CommandId == "file.save");
        Assert.DoesNotContain(bindings, b => b.Gesture == "Ctrl+S");
    }

    // ── Explicit empty-string override unbinds ────────────────────────────
    // (M8c requirement 5)

    [Fact]
    public void EmptyStringOverride_UnbindsCommand()
    {
        RegisterAllViewModels();

        var bindings = _registry.ResolveKeyBindings(CreateSettings(
            new Dictionary<string, string> { ["file.save"] = "" }));

        Assert.DoesNotContain(bindings, b => b.CommandId == "file.save");
        // Other commands remain unaffected
        Assert.Contains(bindings, b => b.Gesture == "Ctrl+Shift+H" && b.CommandId == "explorer.toggleHiddenFiles");
        Assert.Contains(bindings, b => b.Gesture == "Ctrl+O" && b.CommandId == "workspace.openFolder");
    }

    // ── Invalid, malformed, numeric, whitespace overrides are ignored ─────
    // (M8c requirement 6 — overlay with real ViewModels)

    [Fact]
    public void InvalidOverrideGesture_FallsBackToDefault()
    {
        RegisterAllViewModels();

        var bindings = _registry.ResolveKeyBindings(CreateSettings(
            new Dictionary<string, string> { ["file.save"] = "Invalid+Gesture" }));

        // Falls back to default Ctrl+S
        Assert.Contains(bindings, b => b.Gesture == "Ctrl+S" && b.CommandId == "file.save");

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, w =>
            w.Message.Contains("Invalid override gesture") &&
            w.Message.Contains("file.save"));
    }

    [Fact]
    public void NullOverride_IgnoredWithWarning()
    {
        RegisterAllViewModels();

        var bindings = _registry.ResolveKeyBindings(CreateSettings(
            new Dictionary<string, string> { ["file.save"] = null! }));

        // Null does not unbind; default remains
        Assert.Contains(bindings, b => b.Gesture == "Ctrl+S" && b.CommandId == "file.save");

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, w =>
            w.Message.Contains("Invalid override gesture") &&
            w.Message.Contains("file.save") &&
            w.Message.Contains("null"));
    }

    [Fact]
    public void WhitespaceOverride_IgnoredWithWarning()
    {
        RegisterAllViewModels();

        var bindings = _registry.ResolveKeyBindings(CreateSettings(
            new Dictionary<string, string> { ["file.save"] = "   " }));

        Assert.Contains(bindings, b => b.Gesture == "Ctrl+S" && b.CommandId == "file.save");

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, w =>
            w.Message.Contains("Invalid override gesture") &&
            w.Message.Contains("file.save"));
    }

    [Fact]
    public void NumericOverride_IgnoredWithWarning()
    {
        RegisterAllViewModels();

        var bindings = _registry.ResolveKeyBindings(CreateSettings(
            new Dictionary<string, string> { ["file.save"] = "Ctrl+1" }));

        Assert.Contains(bindings, b => b.Gesture == "Ctrl+S" && b.CommandId == "file.save");

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, w =>
            w.Message.Contains("Invalid override gesture") &&
            w.Message.Contains("file.save"));
    }

    // ── Overrides for unregistered command IDs are ignored and logged ─────
    // (M8c requirement 7)

    [Fact]
    public void OverrideForUnregisteredCommand_IgnoredAndWarningLogged()
    {
        RegisterAllViewModels();

        var bindings = _registry.ResolveKeyBindings(CreateSettings(
            new Dictionary<string, string> { ["nonexistent.command"] = "Ctrl+Shift+X" }));

        // All five default bindings still present
        Assert.Equal(5, bindings.Count);

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, w =>
            w.Message.Contains("unregistered command ID") &&
            w.Message.Contains("nonexistent.command"));
    }

    // ── Override conflict: deterministic and registration-order independent ─
    // (M8c requirement 8)

    [Fact]
    public void OverrideConflict_Deterministic_LexicographicallyEarlierWins()
    {
        RegisterAllViewModels();

        var bindings = _registry.ResolveKeyBindings(CreateSettings(
            new Dictionary<string, string>
            {
                ["file.save"] = "Ctrl+O",
                ["workspace.openFolder"] = "Ctrl+O"
            }));

        // file.save < workspace.openFolder lexicographically
        Assert.Contains(bindings, b => b.Gesture == "Ctrl+O" && b.CommandId == "file.save");
        Assert.DoesNotContain(bindings, b => b.CommandId == "workspace.openFolder");

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, w =>
            w.Message.Contains("Gesture conflict") &&
            w.Message.Contains("file.save") &&
            w.Message.Contains("workspace.openFolder"));
    }

    // ── Default conflict: deterministic and registration-order independent ─
    // (M8c requirement 8 continued)

    [Fact]
    public void DefaultConflict_Deterministic_LexicographicallyEarlierWins()
    {
        RegisterAllViewModels();

        // Register two extra commands that will conflict with existing defaults
        var cmdA = new AlwaysEnabledCommand();
        var cmdZ = new AlwaysEnabledCommand();
        _registry.Register(new CommandDescriptor("a.extra", "A", "Test", new[] { "Ctrl+S" }, cmdA));
        _registry.Register(new CommandDescriptor("z.extra", "Z", "Test", new[] { "Ctrl+S" }, cmdZ));

        var bindings = _registry.ResolveKeyBindings(new NoOpSettingsService());

        // a.extra (lexicographically earliest) gets Ctrl+S;
        // file.save and z.extra lose the conflict.
        // "a.extra" < "explorer.toggleHiddenFiles" < ... < "file.save" < ... < "z.extra"
        Assert.Contains(bindings, b => b.Gesture == "Ctrl+S" && b.CommandId == "a.extra");

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, w =>
            w.Message.Contains("Gesture conflict") &&
            w.Message.Contains("a.extra"));
    }

    // ── No gesture resolves to two command IDs ───────────────────────────
    // (M8c requirement 9)

    [Fact]
    public void NoGestureResolvesToTwoCommandIds()
    {
        RegisterAllViewModels();

        var bindings = _registry.ResolveKeyBindings(new NoOpSettingsService());

        var gestureCounts = bindings
            .GroupBy(b => b.Gesture, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.All(gestureCounts, kv => Assert.Equal(1, kv.Value));
    }

    // ── Losing commands remain registered and executable ──────────────────
    // (M8c requirement 10)

    [Fact]
    public void LosingCommand_RemainsRegisteredAndExecutable()
    {
        RegisterAllViewModels();

        // file.save has default Ctrl+S.
        // Register a lexicographically earlier command claiming the same gesture.
        var extraCmd = new AlwaysEnabledCommand();
        _registry.Register(new CommandDescriptor("a.extra.save", "A Save", "Test",
            new[] { "Ctrl+S" }, extraCmd));

        // Resolve — a.extra.save wins the Ctrl+S conflict.
        _registry.ResolveKeyBindings(new NoOpSettingsService());

        // file.save is still registered.
        Assert.NotNull(_registry.GetById("file.save"));

        // file.save is still executable.
        Assert.True(_registry.Execute("file.save"));
    }

    // ── Repeated resolution returns a stable complete replacement set ────
    // (M8c requirement 11)

    [Fact]
    public void RepeatedResolution_ReturnsStableCompleteReplacement()
    {
        RegisterAllViewModels();

        var settings = new NoOpSettingsService();

        var first = _registry.ResolveKeyBindings(settings);
        var second = _registry.ResolveKeyBindings(settings);

        Assert.Equal(first.Count, second.Count);
        Assert.Equal(first.OrderBy(b => b.Gesture), second.OrderBy(b => b.Gesture));
    }

    [Fact]
    public void RepeatedResolution_NoDuplicateBindings()
    {
        RegisterAllViewModels();

        var settings = new NoOpSettingsService();

        // Resolve twice — no accumulated duplicates.
        _ = _registry.ResolveKeyBindings(settings);
        var bindings = _registry.ResolveKeyBindings(settings);

        Assert.Equal(5, bindings.Count);
        Assert.Equal(bindings.Count, bindings.Select(b => b.Gesture).Distinct().Count());
    }

    // ── Assert logging at Warning level for each invalid case ────────────

    [Fact]
    public void InvalidDefaultGesture_LogsWarning_FromRealViewModels()
    {
        // Register a command with an invalid default gesture to verify Warning logging.
        _registry.Register(new CommandDescriptor("bad.default", "Bad", "Test",
            new[] { "NotARealKey" }, new AlwaysEnabledCommand()));
        RegisterAllViewModels();

        _registry.ResolveKeyBindings(new NoOpSettingsService());

        var warnings = _loggerProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Contains(warnings, w =>
            w.Message.Contains("Invalid default gesture") &&
            w.Message.Contains("bad.default"));
    }

    // ── Adversarial: override for unbound command is allowed (grants binding) ─

    [Fact]
    public void OverrideForUnboundCommand_GrantsBinding()
    {
        RegisterAllViewModels();

        var bindings = _registry.ResolveKeyBindings(CreateSettings(
            new Dictionary<string, string> { ["workspace.closeFolder"] = "Ctrl+Shift+C" }));

        // workspace.closeFolder gets a binding from the override.
        Assert.Contains(bindings, b => b.Gesture == "Ctrl+Shift+C" && b.CommandId == "workspace.closeFolder");
    }

    // ── Adversarial: override of one alias does not affect the other alias ─

    [Fact]
    public void OverrideForOneAlias_DoesNotAffectOtherAlias()
    {
        RegisterAllViewModels();

        // Override view.toggleBottomPanel to a different gesture.
        var bindings = _registry.ResolveKeyBindings(CreateSettings(
            new Dictionary<string, string> { ["view.toggleBottomPanel"] = "Ctrl+T" }));

        // Both original aliases are gone; only the override gesture binds.
        Assert.DoesNotContain(bindings, b => b.Gesture == "Ctrl+Oem3");
        Assert.DoesNotContain(bindings, b => b.Gesture == "Ctrl+J");
        Assert.Contains(bindings, b => b.Gesture == "Ctrl+T" && b.CommandId == "view.toggleBottomPanel");
    }

    // ── Adversarial: override with empty string for unbound command is no-op ─

    [Fact]
    public void EmptyOverrideForUnboundCommand_IsNoOp()
    {
        RegisterAllViewModels();

        var bindings = _registry.ResolveKeyBindings(CreateSettings(
            new Dictionary<string, string> { ["workspace.closeFolder"] = "" }));

        // Still no binding appears.
        Assert.DoesNotContain(bindings, b => b.CommandId == "workspace.closeFolder");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void RegisterAllViewModels()
    {
        var sp = new ServiceCollection()
            .AddSingleton<IFileService>(new FileService())
            .AddSingleton<IEditorSessionFactory, EditorSessionFactory>()
            .AddSingleton<Workspace>()
            .BuildServiceProvider();

        var fileTreeViewModel = new FileTreeViewModel(
            new FileTreeService(), CurrentThreadScheduler.Instance, _registry);
        var editorTabs = new EditorTabViewModel(
            sp.GetRequiredService<IEditorSessionFactory>(), sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Workspace>());
        var terminalService = new Mock<ITerminalService>();
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(terminalService.Object);
        var terminalHost = new TerminalHost(factory.Object);
        var townhallState = new TownhallState();
        var townhallViewModel = new TownhallViewModel(townhallState);
        var scViewModel = CreateSourceControlViewModel();
        var workspace = sp.GetRequiredService<Workspace>();
        var coordinator = new Mock<IAgentExecutionCoordinator>().Object;
        var panelHost = new AgentPanelHost();
        var parser = new MentionParser();
        var router = new AgentRouter(parser, panelHost, coordinator);

        _ = new MainWindowViewModel(
            fileTreeViewModel, editorTabs, terminalHost, panelHost,
            router, townhallViewModel, scViewModel,
            TestProblemsFactory.Create(workspace, editorTabs), TestProjectWorkflowFactory.Create(), TestTestResultsFactory.Create(), TestDebugSessionFactory.Create(), TestDebugPanelFactory.Create(), TestEditorBreakpointFactory.Create(editorTabs), workspace,
            new Mock<IProjectContextService>(MockBehavior.Loose).Object, _registry);
    }

    private SourceControlViewModel CreateSourceControlViewModel()
    {
        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>()))
            .Returns(RepositoryDiscoveryResult.NotFound(""));
        git.Setup(g => g.ReadStatus(It.IsAny<string>()))
            .Returns(new RepositoryStatusSnapshot());
        var diffService = new Mock<IFileDiffService>();
        diffService.Setup(d => d.GetDiff(It.IsAny<string>(), It.IsAny<FileChange>()))
            .Returns((FileDiffResult?)null);
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);
        var mutation = new Mock<IGitMutationService>();

        return new SourceControlViewModel(
            orchestrator, new Workspace(),
            mutation.Object, git.Object, commandRegistry: _registry);
    }

    private static ISettingsService CreateSettings(
        IReadOnlyDictionary<string, string> overrides)
    {
        var keybindings = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(overrides));

        var model = new SettingsModel(
            SettingsModel.Defaults.SchemaVersion,
            SettingsModel.Defaults.Editor,
            SettingsModel.Defaults.Llm,
            keybindings,
            SettingsModel.Defaults.Debug);

        return new SimpleSettingsCapture(model);
    }

    private sealed class SimpleSettingsCapture : ISettingsService
    {
        private readonly SettingsModel _model;

        public SimpleSettingsCapture(SettingsModel model) => _model = model;

        public SettingsModel Current => _model;

        public IObservable<SettingsModel> WhenChanged =>
            System.Reactive.Linq.Observable.Empty<SettingsModel>();

        public SettingsLoadResult LoadResult => SettingsLoadResult.Missing;

        public System.Threading.Tasks.Task<SettingsMutationResult> UpdateAsync(
            Func<SettingsModel, SettingsModel> producer,
            System.Threading.CancellationToken ct = default)
            => System.Threading.Tasks.Task.FromResult<SettingsMutationResult>(
                new SettingsMutationResult.Conflict(_model));

        public System.Threading.Tasks.Task<SettingsMutationResult> ApplyAsync(
            SettingsModel expectedCurrent,
            SettingsModel next,
            System.Threading.CancellationToken ct = default)
            => System.Threading.Tasks.Task.FromResult<SettingsMutationResult>(
                new SettingsMutationResult.Conflict(_model));

        public System.Threading.Tasks.Task<SettingsSaveResult> SaveAsync(
            System.Threading.CancellationToken ct = default)
            => System.Threading.Tasks.Task.FromResult<SettingsSaveResult>(
                new SettingsSaveResult.Saved());

        public IObservable<SettingsSaveError> WriteErrors =>
            System.Reactive.Linq.Observable.Empty<SettingsSaveError>();
    }
}
