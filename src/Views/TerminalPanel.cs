using Avalonia.Input.Platform;
using System;
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
        _renderControl.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        _renderControl.AddHandler(InputElement.TextInputEvent, OnTextInput, RoutingStrategies.Tunnel, handledEventsToo: true);

        _statusText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 12, Foreground = (IBrush?)Application.Current!.Resources["SoftAccent"] };
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
        var toolbar = new Border { Background = (IBrush?)Application.Current!.Resources["DeepBase"], Padding = new Thickness(16, 6, 16, 6), Child = toolbarGrid };
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
        Background = (IBrush?)Application.Current!.Resources["SurfacePanel"],
        Foreground = (IBrush?)Application.Current!.Resources["TextActive"],
        BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(6),
        Cursor = new Cursor(StandardCursorType.Hand)
    };

    public void FocusTerminal() => _renderControl.Focus();

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is null) return;
        if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            if (e.Key == Key.C) { await CopyAllVisibleTextAsync(); e.Handled = true; return; }
            if (e.Key == Key.V) { await PasteAsync(); e.Handled = true; return; }
        }
        byte[]? bytes = TerminalKeyMapper.Map(e.Key, e.KeyModifiers);
        if (bytes is null) return;
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
        await ViewModel.SendInputAsync(Encoding.UTF8.GetBytes(text));
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
}
