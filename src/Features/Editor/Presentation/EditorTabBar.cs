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
using Zaide.UI.DesignSystem;
using Zaide.App.Shell;
using Zaide.Features.Workspace.Presentation;

namespace Zaide.Features.Editor.Presentation;

/// <summary>
/// Horizontal tab bar for the editor. Shows open tabs with file names,
/// hover-to-reveal close buttons, active-tab highlighting, and
/// pointer-driven tab reordering.
///
/// Phase 9 M5b interaction contract:
///
/// <list type="bullet">
///   <item><description><b>Drag threshold:</b> 8 device-independent pixels of
///     horizontal pointer movement before a drag begins.</description></item>
///   <item><description><b>Drop position:</b> Pointer X relative to the
///     target tab's center. Before center = drop before that tab; at or after
///     center = drop after. Drops before the first tab or after the last tab
///     work naturally. The dragged tab's own bounds are excluded from hit
///     testing.</description></item>
///   <item><description><b>Click vs drag:</b> TabClicked fires on
///     PointerReleased only when the pointer has NOT crossed the drag
///     threshold. Crossing the threshold enters drag mode; TabMoveRequested
///     fires on release instead.</description></item>
///   <item><description><b>Close button:</b> PointerPressed on the close
///     glyph sets Handled=true, preventing drag initiation on the parent
///     border.</description></item>
///   <item><description><b>Active tab:</b> The same object reference remains
///     active after reorder — only its index in OpenTabs changes. Visual
///     highlighting is preserved because Border.Background is set directly
///     on the control, not derived from its position.</description></item>
///   <item><description><b>Dirty state / display name:</b> These are
///     ViewModel properties unaffected by collection reorder. The bound
///     TextBlock updates automatically.</description></item>
///   <item><description><b>Scroll:</b> Existing horizontal wheel scrolling
///     is preserved. Coordinates are evaluated relative to _tabsPanel, which
///     accounts for scroll offset. Drops after manual scroll are correct.
///     </description></item>
///   <item><description><b>Lifecycle:</b> CollectionChanged handles Move
///     by removing and reinserting the visual at the correct index. Active
///     drag is cancelled on remove/reset/detach. Pointer handlers and hover
///     subscriptions are cleaned up per-tab and on DisposeAllSubscriptions.
///     </description></item>
///   <item><description><b>Escape cancellation:</b> Pressing Escape during a
///     drag fires a window-level KeyDown handler attached in PointerPressed
///     and removed in CancelDrag / cleanup. This works regardless of which
///     control has keyboard focus.</description></item>
/// </list>
/// </summary>
public partial class EditorTabBar : UserControl
{
    // ── Drag-reorder constants ───────────────────────────────────────────

    /// <summary>
    /// Minimum horizontal pointer movement (in DIPs) to begin a tab drag.
    /// Values below this threshold are treated as a click.
    /// </summary>
    private const double DragThreshold = 8.0;

    // ── Fields ───────────────────────────────────────────────────────────

    private readonly ScrollViewer _scrollViewer;
    private readonly StackPanel _tabsPanel;
    private readonly TextBlock _townhallLink;
    private readonly Dictionary<EditorViewModel, Border> _tabItems = new();
    private readonly Dictionary<EditorViewModel, IDisposable> _hoverSubscriptions = new();
    private readonly Dictionary<EditorViewModel, CancellationTokenSource> _hoverCts = new();

    private ObservableCollection<EditorViewModel>? _tabs;
    private EditorViewModel? _activeTab;

    // Drag-reorder state
    private bool _isDragging;
    private Point _dragStartPoint;
    private EditorViewModel? _draggedTab;
    private Border? _draggedBorder;
    private readonly Border _dropIndicator;
    private double _draggedTabOriginalOpacity;

    // Escape-key drag cancellation: stores an Action that removes the
    // window-level KeyDown handler. The closure captures the exact TopLevel
    // instance from subscription time, so removal works even after the
    // control has been detached from the visual tree.
    private Action? _unsubscribeEscapeAction;

    // Test seam: exposes whether the Escape handler is currently subscribed.
    internal bool IsEscapeSubscribed => _unsubscribeEscapeAction is not null;

    // Test seam: allows tests to inject a fake unsubscribe action so they
    // can verify cleanup is invoked. Not used in production.
    internal Action? TestOnly_UnsubscribeEscapeAction
    {
        set => _unsubscribeEscapeAction = value;
    }

    // Brushes resolved from app resources in constructor (not static —
    // Application.Current may be null at type-init time in test harnesses).
    private readonly IBrush? _activeTabBrush;
    private readonly IBrush? _inactiveTabBrush = Brushes.Transparent;

    // ── Events ───────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the user clicks a tab's close button. M3 subscribes to
    /// route this to EditorTabViewModel.CloseTabCommand.
    /// </summary>
    public event Action<EditorViewModel>? TabCloseRequested;

    /// <summary>
    /// Raised when the user clicks a tab (press + release without drag).
    /// M3 subscribes to set ActiveTab.
    /// </summary>
    public event Action<EditorViewModel>? TabClicked;

    /// <summary>
    /// Phase 9 M5b: Raised when the user drags a tab to a new position.
    /// Args: (viewModel, fromIndex, toIndex).
    /// The ViewModel's <c>MoveTab</c> method handles the reorder.
    /// </summary>
    public event Action<EditorViewModel, int, int>? TabMoveRequested;

    // ── Constructor ──────────────────────────────────────────────────────

    public EditorTabBar()
    {
        _tabsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingNone
        };

        // Resolve brushes now — safe because the constructor only runs when
        // the control is created, which happens inside a running application.
        _activeTabBrush = Application.Current?.Resources["PrimaryAccentBrush"] as IBrush;

        // Phase 9 M5b: drop-position visual indicator.
        // A thin vertical line shown at the target insertion point during drag.
        _dropIndicator = new Border
        {
            Width = 2,
            Background = _activeTabBrush,
            IsVisible = false,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsHitTestVisible = false
        };

        // Wrap tabs panel and drop indicator in a single-cell Grid so the
        // indicator can overlay the tabs at arbitrary positions.
        var tabsContainer = new Grid();
        tabsContainer.Children.Add(_tabsPanel);
        tabsContainer.Children.Add(_dropIndicator);

        _scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = tabsContainer,
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
        _townhallLink = TextStyles.Body("Shared in #townhall");
        _townhallLink.Foreground = (IBrush?)Application.Current?.Resources["SecondaryAccentBrush"];
        _townhallLink.VerticalAlignment = VerticalAlignment.Center;
        _townhallLink.Margin = LayoutTokens.Horizontal(LayoutTokens.SpacingMd);
        _townhallLink.IsVisible = false;

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

    // ── Public API ───────────────────────────────────────────────────────

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
    /// Subscribes to CollectionChanged to add/remove/move tab visuals dynamically.
    /// </summary>
    public void SetTabs(ObservableCollection<EditorViewModel> tabs)
    {
        CancelDrag();

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
        {
            prevBorder.Background = _inactiveTabBrush;
            Animations.Transition(prevBorder, Animations.TabFadeOut());
        }

        _activeTab = tab;

        if (tab is not null && _tabItems.TryGetValue(tab, out var activeBorder))
        {
            activeBorder.Background = _activeTabBrush;
            activeBorder.Opacity = 0.72;
            Animations.Transition(activeBorder, Animations.TabFadeIn());
        }
    }

    // ── Collection change handling ───────────────────────────────────────

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
        else if (e.Action == NotifyCollectionChangedAction.Move && e.NewItems is not null)
        {
            // Phase 9 M5b: reconcile visual order with collection order.
            // Move the visual control to match the collection's new index.
            var vm = (EditorViewModel)e.NewItems[0]!;
            if (_tabItems.TryGetValue(vm, out var border))
            {
                _tabsPanel.Children.Remove(border);
                _tabsPanel.Children.Insert(e.NewStartingIndex, border);
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            CancelDrag();
            _tabsPanel.Children.Clear();
            DisposeAllSubscriptions();
            _tabItems.Clear();
        }
    }

    // ── Tab item lifecycle ───────────────────────────────────────────────

    private void AddTab(EditorViewModel vm)
    {
        var tabBorder = BuildTabItem(vm);
        _tabItems[vm] = tabBorder;
        _tabsPanel.Children.Add(tabBorder);
    }

    private void RemoveTab(EditorViewModel vm)
    {
        // Cancel active drag if the dragged tab is being removed.
        if (ReferenceEquals(_draggedTab, vm))
            CancelDrag();

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

    // ── Drag reorder implementation ──────────────────────────────────────

    /// <summary>
    /// Computes the target index in <see cref="_tabs"/> for a drop at the
    /// given pointer position. The dragged tab's own visual is excluded from
    /// the hit test so the tab cannot be dropped "onto itself."
    /// </summary>
    /// <returns>
    /// The index in the collection where the dragged tab should be placed.
    /// Returns 0 for pointer before the first non-dragged tab's center, and
    /// <c>_tabs.Count - 1</c> for pointer after the last.
    /// </returns>
    private int ComputeDropIndex(Point positionInTabsPanel, Border? draggedVisual)
    {
        int visualIndex = 0;
        foreach (var child in _tabsPanel.Children)
        {
            if (child == draggedVisual) continue;
            if (child is not Border border || !border.IsVisible) continue;

            var bounds = border.Bounds;
            double centerX = bounds.X + bounds.Width / 2;

            if (positionInTabsPanel.X < centerX)
                return visualIndex;

            visualIndex++;
        }

        // Past the last non-dragged tab — insert at end.
        return visualIndex;
    }

    /// <summary>
    /// Shows the drop-position indicator at the computed insertion point.
    /// </summary>
    private void UpdateDropIndicator(Point positionInTabsPanel)
    {
        if (_draggedBorder is null)
        {
            _dropIndicator.IsVisible = false;
            return;
        }

        var index = ComputeDropIndex(positionInTabsPanel, _draggedBorder);
        double xPos = ComputeIndicatorX(index);

        _dropIndicator.Margin = new Thickness(xPos, 0, 0, 0);
        _dropIndicator.IsVisible = true;
    }

    /// <summary>
    /// Computes the X position (relative to the tabs container) for the drop
    /// indicator at the given visual insertion index.
    /// </summary>
    private double ComputeIndicatorX(int insertIndex)
    {
        int nonSkipped = 0;
        foreach (var child in _tabsPanel.Children)
        {
            if (child == _draggedBorder) continue;
            if (child is not Border border || !border.IsVisible) continue;

            if (nonSkipped == insertIndex)
                return border.Bounds.X;

            nonSkipped++;
        }

        // After last — position at the right edge of the last visible tab.
        for (int i = _tabsPanel.Children.Count - 1; i >= 0; i--)
        {
            var child = _tabsPanel.Children[i];
            if (child == _draggedBorder) continue;
            if (child is Border lastBorder && lastBorder.IsVisible)
                return lastBorder.Bounds.Right;
        }

        return 0;
    }

    /// <summary>
    /// Hides the drop-position indicator.
    /// </summary>
    private void HideDropIndicator()
    {
        _dropIndicator.IsVisible = false;
    }

    /// <summary>
    /// Cancels an in-progress drag, restoring visual state and releasing
    /// the dragged tab reference. Safe to call when no drag is active.
    /// </summary>
    private void CancelDrag()
    {
        if (_isDragging)
        {
            HideDropIndicator();
            if (_draggedBorder is not null)
                _draggedBorder.Opacity = _draggedTabOriginalOpacity;
        }

        _isDragging = false;
        _draggedTab = null;
        _draggedBorder = null;

        UnsubscribeEscape();
    }

    /// <summary>
    /// Subscribes to the top-level window's <c>KeyDown</c> event so that
    /// pressing Escape during a drag cancels the drag and restores all
    /// visual state. The subscription lives only while a drag is active.
    ///
    /// <para>Stores an unsubscribe <c>Action</c> (closure) that captures
    /// the exact <see cref="TopLevel"/> instance. This guarantees removal
    /// works even after the control is detached from the visual tree — no
    /// fresh <c>TopLevel.GetTopLevel</c> lookup is needed.</para>
    /// </summary>
    internal void SubscribeEscape()
    {
        UnsubscribeEscape(); // no-op if already clean

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        EventHandler<KeyEventArgs> handler = (_, args) =>
        {
            if (args.Key == Key.Escape && _isDragging)
            {
                CancelDrag();
                args.Handled = true;
            }
        };

        topLevel.KeyDown += handler;
        _unsubscribeEscapeAction = () => topLevel.KeyDown -= handler;
    }

    /// <summary>
    /// Removes the Escape-key subscription from the top-level window that
    /// was stored at subscription time. Uses the captured closure, not a
    /// fresh <c>TopLevel.GetTopLevel</c> lookup, so it works even after
    /// the control has been detached from the visual tree.
    ///
    /// Safe to call when no subscription exists (no-op).
    /// Idempotent — calling multiple times has no effect.
    /// </summary>
    internal void UnsubscribeEscape()
    {
        _unsubscribeEscapeAction?.Invoke();
        _unsubscribeEscapeAction = null;
    }

    /// <summary>
    /// Handles PointerPressed on a tab border. Records the start position
    /// and captures the pointer so subsequent PointerMoved/PointerReleased
    /// events are received even when the pointer leaves the tab bounds.
    /// Does NOT fire TabClicked — that is deferred to PointerReleased
    /// so click and drag can be separated.
    /// </summary>
    private void OnTabPointerPressed(EditorViewModel vm, Border border, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_tabsPanel).Properties.IsLeftButtonPressed)
            return;

        _dragStartPoint = e.GetPosition(_tabsPanel);
        _draggedTab = vm;
        _draggedBorder = border;
        _isDragging = false;
        _draggedTabOriginalOpacity = border.Opacity;

        e.Pointer.Capture(border);
        e.Handled = true;

        // Subscribe to window-level Escape key so drag can be cancelled
        // via keyboard even when focus is elsewhere (e.g. the editor).
        SubscribeEscape();
    }

    /// <summary>
    /// Handles PointerMoved on a tab border. Checks whether the pointer has
    /// crossed the drag threshold. Once the threshold is crossed, enters drag
    /// mode and shows the drop-position indicator.
    /// </summary>
    private void OnTabPointerMoved(Border border, PointerEventArgs e)
    {
        if (_draggedTab is null || _draggedBorder is null)
            return;

        if (_isDragging)
        {
            // Already dragging — update the drop indicator position.
            var pos = e.GetPosition(_tabsPanel);
            UpdateDropIndicator(pos);
            return;
        }

        var currentPos = e.GetPosition(_tabsPanel);
        var deltaX = currentPos.X - _dragStartPoint.X;

        if (Math.Abs(deltaX) >= DragThreshold)
        {
            // Crossed the drag threshold — enter drag mode.
            _isDragging = true;
            _draggedBorder.Opacity = 0.5;
            UpdateDropIndicator(currentPos);
        }
    }

    /// <summary>
    /// Handles PointerReleased on a tab border.
    ///
    /// If a drag was in progress, calculates the drop target index and fires
    /// <see cref="TabMoveRequested"/> so the ViewModel can reorder.
    ///
    /// If no drag occurred (the pointer never crossed the threshold), fires
    /// <see cref="TabClicked"/> for normal tab activation.
    ///
    /// <para><b>IMPORTANT:</b> Local state (wasDragging, draggedTab, etc.)
    /// must be saved at the top of this method before any side effects.
    /// Releasing pointer capture or calling TabMoveRequested can trigger
    /// PointerCaptureLost or CollectionChanged handlers that modify visual
    /// tree and call CancelDrag(), which would reset instance fields and
    /// cause the wrong branch to be taken (e.g. TabClicked with null tab,
    /// setting ActiveTab = null).</para>
    /// </summary>
    private void OnTabPointerReleased(PointerEventArgs e)
    {
        if (_draggedTab is null)
            return;

        // ── 1. Save local state before any side effects ────────────────
        // PointerCaptureLost can fire synchronously during Capture(null)
        // or when MoveTab modifies the visual tree. CancelDrag() would
        // reset _isDragging and _draggedTab, causing us to take the wrong
        // branch below. Save what we need now.
        var wasDragging = _isDragging;
        var draggedTab = _draggedTab;
        var draggedBorder = _draggedBorder;

        // ── 2. Reset instance fields immediately ───────────────────────
        _isDragging = false;
        _draggedTab = null;
        _draggedBorder = null;

        // ── 3. Release capture (may fire PointerCaptureLost → CancelDrag,
        //       but our instance fields are already reset so it's a no-op) ─
        e.Pointer.Capture(null);

        // ── 3b. Clean up Escape subscription. CancelDrag() normally does
        //       this, but we bypass CancelDrag here to avoid reentrancy
        //       issues (instance fields cleared in step 2). Clean up
        //       the subscription ourselves. ──────────────────────────────
        UnsubscribeEscape();

        if (wasDragging)
        {
            // ── 4a. Drag: compute drop target and fire reorder ─────────
            HideDropIndicator();
            if (draggedBorder is not null)
                draggedBorder.Opacity = _draggedTabOriginalOpacity;

            var currentPos = e.GetPosition(_tabsPanel);
            var targetIndex = ComputeDropIndex(currentPos, draggedBorder);
            var fromIndex = _tabs is not null ? _tabs.IndexOf(draggedTab) : -1;

            if (fromIndex >= 0 && fromIndex != targetIndex)
            {
                TabMoveRequested?.Invoke(draggedTab, fromIndex, targetIndex);
            }
        }
        else
        {
            // ── 4b. Click: fire tab activation ─────────────────────────
            TabClicked?.Invoke(draggedTab);
        }
    }

    /// <summary>
    /// Handles PointerCaptureLost on a tab border. Cancels any active drag
    /// and restores visual state. This fires when capture is stolen by
    /// another element or the pointer leaves the window.
    /// </summary>
    private void OnTabPointerCaptureLost()
    {
        // Only cancel if we were dragging — pointer capture can be lost
        // for other transient reasons during a normal click cycle.
        if (!_isDragging && _draggedTab is null)
            return;

        CancelDrag();
    }

    // ── Tab item builder ─────────────────────────────────────────────────

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
            Margin = LayoutTokens.Horizontal(LayoutTokens.SpacingSm),
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
            Margin = LayoutTokens.Inset(0, 0, LayoutTokens.SpacingXs, 0),
            Background = Brushes.Transparent,
            CornerRadius = LayoutTokens.RadiusSm,
            Cursor = new Cursor(StandardCursorType.Hand),
            Opacity = 0,
            Padding = LayoutTokens.Uniform(LayoutTokens.SpacingXs),
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
            Padding = LayoutTokens.NoneThickness,
            MinHeight = 36,
            Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        // Phase 9 M5b: Pointer-driven drag reorder.
        // PointerPressed records the start position and captures the pointer.
        // TabClicked fires on PointerReleased only if no drag occurred.
        // Pointer move across DragThreshold enters drag mode.
        var localVm = vm; // capture for closure
        var localBorder = border;

        border.PointerPressed += (_, e) =>
            OnTabPointerPressed(localVm, localBorder, e);

        border.PointerMoved += (_, e) =>
            OnTabPointerMoved(localBorder, e);

        border.PointerReleased += (_, e) =>
            OnTabPointerReleased(e);

        border.PointerCaptureLost += (_, _) =>
            OnTabPointerCaptureLost();

        // ── Hover delay for close button reveal ──────────────────────────

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

    // ── Cleanup ──────────────────────────────────────────────────────────

    /// <summary>
    /// Cancels any active drag and unsubscribes from the collection when
    /// the control is removed from the visual tree. Prevents stale
    /// subscriptions if the tab bar becomes short-lived.
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CancelDrag();

        if (_tabs is not null)
        {
            _tabs.CollectionChanged -= OnTabsChanged;
            _tabs = null;
        }
    }
}
