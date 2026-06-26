using System;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TextMateSharp.Grammars;
using Zaide.ViewModels;

namespace Zaide.Views;

/// <summary>
/// Code editor view wrapping AvaloniaEdit's TextEditor with TextMate
/// syntax highlighting. Uses event-based sync (not two-way Bind) to
/// avoid feedback loops. Handles null ViewModel gracefully.
/// </summary>
public partial class EditorView : ReactiveUserControl<EditorViewModel>
{
    private readonly TextEditor _textEditor;
    private readonly TextMate.Installation _textMateInstallation;

    // Fonts: monospace for code, serif for prose (Markdown).
    private static readonly FontFamily CodeFont =
        new("Cascadia Code, Consolas, monospace");
    private static readonly FontFamily ProseFont =
        new("Georgia, serif");

    public EditorView()
    {
        _textEditor = new TextEditor
        {
            ShowLineNumbers = true,
            FontSize = 14,
            FontFamily = CodeFont,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
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

        // Initialize TextMate with the DarkPlus theme (matches app dark theme).
        // TextMateSharp.Grammars bundles 100+ grammars — no external downloads.
        var registry = new RegistryOptions(ThemeName.DarkPlus);
        _textMateInstallation = _textEditor.InstallTextMate(registry);

        // Indent guides: not exposed as a built-in option in AvaloniaEdit v12.
        // Phase 2.1 — implement via IBackgroundRenderer on the TextView
        // (draw vertical lines at indentation boundaries in custom render pass).

        Content = _textEditor;

        this.WhenActivated(d =>
        {
            d.Add(this.GetObservable(ViewModelProperty)
                .Subscribe(obj =>
                {
                    if (obj is EditorViewModel vm)
                    {
                        _textEditor.Text = vm.TextContent;
                        ApplyFileMode(vm.FilePath);
                    }
                    else
                    {
                        _textEditor.Text = string.Empty;
                    }
                }));

            _textEditor.TextChanged += OnTextChanged;
            d.Add(Disposable.Create(() => _textEditor.TextChanged -= OnTextChanged));
        });
    }

    /// <summary>
    /// Applies syntax highlighting and font based on the file extension.
    /// Parses the extension once — no duplicated Path.GetExtension calls.
    /// </summary>
    private void ApplyFileMode(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        // Grammar — always set, even for unsupported files.
        // Passing a null/empty scope clears the previous grammar
        // so plain-text files don't inherit the last tab's highlighting.
        var scope = ext switch
        {
            ".cs" => "source.cs",
            ".json" => "source.json",
            ".md" => "text.html.markdown",
            _ => null
        };
        _textMateInstallation.SetGrammar(scope ?? "");

        // Font — monospace for code, serif for prose
        _textEditor.FontFamily = ext == ".md" ? ProseFont : CodeFont;
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        if (ViewModel is null) return;
        ViewModel.TextContent = _textEditor.Text;
    }
}
