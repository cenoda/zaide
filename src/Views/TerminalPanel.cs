using Avalonia.Input.Platform;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.ViewModels;
using Zaide.UI.DesignSystem;
using Zaide.Services;
using Zaide.Models;

namespace Zaide.Views;

public class TerminalPanel : ReactiveUserControl<TerminalViewModel>
{
    private readonly SettingsBinding _settingsBinding;
    private bool _settingsDisposed;
    private readonly TerminalRenderControl _renderControl;
    private readonly Button _latestButton;
    private readonly Button _clearButton;
    private readonly Button _restartButton;
    private readonly TextBlock _statusText;
    private readonly ListBox _logListBox;
    private readonly Button _toggleViewButton;
    private readonly Grid _contentArea;

    // ── Search (M3) ────────────────────────────────────────────────
    private readonly Button _searchToggleButton;
    private readonly Panel _searchGroup;
    private readonly TextBox _searchBox;
    private readonly Button _searchPrevButton;
    private readonly Button _searchNextButton;
    private readonly TextBlock _searchCount;
    private string _searchQuery = "";
    private TerminalSearchResult? _searchResult;

    public TerminalPanel(ISettingsService settings)
    {
        var resourceDictionary = Application.Current!.Resources;
        _renderControl = new TerminalRenderControl();
        _renderControl.ContextMenu = BuildContextMenu();
        _renderControl.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        _renderControl.AddHandler(InputElement.TextInputEvent, OnTextInput, RoutingStrategies.Tunnel, handledEventsToo: true);

        // ── Header: "Terminal / Logs" ──────────────────────────────
        var headerIcon = IconFactory.Create(
            "Icon.Terminal",
            (IBrush?)resourceDictionary["SecondaryAccentBrush"],
            14);

        var headerText = TextStyles.Header("Terminal / Logs");
        headerText.VerticalAlignment = VerticalAlignment.Center;

        _statusText = TextStyles.Caption("");
        _statusText.VerticalAlignment = VerticalAlignment.Center;

        _toggleViewButton = new Button
        {
            Content = "Logs",
            FontSize = 12,
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs),
            Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"],
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            BorderThickness = new Thickness(0),
            CornerRadius = LayoutTokens.RadiusSm,
            Cursor = new Cursor(StandardCursorType.Hand),
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingSm, 0, 0, 0)
        };

        _clearButton = BuildToolbarButton("Icon.Broom", "Clear");
        _restartButton = BuildToolbarButton("Icon.ArrowClockwise", "Restart");

        // Lightweight "jump to latest" affordance: reuses the render control's
        // existing bottom-follow seam rather than inventing a second mechanism.
        _latestButton = BuildToolbarButton("Icon.ChevronDown", "Latest");

        // ── Search group (M3) ──────────────────────────────────────
        // Toggle reveals a compact search strip: query box, prev/next, count.
        // Kept visually consistent with the toolbar and intentionally narrow.
        _searchToggleButton = BuildToolbarButton("Icon.Search", "Find");

        _searchBox = new TextBox
        {
            Width = 180,
            FontSize = 12,
            PlaceholderText = "Find in terminal",
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingXs, LayoutTokens.SpacingXxs),
            BorderThickness = new Thickness(1),
            CornerRadius = LayoutTokens.RadiusSm,
            VerticalAlignment = VerticalAlignment.Center
        };
        _searchBox.TextChanged += OnSearchTextChanged;
        _searchBox.KeyDown += OnSearchBoxKeyDown;

        _searchPrevButton = BuildSmallButton("Prev");
        _searchNextButton = BuildSmallButton("Next");

        _searchCount = TextStyles.Caption("");
        _searchCount.VerticalAlignment = VerticalAlignment.Center;
        _searchCount.TextAlignment = TextAlignment.Right;
        _searchCount.Width = 44;

        _searchGroup = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingXs,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            Children = { _searchBox, _searchPrevButton, _searchNextButton, _searchCount }
        };

        var leftStrip = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingSm,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { headerIcon, headerText, _statusText }
        };

        var rightStrip = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingXs,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { _toggleViewButton, _searchToggleButton, _latestButton, _clearButton, _restartButton, _searchGroup }
        };

        var toolbarGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Children = { leftStrip, rightStrip }
        };
        Grid.SetColumn(leftStrip, 0);
        Grid.SetColumn(rightStrip, 1);

        var toolbar = new Border
        {
            Background = (IBrush?)Application.Current!.Resources["SurfaceBaseBrush"],
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingLg, LayoutTokens.SpacingSm - LayoutTokens.SpacingXxs),
            Child = toolbarGrid
        };

        // ── Log list view ──────────────────────────────────────────
        _logListBox = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            IsVisible = false
        };

        var res = resourceDictionary;

        // Item template for log entries
        _logListBox.ItemTemplate = new FuncDataTemplate<LogEntry>((entry, _) =>
        {
            if (entry is null) return null;

            // Tag badge color based on category
            var tagColor = entry.Category switch
            {
                LogCategory.Build => (IBrush)res["TextPrimaryBrush"]!,
                LogCategory.Agent => (IBrush)res["SecondaryAccentBrush"]!,
                LogCategory.Log => (IBrush)res["TextSecondaryBrush"]!,
                _ => (IBrush)res["TextSecondaryBrush"]!
            };

            var tagBlock = TextStyles.Caption(entry.Tag);
            tagBlock.Foreground = tagColor;
            tagBlock.Margin = LayoutTokens.Inset(0, 0, LayoutTokens.SpacingXs, 0);

            var contentBlock = TextStyles.Body(entry.Content);
            contentBlock.TextWrapping = TextWrapping.NoWrap;
            contentBlock.TextTrimming = TextTrimming.CharacterEllipsis;

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = LayoutTokens.SpacingXs,
                Children = { tagBlock, contentBlock }
            };

            // Warning icon prefix
            if (entry.HasWarning)
            {
                var warningIcon = IconFactory.Create(
                    "Icon.Warning",
                    (IBrush)res["WarningBrush"]!,
                    14);
                warningIcon.Margin = LayoutTokens.Inset(0, 0, LayoutTokens.SpacingXxs, 0);
                row.Children.Insert(0, warningIcon);
            }

            return row;
        });

        // ── Content area: render control + log list ────────────────
        _contentArea = new Grid
        {
            Children = { _renderControl, _logListBox }
        };

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            }
        };
        Grid.SetRow(toolbar, 0);
        Grid.SetRow(_contentArea, 1);
        root.Children.Add(toolbar);
        root.Children.Add(_contentArea);
        Content = root;

        _settingsBinding = new SettingsBinding(
            settings,
            model => ApplyTerminalSettings(model.Editor));

        // ── Bindings ───────────────────────────────────────────────
        this.WhenActivated(d =>
        {
            d.Add(this.OneWayBind(ViewModel, vm => vm.ScreenSnapshot, v => v._renderControl.Snapshot));
            d.Add(this.OneWayBind(ViewModel, vm => vm.CursorRow, v => v._renderControl.CursorRow));
            d.Add(this.OneWayBind(ViewModel, vm => vm.CursorCol, v => v._renderControl.CursorCol));
            d.Add(this.OneWayBind(ViewModel, vm => vm.CursorVisible, v => v._renderControl.CursorVisible));
            d.Add(this.OneWayBind(ViewModel, vm => vm.IsAlternateScreenActive, v => v._renderControl.IsAlternateScreenActive));
            d.Add(this.OneWayBind(ViewModel, vm => vm.StatusLabel, v => v._statusText.Text));
            d.Add(this.BindCommand(ViewModel, vm => vm.ClearCommand, v => v._clearButton));
            d.Add(this.BindCommand(ViewModel, vm => vm.RestartCommand, v => v._restartButton));

            // Bind log entries to the list box
            d.Add(this.OneWayBind(ViewModel, vm => vm.LogEntries, v => v._logListBox.ItemsSource));

            // Toggle between terminal and log view
            d.Add(this.Bind(ViewModel, vm => vm.IsLogView, v => v._logListBox.IsVisible));
            d.Add(this.WhenAnyValue(x => x.ViewModel!.IsLogView)
                .Subscribe(isLogView =>
                {
                    _renderControl.IsVisible = !isLogView;
                    _toggleViewButton.Content = isLogView ? "Terminal" : "Logs";
                }));

            // Toggle button click
            var toggleSub = Observable.FromEventPattern<RoutedEventArgs>(
                h => _toggleViewButton.Click += h,
                h => _toggleViewButton.Click -= h)
                .Subscribe(_ =>
                {
                    if (ViewModel is not null)
                        ViewModel.IsLogView = !ViewModel.IsLogView;
                });
            d.Add(toggleSub);

            // Jump-to-latest reuses the render control's bottom-follow seam.
            var latestSub = Observable.FromEventPattern<RoutedEventArgs>(
                h => _latestButton.Click += h,
                h => _latestButton.Click -= h)
                .Subscribe(_ => _renderControl.ScrollToBottom(clearSelection: true));
            d.Add(latestSub);

            // ── Search (M3) ─────────────────────────────────────────
            // Toggle reveals/hides the compact search strip and clears state.
            var searchToggleSub = Observable.FromEventPattern<RoutedEventArgs>(
                h => _searchToggleButton.Click += h,
                h => _searchToggleButton.Click -= h)
                .Subscribe(_ => ToggleSearch());
            d.Add(searchToggleSub);

            var prevSub = Observable.FromEventPattern<RoutedEventArgs>(
                h => _searchPrevButton.Click += h,
                h => _searchPrevButton.Click -= h)
                .Subscribe(_ => NavigateSearch(previous: true));
            d.Add(prevSub);

            var nextSub = Observable.FromEventPattern<RoutedEventArgs>(
                h => _searchNextButton.Click += h,
                h => _searchNextButton.Click -= h)
                .Subscribe(_ => NavigateSearch(previous: false));
            d.Add(nextSub);

            // While a full-screen TUI owns the terminal, search must not expose
            // hidden main-buffer content: hide and clear the search strip.
            d.Add(this.WhenAnyValue(x => x.ViewModel!.IsAlternateScreenActive)
                .Subscribe(active =>
                {
                    if (active)
                    {
                        ClearSearch();
                        _searchGroup.IsVisible = false;
                    }
                }));

            // Keep matches fresh as the snapshot changes (new output arrives),
            // but do not yank the viewport when only the buffer advances.
            d.Add(this.WhenAnyValue(x => x.ViewModel!.ScreenSnapshot)
                .Subscribe(_ => RefreshSearch(jumpToActive: false)));

            d.Add(TerminalPanelSubscriptions.SubscribeToRestarted(ViewModel, OnRestarted));
            d.Add(this.GetObservable(IsVisibleProperty).Subscribe(visible => { if (visible) FocusTerminal(); }));
            d.Add(_renderControl.GetObservable(BoundsProperty)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .ObserveOn(AvaloniaScheduler.Instance)
                .Subscribe(_ => ForwardResize()));
        });
    }

    private static Button BuildToolbarButton(string iconKey, string label)
    {
        var resources = Application.Current!.Resources;
        var labelText = TextStyles.Caption(label);
        labelText.VerticalAlignment = VerticalAlignment.Center;

        return new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = LayoutTokens.SpacingSm - LayoutTokens.SpacingXxs,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    IconFactory.Create(iconKey, (IBrush?)resources["TextPrimaryBrush"], 12),
                    labelText
                }
            },
            Padding = LayoutTokens.Inset(LayoutTokens.SpacingSm + LayoutTokens.SpacingXxs, LayoutTokens.SpacingXs, LayoutTokens.SpacingSm + LayoutTokens.SpacingXxs, LayoutTokens.SpacingXs),
            Background = (IBrush?)resources["SurfacePanelBrush"],
            Foreground = (IBrush?)resources["TextPrimaryBrush"],
            BorderThickness = new Thickness(0),
            CornerRadius = LayoutTokens.RadiusSm,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
    }

    /// <summary>Small text-only button used for search prev/next navigation.</summary>
    private static Button BuildSmallButton(string label)
    {
        var resources = Application.Current!.Resources;
        return new Button
        {
            Content = TextStyles.Caption(label),
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs),
            Background = (IBrush?)resources["SurfacePanelBrush"],
            Foreground = (IBrush?)resources["TextPrimaryBrush"],
            BorderThickness = new Thickness(0),
            CornerRadius = LayoutTokens.RadiusSm,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
    }

    private ContextMenu BuildContextMenu()
    {
        var copyItem = new MenuItem { Header = "Copy" };
        copyItem.Click += async (_, _) => await CopySelectionAsync();

        var copyVisibleItem = new MenuItem { Header = "Copy Visible" };
        copyVisibleItem.Click += async (_, _) => await CopyVisibleTextAsync();

        var pasteItem = new MenuItem { Header = "Paste" };
        pasteItem.Click += async (_, _) => await PasteAsync();

        return new ContextMenu
        {
            ItemsSource = new object[]
            {
                copyItem,
                copyVisibleItem,
                pasteItem
            }
        };
    }

    public void FocusTerminal() => _renderControl.Focus();

    private void ApplyTerminalSettings(EditorSettings settings)
    {
        var projection = ProjectTerminalSettings(settings);
        _renderControl.ApplyFontSettings(projection.Family, projection.Size);
        ForwardResize();
    }

    internal static (string Family, int Size) ProjectTerminalSettings(EditorSettings settings) =>
        (settings.TerminalFontFamily, settings.TerminalFontSize);

    public void DisposeSettingsSubscription()
    {
        if (_settingsDisposed) return;
        _settingsDisposed = true;
        _settingsBinding.Dispose();
    }

    // ── Search (M3) ────────────────────────────────────────────────

    private void ToggleSearch()
    {
        bool nowVisible = !_searchGroup.IsVisible;
        _searchGroup.IsVisible = nowVisible;
        if (nowVisible)
        {
            _searchBox.Focus();
        }
        else
        {
            ClearSearch();
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        string text = _searchBox.Text ?? "";
        if (text == _searchQuery)
        {
            // Avoid re-processing a programmatic clear that already ran.
            return;
        }

        _searchQuery = text;
        RefreshSearch(jumpToActive: true);
    }

    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NavigateSearch(previous: false);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ClearSearch();
            FocusTerminal();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Recomputes matches for the current query against the live snapshot.
    /// While <see cref="TerminalViewModel.IsAlternateScreenActive"/> is true the
    /// result is suppressed so no hidden main-buffer content can surface.
    /// </summary>
    private void RefreshSearch(bool jumpToActive)
    {
        if (string.IsNullOrEmpty(_searchQuery) || ViewModel?.ScreenSnapshot is null)
        {
            _searchResult = null;
        }
        else
        {
            _searchResult = TerminalSnapshotSearch.Search(ViewModel.ScreenSnapshot, _searchQuery);
        }

        _renderControl.SearchResult = EffectiveSearchResult();
        UpdateSearchControls();

        if (jumpToActive && _searchResult is { HasMatches: true })
        {
            _renderControl.BringSearchMatchIntoView(_searchResult.ActiveMatch!.Value);
        }
    }

    private void NavigateSearch(bool previous)
    {
        if (_searchResult is not { HasMatches: true })
        {
            return;
        }

        _searchResult = previous ? _searchResult.MoveToPrevious() : _searchResult.MoveToNext();
        _renderControl.SearchResult = EffectiveSearchResult();
        _renderControl.BringSearchMatchIntoView(_searchResult.ActiveMatch!.Value);
        UpdateSearchControls();
    }

    private void ClearSearch()
    {
        _searchQuery = "";
        _searchResult = null;
        _renderControl.SearchResult = null;
        if (_searchBox.Text != "")
        {
            _searchBox.Text = "";
        }

        UpdateSearchControls();
    }

    /// <summary>
    /// The result to hand the renderer: <c>null</c> while a full-screen TUI owns
    /// the terminal, mirroring the renderer's own <c>EffectiveSearchResult</c> gate.
    /// </summary>
    private TerminalSearchResult? EffectiveSearchResult() =>
        (ViewModel?.IsAlternateScreenActive ?? false) ? null : _searchResult;

    private void UpdateSearchControls()
    {
        bool hasMatches = _searchResult is { HasMatches: true };
        _searchPrevButton.IsEnabled = hasMatches;
        _searchNextButton.IsEnabled = hasMatches;

        if (hasMatches)
        {
            _searchCount.Text = $"{_searchResult!.ActiveIndex + 1}/{_searchResult.MatchCount}";
        }
        else
        {
            _searchCount.Text = string.IsNullOrEmpty(_searchQuery) ? "" : "0/0";
        }
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is null) return;
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        if (ctrl && !alt && e.Key == Key.C && _renderControl.TryGetSelectedText(out _))
        {
            await CopySelectionAsync();
            e.Handled = true;
            return;
        }

        if (ctrl && shift && !alt)
        {
            if (e.Key == Key.C) { await CopySelectionAsync(); e.Handled = true; return; }
            if (e.Key == Key.V) { await PasteAsync(); e.Handled = true; return; }
        }

        // Viewport navigation: consume PageUp/PageDown/Home/End so they scroll
        // the terminal surface instead of being forwarded to the PTY. Only when
        // the main buffer is scrollable — during a full-screen TUI the keys
        // must still reach the application, so we fall through to terminal
        // forwarding below and never suppress them here.
        if (!ctrl && !alt && !_renderControl.IsAlternateScreenActive)
        {
            switch (e.Key)
            {
                case Key.PageUp:
                    _renderControl.ScrollPageUp();
                    e.Handled = true;
                    return;
                case Key.PageDown:
                    _renderControl.ScrollPageDown();
                    e.Handled = true;
                    return;
                case Key.Home:
                    _renderControl.ScrollToTop();
                    e.Handled = true;
                    return;
                case Key.End:
                    _renderControl.ScrollToBottom(clearSelection: true);
                    e.Handled = true;
                    return;
            }
        }

        byte[]? bytes = TerminalKeyMapper.Map(e.Key, e.KeyModifiers);
        if (bytes is null) return;
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            _renderControl.ScrollToBottom(clearSelection: true);
        }

        await SendInputAsync(bytes);
        e.Handled = true;
    }

    private async void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (ViewModel is null || string.IsNullOrEmpty(e.Text)) return;
        await SendInputAsync(Encoding.UTF8.GetBytes(e.Text));
        e.Handled = true;
    }

    private Task SendInputAsync(byte[] bytes) => ViewModel?.SendInputAsync(bytes) ?? Task.CompletedTask;

    /// <summary>
    /// Copies the current selection only. Does not fall back to copying the
    /// visible viewport — that ambiguous fallback is now the explicit
    /// "Copy Visible" action so "Copy" never surprises the user with
    /// unselected text.
    /// </summary>
    private async Task CopySelectionAsync()
    {
        if (!_renderControl.TryGetSelectedText(out string? selectedText) || string.IsNullOrEmpty(selectedText))
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;
        await clipboard.SetTextAsync(selectedText);
    }

    /// <summary>
    /// Copies the entire visible viewport, regardless of selection. This is
    /// the deliberate fallback action for when the user wants the on-screen
    /// content rather than a specific selection.
    /// </summary>
    private async Task CopyVisibleTextAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;

        var snapshot = ViewModel?.ScreenSnapshot;
        if (snapshot is null || snapshot.Lines.Count == 0) return;
        await clipboard.SetTextAsync(string.Join("\n", snapshot.Lines));
    }

    private async Task PasteAsync()
    {
        if (ViewModel is null) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;
        string? text = await clipboard.TryGetTextAsync();
        if (string.IsNullOrEmpty(text)) return;
        await ViewModel.PasteAsync(text);
    }

    private void ForwardResize()
    {
        if (ViewModel is null) return;
        double cw = _renderControl.CellWidth;
        double lh = _renderControl.LineHeight;
        if (cw <= 0 || lh <= 0) return;
        var bounds = _renderControl.Bounds;
        var (columns, rows) = TerminalGeometry.Compute(bounds.Width, bounds.Height, cw, lh, 0, 0, 0, 0);
        ViewModel.Resize(columns, rows);
    }

    private void OnRestarted()
    {
        _renderControl.ScrollToBottom(clearSelection: true);
    }
}
