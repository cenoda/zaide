using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Search;
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
public partial class EditorView : ReactiveUserControl<EditorViewModel>, IDisposable, IEditorLanguageOperations
{
    private readonly TextEditor _textEditor;
    private readonly EditorLanguageInputViewModel _languageInput;
    private readonly EditorCompletionPopup _completionPopup;
    private readonly EditorHoverPopup _hoverPopup;
    private readonly EditorLanguagePickerPopup _definitionPicker;
    private readonly EditorLanguagePickerPopup _documentSymbolPicker;
    private readonly EditorLanguagePickerPopup _workspaceSymbolPicker;
    private readonly TextMate.Installation _textMateInstallation;
    private readonly IndentGuideRenderer _indentGuideRenderer;
    private readonly BreakpointOperations _breakpointOperations;
    private readonly InstructionPointerOperations _instructionPointerOperations;
    private readonly EditorBreakpointViewModel _breakpointViewModel;
    private readonly DebugCurrentLocationViewModel _currentLocationViewModel;
    private readonly ContentControl _fileInfoIconHost;
    private readonly TextBlock _fileInfoText;
    private readonly FoldingOperations _foldingOperations;

    // Guard flag: true while the View is pushing text to the editor,
    // preventing OnTextChanged from bouncing it back to the ViewModel.
    private bool _isUpdatingFromViewModel;

    // Fonts: monospace for code, serif for prose (Markdown).
    private readonly ISettingsService _settings;
    private readonly SettingsBinding _settingsBinding;
    private FontFamily _codeFont = new("Cascadia Code, Consolas, monospace");
    private FontFamily _proseFont = new("Georgia, serif");
    private bool _disposed;

    public EditorView(
        ISettingsService settings,
        EditorLanguageInputViewModel languageInput,
        EditorBreakpointViewModel breakpointViewModel,
        DebugCurrentLocationViewModel currentLocationViewModel)
    {
        _settings = settings;
        _languageInput = languageInput ?? throw new ArgumentNullException(nameof(languageInput));
        _breakpointViewModel = breakpointViewModel
            ?? throw new ArgumentNullException(nameof(breakpointViewModel));
        _currentLocationViewModel = currentLocationViewModel
            ?? throw new ArgumentNullException(nameof(currentLocationViewModel));
        _completionPopup = new EditorCompletionPopup
        {
            PlacementTarget = null,
        };
        _hoverPopup = new EditorHoverPopup
        {
            PlacementTarget = null,
        };
        _definitionPicker = new EditorLanguagePickerPopup(showQuery: false)
        {
            PlacementTarget = null,
        };
        _documentSymbolPicker = new EditorLanguagePickerPopup(showQuery: false)
        {
            PlacementTarget = null,
        };
        _workspaceSymbolPicker = new EditorLanguagePickerPopup(showQuery: true)
        {
            PlacementTarget = null,
        };
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

        _breakpointOperations = new BreakpointOperations(
            _textEditor,
            line => _breakpointViewModel.ToggleAtLineCommand.Execute(line).Subscribe());
        _instructionPointerOperations = new InstructionPointerOperations(_textEditor);

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

        // Phase 9 M4: folding operations wrapping the shared TextEditor.
        _foldingOperations = new FoldingOperations(_textEditor);

        _settingsBinding = CreateSettingsBinding(
            settings,
            model => ApplyEditorSettings(model.Editor));

        _completionPopup.PlacementTarget = _textEditor;
        _hoverPopup.PlacementTarget = _textEditor;
        _definitionPicker.PlacementTarget = _textEditor;
        _documentSymbolPicker.PlacementTarget = _textEditor;
        _workspaceSymbolPicker.PlacementTarget = _textEditor;
        _completionPopup.ItemConfirmed += OnCompletionItemConfirmed;
        _completionPopup.DismissRequested += () => _languageInput.DismissAll();
        _definitionPicker.ItemConfirmed += OnDefinitionItemConfirmed;
        _definitionPicker.DismissRequested += () => _languageInput.DefinitionDismissCommand.Execute().Subscribe();
        _documentSymbolPicker.ItemConfirmed += OnSymbolItemConfirmed;
        _documentSymbolPicker.DismissRequested += () => _languageInput.SymbolDismissCommand.Execute().Subscribe();
        _workspaceSymbolPicker.ItemConfirmed += OnSymbolItemConfirmed;
        _workspaceSymbolPicker.DismissRequested += () => _languageInput.SymbolDismissCommand.Execute().Subscribe();
        _workspaceSymbolPicker.QueryChanged += OnWorkspaceSymbolQueryChanged;

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

                    // Phase 9 M4: install folding when the source ViewModel
                    // changes (tab switch). We compare by reference so folds
                    // are not re-computed on every keystroke.
                    if (!ReferenceEquals(ViewModel, _lastFoldVm))
                    {
                        _lastFoldVm = ViewModel;
                        if (ViewModel is not null && newContent.Length > 0)
                            _foldingOperations.Install(newContent);
                        else
                            _foldingOperations.Clear();
                    }
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
                        // Phase 9 M4: clear folding state when no tab is active.
                        _lastFoldVm = null;
                        _foldingOperations.Clear();
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
                _languageInput.OnCaretMoved();
            }
            _textEditor.TextArea.Caret.PositionChanged += OnCaretChanged;
            d.Add(Disposable.Create(() => _textEditor.TextArea.Caret.PositionChanged -= OnCaretChanged));

            // Phase 10 M4: route active editor identity to language input VM.
            d.Add(this.GetObservable(ViewModelProperty)
                .Subscribe(obj =>
                {
                    if (obj is EditorViewModel vm)
                    {
                        _languageInput.ActiveEditor = this;
                        _languageInput.ActiveDocumentId = string.IsNullOrEmpty(vm.FilePath)
                            ? null
                            : vm.FilePath;
                    }
                    else
                    {
                        _languageInput.ActiveEditor = null;
                        _languageInput.ActiveDocumentId = null;
                    }

                    ClearLanguagePresentation();
                }));

            d.Add(_languageInput.CompletionWhenChanged.Subscribe(ApplyCompletionSnapshot));
            d.Add(_languageInput.HoverWhenChanged.Subscribe(ApplyHoverSnapshot));
            d.Add(_languageInput.NavigationWhenChanged.Subscribe(ApplyNavigationSnapshot));
            d.Add(_languageInput.SymbolWhenChanged.Subscribe(ApplySymbolSnapshot));

            void OnEditorKeyDown(object? s, KeyEventArgs e)
            {
                if (_definitionPicker.IsOpen)
                {
                    switch (e.Key)
                    {
                        case Key.Escape:
                            _languageInput.DefinitionDismissCommand.Execute().Subscribe();
                            e.Handled = true;
                            return;
                        case Key.Up:
                            _languageInput.DefinitionMoveUpCommand.Execute().Subscribe();
                            e.Handled = true;
                            return;
                        case Key.Down:
                            _languageInput.DefinitionMoveDownCommand.Execute().Subscribe();
                            e.Handled = true;
                            return;
                        case Key.Enter:
                            _languageInput.DefinitionAcceptCommand.Execute().Subscribe();
                            e.Handled = true;
                            return;
                    }
                }

                if (_documentSymbolPicker.IsOpen || _workspaceSymbolPicker.IsOpen)
                {
                    switch (e.Key)
                    {
                        case Key.Escape:
                            _languageInput.SymbolDismissCommand.Execute().Subscribe();
                            e.Handled = true;
                            return;
                        case Key.Up:
                            _languageInput.SymbolMoveUpCommand.Execute().Subscribe();
                            e.Handled = true;
                            return;
                        case Key.Down:
                            _languageInput.SymbolMoveDownCommand.Execute().Subscribe();
                            e.Handled = true;
                            return;
                        case Key.Enter:
                            _languageInput.SymbolAcceptCommand.Execute().Subscribe();
                            e.Handled = true;
                            return;
                    }
                }

                if (_completionPopup.IsOpen)
                {
                    switch (e.Key)
                    {
                        case Key.Escape:
                            _languageInput.DismissAll();
                            e.Handled = true;
                            return;
                        case Key.Up:
                            _languageInput.CompletionMoveUpCommand.Execute().Subscribe();
                            e.Handled = true;
                            return;
                        case Key.Down:
                            _languageInput.CompletionMoveDownCommand.Execute().Subscribe();
                            e.Handled = true;
                            return;
                        case Key.Enter:
                        case Key.Tab:
                            _languageInput.CompletionCommitCommand.Execute().Subscribe();
                            e.Handled = true;
                            return;
                    }
                }
            }

            void OnEditorTextInput(object? s, TextInputEventArgs e)
            {
                if (!string.IsNullOrEmpty(e.Text))
                    _lastTypedCharacter = e.Text[0];
            }

            _textEditor.KeyDown += OnEditorKeyDown;
            _textEditor.TextInput += OnEditorTextInput;
            d.Add(Disposable.Create(() =>
            {
                _textEditor.KeyDown -= OnEditorKeyDown;
                _textEditor.TextInput -= OnEditorTextInput;
            }));

            // Phase 10 M3: apply Problems/navigation requests from the active ViewModel.
            d.Add(this.GetObservable(ViewModelProperty)
                .Select(vm => vm is EditorViewModel evm
                    ? evm.WhenAnyValue(x => x.NavigationRequestId)
                        .Where(_ => evm.PendingNavigationOffset is not null)
                        .Select(_ => evm)
                    : Observable.Never<EditorViewModel>())
                .Switch()
                .Subscribe(ApplyPendingNavigation));

            // Phase 9 M6: selection tracking — push selection state to ViewModel.
            void OnSelectionChanged(object? s, EventArgs e)
            {
                if (ViewModel is null) return;
                var selection = _textEditor.TextArea.Selection;
                var startPos = selection.StartPosition;
                var startOffset = _textEditor.Document.GetOffset(startPos.Line, startPos.Column);
                ViewModel.SelectionStart = startOffset;
                ViewModel.SelectionLength = selection.Length;
                ViewModel.SelectionText = selection.IsEmpty ? null : selection.GetText();
            }
            _textEditor.TextArea.SelectionChanged += OnSelectionChanged;
            d.Add(Disposable.Create(() => _textEditor.TextArea.SelectionChanged -= OnSelectionChanged));

            _textEditor.TextChanged += OnTextChanged;
            d.Add(Disposable.Create(() => _textEditor.TextChanged -= OnTextChanged));

            d.Add(_breakpointViewModel
                .WhenAnyValue(x => x.ProjectionRevision)
                .Subscribe(_ => SyncBreakpointMargin()));

            d.Add(_currentLocationViewModel
                .WhenAnyValue(x => x.ProjectionRevision)
                .Subscribe(_ => SyncInstructionPointerMargin()));

            d.Add(this.GetObservable(ViewModelProperty)
                .Select(vm => vm is EditorViewModel evm
                    ? evm.WhenAnyValue(x => x.FilePath)
                    : Observable.Never<string>())
                .Switch()
                .Subscribe(_ =>
                {
                    SyncBreakpointMargin();
                    SyncInstructionPointerMargin();
                }));
        });
    }

    private void SyncBreakpointMargin()
    {
        var normalizedPath = string.IsNullOrWhiteSpace(ViewModel?.FilePath)
            ? null
            : Path.GetFullPath(ViewModel.FilePath);

        if (ViewModel is null ||
            _breakpointViewModel.ActiveDocumentPath is null ||
            !string.Equals(normalizedPath, _breakpointViewModel.ActiveDocumentPath, StringComparison.Ordinal))
        {
            _breakpointOperations.Clear();
            return;
        }

        _breakpointOperations.Install();
        _breakpointOperations.SetMarkers(_breakpointViewModel.Markers);
    }

    private void SyncInstructionPointerMargin()
    {
        var normalizedPath = string.IsNullOrWhiteSpace(ViewModel?.FilePath)
            ? null
            : Path.GetFullPath(ViewModel.FilePath);

        if (ViewModel is null ||
            _currentLocationViewModel.ActiveDocumentPath is null ||
            !string.Equals(normalizedPath, _currentLocationViewModel.ActiveDocumentPath, StringComparison.Ordinal))
        {
            _instructionPointerOperations.Clear();
            return;
        }

        _instructionPointerOperations.Install();
        _instructionPointerOperations.SetMarker(_currentLocationViewModel.Marker);
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

    private char? _lastTypedCharacter;

    private void OnTextChanged(object? sender, EventArgs e)
    {
        if (ViewModel is null || _isUpdatingFromViewModel) return;
        ViewModel.TextContent = _textEditor.Text;

        if (_lastTypedCharacter is char typed)
        {
            _languageInput.OnTextEdited(typed);
            _lastTypedCharacter = null;
        }
        else
        {
            _languageInput.OnCaretMoved();
        }
    }

    private void OnCompletionItemConfirmed() =>
        _languageInput.CompletionCommitCommand.Execute().Subscribe();

    private void OnDefinitionItemConfirmed() =>
        _languageInput.DefinitionAcceptCommand.Execute().Subscribe();

    private void OnSymbolItemConfirmed() =>
        _languageInput.SymbolAcceptCommand.Execute().Subscribe();

    private void OnWorkspaceSymbolQueryChanged(string query) =>
        _languageInput.SetWorkspaceSymbolQuery(query);

    private void ApplyCompletionSnapshot(LanguageCompletionSnapshot snapshot)
    {
        if (!snapshot.IsPopupOpen)
        {
            _completionPopup.IsOpen = false;
            return;
        }

        if (ViewModel is null ||
            !string.Equals(ViewModel.FilePath, snapshot.FilePath, StringComparison.Ordinal))
        {
            _completionPopup.IsOpen = false;
            return;
        }

        _completionPopup.BindItems(snapshot.Items, snapshot.SelectedIndex);
        _completionPopup.IsOpen = true;
        _hoverPopup.IsOpen = false;
        _definitionPicker.IsOpen = false;
        _documentSymbolPicker.IsOpen = false;
        _workspaceSymbolPicker.IsOpen = false;
    }

    private void ApplyHoverSnapshot(LanguageHoverSnapshot snapshot)
    {
        if (!snapshot.IsVisible)
        {
            _hoverPopup.IsOpen = false;
            return;
        }

        if (ViewModel is null ||
            !string.Equals(ViewModel.FilePath, snapshot.FilePath, StringComparison.Ordinal))
        {
            _hoverPopup.IsOpen = false;
            return;
        }

        if (_completionPopup.IsOpen ||
            _definitionPicker.IsOpen ||
            _documentSymbolPicker.IsOpen ||
            _workspaceSymbolPicker.IsOpen)
        {
            _hoverPopup.IsOpen = false;
            return;
        }

        _hoverPopup.SetContent(snapshot.Content);
        _hoverPopup.IsOpen = true;
    }

    private void ApplyNavigationSnapshot(LanguageNavigationSnapshot snapshot)
    {
        if (!snapshot.IsChooserOpen)
        {
            _definitionPicker.IsOpen = false;
            return;
        }

        if (ViewModel is null ||
            !string.Equals(ViewModel.FilePath, snapshot.SourceFilePath, StringComparison.Ordinal))
        {
            _definitionPicker.IsOpen = false;
            return;
        }

        var labels = new List<string>(snapshot.Locations.Count);
        foreach (var location in snapshot.Locations)
            labels.Add(FormatLocationLabel(location));

        _definitionPicker.SetHeader("Go to Definition");
        _definitionPicker.BindItems(labels, snapshot.SelectedIndex);
        _definitionPicker.IsOpen = true;
        _completionPopup.IsOpen = false;
        _hoverPopup.IsOpen = false;
        _documentSymbolPicker.IsOpen = false;
        _workspaceSymbolPicker.IsOpen = false;
    }

    private void ApplySymbolSnapshot(LanguageSymbolSnapshot snapshot)
    {
        if (!snapshot.IsSurfaceOpen)
        {
            _documentSymbolPicker.IsOpen = false;
            _workspaceSymbolPicker.IsOpen = false;
            return;
        }

        if (snapshot.Scope == LanguageSymbolScope.Document)
        {
            if (ViewModel is null ||
                !string.Equals(ViewModel.FilePath, snapshot.FilePath, StringComparison.Ordinal))
            {
                _documentSymbolPicker.IsOpen = false;
                return;
            }

            var labels = FormatSymbolLabels(snapshot.Symbols);
            _documentSymbolPicker.SetHeader(
                snapshot.State == LanguageSymbolState.Empty
                    ? snapshot.FeedbackMessage ?? "No symbols"
                    : "Document symbols");
            _documentSymbolPicker.BindItems(labels, snapshot.SelectedIndex);
            _documentSymbolPicker.IsOpen = true;
            _workspaceSymbolPicker.IsOpen = false;
            _completionPopup.IsOpen = false;
            _hoverPopup.IsOpen = false;
            _definitionPicker.IsOpen = false;
            return;
        }

        if (snapshot.Scope == LanguageSymbolScope.Workspace)
        {
            var labels = FormatSymbolLabels(snapshot.Symbols);
            _workspaceSymbolPicker.SetHeader(
                snapshot.State == LanguageSymbolState.Empty
                    ? snapshot.FeedbackMessage ?? "No symbols"
                    : snapshot.State == LanguageSymbolState.Loading
                        ? "Searching workspace symbols…"
                        : "Workspace symbols");
            _workspaceSymbolPicker.BindItems(labels, snapshot.SelectedIndex);
            _workspaceSymbolPicker.IsOpen = true;
            _documentSymbolPicker.IsOpen = false;
            _completionPopup.IsOpen = false;
            _hoverPopup.IsOpen = false;
            _definitionPicker.IsOpen = false;
        }
    }

    private static List<string> FormatSymbolLabels(IReadOnlyList<LanguageSymbol> symbols)
    {
        var labels = new List<string>(symbols.Count);
        foreach (var symbol in symbols)
        {
            var indent = symbol.Depth > 0 ? new string(' ', symbol.Depth * 2) : string.Empty;
            var path = symbol.Location?.FilePath is { Length: > 0 } filePath
                ? Path.GetFileName(filePath)
                : null;
            var line = symbol.Location is null
                ? string.Empty
                : $":{symbol.Location.Range.StartLine + 1}";
            var suffix = path is null ? string.Empty : $"  ({path}{line})";
            labels.Add($"{indent}{symbol.Name}{suffix}");
        }

        return labels;
    }

    private static string FormatLocationLabel(LanguageLocation location)
    {
        var file = location.FilePath is { Length: > 0 }
            ? Path.GetFileName(location.FilePath)
            : location.DocumentUri;
        var line = location.Range.StartLine + 1;
        var column = location.Range.StartCharacter + 1;
        var name = string.IsNullOrWhiteSpace(location.Name) ? string.Empty : $"{location.Name} — ";
        return $"{name}{file}:{line}:{column}";
    }

    private void ClearLanguagePresentation()
    {
        _completionPopup.IsOpen = false;
        _hoverPopup.IsOpen = false;
        _definitionPicker.IsOpen = false;
        _documentSymbolPicker.IsOpen = false;
        _workspaceSymbolPicker.IsOpen = false;
    }



    /// <summary>
    /// Applies a pending caret/selection navigation request from the active ViewModel.
    /// No-ops when the offset is outside the live document.
    /// </summary>
    private void ApplyPendingNavigation(EditorViewModel vm)
    {
        if (!ReferenceEquals(ViewModel, vm))
            return;

        if (vm.PendingNavigationOffset is not int offset)
            return;

        var length = vm.PendingNavigationLength;
        var docLength = _textEditor.Document.TextLength;
        if (offset < 0 || offset > docLength)
        {
            vm.ClearNavigationRequest();
            return;
        }

        var clampedLength = Math.Max(0, Math.Min(length, docLength - offset));
        SetSelection(offset, clampedLength);
        vm.ClearNavigationRequest();
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

        // Phase 9 M4: re-install folding after settings changes so folds
        // remain visually consistent with the new font/size. The
        // FoldingManager uses text offsets, so fold positions are
        // unaffected by pixel-level changes, but re-installing ensures
        // the margin and generation are refreshed.
        if (ViewModel is not null && _foldingOperations.IsAvailable)
        {
            var currentText = _textEditor.Text;
            if (currentText.Length > 0)
                _foldingOperations.Install(currentText);
        }
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

    /// <summary>
    /// Phase 9 M4: folding-operations seam for the shared TextEditor.
    /// The MainWindow sets this on EditorTabViewModel.FoldingEditor so
    /// registered folding commands can reach the view layer.
    /// </summary>
    public IFoldingOperations Folding => _foldingOperations;

    /// <summary>
    /// Phase 12 M3b: breakpoint margin seam for tests and composition checks.
    /// </summary>
    public BreakpointOperations Breakpoints => _breakpointOperations;

    /// <summary>
    /// Tracks the last ViewModel for which we installed folds so we only
    /// re-install on tab switches, not on every keystroke.
    /// </summary>
    private EditorViewModel? _lastFoldVm;

    // ── IEditorTextOperations ────────────────────────────────────────────

    public string GetText() => _textEditor.Text;

    public void SetText(string text)
    {
        _isUpdatingFromViewModel = true;
        try { _textEditor.Text = text; }
        finally { _isUpdatingFromViewModel = false; }

        // Push the new text to the ViewModel so Document.Content and IsDirty
        // stay truthful. The guard above blocks OnTextChanged from doing this
        // automatically, so we must sync explicitly after the assignment.
        if (ViewModel is EditorViewModel evm && evm.TextContent != text)
            evm.TextContent = text;
    }

    public void SetSelection(int offset, int length)
    {
        if (offset < 0 || offset > _textEditor.Document.TextLength) return;
        var end = Math.Min(offset + length, _textEditor.Document.TextLength);
        _textEditor.SelectionStart = offset;
        _textEditor.SelectionLength = end - offset;
        _textEditor.ScrollToLine(_textEditor.Document.GetLineByOffset(offset).LineNumber);
    }

    public int GetSelectionOffset() => _textEditor.SelectionStart;

    public int GetSelectionLength() => _textEditor.SelectionLength;

    public int GetCaretOffset() => _textEditor.CaretOffset;

    public char? GetCharBeforeCaret()
    {
        var offset = _textEditor.CaretOffset;
        if (offset <= 0)
            return null;

        return _textEditor.Document.GetCharAt(offset - 1);
    }

    public void ReplaceRange(int start, int length, string newText)
    {
        if (start < 0 || length < 0 || start + length > _textEditor.Document.TextLength)
            return;

        _isUpdatingFromViewModel = true;
        try
        {
            _textEditor.Document.Replace(start, length, newText);
        }
        finally
        {
            _isUpdatingFromViewModel = false;
        }

        if (ViewModel is not null && ViewModel.TextContent != _textEditor.Text)
            ViewModel.TextContent = _textEditor.Text;
    }

    public int ReplaceAllMatches(string query, string replacement, bool caseSensitive)
    {
        if (string.IsNullOrEmpty(query)) return 0;

        var doc = _textEditor.Document;
        var undoStack = doc.UndoStack;
        undoStack.StartUndoGroup();
        try
        {
            var count = 0;
            var search = SearchEngine.FindAll(doc.Text, query, caseSensitive);

            for (var i = search.Count - 1; i >= 0; i--)
            {
                var match = search[i];
                doc.Replace(match.Offset, match.Length,
                    new StringTextSource(replacement));
                count++;
            }

            return count;
        }
        finally
        {
            undoStack.EndUndoGroup();
        }
    }

    /// <inheritdoc />
    public bool ApplyFormattedDocument(string formattedText)
    {
        if (formattedText is null)
            return false;

        var doc = _textEditor.Document;
        if (string.Equals(doc.Text, formattedText, StringComparison.Ordinal))
            return true;

        // M0 locked caret/selection mapping after full-document replace.
        var caretBefore = _textEditor.CaretOffset;
        if (caretBefore < 0)
            caretBefore = 0;
        if (caretBefore > doc.TextLength)
            caretBefore = doc.TextLength;

        var preLocation = doc.GetLocation(caretBefore);
        var undoStack = doc.UndoStack;
        undoStack.StartUndoGroup();
        try
        {
            _isUpdatingFromViewModel = true;
            try
            {
                doc.Text = formattedText;
            }
            finally
            {
                _isUpdatingFromViewModel = false;
            }
        }
        finally
        {
            undoStack.EndUndoGroup();
        }

        var mappedCaret = MapCaretAfterFullReplace(doc, preLocation, caretBefore);
        _textEditor.CaretOffset = mappedCaret;
        _textEditor.SelectionStart = mappedCaret;
        _textEditor.SelectionLength = 0;

        if (ViewModel is not null && ViewModel.TextContent != _textEditor.Text)
            ViewModel.TextContent = _textEditor.Text;

        return true;
    }

    /// <inheritdoc />
    public bool TryUndo()
    {
        var undoStack = _textEditor.Document.UndoStack;
        if (!undoStack.CanUndo)
            return false;

        _isUpdatingFromViewModel = true;
        try
        {
            undoStack.Undo();
        }
        finally
        {
            _isUpdatingFromViewModel = false;
        }

        if (ViewModel is not null && ViewModel.TextContent != _textEditor.Text)
            ViewModel.TextContent = _textEditor.Text;

        return true;
    }

    /// <summary>
    /// Locked M6 caret mapping after whole-document formatting replacement
    /// (M0 proof: prefer same line/column when the line still exists).
    /// </summary>
    internal static int MapCaretAfterFullReplace(
        TextDocument document,
        TextLocation preLocation,
        int preOffset)
    {
        if (preLocation.Line >= 1 && preLocation.Line <= document.LineCount)
        {
            var line = document.GetLineByNumber(preLocation.Line);
            var column = Math.Clamp(preLocation.Column, 1, line.Length + 1);
            return document.GetOffset(preLocation.Line, column);
        }

        return Math.Clamp(preOffset, 0, document.TextLength);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _completionPopup.ItemConfirmed -= OnCompletionItemConfirmed;
        _definitionPicker.ItemConfirmed -= OnDefinitionItemConfirmed;
        _documentSymbolPicker.ItemConfirmed -= OnSymbolItemConfirmed;
        _workspaceSymbolPicker.ItemConfirmed -= OnSymbolItemConfirmed;
        _workspaceSymbolPicker.QueryChanged -= OnWorkspaceSymbolQueryChanged;
        _languageInput.DismissAll();
        ClearLanguagePresentation();
        _breakpointOperations.Dispose();
        _instructionPointerOperations.Dispose();
        _settingsBinding.Dispose();
    }
}
