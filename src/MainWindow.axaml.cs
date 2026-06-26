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
        this.WhenActivated(d =>
        {
            // Wire FileTreeView to its ViewModel
            _fileTreeView.ViewModel = ViewModel!.FileTreeViewModel;

            // Wire EditorTabBar: collection + events (with disposal)
            var editorTabs = ViewModel!.EditorTabs;
            _editorTabBar.SetTabs(editorTabs.OpenTabs);

            void OnTabClicked(EditorViewModel tab) => editorTabs.ActiveTab = tab;
            void OnTabCloseRequested(EditorViewModel tab) =>
                editorTabs.CloseTabCommand.Execute(tab).Subscribe();

            _editorTabBar.TabClicked += OnTabClicked;
            _editorTabBar.TabCloseRequested += OnTabCloseRequested;

            d.Add(Disposable.Create(() =>
            {
                _editorTabBar.TabClicked -= OnTabClicked;
                _editorTabBar.TabCloseRequested -= OnTabCloseRequested;
            }));

            // Wire active tab → EditorView + tab bar highlight + welcome text
            d.Add(this.WhenAnyValue(x => x.ViewModel!.EditorTabs.ActiveTab)
                .Subscribe(active =>
                {
                    Log($"[MainWindow] ActiveTab changed: {active?.FileName ?? "null"}");
                    _editorView.ViewModel = active;
                    _editorTabBar.SetActiveTab(active);
                    _welcomeText.IsVisible = active is null;
                    Log($"[MainWindow] _editorView.ViewModel is now: " +
                        $"{_editorView.ViewModel?.FileName ?? "null"}");
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
                Command = ReactiveUI.ReactiveCommand.Create(
                    () => editorTabs.ActiveTab?.SaveCommand.Execute().Subscribe())
            };
            KeyBindings.Add(saveBinding);
            d.Add(Disposable.Create(() => KeyBindings.Remove(saveBinding)));

            // Welcome text: always shows the static message. StatusText is
            // preserved for a future status bar, not bound to the welcome overlay.

            // Bind bottom panel visibility → row height (instant toggle, no animation per Phase 0)
            d.Add(this.WhenAnyValue(x => x.ViewModel!.IsBottomPanelVisible)
                .Subscribe(visible =>
                {
                    _bottomPanelRow.Height = visible
                        ? new GridLength(250)
                        : new GridLength(0);
                    _bottomPanel.IsVisible = visible;
                }));
        });
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
                new ColumnDefinition { Width = new GridLength(260) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(320) }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = new GridLength(0) }
            },
            Background = new SolidColorBrush(Color.Parse("#1E1E23"))
        };

        var bottomRow = grid.RowDefinitions[1];

        // --- Left Sidebar (Phase 1: FileTreeView) ---
        var sidebar = new FileTreeView();
        Grid.SetColumn(sidebar, 0);
        Grid.SetRow(sidebar, 0);
        grid.Children.Add(sidebar);

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

        Grid.SetColumn(center, 1);
        Grid.SetRow(center, 0);
        grid.Children.Add(center);

        // --- Right Agent Area ---
        var agentArea = BuildPanel("Agent Area", "#142043", 1, 0, 0, 0);
        Grid.SetColumn(agentArea, 2);
        Grid.SetRow(agentArea, 0);
        grid.Children.Add(agentArea);

        // --- Bottom Panel (hidden by default) ---
        var bottomPanel = BuildPanel("Terminal (Ctrl+`)", "#0F1A33", 0, 1, 0, 0);
        bottomPanel.IsVisible = false;
        Grid.SetColumn(bottomPanel, 0);
        Grid.SetColumnSpan(bottomPanel, 3);
        Grid.SetRow(bottomPanel, 1);
        grid.Children.Add(bottomPanel);

        Content = grid;
        return (bottomRow, bottomPanel, sidebar);
    }

    /// <summary>
    /// Creates a placeholder panel Border with Ayaka palette colors + centered label.
    /// Margins create subtle 1px separators per DESIGN.md §5.
    /// </summary>
    private static Border BuildPanel(
        string label,
        string backgroundColor,
        double marginLeft,
        double marginTop,
        double marginRight,
        double marginBottom)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse(backgroundColor)),
            Padding = new Thickness(16),
            Margin = new Thickness(marginLeft, marginTop, marginRight, marginBottom),
            Child = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Color.Parse("#E3E4F4")),
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
    }

    private static void Log(string msg)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss.fff");
        System.IO.File.AppendAllText("/tmp/zaide-debug.log", $"[{ts}] {msg}\n");
    }
}
