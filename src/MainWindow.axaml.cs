using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
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
/// Phase 0: 3-panel grid + bottom panel toggle. Phase 1: file tree sidebar.
/// </summary>
public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    private readonly RowDefinition _bottomPanelRow;
    private readonly Border _bottomPanel;
    private readonly FileTreeView _fileTreeView;
    private EditorTabBar _editorTabBar = null!;
    private EditorView _editorView = null!;
    private TextBlock _welcomeText = null!;

    public MainWindow()
    {
        InitializeComponent();

        // === Window Chrome (M5) ===
        Title = "Zaide";
        Width = 1280;
        Height = 800;
        MinWidth = 800;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        // === Build Layout (M2, Phase 2) ===
        (_bottomPanelRow, _bottomPanel, _fileTreeView) = BuildLayout();

        // === ReactiveUI Bindings (M3, Phase 2) ===
        this.WhenActivated(disposables =>
        {
            // Wire FileTreeView to its ViewModel
            _fileTreeView.ViewModel = ViewModel!.FileTreeViewModel;

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

            // M5: unsaved-changes dialog. ViewModel raises ConfirmClose,
            // MainWindow shows UnsavedDialog and feeds the result back.
            disposables.Add(editorTabs.ConfirmClose.RegisterHandler(async ctx =>
            {
                var dialog = new UnsavedDialog { DataContext = ctx.Input };
                var result = await dialog.ShowDialog<bool?>(this);
                ctx.SetOutput(result);
            }));

            // Wire active tab → EditorView + tab bar highlight + welcome text
            disposables.Add(this.WhenAnyValue(x => x.ViewModel!.EditorTabs.ActiveTab)
                .Subscribe(active =>
                {
                    _editorView.ViewModel = active;
                    _editorView.IsVisible = active is not null;
                    _editorTabBar.SetActiveTab(active);
                    _welcomeText.IsVisible = active is null;
                }));

            // Ctrl+` toggle bottom panel. Key.Oem3 is the physical backtick key
            // (to the left of 1 on US layout). OemTilde fails on many non-US
            // keyboard layouts. Ctrl+J is the universal fallback.
            // Guard against duplicates — WhenActivated may fire multiple times.
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

            // Ctrl+S: save the active tab. Placed on MainWindow because
            // AvaloniaEdit's TextEditor intercepts Ctrl+S internally.
            // Guard against duplicates — WhenActivated may fire multiple times.
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

            // Welcome text: always shows the static message. StatusText is
            // preserved for a future status bar, not bound to the welcome overlay.

            // Bind bottom panel visibility → row height (instant toggle, no animation per Phase 0)
            disposables.Add(this.WhenAnyValue(x => x.ViewModel!.IsBottomPanelVisible)
                .Subscribe(visible =>
                {
                    _bottomPanelRow.Height = visible
                        ? new GridLength(250)
                        : new GridLength(0);
                    _bottomPanel.IsVisible = visible;
                }));
        });

        // Add Ctrl+O key binding
        var inputElement = this.GetVisualDescendants().OfType<IInputElement>().FirstOrDefault();
        if (inputElement != null)
        {
            inputElement.KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(Key.O, KeyModifiers.Control),
                Command = ViewModel!.OpenFolderCommand
            });
        }
    }

    /// <summary>
    /// Builds the 3-panel grid layout with bottom panel placeholder.
    /// Left: 260px sidebar | Center: * | Right: 320px agent area.
    /// </summary>
    private (RowDefinition bottomRow, Border bottomPanel, FileTreeView fileTreeView) BuildLayout()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                // Sidebar (Fixed min width 180px, max width 500px) - M2
                new ColumnDefinition { Width = new GridLength(260), MinWidth = 180, MaxWidth = 500 },
                // GridSplitter between Sidebar and Center (4px wide, transparent) - M2
                new ColumnDefinition { Width = new GridLength(4, GridUnitType.Pixel) },
                // Center: Editor + Tab Bar
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                // Right Agent Area (Fixed width 320px)
                new ColumnDefinition { Width = new GridLength(320) }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = new GridLength(0) }
            },
            Background = (IBrush?)Application.Current!.Resources["SurfaceBase"]
        };

        var bottomRow = grid.RowDefinitions[1];

        // --- Left Sidebar (Phase 1: FileTreeView) ---
        var sidebar = new FileTreeView();
        Grid.SetColumn(sidebar, 0);
        Grid.SetRow(sidebar, 0);
        grid.Children.Add(sidebar);

        // --- GridSplitter (M2) ---
        var splitter = new GridSplitter
        {
            Width = 4,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Grid.SetColumn(splitter, 1);
        Grid.SetRow(splitter, 0);
        grid.Children.Add(splitter);

        // --- Center Panel (Phase 2: Editor + Tab Bar) ---
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

        var center = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            Background = (IBrush?)Application.Current!.Resources["DeepBase"],
            Margin = new Thickness(1, 0, 1, 0),
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
        _welcomeText.IsVisible = true; // shown when no tabs are open

        Grid.SetColumn(center, 2); // Center now in column 2 (after splitter)
        Grid.SetRow(center, 0);
        grid.Children.Add(center);

        // --- Right Agent Area ---
        var agentArea = BuildPanel("Agent Area", "DeepBase", 1, 0, 0, 0);
        Grid.SetColumn(agentArea, 3); // Agent area now in column 3 (after center)
        Grid.SetRow(agentArea, 0);
        grid.Children.Add(agentArea);

        // --- Bottom Panel (hidden by default) ---
        var bottomPanel = BuildPanel("Terminal (Ctrl+`)", "PanelDeep", 0, 1, 0, 0);
        bottomPanel.IsVisible = false;
        Grid.SetColumn(bottomPanel, 0);
        Grid.SetColumnSpan(bottomPanel, 4); // Span all columns (sidebar, splitter, center, agent)
        Grid.SetRow(bottomPanel, 1);
        grid.Children.Add(bottomPanel);

        Content = grid;
        return (bottomRow, bottomPanel, sidebar);
    }

    /// <summary>
    /// Creates a placeholder panel Border with themed palette colors + centered label.
    /// Margins create subtle 1px separators per DESIGN.md §5.
    /// </summary>
    private static Border BuildPanel(
        string label,
        string backgroundResourceKey,
        double marginLeft,
        double marginTop,
        double marginRight,
        double marginBottom)
    {
        return new Border
        {
            Background = (IBrush?)Application.Current!.Resources[backgroundResourceKey],
            Padding = new Thickness(16),
            Margin = new Thickness(marginLeft, marginTop, marginRight, marginBottom),
            Child = new TextBlock
            {
                Text = label,
                Foreground = (IBrush?)Application.Current!.Resources["TextActive"],
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
    }

}