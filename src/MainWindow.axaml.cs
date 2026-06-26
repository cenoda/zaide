using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI;
using ReactiveUI.Avalonia;
using System;
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
    private TextBlock _centerText = null!;

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

        // === Build Layout (M2) ===
        (_bottomPanelRow, _bottomPanel, _fileTreeView, _centerText) = BuildLayout();

        // === ReactiveUI Bindings (M3, M4) ===
        this.WhenActivated(d =>
        {
            // Wire FileTreeView to its ViewModel
            _fileTreeView.ViewModel = ViewModel!.FileTreeViewModel;

            // Ctrl+` key binding → ViewModel's toggle command
            KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(Key.OemTilde, KeyModifiers.Control),
                Command = ViewModel!.ToggleBottomPanelCommand
            });

            // Bind StatusText to center panel
            d.Add(this.OneWayBind(ViewModel, vm => vm.StatusText, v => v._centerText.Text));

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
    private (RowDefinition bottomRow, Border bottomPanel, FileTreeView fileTreeView, TextBlock centerText) BuildLayout()
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

        // --- Center Panel (Phase 1 M4) ---
        _centerText = new TextBlock
        {
            Text = "Open a folder to begin",
            Foreground = (IBrush?)Application.Current!.Resources["TextActive"],
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var center = new Border
        {
            Background = (IBrush?)Application.Current!.Resources["DeepBase"],
            Padding = new Thickness(16),
            Margin = new Thickness(1, 0, 1, 0),
            Child = _centerText
        };
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
        return (bottomRow, bottomPanel, sidebar, _centerText);
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
}
