using System;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
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
    private readonly IndentGuideRenderer _indentGuideRenderer;
    private readonly ContentControl _fileInfoIconHost;
    private readonly TextBlock _fileInfoText;

    // Guard flag: true while the View is pushing text to the editor,
    // preventing OnTextChanged from bouncing it back to the ViewModel.
    private bool _isUpdatingFromViewModel;

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
            Background = (IBrush?)Application.Current!.Resources["SurfaceBaseBrush"],
            Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
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

        _indentGuideRenderer = new IndentGuideRenderer(
            _textEditor.TextArea.TextView,
            new SolidColorBrush(Color.FromArgb(90, 194, 194, 229)));

        // M4: Focused file info bar — shows current file name and "diff/edit" indicator.
        // Styled with TextSecondaryBrush for a quieter, utility-focused appearance.
        // Text is updated reactively when the ViewModel changes (see WhenActivated).
        _fileInfoIconHost = new ContentControl
        {
            Content = IconFactory.Create(
                "Icon.Unknown",
                (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
                12),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        _fileInfoText = new TextBlock
        {
            Text = "diff/edit",
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            FontSize = 12,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var fileInfoBar = new Border
        {
            Padding = new Thickness(12, 6, 12, 6),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { _fileInfoIconHost, _fileInfoText }
            }
        };

        // Layout: code area fills star, file info bar at bottom (auto height).
        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto }
            }
        };
        Grid.SetRow(_textEditor, 0);
        Grid.SetRow(fileInfoBar, 1);
        layout.Children.Add(_textEditor);
        layout.Children.Add(fileInfoBar);

        Content = layout;

        this.WhenActivated(d =>
        {
            // VM → editor: follow the active ViewModel's TextContent.
            // Uses Switch() so the subscription tracks the *current* VM —
            // when the VM changes or TextContent changes programmatically,
            // the editor updates. Guard flag prevents feedback loop.
            d.Add(this.GetObservable(ViewModelProperty)
                .Select(vm => vm is EditorViewModel evm
                    ? evm.WhenAnyValue(x => x.TextContent)
                    : Observable.Never<string>())
                .Switch()
                .Subscribe(newContent =>
                {
                    if (_isUpdatingFromViewModel) return;
                    if (_textEditor.Text == newContent) return;

                    _isUpdatingFromViewModel = true;
                    try { _textEditor.Text = newContent; }
                    finally { _isUpdatingFromViewModel = false; }
                }));

            // File mode (grammar + font): only on VM change, not on keystroke.
            d.Add(this.GetObservable(ViewModelProperty)
                .Subscribe(obj =>
                {
                    if (obj is EditorViewModel vm)
                    {
                        ApplyFileMode(vm.FilePath);
                        UpdateFileInfoBar(vm);
                    }
                    else
                    {
                        _textEditor.Text = string.Empty;
                        _indentGuideRenderer.IsEnabled = false;
                        _fileInfoIconHost.Content = IconFactory.Create(
                            "Icon.Unknown",
                            (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
                            12);
                        _fileInfoText.Text = "diff/edit";
                    }
                }));

            // File name updates: track FilePath changes on the current ViewModel
            // and update the file info bar text accordingly.
            d.Add(this.GetObservable(ViewModelProperty)
                .Select(vm => vm is EditorViewModel evm
                    ? evm.WhenAnyValue(x => x.FileName)
                    : Observable.Never<string>())
                .Switch()
                .Subscribe(_ =>
                {
                    if (ViewModel is not null)
                        UpdateFileInfoBar(ViewModel);
                }));

            // Caret position tracking: push line/column to ViewModel
            void OnCaretChanged(object? s, EventArgs e)
            {
                if (ViewModel is null) return;
                var caret = _textEditor.TextArea.Caret;
                ViewModel.CaretLine = caret.Line;
                ViewModel.CaretColumn = caret.Column;
            }
            _textEditor.TextArea.Caret.PositionChanged += OnCaretChanged;
            d.Add(Disposable.Create(() => _textEditor.TextArea.Caret.PositionChanged -= OnCaretChanged));

            _textEditor.TextChanged += OnTextChanged;
            d.Add(Disposable.Create(() => _textEditor.TextChanged -= OnTextChanged));
        });
    }

    /// <summary>
    /// Returns the TextMate grammar scope for a file extension,
    /// or null when no grammar is available.
    /// Internal + static so unit tests can verify the mapping
    /// without instantiating an Avalonia control.
    /// </summary>
    internal static string? GetGrammarScope(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".cs" => "source.cs",
            ".json" => "source.json",
            ".md" => "text.html.markdown",
            _ => null
        };
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
        var scope = GetGrammarScope(filePath);
        _textMateInstallation.SetGrammar(scope ?? "");

        // Font — monospace for code, serif for prose
        _textEditor.FontFamily = ext == ".md" ? ProseFont : CodeFont;

        // Experiment path: only enable indent guides for C# files while the
        // visual behavior is being validated. If more file types need guides
        // later, extract a helper like ShouldEnableForFile(ext) or gate on
        // GetGrammarScope returning non-null.
        _indentGuideRenderer.IsEnabled = ext == ".cs";
    }

    /// <summary>
    /// Updates the focused file info bar text to show the current file name
    /// and "diff/edit" indicator.
    /// </summary>
    private void UpdateFileInfoBar(EditorViewModel vm)
    {
        var name = string.IsNullOrEmpty(vm.FileName) ? "Untitled" : vm.FileName;
        _fileInfoText.Text = $"{name}  —  diff/edit";
        _fileInfoIconHost.Content = IconFactory.Create(
            FileIconKeyResolver.GetIconKey(vm.FilePath),
            (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            12);
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        if (ViewModel is null || _isUpdatingFromViewModel) return;
        ViewModel.TextContent = _textEditor.Text;
    }
}
