using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Zaide.UI.DesignSystem;

namespace Zaide.App.Shell;
/// <summary>
/// Command Palette overlay control. Handles Avalonia-specific concerns:
/// control construction, focus management, key event routing, and entry
/// list rendering. All query/filter/order/availability logic lives in
/// <see cref="CommandPaletteViewModel"/>.
/// </summary>
public sealed class CommandPaletteOverlay : UserControl
{
    private readonly CommandPaletteViewModel _viewModel;
    private readonly TextBox _searchBox;
    private readonly StackPanel _entriesPanel;
    private readonly ScrollViewer _scrollViewer;
    private readonly Border _popupBorder;
    private readonly TextBlock _emptyText;
    private readonly List<Border> _itemBorders = new();

    /// <summary>Raised when the overlay should be dismissed (Escape, execution, backdrop click).</summary>
    public event Action? Dismissed;

    public CommandPaletteOverlay(CommandPaletteViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        _searchBox = new TextBox
        {
            PlaceholderText = "Type a command...",
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        _emptyText = TextStyles.Caption("No matching commands");
        _emptyText.Margin = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingSm);
        _emptyText.IsVisible = false;

        _entriesPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
        };

        _scrollViewer = new ScrollViewer
        {
            Content = _entriesPanel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 300,
        };

        var contentPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = LayoutTokens.SpacingXs,
            Children = { _searchBox, _emptyText, _scrollViewer }
        };

        _popupBorder = new Border
        {
            Background = ResolveBrush("SurfacePanelBrush", Color.Parse("#1E1E2E")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3A3A4A")),
            BorderThickness = new Thickness(1),
            CornerRadius = LayoutTokens.RadiusMd,
            Padding = LayoutTokens.Uniform(LayoutTokens.SpacingSm),
            Child = contentPanel,
            Width = 480,
            MaxHeight = 400,
        };

        var backdrop = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
            Child = new Grid
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = LayoutTokens.Symmetric(0, 80),
                Children = { _popupBorder }
            }
        };

        Content = backdrop;
        IsVisible = false;

        _searchBox.TextChanged += OnSearchTextChanged;
        _searchBox.AddHandler(KeyDownEvent, OnSearchBoxKeyDown, RoutingStrategies.Tunnel);
        backdrop.PointerPressed += OnBackdropPointerPressed;
    }

    /// <summary>Shows the overlay and focuses the search box.</summary>
    public void Show()
    {
        _searchBox.Text = string.Empty;
        IsVisible = true;
        RebuildEntries();
        _searchBox.Focus();
    }

    /// <summary>Hides the overlay.</summary>
    public void Hide()
    {
        IsVisible = false;
    }

    /// <summary>The search box control, exposed for focus management by the host.</summary>
    public TextBox SearchBox => _searchBox;

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _viewModel.SetQuery(_searchBox.Text);
        RebuildEntries();
    }

    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                _viewModel.MoveDown();
                UpdateSelectionVisual();
                e.Handled = true;
                break;

            case Key.Up:
                _viewModel.MoveUp();
                UpdateSelectionVisual();
                e.Handled = true;
                break;

            case Key.Enter:
                if (_viewModel.ExecuteSelected())
                    Dismissed?.Invoke();
                e.Handled = true;
                break;

            case Key.Escape:
                Dismissed?.Invoke();
                e.Handled = true;
                break;
        }
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_popupBorder.IsPointerOver)
            Dismissed?.Invoke();
    }

    private void RebuildEntries()
    {
        _entriesPanel.Children.Clear();
        _itemBorders.Clear();

        var entries = _viewModel.FilteredEntries;
        _emptyText.IsVisible = entries.Count == 0;
        _scrollViewer.IsVisible = entries.Count > 0;

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var isSelected = i == _viewModel.SelectedIndex;
            var isAvailable = entry.IsAvailable;

            var nameText = TextStyles.Body(entry.DisplayName);
            if (!isAvailable)
                nameText.Foreground = new SolidColorBrush(Color.Parse("#555566"));

            var categoryText = TextStyles.Caption(entry.Category);
            categoryText.VerticalAlignment = VerticalAlignment.Center;

            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };
            Grid.SetColumn(nameText, 0);
            Grid.SetColumn(categoryText, 1);
            row.Children.Add(nameText);
            row.Children.Add(categoryText);

            var border = new Border
            {
                Child = row,
                Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs),
                CornerRadius = LayoutTokens.RadiusSm,
                Background = isSelected
                    ? new SolidColorBrush(Color.FromArgb(60, 194, 194, 229))
                    : Brushes.Transparent,
                Tag = i,
            };

            border.PointerPressed += OnEntryPointerPressed;

            _entriesPanel.Children.Add(border);
            _itemBorders.Add(border);
        }
    }

    private void UpdateSelectionVisual()
    {
        var selectedIndex = _viewModel.SelectedIndex;
        var selectedBrush = new SolidColorBrush(Color.FromArgb(60, 194, 194, 229));

        for (var i = 0; i < _itemBorders.Count; i++)
        {
            _itemBorders[i].Background = i == selectedIndex
                ? selectedBrush
                : Brushes.Transparent;
        }

        if (selectedIndex >= 0 && selectedIndex < _itemBorders.Count)
        {
            _itemBorders[selectedIndex].BringIntoView();
        }
    }

    private void OnEntryPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border || border.Tag is not int index)
            return;

        var entries = _viewModel.FilteredEntries;
        if (index < 0 || index >= entries.Count)
            return;

        if (!entries[index].IsAvailable)
            return;

        if (_viewModel.ExecuteSelected())
            Dismissed?.Invoke();
    }

    private static IBrush ResolveBrush(string resourceKey, Color fallback)
    {
        try
        {
            if (Application.Current?.Resources.TryGetValue(resourceKey, out var value) == true &&
                value is IBrush brush)
                return brush;
        }
        catch (InvalidOperationException)
        {
            // Unit tests may share an Application on another dispatcher thread.
        }

        return new SolidColorBrush(fallback);
    }
}
