using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Zaide.ViewModels;
using Zaide.Services;

namespace Zaide.Views;

/// <summary>
/// View-only host for the bottom-panel terminal area. Retains one
/// <see cref="TerminalPanel"/> per <see cref="TerminalTabViewModel"/> so each
/// session keeps its own search query, viewport, selection, search highlight,
/// and log-view toggle. Renders the active tab's panel and exposes
/// <see cref="FocusActiveSession"/> as the view seam for the window to call
/// when the bottom panel is opened or a tab is switched.
///
/// <para>By design this control never lives inside a ViewModel — the retained
/// <c>TerminalPanel</c> cache stays in the view layer only.</para>
/// </summary>
public class TerminalTabHost : UserControl, IDisposable
{
    private readonly TerminalTabStrip _strip;
    private readonly ContentControl _content;
    private readonly Dictionary<TerminalTabViewModel, TerminalPanel> _panels = new();
    private readonly Dictionary<TerminalTabViewModel, IDisposable> _panelSettingsSubscriptions = new();

    private ITerminalHost? _host;
    private TerminalTabViewModel? _activeTab;
    private readonly Func<TerminalTabViewModel, TerminalPanel?> _panelFactory;
    private readonly Func<TerminalTabViewModel, TerminalPanel?, IDisposable?> _subscriptionFactory;
    private bool _disposed;

    public event Action? LastTabCloseRequested;

    public TerminalTabHost(ISettingsService settings)
        : this(
            settings,
            tab => new TerminalPanel(settings),
            (_, panel) => panel is null
                ? null
                : Disposable.Create(panel.DisposeSettingsSubscription))
    {
    }

    internal TerminalTabHost(
        ISettingsService settings,
        Func<TerminalTabViewModel, TerminalPanel?> panelFactory,
        Func<TerminalTabViewModel, TerminalPanel?, IDisposable?> subscriptionFactory)
    {
        _panelFactory = panelFactory;
        _subscriptionFactory = subscriptionFactory;
        _strip = new TerminalTabStrip();
        _content = new ContentControl();
        _strip.LastTabCloseRequested += () => LastTabCloseRequested?.Invoke();

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            Background = (IBrush?)Application.Current?.Resources["SurfacePanelBrush"],
            Children = { _strip, _content }
        };
        Grid.SetRow(_strip, 0);
        Grid.SetRow(_content, 1);

        Content = grid;
    }

    /// <summary>
    /// Read-only view of the retained panel cache. One entry per open tab.
    /// </summary>
    public IReadOnlyDictionary<TerminalTabViewModel, TerminalPanel> Panels => _panels;

    /// <summary>
    /// The panel currently shown in the content area, or null when no tab is
    /// active.
    /// </summary>
    public TerminalPanel? ActivePanel =>
        _activeTab is not null && _panels.TryGetValue(_activeTab, out var panel)
            ? panel
            : null;

    /// <summary>
    /// Binds the host to a terminal host. Replaces any prior binding. The host
    /// is used to subscribe to <c>Tabs</c> and <c>ActiveTab</c> changes; this
    /// view does not call any state-mutating methods on the host directly.
    /// </summary>
    public void SetHost(ITerminalHost host)
    {
        DetachHost();

        _host = host;
        _strip.SetHost(host);

        // Build panels for any tabs that already exist in the host
        foreach (var tab in host.Tabs)
        {
            EnsurePanel(tab);
        }
        ShowActivePanel(host.ActiveTab);

        host.Tabs.CollectionChanged += OnTabsChanged;
        if (host is INotifyPropertyChanged hostNotify2)
        {
            hostNotify2.PropertyChanged += OnHostPropertyChanged;
        }
    }

    /// <summary>
    /// View seam called by <c>MainWindow</c> when the bottom panel is opened.
    /// Forwards to <see cref="TerminalPanel.FocusTerminal"/> on the active tab's
    /// retained panel, keeping focus out of the ViewModel.
    /// </summary>
    public void FocusActiveSession()
    {
        var panel = ActivePanel;
        if (panel is null) return;
        panel.FocusTerminal();
    }

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_host is null) return;

        if (e.OldItems is not null)
        {
            foreach (TerminalTabViewModel tab in e.OldItems)
                RemovePanel(tab);
        }
        if (e.NewItems is not null)
        {
            foreach (TerminalTabViewModel tab in e.NewItems)
                EnsurePanel(tab);
        }
        // The host's close flow may have switched the active tab; reflect that
        ShowActivePanel(_host.ActiveTab);
    }

    private void OnHostPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ITerminalHost.ActiveTab))
        {
            ShowActivePanel(_host?.ActiveTab);
        }
    }

    private void EnsurePanel(TerminalTabViewModel tab)
    {
        if (_panels.ContainsKey(tab)) return;
        // One panel per tab — never rebind a single shared panel across sessions,
        // otherwise per-tab search/viewport/selection/log-view state would smear.
        var panel = _panelFactory(tab);
        if (panel is not null)
        {
            panel.ViewModel = tab.Session;
            _panels[tab] = panel;
        }

        var subscription = _subscriptionFactory(tab, panel);
        if (subscription is not null)
            _panelSettingsSubscriptions[tab] = subscription;
    }

    private void RemovePanel(TerminalTabViewModel tab)
    {
        DisposePanelSettingsSubscription(tab);
        if (!_panels.TryGetValue(tab, out var panel)) return;
        panel.ViewModel = null;
        _panels.Remove(tab);
        if (_activeTab == tab)
        {
            _content.Content = null;
        }
    }

    private void ShowActivePanel(TerminalTabViewModel? tab)
    {
        _activeTab = tab;
        _strip.SetActiveTab(tab);
        if (tab is null)
        {
            _content.Content = null;
            return;
        }
        if (_panels.TryGetValue(tab, out var panel))
        {
            _content.Content = panel;
        }
    }

    public void DetachHost()
    {
        if (_host is not null)
            _host.Tabs.CollectionChanged -= OnTabsChanged;
        if (_host is INotifyPropertyChanged hostNotify)
            hostNotify.PropertyChanged -= OnHostPropertyChanged;

        foreach (var tab in _panelSettingsSubscriptions.Keys.ToList())
            DisposePanelSettingsSubscription(tab);

        foreach (var panel in _panels.Values)
        {
            panel.ViewModel = null;
        }
        _panelSettingsSubscriptions.Clear();
        _panels.Clear();
        _content.Content = null;
        _activeTab = null;
        _host = null;
    }

    private void DisposePanelSettingsSubscription(TerminalTabViewModel tab)
    {
        if (!_panelSettingsSubscriptions.Remove(tab, out var subscription))
            return;
        subscription.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DetachHost();
    }
}
