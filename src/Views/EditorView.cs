using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
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
            // ViewModel → editor: load content when ViewModel changes (tab switch)
            d.Add(this.WhenAnyValue(x => x.ViewModel)
                .Subscribe(vm =>
                {
                    if (vm is null) return;
                    _textEditor.Text = vm.TextContent;
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
}
