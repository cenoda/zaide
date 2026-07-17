using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using Zaide.App.Composition;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Editor.Contracts;

namespace Zaide.Features.Editor.Presentation;

/// <summary>
/// UI-independent search/replace state and command seam for the active document.
/// Registered as a singleton; the View provides <see cref="ActiveDocument"/> on activation
/// and on every active-tab switch.
/// <para>
/// Matching contract (locked by M3):
/// <list type="bullet">
/// <item>Literal text only — regex is never used.</item>
/// <item>Case-sensitive by default (<see cref="CaseSensitive"/> = true).</item>
/// <item>Find Next wraps from last match to first; Find Previous wraps from first to last.</item>
/// <item>Empty query: no search, no mutation, zero matches.</item>
/// <item>Zero matches: <see cref="StatusMessage"/> reports "No matches found".</item>
/// <item>Replace Next: replaces only when the current selection exactly covers the current match;
/// otherwise advances to the next match without replacing.</item>
/// <item>Replace All: replaces every literal match as one undoable action via
/// <see cref="IEditorTextOperations.ReplaceAllMatches"/>.</item>
/// <item>Dismiss/Cancel: closes the search surface without any document mutation.</item>
/// </list>
/// </para>
/// <para>
/// Tab-switch contract: setting <see cref="ActiveDocument"/> to a different value or null
/// resets all search state (query, matches, visibility, replace mode) so that stale state
/// from the old document never mutates the new one.
/// </para>
/// </summary>
public sealed class EditorSearchViewModel : ReactiveObject
{
    private readonly ICommandRegistry _registry;

    private string _query = string.Empty;
    private string _replacementText = string.Empty;
    private bool _caseSensitive = true;
    private bool _isVisible;
    private bool _isReplaceMode;
    private IReadOnlyList<SearchMatch> _matches = Array.Empty<SearchMatch>();
    private int _currentMatchIndex = -1;
    private int _matchCount;
    private string _statusMessage = string.Empty;
    private IEditorTextOperations? _activeDocument;
    private string? _activeDocumentId;

    /// <summary>Raised when the search surface should receive focus (e.g. after Find/Replace opens).</summary>
    public event Action? FocusRequested;

    /// <summary>
    /// Raised when <see cref="SelectCurrentMatch"/> runs, which updates the editor selection.
    /// The View should ensure the search query TextBox retains focus after this event,
    /// because the editor selection update may steal X11 input focus on Linux.
    /// </summary>
    public event Action? SelectionUpdated;

    /// <summary>
    /// The active document's text-operations seam. Set by the View on activation
    /// and on every active-tab switch. Setting to a different value (or null)
    /// resets all search state.
    /// </summary>
    public IEditorTextOperations? ActiveDocument
    {
        get => _activeDocument;
        set
        {
            if (ReferenceEquals(_activeDocument, value)) return;
            _activeDocument = value;
            this.RaisePropertyChanged();
            Reset();
        }
    }

    /// <summary>
    /// Identity of the active document (e.g. file path). Set by the View on every
    /// active-tab switch. When the same <see cref="EditorView"/> instance is reused
    /// across tabs, <see cref="ActiveDocument"/> alone cannot detect the switch
    /// (ReferenceEquals returns early). This property provides a separate identity
    /// check so that <see cref="Reset"/> fires on every tab change.
    /// </summary>
    public string? ActiveDocumentId
    {
        get => _activeDocumentId;
        set
        {
            if (_activeDocumentId == value
                && (_activeDocumentId is not null || _activeDocument is null))
                return;
            _activeDocumentId = value;
            this.RaisePropertyChanged();
            Reset();
        }
    }

    public string Query
    {
        get => _query;
        set
        {
            this.RaiseAndSetIfChanged(ref _query, value);
            PerformSearch();
        }
    }

    public string ReplacementText
    {
        get => _replacementText;
        set
        {
            if (_replacementText == value) return;
            _replacementText = value;
            this.RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Case-sensitivity flag. Default: true (case-sensitive, Ordinal comparison).
    /// Changing this re-runs the search.
    /// </summary>
    public bool CaseSensitive
    {
        get => _caseSensitive;
        set
        {
            this.RaiseAndSetIfChanged(ref _caseSensitive, value);
            PerformSearch();
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    public bool IsReplaceMode
    {
        get => _isReplaceMode;
        set => this.RaiseAndSetIfChanged(ref _isReplaceMode, value);
    }

    public IReadOnlyList<SearchMatch> Matches => _matches;

    /// <summary>
    /// 0-based index into <see cref="Matches"/> of the currently selected match.
    /// -1 when there are no matches.
    /// </summary>
    public int CurrentMatchIndex
    {
        get => _currentMatchIndex;
        private set
        {
            if (_currentMatchIndex == value) return;
            _currentMatchIndex = value;
            this.RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Total number of matches. Reactive — raises PropertyChanged so command
    /// availability observables re-evaluate.
    /// </summary>
    public int MatchCount
    {
        get => _matchCount;
        private set
        {
            if (_matchCount == value) return;
            _matchCount = value;
            this.RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Human-readable status: "No matches found", "1 of 5", etc.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    // ── Commands ─────────────────────────────────────────────────────────

    public ICommand FindCommand { get; }
    public ICommand ReplaceCommand { get; }
    public ICommand FindNextCommand { get; }
    public ICommand FindPreviousCommand { get; }
    public ICommand ReplaceNextCommand { get; }
    public ICommand ReplaceAllCommand { get; }

    public EditorSearchViewModel(ICommandRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));

        FindCommand = ReactiveCommand.Create(ExecuteFind,
            this.WhenAnyValue(x => x.ActiveDocument).Select(doc => doc != null));

        ReplaceCommand = ReactiveCommand.Create(ExecuteReplace,
            this.WhenAnyValue(x => x.ActiveDocument).Select(doc => doc != null));

        FindNextCommand = ReactiveCommand.Create(ExecuteFindNext,
            this.WhenAnyValue(x => x.ActiveDocument, x => x.Query, x => x.MatchCount,
                (doc, query, count) => doc != null && !string.IsNullOrEmpty(query) && count > 0));

        FindPreviousCommand = ReactiveCommand.Create(ExecuteFindPrevious,
            this.WhenAnyValue(x => x.ActiveDocument, x => x.Query, x => x.MatchCount,
                (doc, query, count) => doc != null && !string.IsNullOrEmpty(query) && count > 0));

        ReplaceNextCommand = ReactiveCommand.Create(ExecuteReplaceNext,
            this.WhenAnyValue(x => x.ActiveDocument, x => x.Query, x => x.MatchCount, x => x.IsReplaceMode,
                (doc, query, count, mode) => doc != null && !string.IsNullOrEmpty(query) && count > 0 && mode));

        ReplaceAllCommand = ReactiveCommand.Create(ExecuteReplaceAll,
            this.WhenAnyValue(x => x.ActiveDocument, x => x.Query, x => x.MatchCount, x => x.IsReplaceMode,
                (doc, query, count, mode) => doc != null && !string.IsNullOrEmpty(query) && count > 0 && mode));

        RegisterCommands();
    }

    // ── Command handlers ─────────────────────────────────────────────────

    private void ExecuteFind()
    {
        IsReplaceMode = false;
        IsVisible = true;
        PerformSearchWithSelection();
        FocusRequested?.Invoke();
    }

    private void ExecuteReplace()
    {
        IsReplaceMode = true;
        IsVisible = true;
        PerformSearchWithSelection();
        FocusRequested?.Invoke();
    }

    private void ExecuteFindNext()
    {
        if (Matches.Count == 0 || _activeDocument is null) return;
        CurrentMatchIndex = SearchEngine.NextMatchIndex(_currentMatchIndex, Matches.Count);
        SelectCurrentMatch();
    }

    private void ExecuteFindPrevious()
    {
        if (Matches.Count == 0 || _activeDocument is null) return;
        CurrentMatchIndex = SearchEngine.PreviousMatchIndex(_currentMatchIndex, Matches.Count);
        SelectCurrentMatch();
    }

    private void ExecuteReplaceNext()
    {
        if (_activeDocument is null || Matches.Count == 0) return;

        // Replace only when the current selection exactly covers the current match.
        if (_currentMatchIndex >= 0 && _currentMatchIndex < Matches.Count)
        {
            var current = Matches[_currentMatchIndex];
            var selOffset = _activeDocument.GetSelectionOffset();
            var selLength = _activeDocument.GetSelectionLength();

            if (selOffset == current.Offset && selLength == current.Length)
            {
                var text = _activeDocument.GetText();
                var before = text.Substring(0, current.Offset);
                var after = text.Substring(current.Offset + current.Length);
                var newText = before + _replacementText + after;
                _activeDocument.SetText(newText);

                // Re-search after mutation and advance.
                PerformSearch();
                if (Matches.Count > 0)
                {
                    var resumeOffset = current.Offset + _replacementText.Length;
                    var nextIdx = -1;
                    for (var i = 0; i < Matches.Count; i++)
                    {
                        if (Matches[i].Offset >= resumeOffset)
                        {
                            nextIdx = i;
                            break;
                        }
                    }
                    CurrentMatchIndex = nextIdx >= 0 ? nextIdx : 0;
                    SelectCurrentMatch();
                }
                return;
            }
        }

        // Selection doesn't match — advance without replacing.
        CurrentMatchIndex = SearchEngine.NextMatchIndex(_currentMatchIndex, Matches.Count);
        SelectCurrentMatch();
    }

    private void ExecuteReplaceAll()
    {
        if (_activeDocument is null || Matches.Count == 0) return;

        var count = _activeDocument.ReplaceAllMatches(_query, _replacementText, _caseSensitive);

        // Re-search after mutation.
        PerformSearch();

        StatusMessage = count > 0
            ? $"Replaced {count} occurrence{(count == 1 ? "" : "s")}"
            : "No matches found";
    }

    // ── Public actions ───────────────────────────────────────────────────

    /// <summary>
    /// Dismisses the search surface without any document mutation.
    /// Clears query, matches, selection, and replace mode.
    /// </summary>
    public void Dismiss()
    {
        IsVisible = false;
        IsReplaceMode = false;
        _query = string.Empty;
        _replacementText = string.Empty;
        _matches = Array.Empty<SearchMatch>();
        _currentMatchIndex = -1;
        MatchCount = 0;
        StatusMessage = string.Empty;
        this.RaisePropertyChanged(nameof(Query));
        this.RaisePropertyChanged(nameof(ReplacementText));
        this.RaisePropertyChanged(nameof(Matches));
        this.RaisePropertyChanged(nameof(CurrentMatchIndex));

        // Clear editor selection so no stale highlight remains from the old search.
        _activeDocument?.SetSelection(0, 0);
    }

    /// <summary>
    /// Full reset — same as <see cref="Dismiss"/> but also clears visibility.
    /// Called automatically when <see cref="ActiveDocument"/> changes.
    /// </summary>
    public void Reset()
    {
        Dismiss();
    }

    // ── Internal ─────────────────────────────────────────────────────────

    private void PerformSearch()
    {
        if (_activeDocument is null || string.IsNullOrEmpty(_query))
        {
            _matches = Array.Empty<SearchMatch>();
            CurrentMatchIndex = -1;
            MatchCount = 0;
            StatusMessage = string.Empty;
            this.RaisePropertyChanged(nameof(Matches));
            return;
        }

        var text = _activeDocument.GetText();
        _matches = SearchEngine.FindAll(text, _query, _caseSensitive);

        if (_matches.Count == 0)
        {
            CurrentMatchIndex = -1;
            MatchCount = 0;
            StatusMessage = "No matches found";
        }
        else
        {
            MatchCount = _matches.Count;
            CurrentMatchIndex = 0;
            StatusMessage = $"{_matches.Count} match{(_matches.Count == 1 ? "" : "es")}";
        }

        this.RaisePropertyChanged(nameof(Matches));
    }

    /// <summary>
    /// Runs a search AND selects the current match in the editor.
    /// Used on explicit search open (Find/Replace) so the user sees
    /// the first result highlighted. Not called during continuous
    /// typing to avoid stealing editor focus on Linux.
    /// </summary>
    private void PerformSearchWithSelection()
    {
        PerformSearch();
        SelectCurrentMatch();
    }

    private void SelectCurrentMatch()
    {
        if (_activeDocument is null || _currentMatchIndex < 0 || _currentMatchIndex >= Matches.Count)
            return;

        var match = Matches[_currentMatchIndex];
        _activeDocument.SetSelection(match.Offset, match.Length);
        StatusMessage = $"{_currentMatchIndex + 1} of {Matches.Count}";
        SelectionUpdated?.Invoke();
    }

    // ── Command registration ─────────────────────────────────────────────

    private void RegisterCommands()
    {
        _registry.Register(new CommandDescriptor(
            "editor.find", "Find", "Editor",
            new[] { "Ctrl+F" }, FindCommand));

        _registry.Register(new CommandDescriptor(
            "editor.replace", "Replace", "Editor",
            new[] { "Ctrl+H" }, ReplaceCommand));

        _registry.Register(new CommandDescriptor(
            "editor.findNext", "Find Next", "Editor",
            new[] { "F3" }, FindNextCommand));

        _registry.Register(new CommandDescriptor(
            "editor.findPrevious", "Find Previous", "Editor",
            new[] { "Shift+F3" }, FindPreviousCommand));

        _registry.Register(new CommandDescriptor(
            "editor.replaceNext", "Replace Next", "Editor",
            Array.Empty<string>(), ReplaceNextCommand));

        _registry.Register(new CommandDescriptor(
            "editor.replaceAll", "Replace All", "Editor",
            Array.Empty<string>(), ReplaceAllCommand));
    }
}
