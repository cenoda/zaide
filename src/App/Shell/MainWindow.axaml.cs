using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Controls.Primitives;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaide.UI.DesignSystem;
using Zaide.App.Composition;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Presentation;
using Zaide.Features.Workspace.Presentation;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.ProjectSystem.Presentation;
using Zaide.Features.Debugging.Presentation;
using Zaide.Features.SourceControl.Presentation;
using Zaide.Features.Terminal.Contracts;
using Zaide.Features.Terminal.Infrastructure;
using Zaide.Features.Terminal.Presentation;
using Zaide.Features.Townhall.Presentation;

namespace Zaide.App.Shell;
/// <summary>
/// Main application window. Layout built in C# per DESIGN.md §1.
/// Refactor 3 M6: nav bar | left-panel mode slot (Explorer/SC) | townhall | editor.
/// Status bar at the bottom.
///
/// M3 (Phase 3.9.1): The bottom panel is now a <see cref="TerminalTabHost"/>
/// that retains one <see cref="TerminalPanel"/> per tab and exposes a
/// <see cref="TerminalTabHost.FocusActiveSession"/> view seam. <c>MainWindow</c>
/// no longer holds a single concrete terminal surface.
/// </summary>
public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    private readonly ISettingsService _settings;
    private readonly ISecretStore _secrets;
    private readonly ISettingsPanelFactory _settingsPanelFactory;
    private readonly ICommandRegistry _registry;
    private readonly StatusBarViewModel _statusBarViewModel;
    private readonly CommandPaletteViewModel _paletteViewModel;
    private readonly EditorSearchViewModel _searchViewModel;
    private readonly EditorLanguageInputViewModel _languageInputViewModel;
    private readonly EditorBreakpointViewModel _editorBreakpointViewModel;
    private readonly DebugCurrentLocationViewModel _debugCurrentLocationViewModel;
    private readonly List<KeyBinding> _registryBindings = new();
    private readonly NavBar _navBar;
    private readonly FileTreeView _fileTreeView;
    private readonly SourceControlPanel _sourceControlPanel;
    private readonly TownhallView _townhallView;
    private readonly StatusBar _statusBar;
    private readonly CommandPaletteOverlay _commandPaletteOverlay;
    private EditorTabBar _editorTabBar = null!;
    private EditorView _editorView = null!;
    private TextBlock _welcomeText = null!;
    private SearchBar _searchBar = null!;
    private TerminalTabHost _terminalTabHost = null!;
    private ProblemsPanel _problemsPanel = null!;
    private OutputPanel _outputPanel = null!;
    private TestResultsPanel _testResultsPanel = null!;
    private DebugPanel _debugPanel = null!;
    private FinalWindowCleanup _finalWindowCleanup = null!;
    private BottomPanelHost _bottomPanelHost = null!;
    private RightColumnHost _rightColumnHost = null!;
    private readonly RowDefinition _bottomSplitterRow;
    private readonly RowDefinition _bottomPanelRow;
    private readonly RowDefinition _statusBarRow;
    private Grid _layoutRoot = null!;
    private SettingsPanelAttachHost _settingsPanelAttachHost = null!;

    [Obsolete("Use the ISettingsService composition constructor.")]
    public MainWindow()
    {
        throw new InvalidOperationException(
            "MainWindow must be created through the application composition root.");
    }

    public MainWindow(ISettingsService settings, ISecretStore secrets,
        ICommandRegistry registry, StatusBarViewModel statusBarViewModel,
        CommandPaletteViewModel paletteViewModel,
        EditorSearchViewModel searchViewModel,
        EditorLanguageInputViewModel languageInputViewModel,
        EditorBreakpointViewModel editorBreakpointViewModel,
        DebugCurrentLocationViewModel debugCurrentLocationViewModel,
        ISettingsPanelFactory settingsPanelFactory)
    {
        _settings = settings;
        _secrets = secrets;
        _settingsPanelFactory = settingsPanelFactory;
        _registry = registry;
        _statusBarViewModel = statusBarViewModel;
        _paletteViewModel = paletteViewModel;
        _searchViewModel = searchViewModel;
        _languageInputViewModel = languageInputViewModel;
        _editorBreakpointViewModel = editorBreakpointViewModel
            ?? throw new ArgumentNullException(nameof(editorBreakpointViewModel));
        _debugCurrentLocationViewModel = debugCurrentLocationViewModel
            ?? throw new ArgumentNullException(nameof(debugCurrentLocationViewModel));
        InitializeComponent();

        // === Window Chrome ===
        Title = "Zaide";
        Width = 1280;
        Height = 800;
        MinWidth = 960;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        // M1: Enable backdrop blur via TransparencyLevelHint.
        // Priority order: AcrylicBlur → Blur → Transparent.
        // Avalonia picks the first level the platform supports.
        // - Windows 10/11: AcrylicBlur renders.
        // - macOS: Blur renders via NSVisualEffectView.
        // - Linux KDE: Blur renders.
        // - Linux GNOME / tiling WMs: Falls back to Transparent (no blur).
        //   In that case, panels use SurfacePanelBrush / SurfaceBaseBrush
        //   which are near-opaque solid dark colors — the UI still looks
        //   intentional without blur (DESIGN.md §2 fallback path).
        TransparencyLevelHint = new[]
        {
            WindowTransparencyLevel.AcrylicBlur,
            WindowTransparencyLevel.Blur,
            WindowTransparencyLevel.Transparent
        };

        // === Build Layout (M6: nav bar | left slot | townhall | editor | status bar) ===
        var layout = new MainLayoutBuilder().Build(
            _settings,
            _searchViewModel,
            _languageInputViewModel,
            _editorBreakpointViewModel,
            _debugCurrentLocationViewModel);
        _layoutRoot = layout.Root;
        Content = layout.Root;
        _navBar = layout.NavBar;
        _fileTreeView = layout.FileTreeView;
        _sourceControlPanel = layout.SourceControlPanel;
        _townhallView = layout.TownhallView;
        _statusBar = layout.StatusBar;
        _bottomPanelHost = layout.BottomPanelHost;
        _rightColumnHost = layout.RightColumnHost;
        _terminalTabHost = layout.BottomPanelHost.TerminalTabHost;
        _bottomSplitterRow = layout.BottomSplitterRow;
        _bottomPanelRow = layout.BottomPanelRow;
        _statusBarRow = layout.StatusBarRow;
        _problemsPanel = layout.BottomPanelHost.ProblemsPanel;
        _outputPanel = layout.BottomPanelHost.OutputPanel;
        _testResultsPanel = layout.BottomPanelHost.TestResultsPanel;
        _debugPanel = layout.BottomPanelHost.DebugPanel;
        _editorTabBar = layout.RightColumnHost.EditorTabBar;
        _editorView = layout.RightColumnHost.EditorView;
        _welcomeText = layout.RightColumnHost.WelcomeText;
        _searchBar = layout.RightColumnHost.SearchBar;

        // Phase 9 M2: command palette overlay — hosted as a top-layer child of
        // the layout root grid so it covers all content including the status bar.
        _commandPaletteOverlay = new CommandPaletteOverlay(_paletteViewModel);
        Grid.SetColumnSpan(_commandPaletteOverlay, 6);
        Grid.SetRowSpan(_commandPaletteOverlay, 4);
        _layoutRoot.Children.Add(_commandPaletteOverlay);

        _settingsPanelAttachHost = new SettingsPanelAttachHost(
            _settings,
            _secrets,
            _settingsPanelFactory,
            _layoutRoot,
            () => _editorView);

        _finalWindowCleanup = new FinalWindowCleanup(
            _editorView.Dispose,
            _terminalTabHost.Dispose);
        Closed += OnFinalWindowClosed;

        // === ReactiveUI Bindings ===
        this.WhenActivated(disposables =>
        {
            // Wire NavBar to ViewModel
            _navBar.ViewModel = ViewModel;

            // Wire TownhallView to its ViewModel
            _townhallView.ViewModel = ViewModel!.TownhallViewModel;

            // Wire FileTreeView to its ViewModel
            _fileTreeView.ViewModel = ViewModel!.FileTreeViewModel;

            // M3: The bottom-panel host owns the per-tab TerminalPanel cache.
            // Bind it to ITerminalHost — never to a single concrete session VM.
            _terminalTabHost.SetHost(ViewModel!.TerminalHost);

            // Wire SourceControlPanel to its ViewModel
            _sourceControlPanel.ViewModel = ViewModel.SourceControlViewModel;

            // Phase 10 M3: Problems projection surface
            _problemsPanel.ViewModel = ViewModel.ProblemsViewModel;

            // Phase 11 M2: structured output surface
            _outputPanel.ViewModel = ViewModel.ProjectWorkflowViewModel;

            // Phase 11 M5: structured test-results surface
            _testResultsPanel.ViewModel = ViewModel.TestResultsViewModel;

            // Phase 12 M4: debug console and call-stack shell
            _debugPanel.ViewModel = ViewModel.DebugPanelViewModel;

            // Phase 10 M5: route definition/symbol feedback to the status bar.
            disposables.Add(_languageInputViewModel
                .WhenAnyValue(x => x.FeedbackMessage)
                .Where(msg => msg is not null)
                .Subscribe(msg =>
                {
                    if (ViewModel is not null)
                        ViewModel.StatusText = msg;
                }));

            // Wire StatusBar to its singleton child ViewModel.
            _statusBar.ViewModel = _statusBarViewModel;

            // Activate VM subscriptions (save errors, file-open) and clean up
            ViewModel!.Activate();
            disposables.Add(Disposable.Create(() => ViewModel!.Dispose()));

            // Wire EditorTabBar: collection + events (with disposal)
            var editorTabs = ViewModel!.EditorTabs;
            _editorTabBar.SetTabs(editorTabs.OpenTabs);

            void OnTabClicked(EditorViewModel tab) => editorTabs.ActiveTab = tab;
            void OnTabCloseRequested(EditorViewModel tab) =>
                editorTabs.CloseTabCommand.Execute(tab).Subscribe();
            void OnTabMoveRequested(EditorViewModel tab, int fromIndex, int toIndex) =>
                editorTabs.MoveTab(fromIndex, toIndex);
            void OnLastTerminalTabCloseRequested() =>
                ViewModel!.HideBottomPanelCommand.Execute().Subscribe();

            _editorTabBar.TabClicked += OnTabClicked;
            _editorTabBar.TabCloseRequested += OnTabCloseRequested;
            _editorTabBar.TabMoveRequested += OnTabMoveRequested;
            _terminalTabHost.LastTabCloseRequested += OnLastTerminalTabCloseRequested;

            disposables.Add(Disposable.Create(() =>
            {
                _editorTabBar.TabClicked -= OnTabClicked;
                _editorTabBar.TabCloseRequested -= OnTabCloseRequested;
                _editorTabBar.TabMoveRequested -= OnTabMoveRequested;
                _terminalTabHost.LastTabCloseRequested -= OnLastTerminalTabCloseRequested;
            }));

            // Unsaved-changes dialog
            disposables.Add(editorTabs.ConfirmClose.RegisterHandler(async ctx =>
            {
                var dialog = new UnsavedDialog { DataContext = ctx.Input };
                var result = await dialog.ShowDialog<bool?>(this);
                ctx.SetOutput(result);
            }));

            // Wire active tab → EditorView + tab bar highlight + welcome text + townhall link
            // Phase 9 M3: also wire search VM's ActiveDocument and ActiveDocumentId.
            // ActiveDocumentId uses the file path (or a unique counter for untitled tabs)
            // so that tab switching resets search state even though the same EditorView
            // instance is reused across all tabs.
            var untitledCounter = 0;
            disposables.Add(this.WhenAnyValue(x => x.ViewModel!.EditorTabs.ActiveTab)
                .Subscribe(active =>
                {
                    _editorView.ViewModel = active;
                    _editorView.IsVisible = active is not null;
                    _editorTabBar.SetActiveTab(active);
                    _editorTabBar.SetTownhallLinkVisible(active is not null);
                    _welcomeText.IsVisible = active is null;
                    _searchViewModel.ActiveDocument = active is not null ? _editorView : null;
                    _searchViewModel.ActiveDocumentId = active is not null
                        ? (string.IsNullOrEmpty(active.FilePath)
                            ? $"__untitled_{++untitledCounter}"
                            : active.FilePath)
                        : null;
                }));

            // Left panel mode switching: show file tree or SC panel.
            // When switching to Source Control, immediately refresh git state so the
            // panel reflects the current repository state without requiring a manual
            // refresh click.
            disposables.Add(this.WhenAnyValue(x => x.ViewModel!.LeftPanelMode)
                .Subscribe(mode => _ = AnimateLeftPanelModeSwitchAsync(mode)));

            disposables.Add(this.WhenAnyValue(x => x.ViewModel!.LeftPanelMode)
                .Where(mode => mode == LeftPanelMode.SourceControl)
                .Subscribe(_ =>
                    ViewModel!.SourceControlViewModel.RefreshCommand
                        .Execute(Unit.Default).Subscribe()));

            // M9a: materialize registry-driven keybindings atomically
            // Replaces all imperative per-command binding blocks below.
            MaterializeRegistryBindings();

            // M9b: settings-driven keybinding refresh.
            // Each emitted snapshot is captured synchronously and passed
            // directly to the snapshot-aware overload so resolution reads
            // exactly that snapshot's keybindings — not a re-fetch of
            // _settings.Current which may have moved past the emission.
            disposables.Add(_settings.WhenChanged
                .ObserveOn(AvaloniaScheduler.Instance)
                .Subscribe(snapshot => MaterializeRegistryBindings(snapshot)));

            // Bind bottom panel visibility and mode routing through the extracted host.
            _bottomPanelHost.WireToViewModel(ViewModel!, disposables);

            // PickFolder handler — opens native folder dialog
            disposables.Add(ViewModel!.PickFolder.RegisterHandler(async ctx =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel is null) { ctx.SetOutput(null); return; }
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
                    new FolderPickerOpenOptions { AllowMultiple = false });
                ctx.SetOutput(folders.Count > 0 ? folders[0].Path.LocalPath : null);
            }));

            _settingsPanelAttachHost.WireToViewModel(ViewModel!, disposables);

            ShellOverlayFocusWiring.Wire(
                disposables,
                _paletteViewModel,
                _commandPaletteOverlay,
                _searchViewModel,
                _searchBar,
                _editorView,
                ViewModel!);

            // Phase 9 M4: wire folding operations from the shared EditorView
            // to EditorTabViewModel so registered folding commands
            // (editor.foldToggle, editor.foldAll, editor.unfoldAll) can
            // reach the View-layer FoldingManager.
            editorTabs.FoldingEditor = _editorView.Folding;
            disposables.Add(Disposable.Create(() => editorTabs.FoldingEditor = null));

            // Phase 9 M6: route search outcomes to the status bar.
            // Only non-empty status messages are piped; empty status (from dismiss)
            // does not overwrite unrelated status text.
            disposables.Add(_searchViewModel.WhenAnyValue(x => x.StatusMessage)
                .Where(msg => !string.IsNullOrEmpty(msg))
                .Subscribe(msg => ViewModel!.StatusText = msg));
        });
    }

    /// <summary>
    /// M9a: resolve neutral bindings from <see cref="_settings"/>'s current snapshot,
    /// convert to Avalonia <see cref="KeyBinding"/>, and atomically replace only
    /// the previously materialized bindings in the window's <see cref="KeyBindings"/>.
    /// Used during initial activation.
    /// </summary>
    private void MaterializeRegistryBindings()
    {
        MaterializeRegistryBindings(_settings.Current);
    }

    /// <summary>
    /// M9b: resolve neutral bindings from the given <paramref name="snapshot"/>,
    /// convert to Avalonia <see cref="KeyBinding"/>, and atomically replace only
    /// the previously materialized bindings. Called from the WhenChanged subscription
    /// so the emitted snapshot — not a re-fetch of <c>_settings.Current</c> — drives
    /// resolution.
    /// </summary>
    private void MaterializeRegistryBindings(SettingsModel snapshot)
    {
        // Remove previously generated bindings
        foreach (var kb in _registryBindings)
            KeyBindings.Remove(kb);

        _registryBindings.Clear();

        // Wrap the emitted snapshot as an ISettingsService so the framework-agnostic
        // registry resolver can consume it without contract changes.
        var snapshotService = new SnapshotSettingsAccessor(snapshot);
        var resolved = _registry.ResolveKeyBindings(snapshotService);
        foreach (var binding in resolved)
        {
            var descriptor = _registry.GetById(binding.CommandId);
            var kb = KeyBindingConverter.TryCreateKeyBinding(binding, descriptor);
            if (kb is null)
            {
                // Should not happen — ResolveKeyBindings only returns registered IDs.
                // Defensive guard for future extension.
                continue;
            }

            KeyBindings.Add(kb);
            _registryBindings.Add(kb);
        }
    }

    /// <summary>
    /// Lightweight ISettingsService wrapper that exposes a single frozen snapshot.
    /// Lets the snapshot-aware MaterializeRegistryBindings overload pass the
    /// emitted snapshot through the existing framework-agnostic resolution API
    /// without modifying ICommandRegistry or CommandRegistry contracts.
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

    private void OnFinalWindowClosed(object? sender, EventArgs e)
    {
        Closed -= OnFinalWindowClosed;
        _finalWindowCleanup.Dispose();
    }

    private async Task AnimateLeftPanelModeSwitchAsync(LeftPanelMode mode)
    {
        var showExplorer = mode == LeftPanelMode.Explorer;
        var entering = showExplorer ? (Visual)_fileTreeView : _sourceControlPanel;
        var exiting = showExplorer ? (Visual)_sourceControlPanel : _fileTreeView;
        var enterDirection = showExplorer ? HorizontalDirection.Left : HorizontalDirection.Right;
        var exitDirection = showExplorer ? HorizontalDirection.Right : HorizontalDirection.Left;

        entering.IsVisible = true;
        entering.Opacity = 0;
        exiting.IsVisible = true;
        exiting.Opacity = 1;

        await Task.WhenAll(
            Animations.RunAsync((Animatable)entering, Animations.PanelEnter(enterDirection)),
            Animations.RunAsync((Animatable)exiting, Animations.PanelExit(exitDirection)));

        entering.Opacity = 1;
        exiting.Opacity = 0;
        exiting.IsVisible = false;
    }
}
