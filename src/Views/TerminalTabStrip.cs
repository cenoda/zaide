using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Zaide.Styles;
using Zaide.ViewModels;

namespace Zaide.Views;

/// <summary>
/// Helper to create <see cref="Cursor"/> instances while gracefully falling
/// back when <c>ICursorFactory</c> is not available (e.g. in test harnesses
/// without the headless platform).
/// </summary>
internal static class CursorHelper
{
    public static Cursor? TryCreateHand()
    {
        try
        {
            return new Cursor(StandardCursorType.Hand);
        }
        catch (InvalidOperationException)
        {
            // ICursorFactory not registered — test environment without
            // headless platform. Return null; the consumer must handle it.
            return null;
        }
    }
}

public class TerminalTabStrip : UserControl
{
    private readonly StackPanel _tabsPanel;
    private readonly Button _newTabButton;
    private readonly IBrush? _activeBrush;
    private readonly IBrush _inactiveBrush = Brushes.Transparent;

    private ITerminalHost? _host;
    private readonly Dictionary<TerminalTabViewModel, Border> _items = new();
    private readonly HashSet<TerminalTabViewModel> _subscribedTabs = new();
    private TerminalTabViewModel? _activeTab;

    public TerminalTabStrip()
    {
        _activeBrush = Application.Current?.Resources["PrimaryAccentBrush"] as IBrush;

        _tabsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingNone,
            VerticalAlignment = VerticalAlignment.Center
        };

        _newTabButton = BuildNewTabButton();
        _newTabButton.Margin = LayoutTokens.Inset(LayoutTokens.SpacingXs, 0, 0, 0);

        var left = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingNone,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _tabsPanel }
        };

        var layout = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Children = { left, _newTabButton }
        };
        Grid.SetColumn(left, 0);
        Grid.SetColumn(_newTabButton, 1);

        var root = new Border
        {
            Background = (IBrush?)Application.Current?.Resources["SurfaceBaseBrush"],
            Padding = LayoutTokens.Inset(LayoutTokens.SpacingSm, 0, LayoutTokens.SpacingXs, 0),
            Child = layout
        };

        Content = root;
    }

    public void SetHost(ITerminalHost host)
    {
        if (_host is not null)
        {
            _host.Tabs.CollectionChanged -= OnTabsChanged;
        }
        UnsubscribeAllTabs();

        _host = host;
        _items.Clear();
        _tabsPanel.Children.Clear();

        _newTabButton.Click -= OnNewTabClick;
        _newTabButton.Click += OnNewTabClick;

        foreach (var tab in host.Tabs)
        {
            AddTab(tab);
        }
        SetActiveTab(host.ActiveTab);

        host.Tabs.CollectionChanged += OnTabsChanged;
    }

    public void SetActiveTab(TerminalTabViewModel? tab)
    {
        if (_activeTab is not null && _items.TryGetValue(_activeTab, out var prev))
        {
            prev.Background = _inactiveBrush;
        }
        _activeTab = tab;
        if (tab is not null && _items.TryGetValue(tab, out var active))
        {
            active.Background = _activeBrush;
        }
    }

    private void OnNewTabClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_host is null) return;
        _host.NewTabCommand.Execute().Subscribe();
    }

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (TerminalTabViewModel tab in e.NewItems)
                AddTab(tab);
        }
        if (e.OldItems is not null)
        {
            foreach (TerminalTabViewModel tab in e.OldItems)
                RemoveTab(tab);
        }
        if (_host is not null)
            SetActiveTab(_host.ActiveTab);
    }

    private void AddTab(TerminalTabViewModel tab)
    {
        if (_items.ContainsKey(tab)) return;
        var item = BuildTabItem(tab);
        _items[tab] = item;
        _tabsPanel.Children.Add(item);
        if (_activeTab == tab)
            item.Background = _activeBrush;
        SubscribeToTab(tab);
    }

    private void RemoveTab(TerminalTabViewModel tab)
    {
        if (_items.TryGetValue(tab, out var item))
        {
            _tabsPanel.Children.Remove(item);
            _items.Remove(tab);
        }
        UnsubscribeTab(tab);
        if (_activeTab == tab) _activeTab = null;
    }

    private void SubscribeToTab(TerminalTabViewModel tab)
    {
        if (!_subscribedTabs.Add(tab)) return;
        if (tab is INotifyPropertyChanged notify)
        {
            notify.PropertyChanged += OnTabPropertyChanged;
        }
    }

    private void UnsubscribeTab(TerminalTabViewModel tab)
    {
        if (!_subscribedTabs.Remove(tab)) return;
        if (tab is INotifyPropertyChanged notify)
        {
            notify.PropertyChanged -= OnTabPropertyChanged;
        }
    }

    private void UnsubscribeAllTabs()
    {
        foreach (var tab in _subscribedTabs)
        {
            if (tab is INotifyPropertyChanged notify)
                notify.PropertyChanged -= OnTabPropertyChanged;
        }
        _subscribedTabs.Clear();
    }

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TerminalTabViewModel tab) return;
        if (e.PropertyName == nameof(TerminalTabViewModel.IsActive) && tab.IsActive)
        {
            SetActiveTab(tab);
        }
    }

    private Border BuildTabItem(TerminalTabViewModel tab)
    {
        var label = new TextBlock
        {
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = LayoutTokens.Horizontal(LayoutTokens.SpacingSm)
        };
        label.DataContext = tab;
        label.Bind(
            TextBlock.TextProperty,
            new Avalonia.Data.Binding(nameof(TerminalTabViewModel.Title))
            {
                FallbackValue = "Terminal"
            });

        var closeIcon = IconFactory.Create(
            "Icon.X",
            (IBrush?)Application.Current?.Resources["TextSecondaryBrush"],
            12);
        var closeButton = new Border
        {
            Width = 18,
            Height = 18,
            Background = Brushes.Transparent,
            CornerRadius = LayoutTokens.RadiusSm,
            Padding = LayoutTokens.Uniform(LayoutTokens.SpacingXxs),
            Child = closeIcon,
            Margin = LayoutTokens.Inset(0, 0, LayoutTokens.SpacingXs, 0),
            Cursor = CursorHelper.TryCreateHand()
        };
        closeButton.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(closeButton).Properties.IsLeftButtonPressed)
            {
                e.Handled = true;
                if (_host is not null)
                    _host.CloseTabCommand.Execute(tab).Subscribe();
            }
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        Grid.SetColumn(label, 0);
        Grid.SetColumn(closeButton, 1);
        grid.Children.Add(label);
        grid.Children.Add(closeButton);

        var border = new Border
        {
            Background = _inactiveBrush,
            Padding = LayoutTokens.Horizontal(LayoutTokens.SpacingXs),
            MinHeight = 28,
            CornerRadius = LayoutTokens.RadiusSm,
            Margin = LayoutTokens.Inset(0, LayoutTokens.SpacingXxs, LayoutTokens.SpacingXxs, LayoutTokens.SpacingXxs),
            Cursor = CursorHelper.TryCreateHand()
        };
        border.Child = grid;
        border.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
            {
                e.Handled = true;
                if (_host is not null)
                    _host.ActivateTabCommand.Execute(tab).Subscribe();
            }
        };

        return border;
    }

    private static Button BuildNewTabButton()
    {
        var plus = IconFactory.Create(
            "Icon.Plus",
            (IBrush?)Application.Current?.Resources["TextSecondaryBrush"],
            14);
        return new Button
        {
            Content = plus,
            Width = 24,
            Height = 24,
            Padding = LayoutTokens.NoneThickness,
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            Cursor = CursorHelper.TryCreateHand()
        };
    }
}
