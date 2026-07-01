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
/// Refactor-3: agent-first layout with nav + mode-switched left panel + townhall + editor.
/// </summary>
public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    private readonly RowDefinition _bottomSplitterRow;
    private readonly RowDefinition _bottomPanelRow;
    private readonly GridSplitter _bottomPanelSplitter;
    private readonly Border _bottomPanel;

    private readonly NavBar _navBar;
    private readonly FileTreeView _fileTreeView;
    private readonly SourceControlPlaceholder _sourceControlPlaceholder;
    private readonly TownhallView _townhallView;
    private readonly TerminalPanel _terminalPanel;

    private EditorTabBar _editorTabBar = null!;
    private EditorView _editorView = null!;
    private TextBlock _welcomeText = null!;

    public MainWindow()
    {
        InitializeComponent();

        Title = "Zaide";
        Width = 1280;
        Height = 800;
        MinWidth = 960;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        (_bottomSplitterRow, _bottomPanelRow, _bottomPanelSplitter, _bottomPanel, _navBar, _fileTreeView, _sourceControlPlaceholder, _townhallView, _terminalPanel) = BuildLayout();

        this.WhenActivated(disposables =>
        {
            _fileTreeView.ViewModel = ViewModel!.FileTreeViewModel;
            _terminalPanel.ViewModel = ViewModel.TerminalViewModel;
            _townhallView.ViewModel = ViewModel.TownhallViewModel;
            _navBar.ActiveMode = ViewModel.ActiveLeftPanelMode;

            ViewModel.Activate();
            disposables.Add(Disposable.Create(() => ViewModel!.Dispose()));

            var editorTabs = ViewModel.EditorTabs;
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

            disposables.Add(editorTabs.ConfirmClose.RegisterHandler(async ctx =>
            {
                var dialog = new UnsavedDialog { DataContext = ctx.Input };
                var result = await dialog.ShowDialog<bool?>(this);
                ctx.SetOutput(result);
            }));

            disposables.Add(this.WhenAnyValue(x => x.ViewModel!.EditorTabs.ActiveTab)
                .Subscribe(active =>
                {
                    _editorView.ViewModel = active;
                    _editorView.IsVisible = active is not null;
                    _editorTabBar.SetActiveTab(active);
                    _welcomeText.IsVisible = active is null;
                }));

            void ApplyLeftPanelMode(LeftPanelMode mode)
            {
                _navBar.ActiveMode = mode;
                _fileTreeView.IsVisible = mode == LeftPanelMode.Explorer;
                _sourceControlPlaceholder.IsVisible = mode == LeftPanelMode.SourceControl;
            }

            disposables.Add(this.WhenAnyValue(x => x.ViewModel!.ActiveLeftPanelMode)
                .Subscribe(ApplyLeftPanelMode));

            void OnModeChanged(LeftPanelMode mode) => ViewModel.ActiveLeftPanelMode = mode;
            _navBar.ModeChanged += OnModeChanged;
            disposables.Add(Disposable.Create(() => _navBar.ModeChanged -= OnModeChanged));

            var toggleCmd = ViewModel.ToggleBottomPanelCommand;
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

            var saveGesture = new KeyGesture(Key.S, KeyModifiers.Control);
            foreach (var kb in KeyBindings.Where(k =>
                k.Gesture?.Key == Key.S && k.Gesture?.KeyModifiers == KeyModifiers.Control).ToList())
                KeyBindings.Remove(kb);

            var saveBinding = new KeyBinding
            {
                Gesture = saveGesture,
                Command = ViewModel.SaveActiveTabCommand
            };
            KeyBindings.Add(saveBinding);
            disposables.Add(Disposable.Create(() => KeyBindings.Remove(saveBinding)));

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

            disposables.Add(ViewModel.PickFolder.RegisterHandler(async ctx =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel is null) { ctx.SetOutput(null); return; }
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
                    new FolderPickerOpenOptions { AllowMultiple = false });
                ctx.SetOutput(folders.Count > 0 ? folders[0].Path.LocalPath : null);
            }));

            var openFolderGesture = new KeyGesture(Key.O, KeyModifiers.Control);
            foreach (var kb in KeyBindings.Where(k =>
                k.Gesture?.Key == Key.O && k.Gesture?.KeyModifiers == KeyModifiers.Control).ToList())
                KeyBindings.Remove(kb);

            var openBinding = new KeyBinding
            {
                Gesture = openFolderGesture,
                Command = ViewModel.OpenFolderCommand
            };
            KeyBindings.Add(openBinding);
            disposables.Add(Disposable.Create(() => KeyBindings.Remove(openBinding)));

            ApplyLeftPanelMode(ViewModel.ActiveLeftPanelMode);
        });
    }

    private (RowDefinition bottomSplitterRow, RowDefinition bottomRow, GridSplitter bottomPanelSplitter, Border bottomPanel, NavBar navBar, FileTreeView fileTreeView, SourceControlPlaceholder sourceControlPlaceholder, TownhallView townhallView, TerminalPanel terminalPanel) BuildLayout()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(44), MinWidth = 44, MaxWidth = 44 }, // Nav
                new ColumnDefinition { Width = new GridLength(260), MinWidth = 220, MaxWidth = 360 }, // Left panel
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 300 }, // Townhall
                new ColumnDefinition { Width = new GridLength(420), MinWidth = 320, MaxWidth = 700 } // Editor
            },
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = new GridLength(0) },
                new RowDefinition { Height = new GridLength(0) }
            },
            Background = (IBrush?)Application.Current!.Resources["DeepBase"]
        };

        var bottomSplitterRow = grid.RowDefinitions[1];
        var bottomRow = grid.RowDefinitions[2];

        var navBar = new NavBar();
        Grid.SetColumn(navBar, 0);
        Grid.SetRow(navBar, 0);
        grid.Children.Add(navBar);

        var fileTreeView = new FileTreeView();
        Grid.SetColumn(fileTreeView, 1);
        Grid.SetRow(fileTreeView, 0);
        grid.Children.Add(fileTreeView);

        var sourceControlPlaceholder = new SourceControlPlaceholder
        {
            IsVisible = false
        };
        Grid.SetColumn(sourceControlPlaceholder, 1);
        Grid.SetRow(sourceControlPlaceholder, 0);
        grid.Children.Add(sourceControlPlaceholder);

        var townhallView = new TownhallView();
        Grid.SetColumn(townhallView, 2);
        Grid.SetRow(townhallView, 0);
        grid.Children.Add(townhallView);

        _editorTabBar = new EditorTabBar();
        _editorView = new EditorView();
        _welcomeText = new TextBlock
        {
            Text = "Open a file to begin",
            Foreground = (IBrush?)Application.Current!.Resources["TextActive"],
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var editorArea = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            Background = (IBrush?)Application.Current!.Resources["GlassBase"],
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

        Grid.SetColumn(editorArea, 3);
        Grid.SetRow(editorArea, 0);
        grid.Children.Add(editorArea);

        var bottomPanelSplitter = new GridSplitter
        {
            Height = 4,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeNorthSouth),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Rows,
            IsVisible = false
        };
        Grid.SetColumn(bottomPanelSplitter, 0);
        Grid.SetColumnSpan(bottomPanelSplitter, 4);
        Grid.SetRow(bottomPanelSplitter, 1);
        grid.Children.Add(bottomPanelSplitter);

        var terminalPanel = new TerminalPanel();
        var bottomPanel = new Border
        {
            Background = (IBrush?)Application.Current!.Resources["GlassBase"],
            Padding = new Thickness(0),
            Child = terminalPanel,
            IsVisible = false
        };
        Grid.SetColumn(bottomPanel, 0);
        Grid.SetColumnSpan(bottomPanel, 4);
        Grid.SetRow(bottomPanel, 2);
        grid.Children.Add(bottomPanel);

        Content = grid;
        return (bottomSplitterRow, bottomRow, bottomPanelSplitter, bottomPanel, navBar, fileTreeView, sourceControlPlaceholder, townhallView, terminalPanel);
    }
}
