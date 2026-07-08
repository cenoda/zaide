using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Interactivity;
using Zaide.Models;
using Zaide.Styles;
using Zaide.ViewModels;

namespace Zaide.Views;

/// <summary>
/// View-only host for the agent panel area. Retains one <see cref="AgentPanelView"/>
/// per <see cref="AgentPanelState"/> and renders the active panel's view.
///
/// By design this control never lives inside a ViewModel — the retained
/// <c>AgentPanelView</c> cache stays in the view layer only.
///
/// M2: Exposes <see cref="PanelSendRequested"/> event that bubbles from
/// individual <see cref="AgentPanelView.SendRequested"/> events.
/// </summary>
public sealed class AgentPanelHostView : UserControl
{
    private readonly StackPanel _tabsPanel;
    private readonly Button _newPanelButton;
    private readonly ContentControl _content;
    private readonly Dictionary<AgentPanelState, AgentPanelView> _panels = new();
    private readonly Dictionary<AgentPanelState, Border> _tabItems = new();

    private IAgentPanelHost? _host;
    private AgentPanelState? _activePanel;
    private readonly IBrush? _activeBrush;
    private readonly IBrush _inactiveBrush = Brushes.Transparent;

    /// <summary>
    /// Raised when the user requests to send a message from a panel.
    /// Payload is (panelId, messageText).
    /// </summary>
    public event Action<string, string>? PanelSendRequested;

    public AgentPanelHostView()
    {
        _activeBrush = Application.Current?.Resources["PrimaryAccentBrush"] as IBrush;

        // --- Tab strip ---
        _tabsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingNone,
            VerticalAlignment = VerticalAlignment.Center
        };

        _newPanelButton = BuildNewPanelButton();
        _newPanelButton.Margin = LayoutTokens.Inset(LayoutTokens.SpacingXs, 0, 0, 0);

        var leftStrip = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingNone,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _tabsPanel }
        };

        var stripGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Children = { leftStrip, _newPanelButton }
        };
        Grid.SetColumn(leftStrip, 0);
        Grid.SetColumn(_newPanelButton, 1);

        var stripBorder = new Border
        {
            Background = (IBrush?)Application.Current?.Resources["SurfaceBaseBrush"],
            Padding = LayoutTokens.Inset(LayoutTokens.SpacingSm, 0, LayoutTokens.SpacingXs, 0),
            Child = stripGrid
        };

        // --- Content area ---
        _content = new ContentControl();

        // --- Root layout ---
        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            Children = { stripBorder, _content }
        };
        Grid.SetRow(stripBorder, 0);
        Grid.SetRow(_content, 1);

        Content = grid;
    }

    /// <summary>
    /// Binds the view to an <see cref="IAgentPanelHost"/>. Replaces any prior
    /// binding.
    /// </summary>
    public void SetHost(IAgentPanelHost host)
    {
        if (_host is not null)
        {
            _host.Panels.CollectionChanged -= OnPanelsChanged;
        }
        if (_host is INotifyPropertyChanged oldNotify)
        {
            oldNotify.PropertyChanged -= OnHostPropertyChanged;
        }

        // Detach existing panels
        foreach (var panel in _panels.Values)
        {
            panel.ViewModel = null;
        }
        _panels.Clear();
        _tabItems.Clear();
        _tabsPanel.Children.Clear();
        _content.Content = null;
        _activePanel = null;

        _host = host;

        // Subscribe to new-panel button
        _newPanelButton.Click -= OnNewPanelClick;
        _newPanelButton.Click += OnNewPanelClick;

        // Build panels for any existing panels
        foreach (var panel in host.Panels)
        {
            AddPanel(panel);
        }
        ShowActivePanel(host.ActivePanel);

        host.Panels.CollectionChanged += OnPanelsChanged;
        if (host is INotifyPropertyChanged hostNotify)
        {
            hostNotify.PropertyChanged += OnHostPropertyChanged;
        }
    }

    private void OnNewPanelClick(object? sender, RoutedEventArgs e)
    {
        if (_host is null) return;
        _host.CreatePanel();
    }

    private void OnPanelsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (AgentPanelState panel in e.NewItems)
                AddPanel(panel);
        }
        if (e.OldItems is not null)
        {
            foreach (AgentPanelState panel in e.OldItems)
                RemovePanel(panel);
        }
        if (_host is not null)
            ShowActivePanel(_host.ActivePanel);
    }

    private void OnHostPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IAgentPanelHost.ActivePanel))
        {
            ShowActivePanel(_host?.ActivePanel);
        }
    }

    private void AddPanel(AgentPanelState panel)
    {
        if (_panels.ContainsKey(panel)) return;

        var view = new AgentPanelView { ViewModel = panel };
        view.SendRequested += OnPanelViewSendRequested;
        _panels[panel] = view;

        var tabItem = BuildTabItem(panel);
        _tabItems[panel] = tabItem;
        _tabsPanel.Children.Add(tabItem);

        if (panel == _activePanel)
            tabItem.Background = _activeBrush;
    }

    private void RemovePanel(AgentPanelState panel)
    {
        if (_panels.TryGetValue(panel, out var view))
        {
            view.SendRequested -= OnPanelViewSendRequested;
            view.ViewModel = null;
            _panels.Remove(panel);
        }
        if (_tabItems.TryGetValue(panel, out var tabItem))
        {
            _tabsPanel.Children.Remove(tabItem);
            _tabItems.Remove(panel);
        }
        if (_activePanel == panel)
        {
            _content.Content = null;
            _activePanel = null;
        }
    }

    private void ShowActivePanel(AgentPanelState? panel)
    {
        // Update tab highlighting
        if (_activePanel is not null && _tabItems.TryGetValue(_activePanel, out var prev))
        {
            prev.Background = _inactiveBrush;
        }
        _activePanel = panel;
        if (panel is not null && _tabItems.TryGetValue(panel, out var active))
        {
            active.Background = _activeBrush;
        }

        // Show active panel content
        if (panel is not null && _panels.TryGetValue(panel, out var view))
        {
            _content.Content = view;
        }
        else
        {
            _content.Content = null;
        }
    }

    private Border BuildTabItem(AgentPanelState panel)
    {
        var label = new TextBlock
        {
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = LayoutTokens.Horizontal(LayoutTokens.SpacingSm)
        };
        label.DataContext = panel;
        label.Bind(
            TextBlock.TextProperty,
            new Avalonia.Data.Binding(nameof(AgentPanelState.AgentName))
            {
                FallbackValue = "Agent"
            });

        var border = new Border
        {
            Background = _inactiveBrush,
            Padding = LayoutTokens.Horizontal(LayoutTokens.SpacingXs),
            MinHeight = 28,
            CornerRadius = LayoutTokens.RadiusSm,
            Margin = LayoutTokens.Inset(0, LayoutTokens.SpacingXxs, LayoutTokens.SpacingXxs, LayoutTokens.SpacingXxs),
            Cursor = TryCreateHandCursor(),
            Child = label
        };
        border.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
            {
                e.Handled = true;
                if (_host is not null)
                    _host.ActivatePanel(panel.PanelId);
            }
        };

        return border;
    }

    private void OnPanelViewSendRequested(string panelId, string message)
    {
        PanelSendRequested?.Invoke(panelId, message);
    }

    private static Button BuildNewPanelButton()
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
            Cursor = TryCreateHandCursor()
        };
    }

    private static Cursor? TryCreateHandCursor()
    {
        try
        {
            return new Cursor(StandardCursorType.Hand);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
