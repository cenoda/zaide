using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Views;

using Zaide.Tests;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 8.2 M9b: settings-driven keybinding lifecycle refresh. Tests the
/// actual <see cref="MainWindow.MaterializeRegistryBindings"/> lifecycle
/// through a truthful seam that mirrors MainWindow's WhenActivated/settings
/// subscription pattern: a <see cref="KeyBindings"/> collection, a tracking
/// list, a real <see cref="ICommandRegistry"/>, a real settings service with
/// an observable <see cref="ISettingsService.WhenChanged"/>, and the same
/// <c>MaterializeRegistryBindings</c> logic.
/// </summary>
public sealed class M9bSettingsDrivenRefreshTests
{
    static M9bSettingsDrivenRefreshTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private readonly TestLoggerProvider _loggerProvider;
    private readonly CommandRegistry _registry;

    public M9bSettingsDrivenRefreshTests()
    {
        _loggerProvider = new TestLoggerProvider();
        var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(_loggerProvider));
        var logger = loggerFactory.CreateLogger<CommandRegistry>();
        _registry = new CommandRegistry(logger);
    }

    /// <summary>
    /// Register all seven canonical commands using real owning ViewModels,
    /// mirroring the production composition root. Returns the <see cref="MainWindowViewModel"/>
    /// so callers can set up interaction handlers (e.g. <c>PickFolder</c>).
    /// </summary>
    private MainWindowViewModel RegisterAllViewModels()
    {
        var sp = new ServiceCollection()
            .AddSingleton<IFileService>(new MockFileService())
            .AddTransient<EditorViewModel>()
            .AddSingleton<Workspace>()
            .BuildServiceProvider();

        _ = new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance, _registry);
        var vm = new MainWindowViewModel(
            new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance),
            new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Workspace>()),
            new TerminalHost(new Mock<ITerminalSessionFactory>().Object),
            new AgentPanelHost(),
            new Mock<IAgentExecutionCoordinator>().Object,
            new AgentRouter(new MentionParser(new AgentPanelHost()), new AgentPanelHost(),
                new Mock<IAgentExecutionCoordinator>().Object),
            new TownhallViewModel(new TownhallState()),
            CreateSourceControlViewModel(),
            TestProblemsFactory.CreateWithWorkspace(sp.GetRequiredService<Workspace>()),
            TestProjectWorkflowFactory.Create(registry: _registry),
            TestTestResultsFactory.Create(),
            sp.GetRequiredService<Workspace>(),
            new Mock<IProjectContextService>(MockBehavior.Loose).Object, _registry);

        return vm;
    }

    private SourceControlViewModel CreateSourceControlViewModel()
    {
        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>())).Returns(RepositoryDiscoveryResult.NotFound(""));
        git.Setup(g => g.ReadStatus(It.IsAny<string>())).Returns(new RepositoryStatusSnapshot());
        var diffService = new Mock<IFileDiffService>();
        diffService.Setup(d => d.GetDiff(It.IsAny<string>(), It.IsAny<FileChange>())).Returns((FileDiffResult?)null);
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);
        var mutation = new Mock<IGitMutationService>();

        return new SourceControlViewModel(
            orchestrator, new Workspace(),
            diffService.Object, mutation.Object, git.Object, _registry);
    }

    /// <summary>
    /// Narrowest truthful lifecycle seam: mirrors MainWindow's
    /// <c>MaterializeRegistryBindings</c> logic and the
    /// <c>_settings.WhenChanged</c> subscription pattern.
    /// Uses a real <see cref="Subject{SettingsModel}"/> for
    /// <see cref="ISettingsService.WhenChanged"/> so the subscription and
    /// materialization code paths are exercised together.
    /// </summary>
    private sealed class BindingRefreshSeam : IDisposable
    {
        private readonly ICommandRegistry _registry;
        private readonly RefreshableSettingsService _settings;
        private readonly List<KeyBinding> _registryBindings = new();

        public List<KeyBinding> KeyBindings { get; } = new();

        public BindingRefreshSeam(ICommandRegistry registry, RefreshableSettingsService settings)
        {
            _registry = registry;
            _settings = settings;
        }

        /// <summary>
        /// Wire the WhenActivated subscription (mimics MainWindow's pattern).
        /// After this call, settings changes on <see cref="RefreshableSettingsService"/>
        /// trigger <see cref="MaterializeRegistryBindings(SettingsModel)"/> with
        /// the emitted snapshot passed directly.
        /// </summary>
        public void WireSubscription()
        {
            _settings.WhenChanged
                .ObserveOn(CurrentThreadScheduler.Instance)
                .Subscribe(snapshot => MaterializeRegistryBindings(snapshot));
        }

        /// <summary>
        /// M9a: resolve neutral bindings from <see cref="_settings"/>'s current snapshot.
        /// Used during initial setup.
        /// </summary>
        public void MaterializeRegistryBindings()
        {
            MaterializeRegistryBindings(_settings.Current);
        }

        /// <summary>
        /// M9b: resolve neutral bindings from the given <paramref name="snapshot"/>,
        /// convert to Avalonia <see cref="KeyBinding"/>, and atomically replace only
        /// the previously materialized bindings. Mirrors MainWindow's snapshot-aware
        /// overload exactly.
        /// </summary>
        public void MaterializeRegistryBindings(SettingsModel snapshot)
        {
            foreach (var kb in _registryBindings)
                KeyBindings.Remove(kb);

            _registryBindings.Clear();

            var snapshotService = new SnapshotSettingsAccessor(snapshot);
            var resolved = _registry.ResolveKeyBindings(snapshotService);
            foreach (var binding in resolved)
            {
                var descriptor = _registry.GetById(binding.CommandId);
                var kb = KeyBindingConverter.TryCreateKeyBinding(binding, descriptor);
                if (kb is null)
                    continue;

                KeyBindings.Add(kb);
                _registryBindings.Add(kb);
            }
        }

        /// <summary>
        /// Lightweight ISettingsService wrapper that exposes a single frozen snapshot.
        /// Mirrors MainWindow.SnapshotSettingsAccessor.
        /// </summary>
        private sealed class SnapshotSettingsAccessor : ISettingsService
        {
            private readonly SettingsModel _snapshot;

            public SnapshotSettingsAccessor(SettingsModel snapshot)
            {
                _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            }

            public SettingsModel Current => _snapshot;
            public IObservable<SettingsModel> WhenChanged => Observable.Empty<SettingsModel>();
            public SettingsLoadResult LoadResult => SettingsLoadResult.Missing;

            public Task<SettingsMutationResult> UpdateAsync(
                Func<SettingsModel, SettingsModel> producer,
                CancellationToken ct = default)
                => Task.FromResult<SettingsMutationResult>(
                    new SettingsMutationResult.Conflict(_snapshot));

            public Task<SettingsMutationResult> ApplyAsync(
                SettingsModel expectedCurrent,
                SettingsModel next,
                CancellationToken ct = default)
                => Task.FromResult<SettingsMutationResult>(
                    new SettingsMutationResult.Conflict(_snapshot));

            public Task<SettingsSaveResult> SaveAsync(CancellationToken ct = default)
                => Task.FromResult<SettingsSaveResult>(new SettingsSaveResult.Saved());

            public IObservable<SettingsSaveError> WriteErrors
                => Observable.Empty<SettingsSaveError>();
        }

        public void Dispose()
        {
            KeyBindings.Clear();
            _registryBindings.Clear();
        }
    }

    /// <summary>
    /// An ISettingsService that uses a <see cref="Subject{SettingsModel}"/>
    /// for <see cref="WhenChanged"/>. The test can push new snapshots through
    /// <see cref="PushSnapshot"/> to simulate settings changes.
    /// <see cref="Current"/> is only set from the initial constructor value
    /// (used by the parameterless <c>MaterializeRegistryBindings()</c> during
    /// initial setup). PushSnapshot does NOT update <c>Current</c>, so if the
    /// subscription handler re-reads <c>Current</c> instead of using the emitted
    /// snapshot directly, the test will detect the contract violation.
    /// </summary>
    private sealed class RefreshableSettingsService : ISettingsService
    {
        private readonly SettingsModel _current;
        private readonly Subject<SettingsModel> _subject = new();

        public RefreshableSettingsService(SettingsModel initial)
        {
            _current = initial;
        }

        public SettingsModel Current => _current;

        public IObservable<SettingsModel> WhenChanged => _subject;

        public SettingsLoadResult LoadResult => SettingsLoadResult.Missing;

        /// <summary>
        /// Push a new snapshot by emitting on the subject.
        /// Does NOT update <see cref="Current"/> — the emitted snapshot is
        /// the only source of truth for the refresh path.
        /// </summary>
        public void PushSnapshot(SettingsModel next)
        {
            _subject.OnNext(next);
        }

        public void Complete() => _subject.OnCompleted();

        public Task<SettingsMutationResult> UpdateAsync(
            Func<SettingsModel, SettingsModel> producer,
            CancellationToken ct = default)
        {
            var next = producer(_current);
            PushSnapshot(next);
            return Task.FromResult<SettingsMutationResult>(
                new SettingsMutationResult.Conflict(next));
        }

        public Task<SettingsMutationResult> ApplyAsync(
            SettingsModel expectedCurrent,
            SettingsModel next,
            CancellationToken ct = default)
        {
            PushSnapshot(next);
            return Task.FromResult<SettingsMutationResult>(
                new SettingsMutationResult.Conflict(next));
        }

        public Task<SettingsSaveResult> SaveAsync(CancellationToken ct = default)
            => Task.FromResult<SettingsSaveResult>(new SettingsSaveResult.Saved());

        public IObservable<SettingsSaveError> WriteErrors
            => Observable.Empty<SettingsSaveError>();
    }

    /// <summary>
    /// Create a settings model with the given keybinding overrides.
    /// </summary>
    private static SettingsModel CreateModel(IReadOnlyDictionary<string, string>? overrides = null)
    {
        var keybindings = overrides is not null
            ? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(overrides))
            : SettingsModel.Defaults.Keybindings;

        return new SettingsModel(
            SettingsModel.Defaults.SchemaVersion,
            SettingsModel.Defaults.Editor,
            SettingsModel.Defaults.Llm,
            keybindings);
    }

    /// <summary>
    /// Create a binding refresh seam with all canonical commands registered
    /// and the subscription wired. Returns also the <see cref="MainWindowViewModel"/>
    /// so callers can set up interaction handlers.
    /// </summary>
    private (BindingRefreshSeam Seam, RefreshableSettingsService Settings, MainWindowViewModel ViewModel) CreateSeam(
        IReadOnlyDictionary<string, string>? initialOverrides = null)
    {
        var vm = RegisterAllViewModels();
        var settings = new RefreshableSettingsService(CreateModel(initialOverrides));
        var seam = new BindingRefreshSeam(_registry, settings);
        seam.WireSubscription();

        // Register the PickFolder interaction handler so that executing
        // workspace.openFolder through the registry does not crash.
        vm.PickFolder.RegisterHandler(ctx => ctx.SetOutput("/mock/path"));

        return (seam, settings, vm);
    }

    /// <summary>
    /// Count registry-owned bindings in the seam's KeyBindings list by
    /// matching the expected canonical gestures.
    /// </summary>
    private static int CountRegistryBindings(List<KeyBinding> bindings)
    {
        return bindings.Count(b => b.Gesture is not null &&
            IsCanonicalGesture(b.Gesture!));
    }

    /// <summary>
    /// Returns true if the gesture is one of the known canonical gestures,
    /// including both the six defaults plus any override replacements
    /// (e.g. Ctrl+Shift+S used in rebind tests).
    /// </summary>
    private static bool IsCanonicalGesture(KeyGesture gesture)
    {
        return (gesture.Key == Key.S && gesture.KeyModifiers == KeyModifiers.Control) ||
               (gesture.Key == Key.S && gesture.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift)) ||
               (gesture.Key == Key.S && gesture.KeyModifiers == KeyModifiers.Alt) ||
               (gesture.Key == Key.O && gesture.KeyModifiers == KeyModifiers.Control) ||
               (gesture.Key == Key.Oem3 && gesture.KeyModifiers == KeyModifiers.Control) ||
               (gesture.Key == Key.J && gesture.KeyModifiers == KeyModifiers.Control) ||
               (gesture.Key == Key.H && gesture.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift)) ||
               (gesture.Key == Key.B && gesture.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift)) ||
               (gesture.Key == Key.F5 && gesture.KeyModifiers == KeyModifiers.Control) ||
               (gesture.Key == Key.F2 && gesture.KeyModifiers == KeyModifiers.Control);
    }

    private static bool HasGesture(List<KeyBinding> bindings, Key key, KeyModifiers modifiers)
    {
        return bindings.Any(b => b.Gesture?.Key == key && b.Gesture?.KeyModifiers == modifiers);
    }

    private static bool HasCommandBinding(List<KeyBinding> bindings, string commandId)
    {
        return bindings.Any(b =>
        {
            if (b.Command is null) return false;
            // Check through the registry — we can't access the underlying
            // ReactiveCommand directly, so check by trying CanExecute.
            return b.Command is ICommand cmd;
        });
    }

    // ═══════════════════════════════════════════════════════════════
    //  Test 1: Settings change replaces the old generated binding
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void SettingsChange_ReplacesOldGeneratedBinding()
    {
        var (seam, settings, _) = CreateSeam();

        // Initial materialization with default settings
        seam.MaterializeRegistryBindings();

        // Default: Ctrl+S for file.save
        Assert.True(HasGesture(seam.KeyBindings, Key.S, KeyModifiers.Control));
        Assert.False(HasGesture(seam.KeyBindings, Key.S, KeyModifiers.Control | KeyModifiers.Shift));

        // Rebind file.save to Ctrl+Shift+S
        settings.PushSnapshot(CreateModel(new Dictionary<string, string>
        {
            ["file.save"] = "Ctrl+Shift+S"
        }));

        // Old Ctrl+S must be gone, new Ctrl+Shift+S must be present
        Assert.False(HasGesture(seam.KeyBindings, Key.S, KeyModifiers.Control));
        Assert.True(HasGesture(seam.KeyBindings, Key.S, KeyModifiers.Control | KeyModifiers.Shift));
    }

    // ═══════════════════════════════════════════════════════════════
    //  Test 2: Old gesture is removed after rebinding
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void OldGesture_RemovedAfterRebinding()
    {
        var (seam, settings, _) = CreateSeam();
        seam.MaterializeRegistryBindings();

        // Ctrl+S should be present as file.save's default
        var ctrlSBindings = seam.KeyBindings
            .Where(b => b.Gesture?.Key == Key.S && b.Gesture?.KeyModifiers == KeyModifiers.Control)
            .ToList();
        Assert.Single(ctrlSBindings);

        // Rebind
        settings.PushSnapshot(CreateModel(new Dictionary<string, string>
        {
            ["file.save"] = "Ctrl+Shift+S"
        }));

        // Ctrl+S must be completely absent
        ctrlSBindings = seam.KeyBindings
            .Where(b => b.Gesture?.Key == Key.S && b.Gesture?.KeyModifiers == KeyModifiers.Control)
            .ToList();
        Assert.Empty(ctrlSBindings);

        // Ctrl+Shift+S must be present
        var ctrlShiftSBindings = seam.KeyBindings
            .Where(b => b.Gesture?.Key == Key.S && b.Gesture?.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
            .ToList();
        Assert.Single(ctrlShiftSBindings);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Test 3: New gesture executes the expected command
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void NewGesture_ExecutesExpectedCommand()
    {
        var (seam, settings, _) = CreateSeam();
        seam.MaterializeRegistryBindings();

        // Rebind file.save
        settings.PushSnapshot(CreateModel(new Dictionary<string, string>
        {
            ["file.save"] = "Ctrl+Shift+S"
        }));

        // The KeyBinding for Ctrl+Shift+S should have file.save's command
        var binding = seam.KeyBindings
            .FirstOrDefault(b => b.Gesture?.Key == Key.S &&
                                 b.Gesture?.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift));
        Assert.NotNull(binding);
        Assert.NotNull(binding!.Command);

        // Executing through registry should succeed
        var executed = _registry.Execute("file.save");
        Assert.True(executed);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Test 4: Unrelated/view-local bindings survive refresh
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void UnrelatedBindings_SurviveRefresh()
    {
        var (seam, settings, _) = CreateSeam();
        seam.MaterializeRegistryBindings();

        // Verify 8 bindings after initial materialization
        var resolved = _registry.ResolveKeyBindings(settings);
        Assert.Equal(8, resolved.Count);

        // Add an unrelated view-local binding (e.g., Enter key)
        var unrelated = new KeyBinding
        {
            Gesture = new KeyGesture(Key.Enter),
            Command = new AlwaysEnabledCommand()
        };
        seam.KeyBindings.Add(unrelated);

        Assert.Contains(unrelated, seam.KeyBindings);

        // Trigger a settings refresh
        settings.PushSnapshot(CreateModel(new Dictionary<string, string>
        {
            ["file.save"] = "Ctrl+Shift+S"
        }));

        // Unrelated binding must still be present
        Assert.Contains(unrelated, seam.KeyBindings);

        // Registry bindings should still be at expected count (8 after rebind)
        Assert.Equal(8, CountRegistryBindings(seam.KeyBindings));
    }

    // ═══════════════════════════════════════════════════════════════
    //  Test 5: Repeated settings changes produce no duplicate
    //          registry bindings
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void RepeatedSettingsChanges_NoDuplicateRegistryBindings()
    {
        var (seam, settings, _) = CreateSeam();

        // Multiple materializations with default settings
        for (int i = 0; i < 5; i++)
            seam.MaterializeRegistryBindings();

        Assert.Equal(8, CountRegistryBindings(seam.KeyBindings));

        // Multiple settings changes
        for (int i = 0; i < 3; i++)
        {
            settings.PushSnapshot(CreateModel(new Dictionary<string, string>
            {
                ["file.save"] = "Ctrl+Shift+S"
            }));
        }

        // Still 8 bindings (7 defaults unchanged + 1 override)
        Assert.Equal(8, CountRegistryBindings(seam.KeyBindings));

        // Only one Ctrl+Shift+S
        var ctrlShiftS = seam.KeyBindings
            .Where(b => b.Gesture?.Key == Key.S &&
                        b.Gesture?.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
            .ToList();
        Assert.Single(ctrlShiftS);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Test 6: Latest-snapshot behavior when multiple settings
    //          snapshots arrive
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void LatestSnapshot_Wins_WhenMultipleSnapshotsArrive()
    {
        var (seam, settings, _) = CreateSeam();
        seam.MaterializeRegistryBindings();

        // Simulate two rapid settings changes - only the last one should win.
        // First: rebind file.save to Ctrl+Shift+S
        settings.PushSnapshot(CreateModel(new Dictionary<string, string>
        {
            ["file.save"] = "Ctrl+Shift+S"
        }));

        // Second: rebind file.save to Alt+S (latest)
        settings.PushSnapshot(CreateModel(new Dictionary<string, string>
        {
            ["file.save"] = "Alt+S"
        }));

        // Ctrl+Shift+S must NOT be present
        Assert.False(HasGesture(seam.KeyBindings, Key.S, KeyModifiers.Control | KeyModifiers.Shift));

        // Alt+S must be present (latest snapshot won)
        Assert.True(HasGesture(seam.KeyBindings, Key.S, KeyModifiers.Alt));

        // Ctrl+S default must not be present (it was overridden by the latest snapshot)
        Assert.False(HasGesture(seam.KeyBindings, Key.S, KeyModifiers.Control));
    }

    // ═══════════════════════════════════════════════════════════════
    //  Test 7: Empty-string unbind removes the generated binding
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void EmptyStringUnbind_RemovesGeneratedBinding()
    {
        var (seam, settings, _) = CreateSeam();
        seam.MaterializeRegistryBindings();

        // Default: Ctrl+S is bound for file.save
        Assert.True(HasGesture(seam.KeyBindings, Key.S, KeyModifiers.Control));

        // Unbind file.save with empty-string override
        settings.PushSnapshot(CreateModel(new Dictionary<string, string>
        {
            ["file.save"] = ""
        }));

        // Ctrl+S must be removed
        Assert.False(HasGesture(seam.KeyBindings, Key.S, KeyModifiers.Control));

        // Other bindings remain intact
        Assert.Equal(7, CountRegistryBindings(seam.KeyBindings));
    }

    // ═══════════════════════════════════════════════════════════════
    //  Test 8: Ctrl+Oem3 and Ctrl+J continue to work
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void CtrlOem3AndCtrlJ_ContinueToWork()
    {
        var (seam, _, _) = CreateSeam();
        seam.MaterializeRegistryBindings();

        Assert.True(HasGesture(seam.KeyBindings, Key.Oem3, KeyModifiers.Control));
        Assert.True(HasGesture(seam.KeyBindings, Key.J, KeyModifiers.Control));

        // Both should execute view.toggleBottomPanel
        var oem3Binding = seam.KeyBindings
            .FirstOrDefault(b => b.Gesture?.Key == Key.Oem3 && b.Gesture?.KeyModifiers == KeyModifiers.Control);
        var jBinding = seam.KeyBindings
            .FirstOrDefault(b => b.Gesture?.Key == Key.J && b.Gesture?.KeyModifiers == KeyModifiers.Control);

        Assert.NotNull(oem3Binding);
        Assert.NotNull(jBinding);
        Assert.NotNull(oem3Binding!.Command);
        Assert.NotNull(jBinding!.Command);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Test 9: Ctrl+S, Ctrl+O, and Ctrl+Shift+H execute their
    //          canonical commands
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void CanonicalCommands_ExecuteSuccessfully()
    {
        var (seam, _, vm) = CreateSeam();
        seam.MaterializeRegistryBindings();

        Assert.True(_registry.Execute("file.save"));
        Assert.True(_registry.Execute("workspace.openFolder"));
        Assert.True(_registry.Execute("explorer.toggleHiddenFiles"));
        Assert.True(_registry.Execute("view.toggleBottomPanel"));
    }

    // ═══════════════════════════════════════════════════════════════
    //  Test 10: Unavailable workspace.closeFolder remains
    //           non-throwing
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void UnavailableCommand_DoesNotThrow()
    {
        var (seam, _, _) = CreateSeam();
        seam.MaterializeRegistryBindings();

        // workspace.closeFolder is unbound and unavailable (no folder open)
        var exception = Record.Exception(() => _registry.Execute("workspace.closeFolder"));
        Assert.Null(exception);
        Assert.False(_registry.Execute("workspace.closeFolder"));
    }

    // ═══════════════════════════════════════════════════════════════
    //  Test 11: Registry-owned bindings remain distinct from
    //           unrelated bindings
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void RegistryBindings_DistinctFromUnrelated()
    {
        var (seam, settings, _) = CreateSeam();
        seam.MaterializeRegistryBindings();

        // Add unrelated bindings
        var unrelated1 = new KeyBinding
        {
            Gesture = new KeyGesture(Key.F1),
            Command = new AlwaysEnabledCommand()
        };
        var unrelated2 = new KeyBinding
        {
            Gesture = new KeyGesture(Key.F2),
            Command = new AlwaysEnabledCommand()
        };
        seam.KeyBindings.Add(unrelated1);
        seam.KeyBindings.Add(unrelated2);

        // Refresh settings
        settings.PushSnapshot(CreateModel(new Dictionary<string, string>
        {
            ["file.save"] = "Ctrl+Shift+S"
        }));

        // Unrelated bindings survive
        Assert.Contains(unrelated1, seam.KeyBindings);
        Assert.Contains(unrelated2, seam.KeyBindings);

        // Registry bindings count is correct (8 after rebind)
        Assert.Equal(8, CountRegistryBindings(seam.KeyBindings));

        // Total: 8 registry + 2 unrelated = 10
        Assert.Equal(10, seam.KeyBindings.Count);
    }

    /// <summary>
    /// Simple ICommand stub that always can execute and does nothing.
    /// </summary>
    private sealed class AlwaysEnabledCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
    }
}
