using System;
using System.Globalization;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Reactive;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.ViewModels;

namespace Zaide.Views;

/// <summary>
/// Minimal terminal surface for the Phase 3 MVP. Renders raw PTY output in a
/// read-only TextBox and forwards key input directly to the TerminalViewModel.
/// ANSI parsing and cell-based rendering are intentionally out of scope here.
/// </summary>
public class TerminalPanel : ReactiveUserControl<TerminalViewModel>
{
    private static readonly FontFamily TerminalFont =
        new("Cascadia Code, JetBrains Mono, monospace");

    private readonly TextBox _outputTextBox;
    private readonly Button _clearButton;
    private readonly Button _restartButton;
    private readonly TextBlock _statusText;
    private double _cellWidth;

    public TerminalPanel()
    {
        _outputTextBox = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = TerminalFont,
            FontSize = 13,
            LineHeight = 19.5,
            Padding = new Thickness(16),
            Background = Brushes.Transparent,
            Foreground = (IBrush?)Application.Current!.Resources["TextActive"],
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        _outputTextBox.AddHandler(
            InputElement.KeyDownEvent,
            OnKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        _outputTextBox.AddHandler(
            InputElement.TextInputEvent,
            OnTextInput,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        // --- M3: control strip (status + clear + restart) ---
        _statusText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            Foreground = (IBrush?)Application.Current!.Resources["SoftAccent"]
        };

        _clearButton = BuildToolbarButton("Clear");
        _restartButton = BuildToolbarButton("Restart");

        var buttonStrip = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { _clearButton, _restartButton }
        };

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

        var toolbar = new Border
        {
            Background = (IBrush?)Application.Current!.Resources["DeepBase"],
            Padding = new Thickness(16, 6, 16, 6),
            Child = toolbarGrid
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
        Grid.SetRow(_outputTextBox, 1);
        root.Children.Add(toolbar);
        root.Children.Add(_outputTextBox);

        Content = root;

        this.WhenActivated(d =>
        {
            d.Add(this.OneWayBind(ViewModel, vm => vm.OutputText, v => v._outputTextBox.Text));
            d.Add(this.OneWayBind(ViewModel, vm => vm.StatusLabel, v => v._statusText.Text));
            d.Add(this.BindCommand(ViewModel, vm => vm.ClearCommand, v => v._clearButton));
            d.Add(this.BindCommand(ViewModel, vm => vm.RestartCommand, v => v._restartButton));

            d.Add(this.WhenAnyValue(x => x.ViewModel!.OutputText)
                .Subscribe(_ => ScrollToEnd()));

            d.Add(this.GetObservable(IsVisibleProperty)
                .Subscribe(visible =>
                {
                    if (visible)
                        FocusTerminal();
                }));

            // --- M1: Resize wiring ---
            // Measure the monospace character cell width once the control is
            // laid out, then forward viewport size changes (throttled) to the
            // ViewModel so it can update the PTY rows/columns. Observe the
            // output TextBox bounds (not the whole panel) so the toolbar row
            // is excluded from the row calculation.
            d.Add(_outputTextBox.GetObservable(BoundsProperty)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .ObserveOn(AvaloniaScheduler.Instance)
                .Subscribe(_ => ForwardResize()));
        });
    }

    private static Button BuildToolbarButton(string label) => new()
    {
        Content = label,
        FontSize = 12,
        Padding = new Thickness(12, 4, 12, 4),
        Background = (IBrush?)Application.Current!.Resources["SurfacePanel"],
        Foreground = (IBrush?)Application.Current!.Resources["TextActive"],
        BorderThickness = new Thickness(0),
        CornerRadius = new CornerRadius(6),
        Cursor = new Cursor(StandardCursorType.Hand)
    };

    public void FocusTerminal()
    {
        _outputTextBox.Focus();
        _outputTextBox.CaretIndex = _outputTextBox.Text?.Length ?? 0;
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is null) return;

        // Clipboard actions live in the View layer (never the ViewModel) and
        // are deliberately not forwarded to the PTY.
        if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            if (e.Key == Key.C)
            {
                await CopySelectionAsync();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.V)
            {
                await PasteAsync();
                e.Handled = true;
                return;
            }
        }

        byte[]? bytes = TerminalKeyMapper.Map(e.Key, e.KeyModifiers);
        if (bytes is null) return;

        await SendInputAsync(bytes);
        e.Handled = true;
    }

    private async void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (ViewModel is null) return;
        if (string.IsNullOrEmpty(e.Text)) return;

        byte[] bytes = Encoding.UTF8.GetBytes(e.Text);
        await SendInputAsync(bytes);
        e.Handled = true;
    }

    private Task SendInputAsync(byte[] bytes)
    {
        return ViewModel?.SendInputAsync(bytes) ?? Task.CompletedTask;
    }

    /// <summary>
    /// Copies the current TextBox selection to the OS clipboard (Ctrl+Shift+C).
    /// No-op when nothing is selected. Clipboard access stays in the View.
    /// </summary>
    private async Task CopySelectionAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;

        string? selected = _outputTextBox.SelectedText;
        if (!string.IsNullOrEmpty(selected))
            await clipboard.SetTextAsync(selected);
    }

    /// <summary>
    /// Pastes clipboard text into the shell as raw input bytes (Ctrl+Shift+V).
    /// MVP limit: the whole clipboard is written in one call; very large
    /// pastes are not chunked yet (tracked in TOFIX).
    /// </summary>
    private async Task PasteAsync()
    {
        if (ViewModel is null) return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;

        string? text = await clipboard.TryGetTextAsync();
        if (string.IsNullOrEmpty(text)) return;

        await ViewModel.SendInputAsync(Encoding.UTF8.GetBytes(text));
    }

    private void ScrollToEnd()
    {
        _outputTextBox.CaretIndex = _outputTextBox.Text?.Length ?? 0;
        int lineCount = _outputTextBox.GetLineCount();
        if (lineCount > 0)
            _outputTextBox.ScrollToLine(lineCount - 1);
    }

    /// <summary>
    /// Computes the terminal columns/rows from the current bounds and font
    /// metrics, then forwards the result to the ViewModel.
    /// </summary>
    private void ForwardResize()
    {
        if (ViewModel is null) return;

        // Use the output TextBox bounds — the actual terminal viewport — so
        // the toolbar row above it is not counted as shell rows.
        var bounds = _outputTextBox.Bounds;

        double cellWidth = MeasureCellWidth();
        double lineHeight = _outputTextBox.LineHeight;
        var padding = _outputTextBox.Padding;

        var (columns, rows) = TerminalGeometry.Compute(
            bounds.Width, bounds.Height,
            cellWidth, lineHeight,
            padding.Left, padding.Top,
            padding.Right, padding.Bottom);

        ViewModel.Resize(columns, rows);
    }

    /// <summary>
    /// Measures the pixel width of a single monospace character cell using
    /// <see cref="FormattedText"/> glyph advance for the configured terminal
    /// font. The result is cached after the first successful measurement.
    /// </summary>
    private double MeasureCellWidth()
    {
        if (_cellWidth > 0) return _cellWidth;

        var typeface = new Typeface(TerminalFont, FontStyle.Normal, FontWeight.Normal);
        double fontSize = _outputTextBox.FontSize;

        // Measure 'M' — the canonical monospace reference character.
        var ft = new FormattedText(
            "M",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.Black);

        _cellWidth = ft.Width;
        return _cellWidth;
    }
}
