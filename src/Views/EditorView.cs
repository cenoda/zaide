using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.ViewModels;

namespace Zaide.Views;

/// <summary>
/// Code editor view wrapping AvaloniaEdit's TextEditor.
/// Uses event-based sync (not two-way Bind) to avoid feedback loops.
/// Handles null ViewModel gracefully during activation ordering.
/// </summary>
public partial class EditorView : ReactiveUserControl<EditorViewModel>
{
    private readonly TextEditor _textEditor;

    public EditorView()
    {
        _textEditor = new TextEditor
        {
            ShowLineNumbers = true,
            FontSize = 14,
            FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = (IBrush?)Application.Current!.Resources["DeepBase"],
            Foreground = (IBrush?)Application.Current!.Resources["TextActive"],
            Options =
            {
                EnableHyperlinks = true,
                EnableEmailHyperlinks = false,
                ShowTabs = false,
                ShowSpaces = false,
            }
        };

        Content = _textEditor;

        this.WhenActivated(d =>
        {
            // ViewModel → editor: load content when ViewModel changes (tab switch).
            // Uses GetObservable(ViewModelProperty) instead of WhenAnyValue(x => x.ViewModel)
            // because ReactiveUserControl<T>.ViewModel is backed by a StyledProperty.
            // WhenAnyValue's expression analysis can resolve to the base property and
            // miss PropertyChanged notifications from SetValue. GetObservable talks to
            // Avalonia's property system directly — no ambiguity.
            d.Add(this.GetObservable(ViewModelProperty)
                .Subscribe(obj =>
                {
                    Log($"[EditorView] ViewModelProperty changed: " +
                        $"{(obj is EditorViewModel vm ? vm.FileName : "null")}");
                    if (obj is EditorViewModel vm2)
                    {
                        Log($"[EditorView] TextContent length={vm2.TextContent.Length}, " +
                            $"preview='{vm2.TextContent[..Math.Min(40, vm2.TextContent.Length)]}'");
                        _textEditor.Text = vm2.TextContent;
                        Log($"[EditorView] After set, _textEditor.Text length={_textEditor.Text.Length}");
                    }
                    else
                    {
                        // ViewModel was cleared — reset the editor to blank
                        _textEditor.Text = string.Empty;
                    }
                }));

            // Editor → ViewModel: sync user edits via TextChanged event
            _textEditor.TextChanged += OnTextChanged;
            d.Add(Disposable.Create(() => _textEditor.TextChanged -= OnTextChanged));
        });
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        if (ViewModel is null) return;
        ViewModel.TextContent = _textEditor.Text;
    }

    private static void Log(string msg)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss.fff");
        System.IO.File.AppendAllText("/tmp/zaide-debug.log", $"[{ts}] {msg}\n");
    }
}
