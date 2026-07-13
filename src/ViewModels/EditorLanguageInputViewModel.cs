using System;
using System.Reactive;
using System.Windows.Input;
using ReactiveUI;
using Zaide.Services;

namespace Zaide.ViewModels;

/// <summary>
/// Routes editor input to Phase 10 completion/hover services and projects accepted results.
/// Registered as a singleton; the shared <see cref="Views.EditorView"/> sets
/// <see cref="ActiveEditor"/> and <see cref="ActiveDocumentId"/> on activation and tab switches.
/// </summary>
public sealed class EditorLanguageInputViewModel : ReactiveObject
{
    private readonly ILanguageCompletionService _completionService;
    private readonly ILanguageHoverService _hoverService;
    private readonly ICommandRegistry _registry;

    private IEditorLanguageOperations? _activeEditor;
    private string? _activeDocumentId;

    public EditorLanguageInputViewModel(
        ILanguageCompletionService completionService,
        ILanguageHoverService hoverService,
        ICommandRegistry registry)
    {
        _completionService = completionService ?? throw new ArgumentNullException(nameof(completionService));
        _hoverService = hoverService ?? throw new ArgumentNullException(nameof(hoverService));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));

        CompletionWhenChanged = _completionService.WhenChanged;
        HoverWhenChanged = _hoverService.WhenChanged;

        TriggerSuggestCommand = ReactiveCommand.Create(
            TriggerExplicitCompletion,
            this.WhenAnyValue(x => x.ActiveEditor, x => x.ActiveDocumentId, (_, _) => CanTriggerSuggest()));
        CompletionMoveUpCommand = ReactiveCommand.Create(() => _completionService.MoveSelection(-1));
        CompletionMoveDownCommand = ReactiveCommand.Create(() => _completionService.MoveSelection(1));
        CompletionCommitCommand = ReactiveCommand.Create(CommitCompletion);
        CompletionDismissCommand = ReactiveCommand.Create(DismissAll);

        RegisterCommands();
    }

    /// <summary>Completion snapshots from the language service.</summary>
    public IObservable<LanguageCompletionSnapshot> CompletionWhenChanged { get; }

    /// <summary>Hover snapshots from the language service.</summary>
    public IObservable<LanguageHoverSnapshot> HoverWhenChanged { get; }

    /// <summary>Current completion snapshot for one-shot view reads.</summary>
    public LanguageCompletionSnapshot Completion => _completionService.Current;

    /// <summary>Current hover snapshot for one-shot view reads.</summary>
    public LanguageHoverSnapshot Hover => _hoverService.Current;

    /// <summary>
    /// Active editor seam. Setting a different value dismisses transient language UI.
    /// </summary>
    public IEditorLanguageOperations? ActiveEditor
    {
        get => _activeEditor;
        set
        {
            if (ReferenceEquals(_activeEditor, value))
                return;

            _activeEditor = value;
            this.RaisePropertyChanged();
            DismissAll();
        }
    }

    /// <summary>
    /// Identity of the active document (file path or untitled token).
    /// Setting a different value dismisses transient language UI.
    /// </summary>
    public string? ActiveDocumentId
    {
        get => _activeDocumentId;
        set
        {
            if (string.Equals(_activeDocumentId, value, StringComparison.Ordinal))
                return;

            _activeDocumentId = value;
            this.RaisePropertyChanged();
            DismissAll();
        }
    }

    public ReactiveCommand<Unit, Unit> TriggerSuggestCommand { get; }
    public ReactiveCommand<Unit, Unit> CompletionMoveUpCommand { get; }
    public ReactiveCommand<Unit, Unit> CompletionMoveDownCommand { get; }
    public ReactiveCommand<Unit, Unit> CompletionCommitCommand { get; }
    public ReactiveCommand<Unit, Unit> CompletionDismissCommand { get; }

    /// <summary>Notifies services that the caret moved in the active editor.</summary>
    public void OnCaretMoved()
    {
        if (!TryGetActiveContext(out var filePath, out var caretOffset))
            return;

        _hoverService.Schedule(filePath, caretOffset);
    }

    /// <summary>Notifies services that text changed after a typed character.</summary>
    public void OnTextEdited(char? typedCharacter)
    {
        if (!TryGetActiveContext(out var filePath, out var caretOffset))
            return;

        _hoverService.Schedule(filePath, caretOffset);

        if (typedCharacter is char trigger)
            _completionService.RequestAutomatic(filePath, caretOffset, trigger);
    }

    /// <summary>Dismisses completion and hover UI/state.</summary>
    public void DismissAll()
    {
        _completionService.Dismiss();
        _hoverService.Dismiss();
    }

    private void TriggerExplicitCompletion()
    {
        if (!TryGetActiveContext(out var filePath, out var caretOffset))
            return;

        _completionService.RequestExplicit(filePath, caretOffset);
    }

    private void CommitCompletion()
    {
        var commit = _completionService.TryCommitSelected();
        if (commit is null || _activeEditor is null)
            return;

        if (!string.Equals(_activeDocumentId, commit.FilePath, StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(commit.FilePath))
        {
            return;
        }

        if (commit.ReplaceStartOffset < 0 ||
            commit.ReplaceLength < 0 ||
            commit.ReplaceStartOffset + commit.ReplaceLength > _activeEditor.GetText().Length)
        {
            return;
        }

        _activeEditor.ReplaceRange(commit.ReplaceStartOffset, commit.ReplaceLength, commit.InsertText);
        _activeEditor.SetSelection(
            commit.ReplaceStartOffset + commit.InsertText.Length,
            0);
    }

    private bool CanTriggerSuggest()
    {
        return TryGetActiveContext(out _, out _);
    }

    private bool TryGetActiveContext(out string filePath, out int caretOffset)
    {
        filePath = string.Empty;
        caretOffset = 0;

        if (_activeEditor is null || string.IsNullOrWhiteSpace(_activeDocumentId))
            return false;

        filePath = _activeDocumentId;
        if (filePath.StartsWith("__untitled_", StringComparison.Ordinal))
            return false;

        if (!LanguageDocumentSyncPolicy.IsEligiblePath(filePath))
            return false;

        caretOffset = _activeEditor.GetCaretOffset();
        return true;
    }

    private void RegisterCommands()
    {
        _registry.Register(new CommandDescriptor(
            LanguageCompletionTriggerPolicy.ExplicitCommandId,
            "Trigger Suggest",
            "Editor",
            LanguageCompletionTriggerPolicy.ExplicitDefaultGestures,
            TriggerSuggestCommand));
    }
}
