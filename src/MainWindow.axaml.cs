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
using Zaide.Models;
using Zaide.ViewModels;
using Zaide.Views;
using Zaide.UI.DesignSystem;
using Zaide.Services;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Presentation;
using Zaide.Features.Workspace.Presentation;

namespace Zaide;

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
    private readonly SearchBar _searchBar;
    private EditorTabBar _editorTabBar = null!;
    private EditorView _editorView = null!;
    private TextBlock _welcomeText = null!;
    private TerminalTabHost _terminalTabHost = null!;
    private ProblemsPanel _problemsPanel = null!;
    private OutputPanel _outputPanel = null!;
    private TestResultsPanel _testResultsPanel = null!;
    private DebugPanel _debugPanel = null!;
    private FinalWindowCleanup _finalWindowCleanup = null!;
    private AgentPanelHostView _agentPanelHostView = null!;
    private Border _bottomPanel = null!;
    private GridSplitter _bottomPanelSplitter = null!;
    private readonly RowDefinition _bottomSplitterRow;
    private readonly RowDefinition _bottomPanelRow;
    private readonly RowDefinition _statusBarRow;
    private Grid _layoutRoot = null!;
    private SettingsPanelView? _settingsPanel;
    private SettingsViewModel? _settingsPanelViewModel;
    private LeftPanelMode _settingsReturnLeftPanelMode = LeftPanelMode.Explorer;
    private MainWindowViewModel? _settingsLifecycleViewModel;

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
        DebugCurrentLocationViewModel debugCurrentLocationViewModel)
    {
        _settings = settings;
        _secrets = secrets;
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

        // Phase 9 M3: search/replace bar for the active editor document.
        // Created before BuildLayout so it can be inserted into the editor panel.
        _searchBar = new SearchBar(_searchViewModel);

        // === Build Layout (M6: nav bar | left slot | townhall | editor | status bar) ===
        (_navBar, _fileTreeView, _sourceControlPanel, _townhallView, _statusBar,
         _terminalTabHost, _agentPanelHostView, _bottomPanel, _bottomPanelSplitter, _bottomSplitterRow, _bottomPanelRow, _statusBarRow) = BuildLayout();

        // Phase 9 M2: command palette overlay — hosted as a top-layer child of
        // the layout root grid so it covers all content including the status bar.
        _commandPaletteOverlay = new CommandPaletteOverlay(_paletteViewModel);
        Grid.SetColumnSpan(_commandPaletteOverlay, 6);
        Grid.SetRowSpan(_commandPaletteOverlay, 4);
        _layoutRoot.Children.Add(_commandPaletteOverlay);

        _finalWindowCleanup = new FinalWindowCleanup(
            _editorView.Dispose,
            _terminalTabHost.Dispose);
        Closed += OnFinalWindowClosed;

        // === ReactiveUI Bindings ===
        this.WhenActivated(disposables =>
        {
            _settingsLifecycleViewModel = ViewModel;

            // Wire NavBar to ViewModel
            _navBar.ViewModel = ViewModel;

            // Wire TownhallView to its ViewModel
            _townhallView.ViewModel = ViewModel!.TownhallViewModel;

            // Wire FileTreeView to its ViewModel
            _fileTreeView.ViewModel = ViewModel!.FileTreeViewModel;

            // M3: The bottom-panel host owns the per-tab TerminalPanel cache.
            // Bind it to ITerminalHost — never to a single concrete session VM.
            _terminalTabHost.SetHost(ViewModel!.TerminalHost);

            // Wire the agent-panel host view to IAgentPanelHost
            _agentPanelHostView.SetHost(ViewModel!.AgentPanelHost);

            // M5: explicit cleanup path — detach host/panel subscriptions and
            // release retained views when the window deactivates.
            disposables.Add(Disposable.Create(() => _agentPanelHostView.DetachHost()));

            // M2: Wire panel send event through the thin composition seam
            void OnPanelSendRequested(string panelId, string message)
            {
                _ = ViewModel!.SendAgentMessageAsync(panelId, message);
            }
            _agentPanelHostView.PanelSendRequested += OnPanelSendRequested;
            disposables.Add(Disposable.Create(() =>
                _agentPanelHostView.PanelSendRequested -= OnPanelSendRequested));

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

            // Bind bottom panel visibility → row height.
            // M3: focus and start are routed through the view host seam so the
            // active tab's retained panel — not a single shared panel — gets focus.
            disposables.Add(this.WhenAnyValue(x => x.ViewModel!.IsBottomPanelVisible)
                .Subscribe(visible =>
                {
                    _bottomSplitterRow.Height = visible
                        ? new GridLength(4, GridUnitType.Pixel)
                        : new GridLength(0);
                    _bottomPanelRow.Height = visible
                        ? new GridLength(250)
                        : new GridLength(0);
                    _bottomPanelSplitter.IsVisible = visible;
                    _bottomPanel.IsVisible = visible;

                    if (visible && ViewModel.BottomPanelMode == BottomPanelMode.Terminal)
                    {
                        _terminalTabHost.FocusActiveSession();
                        _ = ViewModel.TerminalHost.EnsureActiveSessionStartedAsync();
                    }
                }));

            // Phase 10 M3 / Phase 11 M2/M5: switch bottom content between panel modes.
            disposables.Add(this.WhenAnyValue(x => x.ViewModel!.BottomPanelMode)
                .Subscribe(mode =>
                {
                    _terminalTabHost.IsVisible = mode == BottomPanelMode.Terminal;
                    _problemsPanel.IsVisible = mode == BottomPanelMode.Problems;
                    _outputPanel.IsVisible = mode == BottomPanelMode.Output;
                    _testResultsPanel.IsVisible = mode == BottomPanelMode.TestResults;
                    _debugPanel.IsVisible = mode == BottomPanelMode.Debug;
                    if (mode == BottomPanelMode.Terminal && ViewModel!.IsBottomPanelVisible)
                    {
                        _terminalTabHost.FocusActiveSession();
                        _ = ViewModel.TerminalHost.EnsureActiveSessionStartedAsync();
                    }
                }));

            // PickFolder handler — opens native folder dialog
            disposables.Add(ViewModel!.PickFolder.RegisterHandler(async ctx =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel is null) { ctx.SetOutput(null); return; }
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
                    new FolderPickerOpenOptions { AllowMultiple = false });
                ctx.SetOutput(folders.Count > 0 ? folders[0].Path.LocalPath : null);
            }));

            disposables.Add(ViewModel.ShowSettings.RegisterHandler(async context =>
                await HandleShowSettingsAsync(context)));
            disposables.Add(Disposable.Create(CloseSettingsPanel));

            // Phase 9 M2: command palette overlay lifecycle.
            // OpenRequested fires when palette.open executes (Ctrl+Shift+P or rebound gesture).
            void OnPaletteOpenRequested() => _commandPaletteOverlay.Show();
            _paletteViewModel.OpenRequested += OnPaletteOpenRequested;
            disposables.Add(Disposable.Create(() =>
                _paletteViewModel.OpenRequested -= OnPaletteOpenRequested));

            // Dismissed fires on Escape, successful execution, or backdrop click.
            void OnOverlayDismissed()
            {
                _commandPaletteOverlay.Hide();
                RestoreFocusAfterPalette();
            }
            _commandPaletteOverlay.Dismissed += OnOverlayDismissed;
            disposables.Add(Disposable.Create(() =>
                _commandPaletteOverlay.Dismissed -= OnOverlayDismissed));

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

            // Phase 9 M3: search bar focus management.
            // FocusRequested fires when Find/Replace opens the surface.
            void OnSearchFocusRequested() => _searchBar.FocusQuery();
            _searchViewModel.FocusRequested += OnSearchFocusRequested;
            disposables.Add(Disposable.Create(() =>
                _searchViewModel.FocusRequested -= OnSearchFocusRequested));

            // M5c: SelectionUpdated fires when SelectCurrentMatch runs (navigation).
            // The editor selection update may steal X11 focus; ensure the search bar
            // keeps focus so the user can continue typing their query.
            void OnSearchSelectionUpdated() => _searchBar.FocusQueryWithoutSelectAll();
            _searchViewModel.SelectionUpdated += OnSearchSelectionUpdated;
            disposables.Add(Disposable.Create(() =>
                _searchViewModel.SelectionUpdated -= OnSearchSelectionUpdated));

            // When the search surface is dismissed, restore focus to the editor.
            disposables.Add(_searchViewModel.WhenAnyValue(x => x.IsVisible)
                .Subscribe(visible =>
                {
                    if (!visible && _editorView.IsVisible)
                        _editorView.Focus();
                }));
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

    /// <summary>
    /// Builds the M6 layout: nav bar | left-panel mode slot | townhall | editor.
    /// Bottom panel spans under center + right only. Status bar at the bottom.
    ///
    /// M3: the bottom panel hosts a <see cref="TerminalTabHost"/> instead of a
    /// single <see cref="TerminalPanel"/>.
    /// </summary>
    private (NavBar navBar, FileTreeView fileTreeView, SourceControlPanel sourceControlPanel, TownhallView townhallView,
             StatusBar statusBar, TerminalTabHost terminalTabHost, AgentPanelHostView agentPanelHostView,
             Border bottomPanel, GridSplitter bottomPanelSplitter,
             RowDefinition bottomSplitterRow, RowDefinition bottomPanelRow, RowDefinition statusBarRow) BuildLayout()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                // 0: Nav bar (fixed 40px)
                new ColumnDefinition { Width = new GridLength(40) },
                // 1: Left panel (fixed 260px, min 180px)
                new ColumnDefinition { Width = new GridLength(260), MinWidth = 180, MaxWidth = 320 },
                // 2: Splitter between left panel and townhall
                new ColumnDefinition { Width = new GridLength(4, GridUnitType.Pixel) },
                // 3: Center — Townhall (star, dominant)
                new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 300 },
                // 4: Splitter between townhall and editor
                new ColumnDefinition { Width = new GridLength(4, GridUnitType.Pixel) },
                // 5: Right — Editor (star, smaller than center)
                new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star), MinWidth = 240 }
            },
            RowDefinitions =
            {
                // 0: Content area
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                // 1: Bottom panel splitter
                new RowDefinition { Height = new GridLength(0) },
                // 2: Bottom panel
                new RowDefinition { Height = new GridLength(0) },
                // 3: Status bar (24px, always visible)
                new RowDefinition { Height = new GridLength(24, GridUnitType.Pixel) }
            },
            Background = (IBrush?)Application.Current!.Resources["SurfaceBaseBrush"]
        };

        var bottomSplitterRow = grid.RowDefinitions[1];
        var bottomPanelRow = grid.RowDefinitions[2];
        var statusBarRow = grid.RowDefinitions[3];

        // --- Column 0: Nav Bar ---
        var navBar = new NavBar();
        Grid.SetColumn(navBar, 0);
        Grid.SetRow(navBar, 0);
        Grid.SetRowSpan(navBar, 4); // Full height
        grid.Children.Add(navBar);

        // --- Column 1: Left Panel (Explorer / Source Control) ---
        var fileTreeView = new FileTreeView();
        fileTreeView.RenderTransform = new TranslateTransform();
        Grid.SetColumn(fileTreeView, 1);
        Grid.SetRow(fileTreeView, 0);
        Grid.SetRowSpan(fileTreeView, 3);
        grid.Children.Add(fileTreeView);

        var sourceControlPanel = new SourceControlPanel();
        sourceControlPanel.IsVisible = false; // Hidden by default (Explorer mode)
        sourceControlPanel.RenderTransform = new TranslateTransform();
        Grid.SetColumn(sourceControlPanel, 1);
        Grid.SetRow(sourceControlPanel, 0);
        Grid.SetRowSpan(sourceControlPanel, 3);
        grid.Children.Add(sourceControlPanel);

        // --- Column 2: Splitter (left panel ↔ townhall) ---
        var leftSplitter = new GridSplitter
        {
            Width = 4,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        leftSplitter.DragCompleted += (_, _) =>
            GridLayoutResizeHelper.PreservePixelColumnAndNormalizeStarColumns(grid, 1, 3, 5);
        Grid.SetColumn(leftSplitter, 2);
        Grid.SetRow(leftSplitter, 0);
        Grid.SetRowSpan(leftSplitter, 3);
        grid.Children.Add(leftSplitter);

        // --- Column 3: Center — Townhall (real M3 view) ---
        _editorTabBar = new EditorTabBar();
        _editorView = new EditorView(
            _settings,
            _languageInputViewModel,
            _editorBreakpointViewModel,
            _debugCurrentLocationViewModel);
        _welcomeText = TextStyles.Body("Open a file to begin");
        _welcomeText.VerticalAlignment = VerticalAlignment.Center;
        _welcomeText.HorizontalAlignment = HorizontalAlignment.Center;

        var townhallView = new TownhallView();
        Grid.SetColumn(townhallView, 3);
        Grid.SetRow(townhallView, 0);
        grid.Children.Add(townhallView);

        // --- Column 4: Splitter (townhall ↔ editor) ---
        var rightSplitter = new GridSplitter
        {
            Width = 4,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        rightSplitter.DragCompleted += (_, _) =>
            GridLayoutResizeHelper.NormalizeStarColumns(grid, 3, 5);
        Grid.SetColumn(rightSplitter, 4);
        Grid.SetRow(rightSplitter, 0);
        grid.Children.Add(rightSplitter);

        // --- Column 5: Right — Editor (top) + Agent Panel (bottom) ---
        // Split column 5 into editor (upper) and agent panel (lower) via
        // a vertical splitter, keeping both surfaces inside the existing
        // right-side shell column.
        var rightSplitterH = new GridSplitter
        {
            Height = 4,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeNorthSouth),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Rows,
            IsVisible = true
        };

        var editorPanel = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },   // 0: tab bar
                new RowDefinition { Height = GridLength.Auto },   // 1: search bar
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } // 2: editor / welcome
            },
            Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"],
            // M5-allow: M1 introduced the 1px panel seam as a visual divider, not semantic spacing.
            Margin = LayoutTokens.Inset(1, 0, 0, 0),
            Children =
            {
                _editorTabBar,
                _searchBar,
                _editorView,
                _welcomeText
            }
        };
        Grid.SetRow(_editorTabBar, 0);
        Grid.SetRow(_searchBar, 1);
        Grid.SetRow(_editorView, 2);
        Grid.SetRow(_welcomeText, 2);
        _welcomeText.IsVisible = true;

        var agentPanelHostView = new AgentPanelHostView();

        var rightColumn = new Grid
        {
            RowDefinitions =
            {
                // 0: Editor panel (star, larger)
                new RowDefinition { Height = new GridLength(2, GridUnitType.Star) },
                // 1: Horizontal splitter
                new RowDefinition { Height = new GridLength(4, GridUnitType.Pixel) },
                // 2: Agent panel (star, smaller)
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            Children = { editorPanel, rightSplitterH, agentPanelHostView }
        };
        Grid.SetRow(editorPanel, 0);
        Grid.SetRow(rightSplitterH, 1);
        Grid.SetRow(agentPanelHostView, 2);

        Grid.SetColumn(rightColumn, 5);
        Grid.SetRow(rightColumn, 0);
        grid.Children.Add(rightColumn);

        // --- Bottom Panel Splitter (spans columns 3-5: center + editor only) ---
        var bottomPanelSplitter = new GridSplitter
        {
            Height = 4,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeNorthSouth),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Rows
        };
        bottomPanelSplitter.IsVisible = false;
        Grid.SetColumn(bottomPanelSplitter, 3);
        Grid.SetColumnSpan(bottomPanelSplitter, 3); // Under center + editor only
        Grid.SetRow(bottomPanelSplitter, 1);
        grid.Children.Add(bottomPanelSplitter);

        // --- Bottom Panel (spans columns 3-5: center + editor only) ---
        // Hosts Terminal, Problems, Output, Test Results, and Debug surfaces with a mode strip.
        var terminalTabHost = new TerminalTabHost(_settings);
        var problemsPanel = new ProblemsPanel { IsVisible = false };
        var outputPanel = new OutputPanel { IsVisible = false };
        var testResultsPanel = new TestResultsPanel { IsVisible = false };
        var debugPanel = new DebugPanel { IsVisible = false };
        _problemsPanel = problemsPanel;
        _outputPanel = outputPanel;
        _testResultsPanel = testResultsPanel;
        _debugPanel = debugPanel;

        var terminalTabButton = new Button
        {
            Content = "Terminal",
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs),
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs, 0, LayoutTokens.SpacingXxs),
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        terminalTabButton.Click += (_, _) =>
        {
            if (ViewModel is not null)
                ViewModel.SwitchToTerminalBottomCommand.Execute().Subscribe();
        };

        var problemsTabButton = new Button
        {
            Content = "Problems",
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs),
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingXxs, LayoutTokens.SpacingXxs, 0, LayoutTokens.SpacingXxs),
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        problemsTabButton.Click += (_, _) =>
        {
            if (ViewModel is not null)
                ViewModel.SwitchToProblemsBottomCommand.Execute().Subscribe();
        };

        var outputTabButton = new Button
        {
            Content = "Output",
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs),
            FontSize = 12,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        outputTabButton.Click += (_, _) =>
        {
            if (ViewModel is not null)
                ViewModel.SwitchToOutputBottomCommand.Execute().Subscribe();
        };

        var testResultsTabButton = new Button
        {
            Content = "Test Results",
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs),
            FontSize = 12,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        testResultsTabButton.Click += (_, _) =>
        {
            if (ViewModel is not null)
                ViewModel.SwitchToTestResultsBottomCommand.Execute().Subscribe();
        };

        var debugTabButton = new Button
        {
            Content = "Debug",
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs),
            FontSize = 12,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        debugTabButton.Click += (_, _) =>
        {
            if (ViewModel is not null)
                ViewModel.SwitchToDebugBottomCommand.Execute().Subscribe();
        };

        var bottomModeStrip = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingXxs,
            Children =
            {
                terminalTabButton,
                problemsTabButton,
                outputTabButton,
                testResultsTabButton,
                debugTabButton,
            },
        };

        var bottomContent = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
            Children =
            {
                bottomModeStrip,
                terminalTabHost,
                problemsPanel,
                outputPanel,
                testResultsPanel,
                debugPanel,
            },
        };
        Grid.SetRow(bottomModeStrip, 0);
        Grid.SetRow(terminalTabHost, 1);
        Grid.SetRow(problemsPanel, 1);
        Grid.SetRow(outputPanel, 1);
        Grid.SetRow(testResultsPanel, 1);
        Grid.SetRow(debugPanel, 1);

        var bottomPanel = new Border
        {
            Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"],
            Padding = LayoutTokens.NoneThickness,
            // M5-allow: M1 introduced the 1px top seam above the bottom panel to preserve the raised-layer split.
            Margin = LayoutTokens.Inset(0, 1, 0, 0),
            Child = bottomContent
        };
        bottomPanel.IsVisible = false;
        Grid.SetColumn(bottomPanel, 3);
        Grid.SetColumnSpan(bottomPanel, 3); // Under center + editor only
        Grid.SetRow(bottomPanel, 2);
        grid.Children.Add(bottomPanel);

        // --- Status Bar (full width, row 3) ---
        var statusBar = new StatusBar();
        Grid.SetColumn(statusBar, 0);
        Grid.SetColumnSpan(statusBar, 6); // Full width
        Grid.SetRow(statusBar, 3);
        grid.Children.Add(statusBar);

        Content = grid;
        _layoutRoot = grid;
        return (navBar, fileTreeView, sourceControlPanel, townhallView, statusBar,
            terminalTabHost, agentPanelHostView, bottomPanel, bottomPanelSplitter, bottomSplitterRow, bottomPanelRow, statusBarRow);
    }

    private Task HandleShowSettingsAsync(IInteractionContext<System.Reactive.Unit, bool> context)
    {
        var vm = RequireSettingsLifecycleViewModel();
        if (vm.IsSettingsOpen)
        {
            HideSettingsPanel();
            context.SetOutput(false);
            return Task.CompletedTask;
        }

        ShowSettingsPanel();
        context.SetOutput(true);
        return Task.CompletedTask;
    }

    private MainWindowViewModel RequireSettingsLifecycleViewModel() =>
        _settingsLifecycleViewModel
        ?? throw new InvalidOperationException("MainWindow settings lifecycle is not bound.");

    private void ShowSettingsPanel()
    {
        var vm = RequireSettingsLifecycleViewModel();
        _settingsReturnLeftPanelMode = vm.LeftPanelMode;
        if (_settingsPanel is null)
        {
            var viewModel = new SettingsViewModel(_settings, _secrets);
            var panel = new SettingsPanelView(viewModel);
            _settingsPanelViewModel = viewModel;
            _settingsPanel = panel;
            Grid.SetColumn(panel, 0);
            Grid.SetColumnSpan(panel, 6);
            Grid.SetRow(panel, 0);
            Grid.SetRowSpan(panel, 3);
            viewModel.CloseRequested += OnSettingsCloseRequested;
        }

        AttachSettingsPanelToLayout();
        vm.IsSettingsOpen = true;
    }

    private void HideSettingsPanel()
    {
        var vm = RequireSettingsLifecycleViewModel();
        if (_settingsPanel is null || !vm.IsSettingsOpen)
            return;

        DetachSettingsPanelFromLayout();
        vm.IsSettingsOpen = false;
        vm.LeftPanelMode = _settingsReturnLeftPanelMode;
        RestoreFocusAfterSettings();
    }

    private void OnSettingsCloseRequested(object? sender, EventArgs e) => HideSettingsPanel();

    private void AttachSettingsPanelToLayout()
    {
        if (_settingsPanel is null || !_layoutRoot.CheckAccess())
            return;

        if (!_layoutRoot.Children.Contains(_settingsPanel))
            _layoutRoot.Children.Add(_settingsPanel);
    }

    private void DetachSettingsPanelFromLayout()
    {
        if (_settingsPanel is null || !_layoutRoot.CheckAccess())
            return;

        if (_layoutRoot.Children.Contains(_settingsPanel))
            _layoutRoot.Children.Remove(_settingsPanel);
    }

    private void RestoreFocusAfterSettings()
    {
        var activeTab = _settingsLifecycleViewModel?.EditorTabs.ActiveTab;
        if (activeTab is not null && _editorView is not null && _editorView.IsVisible)
        {
            _editorView.Focus();
        }
    }

    private void CloseSettingsPanel()
    {
        if (_settingsPanel is null)
            return;

        if (_settingsPanelViewModel is not null)
            _settingsPanelViewModel.CloseRequested -= OnSettingsCloseRequested;

        var panel = _settingsPanel;
        _settingsPanel = null;
        _settingsPanelViewModel = null;
        RequireSettingsLifecycleViewModel().IsSettingsOpen = false;
        if (_layoutRoot.CheckAccess() && _layoutRoot.Children.Contains(panel))
            _layoutRoot.Children.Remove(panel);
        panel.Dispose();
    }

    /// <summary>
    /// Phase 9 M2: restore focus to the active editor after the command palette
    /// is dismissed. If no active editor exists, does nothing (never throws).
    /// Never restores focus to a closed/replaced tab.
    /// </summary>
    private void RestoreFocusAfterPalette()
    {
        var activeTab = ViewModel?.EditorTabs.ActiveTab;
        if (activeTab is not null && _editorView.IsVisible)
        {
            _editorView.Focus();
        }
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
