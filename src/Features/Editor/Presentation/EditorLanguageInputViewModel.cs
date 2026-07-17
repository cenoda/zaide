using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using Zaide.Services;
using Unit = System.Reactive.Unit;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Editor.Contracts;

namespace Zaide.Features.Editor.Presentation;

/// <summary>
/// Routes editor input to Phase 10 language services and projects accepted results.
/// Registered as a singleton; the shared <see cref="Views.EditorView"/> sets
/// <see cref="ActiveEditor"/> and <see cref="ActiveDocumentId"/> on activation and tab switches.
/// Navigation always goes through <see cref="EditorTabViewModel.OpenFileCommand"/> then
/// <see cref="EditorViewModel.RequestNavigate"/>.
/// </summary>
public sealed class EditorLanguageInputViewModel : ReactiveObject
{
    private readonly ILanguageCompletionService _completionService;
    private readonly ILanguageHoverService _hoverService;
    private readonly ILanguageNavigationService _navigationService;
    private readonly ILanguageSymbolService _symbolService;
    private readonly ILanguageFormattingService _formattingService;
    private readonly ILanguageSessionService _sessionService;
    private readonly EditorTabViewModel _editorTabs;
    private readonly ICommandRegistry _registry;

    private IEditorLanguageOperations? _activeEditor;
    private string? _activeDocumentId;
    private string? _feedbackMessage;
    private int _navigationInFlight;
    private int _formatInFlight;

    public EditorLanguageInputViewModel(
        ILanguageCompletionService completionService,
        ILanguageHoverService hoverService,
        ILanguageNavigationService navigationService,
        ILanguageSymbolService symbolService,
        ILanguageFormattingService formattingService,
        ILanguageSessionService sessionService,
        EditorTabViewModel editorTabs,
        ICommandRegistry registry)
    {
        _completionService = completionService ?? throw new ArgumentNullException(nameof(completionService));
        _hoverService = hoverService ?? throw new ArgumentNullException(nameof(hoverService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _symbolService = symbolService ?? throw new ArgumentNullException(nameof(symbolService));
        _formattingService = formattingService ?? throw new ArgumentNullException(nameof(formattingService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _editorTabs = editorTabs ?? throw new ArgumentNullException(nameof(editorTabs));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));

        var availabilityChanged = Observable.Merge(
            this.WhenAnyValue(x => x.ActiveEditor, x => x.ActiveDocumentId).Select(_ => Unit.Default),
            _sessionService.WhenChanged.Select(_ => Unit.Default));

        CompletionWhenChanged = _completionService.WhenChanged;
        HoverWhenChanged = _hoverService.WhenChanged;
        NavigationWhenChanged = _navigationService.WhenChanged;
        SymbolWhenChanged = _symbolService.WhenChanged;
        FormattingWhenChanged = _formattingService.WhenChanged;

        TriggerSuggestCommand = ReactiveCommand.Create(
            TriggerExplicitCompletion,
            availabilityChanged.Select(_ => CanUseCompletion()));
        CompletionMoveUpCommand = ReactiveCommand.Create(() => _completionService.MoveSelection(-1));
        CompletionMoveDownCommand = ReactiveCommand.Create(() => _completionService.MoveSelection(1));
        CompletionCommitCommand = ReactiveCommand.Create(CommitCompletion);
        CompletionDismissCommand = ReactiveCommand.Create(DismissAll);

        GoToDefinitionCommand = ReactiveCommand.CreateFromTask(
            GoToDefinitionAsync,
            availabilityChanged.Select(_ => CanUseDefinition()));

        DocumentSymbolCommand = ReactiveCommand.Create(
            RequestDocumentSymbols,
            availabilityChanged.Select(_ => CanUseDocumentSymbols()));

        WorkspaceSymbolCommand = ReactiveCommand.Create(
            OpenWorkspaceSymbols,
            availabilityChanged.Select(_ => CanUseWorkspaceSymbols()));

        FormatDocumentCommand = ReactiveCommand.CreateFromTask(
            FormatDocumentAsync,
            availabilityChanged.Select(_ => CanUseFormatting()));

        DefinitionMoveUpCommand = ReactiveCommand.Create(() => _navigationService.MoveSelection(-1));
        DefinitionMoveDownCommand = ReactiveCommand.Create(() => _navigationService.MoveSelection(1));
        DefinitionAcceptCommand = ReactiveCommand.CreateFromTask(AcceptDefinitionSelectionAsync);
        DefinitionDismissCommand = ReactiveCommand.Create(() => _navigationService.Dismiss());

        SymbolMoveUpCommand = ReactiveCommand.Create(() => _symbolService.MoveSelection(-1));
        SymbolMoveDownCommand = ReactiveCommand.Create(() => _symbolService.MoveSelection(1));
        SymbolAcceptCommand = ReactiveCommand.CreateFromTask(AcceptSymbolSelectionAsync);
        SymbolDismissCommand = ReactiveCommand.Create(() => _symbolService.Dismiss());

        // Auto-navigate single definition results; project feedback for terminal states.
        _ = _navigationService.WhenChanged.Subscribe(OnNavigationSnapshot);
        _ = _symbolService.WhenChanged.Subscribe(OnSymbolSnapshot);
        _ = _formattingService.WhenChanged.Subscribe(OnFormattingSnapshot);

        RegisterCommands();
    }

    /// <summary>Completion snapshots from the language service.</summary>
    public IObservable<LanguageCompletionSnapshot> CompletionWhenChanged { get; }

    /// <summary>Hover snapshots from the language service.</summary>
    public IObservable<LanguageHoverSnapshot> HoverWhenChanged { get; }

    /// <summary>Definition snapshots from the language service.</summary>
    public IObservable<LanguageNavigationSnapshot> NavigationWhenChanged { get; }

    /// <summary>Symbol surface snapshots from the language service.</summary>
    public IObservable<LanguageSymbolSnapshot> SymbolWhenChanged { get; }

    /// <summary>Formatting snapshots from the language service.</summary>
    public IObservable<LanguageFormattingSnapshot> FormattingWhenChanged { get; }

    /// <summary>Current completion snapshot for one-shot view reads.</summary>
    public LanguageCompletionSnapshot Completion => _completionService.Current;

    /// <summary>Current hover snapshot for one-shot view reads.</summary>
    public LanguageHoverSnapshot Hover => _hoverService.Current;

    /// <summary>Current definition snapshot for one-shot view reads.</summary>
    public LanguageNavigationSnapshot Navigation => _navigationService.Current;

    /// <summary>Current symbol snapshot for one-shot view reads.</summary>
    public LanguageSymbolSnapshot Symbols => _symbolService.Current;

    /// <summary>Current formatting snapshot for one-shot view reads.</summary>
    public LanguageFormattingSnapshot Formatting => _formattingService.Current;

    /// <summary>
    /// Transient truthful feedback for definition/symbol outcomes (status bar projection).
    /// </summary>
    public string? FeedbackMessage
    {
        get => _feedbackMessage;
        private set => this.RaiseAndSetIfChanged(ref _feedbackMessage, value);
    }

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

    public ReactiveCommand<Unit, Unit> GoToDefinitionCommand { get; }
    public ReactiveCommand<Unit, Unit> DocumentSymbolCommand { get; }
    public ReactiveCommand<Unit, Unit> WorkspaceSymbolCommand { get; }
    public ReactiveCommand<Unit, Unit> FormatDocumentCommand { get; }

    public ReactiveCommand<Unit, Unit> DefinitionMoveUpCommand { get; }
    public ReactiveCommand<Unit, Unit> DefinitionMoveDownCommand { get; }
    public ReactiveCommand<Unit, Unit> DefinitionAcceptCommand { get; }
    public ReactiveCommand<Unit, Unit> DefinitionDismissCommand { get; }

    public ReactiveCommand<Unit, Unit> SymbolMoveUpCommand { get; }
    public ReactiveCommand<Unit, Unit> SymbolMoveDownCommand { get; }
    public ReactiveCommand<Unit, Unit> SymbolAcceptCommand { get; }
    public ReactiveCommand<Unit, Unit> SymbolDismissCommand { get; }

    /// <summary>Updates the workspace-symbol query (cancels/replaces outstanding work).</summary>
    public void SetWorkspaceSymbolQuery(string query) => _symbolService.SetWorkspaceQuery(query);

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

    /// <summary>Dismisses completion, hover, definition chooser, and symbol surfaces.</summary>
    public void DismissAll()
    {
        _completionService.Dismiss();
        _hoverService.Dismiss();
        _navigationService.Dismiss();
        _symbolService.Dismiss();
    }

    private void OnNavigationSnapshot(LanguageNavigationSnapshot snapshot)
    {
        if (snapshot.State is LanguageNavigationState.Empty
            or LanguageNavigationState.Unavailable
            or LanguageNavigationState.Failed)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.FeedbackMessage))
                FeedbackMessage = snapshot.FeedbackMessage;
            return;
        }

        if (snapshot.IsSingleNavigateReady &&
            Interlocked.CompareExchange(ref _navigationInFlight, 1, 0) == 0)
        {
            _ = NavigateSingleDefinitionAsync();
        }
    }

    private void OnSymbolSnapshot(LanguageSymbolSnapshot snapshot)
    {
        if (snapshot.State is LanguageSymbolState.Empty
            or LanguageSymbolState.Unavailable
            or LanguageSymbolState.Failed)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.FeedbackMessage))
                FeedbackMessage = snapshot.FeedbackMessage;
        }
    }

    private async Task NavigateSingleDefinitionAsync()
    {
        try
        {
            var location = _navigationService.TryTakeSingleLocation();
            if (location is null)
                return;

            var navigated = await NavigateToLocationAsync(location).ConfigureAwait(true);
            if (!navigated)
                FeedbackMessage = LanguageNavigationPolicy.InvalidMessage;
        }
        finally
        {
            Interlocked.Exchange(ref _navigationInFlight, 0);
        }
    }

    private async Task GoToDefinitionAsync()
    {
        if (!TryGetActiveContext(out var filePath, out var caretOffset))
        {
            FeedbackMessage = LanguageNavigationPolicy.UnavailableMessage;
            return;
        }

        _completionService.Dismiss();
        _hoverService.Dismiss();
        _symbolService.Dismiss();
        _navigationService.RequestDefinition(filePath, caretOffset);

        // Allow Loading → Ready/Empty/Failed to publish; single-result auto-nav handles Ready.
        await Task.Yield();
    }

    private void RequestDocumentSymbols()
    {
        if (!TryGetActiveContext(out var filePath, out _))
        {
            FeedbackMessage = LanguageSymbolPolicy.DocumentUnavailableMessage;
            return;
        }

        _completionService.Dismiss();
        _hoverService.Dismiss();
        _navigationService.Dismiss();
        _symbolService.RequestDocumentSymbols(filePath);
    }

    private void OpenWorkspaceSymbols()
    {
        _completionService.Dismiss();
        _hoverService.Dismiss();
        _navigationService.Dismiss();
        _symbolService.RequestWorkspaceSymbols(string.Empty);
    }

    private async Task AcceptDefinitionSelectionAsync()
    {
        var location = _navigationService.TryAcceptSelected();
        if (location is null)
            return;

        var navigated = await NavigateToLocationAsync(location).ConfigureAwait(true);
        if (!navigated)
            FeedbackMessage = LanguageNavigationPolicy.InvalidMessage;
    }

    private async Task AcceptSymbolSelectionAsync()
    {
        var location = _symbolService.TryAcceptSelected();
        if (location is null)
            return;

        var navigated = await NavigateToLocationAsync(location).ConfigureAwait(true);
        if (!navigated)
            FeedbackMessage = LanguageNavigationPolicy.InvalidMessage;
    }

    /// <summary>
    /// Opens/activates the target through the existing tab path, re-validates the range
    /// against the live document text, then requests selection via <see cref="EditorViewModel.RequestNavigate"/>.
    /// </summary>
    internal async Task<bool> NavigateToLocationAsync(LanguageLocation location)
    {
        if (location is null || string.IsNullOrWhiteSpace(location.FilePath))
            return false;

        var opened = await _editorTabs.OpenFileCommand.Execute(location.FilePath).FirstAsync();
        if (!opened)
            return false;

        var tab = _editorTabs.ActiveTab;
        if (tab is null ||
            !string.Equals(tab.FilePath, location.FilePath, StringComparison.Ordinal))
        {
            return false;
        }

        var content = tab.Document.Content;
        if (!LspUtf16PositionMapper.TryMapRange(
                content,
                location.Range,
                out var startOffset,
                out var endOffset))
        {
            return false;
        }

        if (startOffset < 0 || endOffset < startOffset || endOffset > content.Length)
            return false;

        tab.RequestNavigate(startOffset, endOffset - startOffset);
        return true;
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

    private bool CanUseCompletion() =>
        _activeEditor is not null &&
        LanguageCommandAvailability.CanUseActiveDocumentFeature(
            _sessionService,
            _activeDocumentId,
            c => c.CompletionSupported);

    private bool CanUseDefinition() =>
        _activeEditor is not null &&
        LanguageCommandAvailability.CanUseActiveDocumentFeature(
            _sessionService,
            _activeDocumentId,
            c => c.DefinitionSupported);

    private bool CanUseDocumentSymbols() =>
        _activeEditor is not null &&
        LanguageCommandAvailability.CanUseActiveDocumentFeature(
            _sessionService,
            _activeDocumentId,
            c => c.DocumentSymbolSupported);

    private bool CanUseWorkspaceSymbols() =>
        LanguageCommandAvailability.CanUseWorkspaceSymbols(_sessionService);

    private bool CanUseFormatting() =>
        _activeEditor is not null &&
        LanguageCommandAvailability.CanUseActiveDocumentFeature(
            _sessionService,
            _activeDocumentId,
            c => c.DocumentFormattingSupported);

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

        _registry.Register(new CommandDescriptor(
            LanguageNavigationPolicy.GoToDefinitionCommandId,
            "Go to Definition",
            "Editor",
            LanguageNavigationPolicy.GoToDefinitionDefaultGestures,
            GoToDefinitionCommand));

        _registry.Register(new CommandDescriptor(
            LanguageSymbolPolicy.DocumentSymbolCommandId,
            "Go to Symbol in Editor",
            "Editor",
            LanguageSymbolPolicy.DocumentSymbolDefaultGestures,
            DocumentSymbolCommand));

        _registry.Register(new CommandDescriptor(
            LanguageSymbolPolicy.WorkspaceSymbolCommandId,
            "Go to Symbol in Workspace",
            "Editor",
            LanguageSymbolPolicy.WorkspaceSymbolDefaultGestures,
            WorkspaceSymbolCommand));

        _registry.Register(new CommandDescriptor(
            LanguageFormattingPolicy.FormatDocumentCommandId,
            "Format Document",
            "Editor",
            LanguageFormattingPolicy.FormatDocumentDefaultGestures,
            FormatDocumentCommand));
    }

    private void OnFormattingSnapshot(LanguageFormattingSnapshot snapshot)
    {
        if (snapshot.State is LanguageFormattingState.Unavailable
            or LanguageFormattingState.Unsupported
            or LanguageFormattingState.Failed
            or LanguageFormattingState.Invalid
            or LanguageFormattingState.Cancelled
            or LanguageFormattingState.Stale
            or LanguageFormattingState.NoEdits
            or LanguageFormattingState.Ready)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.FeedbackMessage))
                FeedbackMessage = snapshot.FeedbackMessage;
        }
    }

    private async Task FormatDocumentAsync()
    {
        if (Interlocked.CompareExchange(ref _formatInFlight, 1, 0) != 0)
            return;

        try
        {
            if (!TryGetActiveContext(out var filePath, out _))
            {
                FeedbackMessage = LanguageFormattingPolicy.UnavailableMessage;
                return;
            }

            var editor = _activeEditor;
            var documentId = _activeDocumentId;
            if (editor is null ||
                string.IsNullOrWhiteSpace(documentId) ||
                !string.Equals(documentId, filePath, StringComparison.Ordinal))
            {
                FeedbackMessage = LanguageFormattingPolicy.UnavailableMessage;
                return;
            }

            var outcome = await _formattingService
                .FormatDocumentAsync(filePath, CancellationToken.None)
                .ConfigureAwait(true);

            // Re-validate active context so a tab switch cannot apply text to
            // an outgoing/inactive document.
            if (!ReferenceEquals(_activeEditor, editor) ||
                !string.Equals(_activeDocumentId, filePath, StringComparison.Ordinal))
            {
                FeedbackMessage = LanguageFormattingPolicy.CancelledMessage;
                return;
            }

            if (!outcome.IsAccepted)
            {
                if (!string.IsNullOrWhiteSpace(outcome.FeedbackMessage))
                    FeedbackMessage = outcome.FeedbackMessage;
                return;
            }

            if (outcome.HasTextChange && outcome.FormattedText is not null)
            {
                if (!editor.ApplyFormattedDocument(outcome.FormattedText))
                {
                    FeedbackMessage = LanguageFormattingPolicy.FailedMessage;
                    return;
                }

                FeedbackMessage = LanguageFormattingPolicy.AppliedMessage;
            }
            else
            {
                // No edits — do not claim a format was applied.
                FeedbackMessage = LanguageFormattingPolicy.NoEditsMessage;
            }
        }
        finally
        {
            Interlocked.Exchange(ref _formatInFlight, 0);
        }
    }
}
