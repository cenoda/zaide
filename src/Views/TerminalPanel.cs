using System;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
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

        _outputTextBox.KeyDown += OnKeyDown;
        _outputTextBox.TextInput += OnTextInput;

        Content = _outputTextBox;

        this.WhenActivated(d =>
        {
            d.Add(this.OneWayBind(ViewModel, vm => vm.OutputText, v => v._outputTextBox.Text));

            d.Add(this.WhenAnyValue(x => x.ViewModel!.OutputText)
                .Subscribe(_ => ScrollToEnd()));

            d.Add(this.GetObservable(IsVisibleProperty)
                .Subscribe(visible =>
                {
                    if (visible)
                        FocusTerminal();
                }));
        });
    }

    public void FocusTerminal()
    {
        _outputTextBox.Focus();
        _outputTextBox.CaretIndex = _outputTextBox.Text?.Length ?? 0;
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is null) return;

        byte[]? bytes = e.Key switch
        {
            Key.Enter => [(byte)'\r'],
            Key.Back => [0x7F],
            Key.Tab => [0x09],
            Key.Left => "\x1B[D"u8.ToArray(),
            Key.Right => "\x1B[C"u8.ToArray(),
            Key.Up => "\x1B[A"u8.ToArray(),
            Key.Down => "\x1B[B"u8.ToArray(),
            _ when e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.C => [0x03],
            _ when e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.D => [0x04],
            _ => null
        };

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

    private void ScrollToEnd()
    {
        _outputTextBox.CaretIndex = _outputTextBox.Text?.Length ?? 0;
        int lineCount = _outputTextBox.GetLineCount();
        if (lineCount > 0)
            _outputTextBox.ScrollToLine(lineCount - 1);
    }
}
