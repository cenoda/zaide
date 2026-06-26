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

    // Fonts: monospace for code, sans-serif for prose (Markdown).
    // Liberation Sans is on nearly every Linux distro; falls back to sans-serif.
    private static readonly FontFamily CodeFont =
        new("Cascadia Code, Consolas, monospace");
    private static readonly FontFamily ProseFont =
        new("Liberation Sans, sans-serif");

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

        // NOTE: ShowIndentGuides is not exposed in AvaloniaEdit v12.
        // Defer to a future upgrade when the API is available.

        Content = _textEditor;

        this.WhenActivated(d =>
        {
            d.Add(this.GetObservable(ViewModelProperty)
                .Subscribe(obj =>
                {
                    if (obj is EditorViewModel vm)
                    {
                        _textEditor.Text = vm.TextContent;
                        SetGrammar(vm.FilePath);
                        SetFont(vm.FilePath);
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
    /// Maps a file extension to its TextMate grammar scope and applies it.
    /// Unsupported extensions get plain text (no highlighting).
    /// </summary>
    private void SetGrammar(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var scope = ext switch
        {
            ".cs" => "source.cs",
            ".json" => "source.json",
            ".md" => "text.html.markdown",
            _ => null
        };

        if (scope is not null)
            _textMateInstallation.SetGrammar(scope);
    }

    /// <summary>
    /// Switches the editor font based on file type.
    /// Code: monospace 14px. Markdown: sans-serif 18px (impossible to miss).
    /// </summary>
    private void SetFont(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".md")
        {
            _textEditor.FontFamily = ProseFont;
            _textEditor.FontSize = 18;
        }
        else
        {
            _textEditor.FontFamily = CodeFont;
            _textEditor.FontSize = 14;
        }
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        if (ViewModel is null) return;
        ViewModel.TextContent = _textEditor.Text;
    }
}
