using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Controls.Primitives;
using ReactiveUI;
using ReactiveUI.Avalonia;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Zaide.ViewModels;
using Zaide.Styles;
using Zaide.Views;

namespace Zaide;

/// <summary>
/// Main application window. Layout built in C# per DESIGN.md §1.
/// Refactor 3 M6: nav bar | left-panel mode slot (Explorer/SC) | townhall | editor.
/// Status bar at the bottom.
/// </summary>
public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    private readonly NavBar _navBar;
    private readonly FileTreeView _fileTreeView;
    private readonly SourceControlPanel _sourceControlPanel;
    private readonly TownhallView _townhallView;
    private readonly StatusBar _statusBar;
    private EditorTabBar _editorTabBar = null!;
    private EditorView _editorView = null!;
    private TextBlock _welcomeText = null!;
    private TerminalPanel _terminalPanel = null!;
    private Border _bottomPanel = null!;
    private GridSplitter _bottomPanelSplitter = null!;
    private readonly RowDefinition _bottomSplitterRow;
    private readonly RowDefinition _bottomPanelRow;
    private readonly RowDefinition _statusBarRow;

    public MainWindow()
    {
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
        (_navBar, _fileTreeView, _sourceControlPanel, _townhallView, _statusBar,
         _terminalPanel, _bottomPanel, _bottomPanelSplitter, _bottomSplitterRow, _bottomPanelRow, _statusBarRow) = BuildLayout();

        // === ReactiveUI Bindings ===
        this.WhenActivated(disposables =>
        {
            // Wire NavBar to ViewModel
            _navBar.ViewModel = ViewModel;

            // Wire TownhallView to its ViewModel
            _townhallView.ViewModel = ViewModel!.TownhallViewModel;

            // Wire FileTreeView to its ViewModel
            _fileTreeView.ViewModel = ViewModel!.FileTreeViewModel;
            _terminalPanel.ViewModel = ViewModel!.TerminalHost.ActiveSession;

            // Wire SourceControlPanel to its ViewModel
            _sourceControlPanel.ViewModel = ViewModel.SourceControlViewModel;

            // Wire StatusBar to ViewModel
            _statusBar.ViewModel = ViewModel;

            // Activate VM subscriptions (save errors, file-open) and clean up
            ViewModel!.Activate();
            disposables.Add(Disposable.Create(() => ViewModel!.Dispose()));

            // Wire EditorTabBar: collection + events (with disposal)
            var editorTabs = ViewModel!.EditorTabs;
            _editorTabBar.SetTabs(editorTabs.OpenTabs);

            void OnTabClicked(EditorViewModel tab) => editorTabs.ActiveTab = tab;
            void OnTabCloseRequested(EditorViewModel tab) =>
                editorTabs.CloseTabCommand.Execute(tab).Subscribe();

            _editorTabBar.TabClicked += OnTabClicked;
            _editorTabBar.TabCloseRequested += OnTabCloseRequested;

            disposables.Add(Disposable.Create(() =>
            {
                _editorTabBar.TabClicked -= OnTabClicked;
                _editorTabBar.TabCloseRequested -= OnTabCloseRequested;
            }));

            // Unsaved-changes dialog
            disposables.Add(editorTabs.ConfirmClose.RegisterHandler(async ctx =>
            {
                var dialog = new UnsavedDialog { DataContext = ctx.Input };
                var result = await dialog.ShowDialog<bool?>(this);
                ctx.SetOutput(result);
            }));

            // Wire active tab → EditorView + tab bar highlight + welcome text + townhall link
            disposables.Add(this.WhenAnyValue(x => x.ViewModel!.EditorTabs.ActiveTab)
                .Subscribe(active =>
                {
                    _editorView.ViewModel = active;
                    _editorView.IsVisible = active is not null;
                    _editorTabBar.SetActiveTab(active);
                    _editorTabBar.SetTownhallLinkVisible(active is not null);
                    _welcomeText.IsVisible = active is null;
                }));

            // Left panel mode switching: show file tree or SC panel
            disposables.Add(this.WhenAnyValue(x => x.ViewModel!.LeftPanelMode)
                .Subscribe(mode => _ = AnimateLeftPanelModeSwitchAsync(mode)));

            // Ctrl+` toggle bottom panel
            var toggleCmd = ViewModel!.ToggleBottomPanelCommand;
            foreach (var kb in KeyBindings.Where(k => k.Command == toggleCmd).ToList())
                KeyBindings.Remove(kb);

            KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(Key.Oem3, KeyModifiers.Control),
                Command = toggleCmd
            });
            KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(Key.J, KeyModifiers.Control),
                Command = toggleCmd
            });

            // Ctrl+S: save the active tab
            var saveGesture = new KeyGesture(Key.S, KeyModifiers.Control);
            foreach (var kb in KeyBindings.Where(k =>
                k.Gesture?.Key == Key.S && k.Gesture?.KeyModifiers == KeyModifiers.Control).ToList())
                KeyBindings.Remove(kb);

            var saveBinding = new KeyBinding
            {
                Gesture = saveGesture,
                Command = ViewModel!.SaveActiveTabCommand
            };
            KeyBindings.Add(saveBinding);
            disposables.Add(Disposable.Create(() => KeyBindings.Remove(saveBinding)));

            // Bind bottom panel visibility → row height
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
                        _terminalPanel.FocusTerminal();
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

            // Ctrl+O key binding
            var openFolderGesture = new KeyGesture(Key.O, KeyModifiers.Control);
            foreach (var kb in KeyBindings.Where(k =>
                k.Gesture?.Key == Key.O && k.Gesture?.KeyModifiers == KeyModifiers.Control).ToList())
                KeyBindings.Remove(kb);
            var openBinding = new KeyBinding
            {
                Gesture = openFolderGesture,
                Command = ViewModel!.OpenFolderCommand
            };
            KeyBindings.Add(openBinding);
            disposables.Add(Disposable.Create(() => KeyBindings.Remove(openBinding)));
        });
    }

    /// <summary>
    /// Builds the M6 layout: nav bar | left-panel mode slot | townhall | editor.
    /// Bottom panel spans under center + right only. Status bar at the bottom.
    /// </summary>
    private (NavBar navBar, FileTreeView fileTreeView, SourceControlPanel sourceControlPanel, TownhallView townhallView,
             StatusBar statusBar, TerminalPanel terminalPanel, Border bottomPanel, GridSplitter bottomPanelSplitter,
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
        _editorView = new EditorView();
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

        // --- Column 5: Right — Editor (quieter, utility-focused surface) ---
        var editorPanel = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"],
            // M5-allow: M1 introduced the 1px panel seam as a visual divider, not semantic spacing.
            Margin = LayoutTokens.Inset(1, 0, 0, 0),
            Children =
            {
                _editorTabBar,
                _editorView,
                _welcomeText
            }
        };
        Grid.SetRow(_editorTabBar, 0);
        Grid.SetRow(_editorView, 1);
        Grid.SetRow(_welcomeText, 1);
        _welcomeText.IsVisible = true;

        Grid.SetColumn(editorPanel, 5);
        Grid.SetRow(editorPanel, 0);
        grid.Children.Add(editorPanel);

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
        var terminalPanel = new TerminalPanel();
        var bottomPanel = new Border
        {
            Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"],
            Padding = LayoutTokens.NoneThickness,
            // M5-allow: M1 introduced the 1px top seam above the bottom panel to preserve the raised-layer split.
            Margin = LayoutTokens.Inset(0, 1, 0, 0),
            Child = terminalPanel
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
        return (navBar, fileTreeView, sourceControlPanel, townhallView, statusBar,
            terminalPanel, bottomPanel, bottomPanelSplitter, bottomSplitterRow, bottomPanelRow, statusBarRow);
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
