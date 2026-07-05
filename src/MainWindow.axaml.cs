using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ReactiveUI;
using ReactiveUI.Avalonia;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Zaide.ViewModels;
using Zaide.Views;

namespace Zaide;

/// <summary>
/// Main application window. Layout built in C# per DESIGN.md §1.
/// Refactor 3 M3: nav bar | left-panel mode slot (Explorer/SC) | townhall | editor.
/// </summary>
public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    private readonly NavBar _navBar;
    private readonly FileTreeView _fileTreeView;
    private readonly Border _sourceControlPlaceholder;
    private readonly TownhallView _townhallView;
    private EditorTabBar _editorTabBar = null!;
    private EditorView _editorView = null!;
    private TextBlock _welcomeText = null!;
    private TerminalPanel _terminalPanel = null!;
    private Border _bottomPanel = null!;
    private GridSplitter _bottomPanelSplitter = null!;
    private readonly RowDefinition _bottomSplitterRow;
    private readonly RowDefinition _bottomPanelRow;

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

        // === Build Layout (M3: nav bar | left slot | townhall | editor) ===
        (_navBar, _fileTreeView, _sourceControlPlaceholder, _townhallView,
         _terminalPanel, _bottomPanel, _bottomPanelSplitter, _bottomSplitterRow, _bottomPanelRow) = BuildLayout();

        // === ReactiveUI Bindings ===
        this.WhenActivated(disposables =>
        {
            // Wire NavBar to ViewModel
            _navBar.ViewModel = ViewModel;

            // Wire TownhallView to its ViewModel
            _townhallView.ViewModel = ViewModel!.TownhallViewModel;

            // Wire FileTreeView to its ViewModel
            _fileTreeView.ViewModel = ViewModel!.FileTreeViewModel;
            _terminalPanel.ViewModel = ViewModel.TerminalViewModel;

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

            // Left panel mode switching: show file tree or SC placeholder
            disposables.Add(this.WhenAnyValue(x => x.ViewModel!.LeftPanelMode)
                .Subscribe(mode =>
                {
                    var isExplorer = mode == LeftPanelMode.Explorer;
                    _fileTreeView.IsVisible = isExplorer;
                    _sourceControlPlaceholder.IsVisible = !isExplorer;
                }));

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
                        _ = ViewModel.TerminalViewModel.EnsureStartedAsync();
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
    /// Builds the M3 layout: nav bar | left-panel mode slot | townhall | editor.
    /// Bottom panel spans under center + right only. Status bar slot reserved.
    /// </summary>
    private (NavBar navBar, FileTreeView fileTreeView, Border scPlaceholder, TownhallView townhallView,
             TerminalPanel terminalPanel, Border bottomPanel, GridSplitter bottomPanelSplitter,
             RowDefinition bottomSplitterRow, RowDefinition bottomPanelRow) BuildLayout()
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
                // 3: Status bar (reserved, 0 height for now — M6)
                new RowDefinition { Height = new GridLength(0) }
            },
            Background = (IBrush?)Application.Current!.Resources["SurfaceBaseBrush"]
        };

        var bottomSplitterRow = grid.RowDefinitions[1];
        var bottomPanelRow = grid.RowDefinitions[2];

        // --- Column 0: Nav Bar ---
        var navBar = new NavBar();
        Grid.SetColumn(navBar, 0);
        Grid.SetRow(navBar, 0);
        Grid.SetRowSpan(navBar, 4); // Full height
        grid.Children.Add(navBar);

        // --- Column 1: Left Panel (Explorer / Source Control) ---
        var fileTreeView = new FileTreeView();
        Grid.SetColumn(fileTreeView, 1);
        Grid.SetRow(fileTreeView, 0);
        grid.Children.Add(fileTreeView);

        var scPlaceholder = new Border
        {
            Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"],
            Child = new TextBlock
            {
                Text = "Source Control",
                Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            },
            IsVisible = false // Hidden by default (Explorer mode)
        };
        Grid.SetColumn(scPlaceholder, 1);
        Grid.SetRow(scPlaceholder, 0);
        grid.Children.Add(scPlaceholder);

        // --- Column 2: Splitter (left panel ↔ townhall) ---
        var leftSplitter = new GridSplitter
        {
            Width = 4,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Grid.SetColumn(leftSplitter, 2);
        Grid.SetRow(leftSplitter, 0);
        grid.Children.Add(leftSplitter);

        // --- Column 3: Center — Townhall (real M3 view) ---
        _editorTabBar = new EditorTabBar();
        _editorView = new EditorView();
        _welcomeText = new TextBlock
        {
            Text = "Open a file to begin",
            Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

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
            // M4: SurfacePanelBrush for quieter right-column feel (not SurfaceBaseBrush).
            // The editor code area still uses SurfaceBaseBrush internally via EditorView,
            // but the outer panel reads as a slightly elevated utility surface.
            Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"],
            Margin = new Thickness(1, 0, 0, 0),
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
            Padding = new Thickness(0),
            Margin = new Thickness(0, 1, 0, 0),
            Child = terminalPanel
        };
        bottomPanel.IsVisible = false;
        Grid.SetColumn(bottomPanel, 3);
        Grid.SetColumnSpan(bottomPanel, 3); // Under center + editor only
        Grid.SetRow(bottomPanel, 2);
        grid.Children.Add(bottomPanel);

        Content = grid;
        return (navBar, fileTreeView, scPlaceholder, townhallView,
                terminalPanel, bottomPanel, bottomPanelSplitter, bottomSplitterRow, bottomPanelRow);
    }
}