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
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Models;
using Zaide.ViewModels;
using Zaide.Views;
using Zaide.Styles;
using Zaide.Services;

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
    private FinalWindowCleanup _finalWindowCleanup = null!;
    private AgentPanelHostView _agentPanelHostView = null!;
    private Border _bottomPanel = null!;
    private GridSplitter _bottomPanelSplitter = null!;
    private readonly RowDefinition _bottomSplitterRow;
    private readonly RowDefinition _bottomPanelRow;
    private readonly RowDefinition _statusBarRow;
    private Grid _layoutRoot = null!;
    private SettingsPanelView? _settingsPanel;
    private TaskCompletionSource<bool>? _settingsCompletion;

    [Obsolete("Use the ISettingsService composition constructor.")]
    public MainWindow()
    {
        throw new InvalidOperationException(
            "MainWindow must be created through the application composition root.");
    }

    public MainWindow(ISettingsService settings, ISecretStore secrets,
        ICommandRegistry registry, StatusBarViewModel statusBarViewModel,
        CommandPaletteViewModel paletteViewModel,
        EditorSearchViewModel searchViewModel)
    {
        _settings = settings;
        _secrets = secrets;
        _registry = registry;
        _statusBarViewModel = statusBarViewModel;
        _paletteViewModel = paletteViewModel;
        _searchViewModel = searchViewModel;
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
            void OnLastTerminalTabCloseRequested() =>
                ViewModel!.HideBottomPanelCommand.Execute().Subscribe();

            _editorTabBar.TabClicked += OnTabClicked;
            _editorTabBar.TabCloseRequested += OnTabCloseRequested;
            _terminalTabHost.LastTabCloseRequested += OnLastTerminalTabCloseRequested;

            disposables.Add(Disposable.Create(() =>
            {
                _editorTabBar.TabClicked -= OnTabClicked;
                _editorTabBar.TabCloseRequested -= OnTabCloseRequested;
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

            // Left panel mode switching: show file tree or SC panel
            disposables.Add(this.WhenAnyValue(x => x.ViewModel!.LeftPanelMode)
                .Subscribe(mode => _ = AnimateLeftPanelModeSwitchAsync(mode)));

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

                    if (visible)
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

            // Phase 9 M3: search bar focus management.
            // FocusRequested fires when Find/Replace opens the surface.
            void OnSearchFocusRequested() => _searchBar.FocusQuery();
            _searchViewModel.FocusRequested += OnSearchFocusRequested;
            disposables.Add(Disposable.Create(() =>
                _searchViewModel.FocusRequested -= OnSearchFocusRequested));

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
        _editorView = new EditorView(_settings);
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
        // M3: TerminalTabHost retains one TerminalPanel per tab and switches
        // the active tab's panel in the content area.
        var terminalTabHost = new TerminalTabHost(_settings);
        var bottomPanel = new Border
        {
            Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"],
            Padding = LayoutTokens.NoneThickness,
            // M5-allow: M1 introduced the 1px top seam above the bottom panel to preserve the raised-layer split.
            Margin = LayoutTokens.Inset(0, 1, 0, 0),
            Child = terminalTabHost
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

    private async Task HandleShowSettingsAsync(IInteractionContext<System.Reactive.Unit, bool> context)
    {
        if (_settingsPanel is not null)
        {
            context.SetOutput(false);
            return;
        }

        var viewModel = new SettingsViewModel(_settings, _secrets);
        var panel = new SettingsPanelView(viewModel);
        _settingsPanel = panel;
        Grid.SetColumn(panel, 0);
        Grid.SetColumnSpan(panel, 6);
        Grid.SetRow(panel, 0);
        Grid.SetRowSpan(panel, 3);
        _layoutRoot.Children.Add(panel);

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _settingsCompletion = completion;
        void OnClose(object? sender, EventArgs args) => completion.TrySetResult(true);
        viewModel.CloseRequested += OnClose;
        try
        {
            await completion.Task;
            _layoutRoot.Children.Remove(panel);
            panel.Dispose();
            _settingsPanel = null;
            context.SetOutput(true);
        }
        finally
        {
            viewModel.CloseRequested -= OnClose;
            if (ReferenceEquals(_settingsCompletion, completion)) _settingsCompletion = null;
        }
    }

    private void CloseSettingsPanel()
    {
        if (_settingsPanel is null) return;
        var panel = _settingsPanel;
        _settingsPanel = null;
        _settingsCompletion?.TrySetResult(true);
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
