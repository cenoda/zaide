using System;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
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
using Zaide.Styles;
using Zaide.Services;
using Zaide.Models;

namespace Zaide.Views;

/// <summary>
/// Code editor view wrapping AvaloniaEdit's TextEditor with TextMate
/// syntax highlighting. Uses event-based sync (not two-way Bind) to
/// avoid feedback loops. Handles null ViewModel gracefully.
/// </summary>
public partial class EditorView : ReactiveUserControl<EditorViewModel>, IDisposable
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
    private readonly ISettingsService _settings;
    private readonly SettingsBinding _settingsBinding;
    private FontFamily _codeFont = new("Cascadia Code, Consolas, monospace");
    private FontFamily _proseFont = new("Georgia, serif");
    private bool _disposed;

    public EditorView(ISettingsService settings)
    {
        _settings = settings;
        _textEditor = new TextEditor
        {
            ShowLineNumbers = true,
            FontSize = 14,
            FontFamily = _codeFont,
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

        _fileInfoText = TextStyles.Caption("diff/edit");
        _fileInfoText.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

        var fileInfoBar = new Border
        {
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingSm - LayoutTokens.SpacingXxs),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = LayoutTokens.SpacingSm - LayoutTokens.SpacingXxs,
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

        _settingsBinding = CreateSettingsBinding(
            settings,
            model => ApplyEditorSettings(model.Editor));

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
        _textEditor.FontFamily = SelectFont(_codeFont, _proseFont, filePath);

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

    private void ApplyEditorSettings(EditorSettings settings)
    {
        var projection = ProjectSettings(settings);
        _codeFont = projection.CodeFont;
        _proseFont = projection.ProseFont;
        _textEditor.FontSize = projection.CodeFontSize;
        _textEditor.Options.IndentationSize = projection.TabSize;
        _textEditor.Options.ConvertTabsToSpaces = projection.InsertSpaces;
        _textEditor.Options.ShowTabs = projection.ShowTabs;
        _textEditor.Options.ShowSpaces = projection.ShowSpaces;

        if (ViewModel is not null)
            ApplyFileMode(ViewModel.FilePath);
        else
            _textEditor.FontFamily = _codeFont;
    }

    internal static EditorSettingsProjection ProjectSettings(EditorSettings settings) =>
        new(
            new FontFamily(settings.CodeFontFamily),
            new FontFamily(settings.ProseFontFamily),
            settings.CodeFontSize,
            settings.TabSize,
            settings.InsertSpaces,
            settings.ShowWhitespace && settings.ShowTabs,
            settings.ShowWhitespace && settings.ShowSpaces);

    internal static SettingsBinding CreateSettingsBinding(
        ISettingsService settings,
        Action<SettingsModel> apply) =>
        new(settings, apply);

    internal static SettingsBinding CreateSettingsBinding(
        ISettingsService settings,
        Action<SettingsModel> apply,
        IScheduler scheduler) =>
        new SettingsBinding(settings, apply, scheduler);

    internal static FontFamily SelectFont(EditorSettingsProjection projection, string filePath) =>
        SelectFont(projection.CodeFont, projection.ProseFont, filePath);

    private static FontFamily SelectFont(FontFamily codeFont, FontFamily proseFont, string filePath) =>
        Path.GetExtension(filePath).Equals(".md", StringComparison.OrdinalIgnoreCase)
            ? proseFont
            : codeFont;

    internal sealed record EditorSettingsProjection(
        FontFamily CodeFont,
        FontFamily ProseFont,
        int CodeFontSize,
        int TabSize,
        bool InsertSpaces,
        bool ShowTabs,
        bool ShowSpaces);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _settingsBinding.Dispose();
    }
}
