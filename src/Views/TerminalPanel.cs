using Avalonia.Input.Platform;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
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

    public TerminalPanel()
    {
        _renderControl = new TerminalRenderControl();
        _renderControl.ContextMenu = BuildContextMenu();
        _renderControl.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        _renderControl.AddHandler(InputElement.TextInputEvent, OnTextInput, RoutingStrategies.Tunnel, handledEventsToo: true);

        _statusText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 12, Foreground = (IBrush?)Application.Current!.Resources["SecondaryAccentBrush"] };
        _clearButton = BuildToolbarButton("Clear");
        _restartButton = BuildToolbarButton("Restart");
        var buttonStrip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { _clearButton, _restartButton } };
        var toolbarGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Children = { _statusText, buttonStrip }
        };
        Grid.SetColumn(_statusText, 0);
        Grid.SetColumn(buttonStrip, 1);
        var toolbar = new Border { Background = (IBrush?)Application.Current!.Resources["SurfaceBaseBrush"], Padding = new Thickness(16, 6, 16, 6), Child = toolbarGrid };
        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            }
        };
        Grid.SetRow(toolbar, 0);
        Grid.SetRow(_renderControl, 1);
        root.Children.Add(toolbar);
        root.Children.Add(_renderControl);
        Content = root;

        this.WhenActivated(d =>
        {
            d.Add(this.OneWayBind(ViewModel, vm => vm.ScreenSnapshot, v => v._renderControl.Snapshot));
            d.Add(this.OneWayBind(ViewModel, vm => vm.CursorRow, v => v._renderControl.CursorRow));
            d.Add(this.OneWayBind(ViewModel, vm => vm.CursorCol, v => v._renderControl.CursorCol));
            d.Add(this.OneWayBind(ViewModel, vm => vm.CursorVisible, v => v._renderControl.CursorVisible));
            d.Add(this.OneWayBind(ViewModel, vm => vm.StatusLabel, v => v._statusText.Text));
            d.Add(this.BindCommand(ViewModel, vm => vm.ClearCommand, v => v._clearButton));
            d.Add(this.BindCommand(ViewModel, vm => vm.RestartCommand, v => v._restartButton));
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
