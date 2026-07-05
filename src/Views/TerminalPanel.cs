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

namespace Zaide.Views;

public class TerminalPanel : ReactiveUserControl<TerminalViewModel>
{
    private readonly TerminalRenderControl _renderControl;
    private readonly Button _clearButton;
    private readonly Button _restartButton;
    private readonly TextBlock _statusText;
    private readonly ListBox _logListBox;
    private readonly Button _toggleViewButton;
    private readonly Grid _contentArea;

    public TerminalPanel()
    {
        _renderControl = new TerminalRenderControl();
        _renderControl.ContextMenu = BuildContextMenu();
        _renderControl.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        _renderControl.AddHandler(InputElement.TextInputEvent, OnTextInput, RoutingStrategies.Tunnel, handledEventsToo: true);

        // ── Header: "Terminal / Logs" ──────────────────────────────
        var headerText = new TextBlock
        {
            Text = "Terminal / Logs",
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
            VerticalAlignment = VerticalAlignment.Center
        };

        _statusText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            Foreground = (IBrush?)Application.Current!.Resources["SecondaryAccentBrush"]
        };

        _toggleViewButton = new Button
        {
            Content = "Logs",
            FontSize = 12,
            Padding = new Thickness(8, 2, 8, 2),
            Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"],
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            Cursor = new Cursor(StandardCursorType.Hand),
            Margin = new Thickness(8, 0, 0, 0)
        };

        _clearButton = BuildToolbarButton("Clear");
        _restartButton = BuildToolbarButton("Restart");

        var leftStrip = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { headerText, _statusText }
        };

        var rightStrip = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { _toggleViewButton, _clearButton, _restartButton }
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
            Padding = new Thickness(16, 6, 16, 6),
            Child = toolbarGrid
        };

        // ── Log list view ──────────────────────────────────────────
        _logListBox = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            IsVisible = false
        };

        var res = Application.Current!.Resources;

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

            var tagBlock = new TextBlock
            {
                Text = entry.Tag,
                FontSize = 11,
                Foreground = tagColor,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };

            var contentBlock = new TextBlock
            {
                Text = entry.Content,
                FontSize = 12,
                Foreground = (IBrush)res["TextPrimaryBrush"]!,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Children = { tagBlock, contentBlock }
            };

            // Warning icon prefix
            if (entry.HasWarning)
            {
                var warningIcon = IconFactory.Create(
                    "Icon.Warning",
                    (IBrush)res["WarningBrush"]!,
                    14);
                warningIcon.Margin = new Thickness(0, 0, 2, 0);
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

        // ── Bindings ───────────────────────────────────────────────
        this.WhenActivated(d =>
        {
            d.Add(this.OneWayBind(ViewModel, vm => vm.ScreenSnapshot, v => v._renderControl.Snapshot));
            d.Add(this.OneWayBind(ViewModel, vm => vm.CursorRow, v => v._renderControl.CursorRow));
            d.Add(this.OneWayBind(ViewModel, vm => vm.CursorCol, v => v._renderControl.CursorCol));
            d.Add(this.OneWayBind(ViewModel, vm => vm.CursorVisible, v => v._renderControl.CursorVisible));
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

            if (ViewModel != null)
            {
                ViewModel.Restarted += OnRestarted;
                d.Add(Disposable.Create(() => ViewModel.Restarted -= OnRestarted));
            }
            d.Add(this.GetObservable(IsVisibleProperty).Subscribe(visible => { if (visible) FocusTerminal(); }));
            d.Add(_renderControl.GetObservable(BoundsProperty)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .ObserveOn(AvaloniaScheduler.Instance)
                .Subscribe(_ => ForwardResize()));
        });
    }

    private static Button BuildToolbarButton(string label) => new()
    {
        Content = label, FontSize = 12, Padding = new Thickness(12, 4, 12, 4),
        Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"],
        Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
        BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(6),
        Cursor = new Cursor(StandardCursorType.Hand)
    };

    private ContextMenu BuildContextMenu()
    {
        var copyItem = new MenuItem { Header = "Copy" };
        copyItem.Click += async (_, _) => await CopyAllVisibleTextAsync();

        var pasteItem = new MenuItem { Header = "Paste" };
        pasteItem.Click += async (_, _) => await PasteAsync();

        return new ContextMenu
        {
            ItemsSource = new object[]
            {
                copyItem,
                pasteItem
            }
        };
    }

    public void FocusTerminal() => _renderControl.Focus();

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is null) return;
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        if (ctrl && !alt && e.Key == Key.C && _renderControl.TryGetSelectedText(out _))
        {
            await CopyAllVisibleTextAsync();
            e.Handled = true;
            return;
        }

        if (ctrl && shift && !alt)
        {
            if (e.Key == Key.C) { await CopyAllVisibleTextAsync(); e.Handled = true; return; }
            if (e.Key == Key.V) { await PasteAsync(); e.Handled = true; return; }
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

    private async Task CopyAllVisibleTextAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;
        if (_renderControl.TryGetSelectedText(out string? selectedText))
        {
            await clipboard.SetTextAsync(selectedText);
            return;
        }

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
