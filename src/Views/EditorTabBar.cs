using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Zaide.ViewModels;

namespace Zaide.Views;

/// <summary>
/// Horizontal tab bar for the editor. Shows open tabs with file names,
/// hover-to-reveal close buttons, and active-tab highlighting.
/// 
/// M2: UI shell only — builds tab visuals from an ObservableCollection.
/// M3: EditorTabViewModel wires up tab switching and close commands.
/// M4: Dirty indicator (●) added to tab labels.
/// 
/// NOTE: This is a plain UserControl, not ReactiveUserControl&lt;T&gt;.
/// The tab bar receives its data via SetTabs() + events rather than
/// ViewModel = ... binding. This is deliberate — the collection is
/// driven by EditorTabViewModel's ObservableCollection, and the tab
/// bar only needs to mirror it visually. M3 will wire up the events
/// to EditorTabViewModel commands. No need for a separate ViewModel
/// for the tab bar itself.
/// </summary>
public partial class EditorTabBar : UserControl
{
    private readonly ScrollViewer _scrollViewer;
    private readonly StackPanel _tabsPanel;
    private readonly TextBlock _townhallLink;
    private readonly Dictionary<EditorViewModel, Border> _tabItems = new();
    private readonly Dictionary<EditorViewModel, IDisposable> _hoverSubscriptions = new();
    private readonly Dictionary<EditorViewModel, CancellationTokenSource> _hoverCts = new();

    private ObservableCollection<EditorViewModel>? _tabs;
    private EditorViewModel? _activeTab;

    // Brushes resolved from app resources in constructor (not static —
    // Application.Current may be null at type-init time in test harnesses).
    private readonly IBrush? _activeTabBrush;
    private readonly IBrush? _inactiveTabBrush = Brushes.Transparent;

    /// <summary>
    /// Raised when the user clicks a tab's close button. M3 subscribes to
    /// route this to EditorTabViewModel.CloseTabCommand.
    /// </summary>
    public event Action<EditorViewModel>? TabCloseRequested;

    /// <summary>
    /// Raised when the user clicks a tab. M3 subscribes to set ActiveTab.
    /// </summary>
    public event Action<EditorViewModel>? TabClicked;

    public EditorTabBar()
    {
        _tabsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 0
        };

        // Resolve brushes now — safe because the constructor only runs when
        // the control is created, which happens inside a running application.
        _activeTabBrush = Application.Current?.Resources["PrimaryAccentBrush"] as IBrush;

        _scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _tabsPanel,
            MinHeight = 36 // content-driven; MinHeight avoids HiDPI clipping
        };

        // Translate vertical mouse wheel → horizontal scrolling.
        // By default ScrollViewer only scrolls vertically on wheel events.
        _scrollViewer.PointerWheelChanged += (_, e) =>
        {
            // Multiply by 50 px/notch — Linux wheel deltas are tiny otherwise.
            var delta = e.Delta.Y * 50;
            _scrollViewer.Offset = new Vector(
                _scrollViewer.Offset.X - delta, _scrollViewer.Offset.Y);
            e.Handled = true;
        };

        // M4: "Shared in #townhall" label — right of tabs, SecondaryAccentBrush,
        // hidden by default. Visibility controlled by MainWindow via SetTownhallLinkVisible.
        _townhallLink = new TextBlock
        {
            Text = "Shared in #townhall",
            Foreground = (IBrush?)Application.Current?.Resources["SecondaryAccentBrush"],
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 12, 0),
            IsVisible = false
        };

        // Layout: tabs scroll on the left, link label on the right.
        var layout = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        Grid.SetColumn(_scrollViewer, 0);
        Grid.SetColumn(_townhallLink, 1);
        layout.Children.Add(_scrollViewer);
        layout.Children.Add(_townhallLink);

        Content = layout;
    }

    /// <summary>
    /// Shows or hides the "Shared in #townhall" link label.
    /// M4: visible only when a tab is open.
    /// </summary>
    public void SetTownhallLinkVisible(bool visible)
    {
        _townhallLink.IsVisible = visible;
    }

    /// <summary>
    /// Binds the tab bar to an observable collection of editor ViewModels.
    /// Subscribes to CollectionChanged to add/remove tab visuals dynamically.
    /// 
    /// NOTE: The CollectionChanged subscription lives until SetTabs is called
    /// again with a different collection or null. If the tab bar is removed
    /// from the visual tree without cleanup, the subscription persists.
    /// For Zaide's current architecture the tab bar lives for the app's
    /// lifetime, so this is acceptable. If the tab bar ever becomes
    /// short-lived, add a DetachedFromVisualTree override to unsubscribe.
    /// </summary>
    public void SetTabs(ObservableCollection<EditorViewModel> tabs)
    {
        if (_tabs is not null)
            _tabs.CollectionChanged -= OnTabsChanged;

        _tabs = tabs;
        _tabs.CollectionChanged += OnTabsChanged;

        _tabsPanel.Children.Clear();
        DisposeAllSubscriptions();
        _tabItems.Clear();

        foreach (var vm in tabs)
            AddTab(vm);
    }

    /// <summary>
    /// Highlights the active tab. Clears highlight from the previous active tab.
    /// </summary>
    public void SetActiveTab(EditorViewModel? tab)
    {
        if (_activeTab is not null && _tabItems.TryGetValue(_activeTab, out var prevBorder))
            prevBorder.Background = _inactiveTabBrush;

        _activeTab = tab;

        if (tab is not null && _tabItems.TryGetValue(tab, out var activeBorder))
            activeBorder.Background = _activeTabBrush;
    }

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
        {
            foreach (EditorViewModel vm in e.NewItems)
                AddTab(vm);
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems is not null)
        {
            foreach (EditorViewModel vm in e.OldItems)
                RemoveTab(vm);
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            _tabsPanel.Children.Clear();
            DisposeAllSubscriptions();
            _tabItems.Clear();
        }
    }

    private void AddTab(EditorViewModel vm)
    {
        var tabBorder = BuildTabItem(vm);
        _tabItems[vm] = tabBorder;
        _tabsPanel.Children.Add(tabBorder);
    }

    private void RemoveTab(EditorViewModel vm)
    {
        if (_tabItems.TryGetValue(vm, out var border))
        {
            _tabsPanel.Children.Remove(border);
            _tabItems.Remove(vm);
        }

        if (_hoverSubscriptions.TryGetValue(vm, out var subscription))
        {
            subscription.Dispose();
            _hoverSubscriptions.Remove(vm);
        }

        if (_hoverCts.TryGetValue(vm, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _hoverCts.Remove(vm);
        }
    }

    private void DisposeAllSubscriptions()
    {
        foreach (var sub in _hoverSubscriptions.Values)
            sub.Dispose();
        _hoverSubscriptions.Clear();

        foreach (var cts in _hoverCts.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _hoverCts.Clear();
    }

    private Border BuildTabItem(EditorViewModel vm)
    {
        var icon = IconFactory.Create(
            FileIconKeyResolver.GetIconKey(vm.FilePath),
            (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            12);

        var label = new TextBlock
        {
            [!TextBlock.TextProperty] = new Avalonia.Data.Binding("DisplayName"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0),
            MaxWidth = 200
        };
        label.DataContext = vm;

        var closeGlyph = IconFactory.Create(
            "Icon.X",
            (IBrush?)Application.Current!.Resources["SecondaryAccentBrush"],
            12);

        var closeButton = new Border
        {
            Width = 20,
            Height = 20,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(4),
            Cursor = new Cursor(StandardCursorType.Hand),
            Opacity = 0,
            Padding = new Thickness(4),
            Child = closeGlyph
        };

        // Deliberate 7% white hover overlay per M0.5 per-component assignments
        // (see IMPLEMENTATION_PLAN.md Nav Bar → Hover overlay). Not a palette token
        // because it's a transparent blend, not a solid color.
        closeButton.PointerEntered += (_, _) =>
            closeButton.Background = new SolidColorBrush(Color.Parse("#12FFFFFF"));
        closeButton.PointerExited += (_, _) => closeButton.Background = Brushes.Transparent;
        closeButton.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(closeButton).Properties.IsLeftButtonPressed)
            {
                e.Handled = true;
                TabCloseRequested?.Invoke(vm);
            }
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        Grid.SetColumn(icon, 0);
        Grid.SetColumn(label, 1);
        Grid.SetColumn(closeButton, 2);
        grid.Children.Add(icon);
        grid.Children.Add(label);
        grid.Children.Add(closeButton);

        var border = new Border
        {
            Child = grid,
            Background = _inactiveTabBrush,
            BorderBrush = (IBrush?)Application.Current!.Resources["SurfaceBaseBrush"],
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(0),
            MinHeight = 36,
            Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        border.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                TabClicked?.Invoke(vm);
        };

        var hoverCts = new CancellationTokenSource();
        _hoverCts[vm] = hoverCts;

        _hoverSubscriptions[vm] = border.GetObservable(InputElement.IsPointerOverProperty)
            .Subscribe(isPointerOver =>
        {
            if (isPointerOver)
            {
                // Cancel any pending hide, show immediately
                hoverCts.Cancel();
                hoverCts.Dispose();
                hoverCts = new CancellationTokenSource();
                _hoverCts[vm] = hoverCts;
                closeButton.Opacity = 1;
                return;
            }

            // Delay hide — cancel old token, start new 200ms delay
            hoverCts.Cancel();
            hoverCts.Dispose();
            hoverCts = new CancellationTokenSource();
            _hoverCts[vm] = hoverCts;
            var token = hoverCts.Token;

            _ = Task.Delay(200, token).ContinueWith(t =>
            {
                if (t.IsCanceled) return;
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (!border.IsPointerOver)
                        closeButton.Opacity = 0;
                });
            }, TaskContinuationOptions.NotOnCanceled);
        });

        return border;
    }
}
