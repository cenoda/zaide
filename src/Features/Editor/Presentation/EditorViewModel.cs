using System;
using System.IO;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using Zaide.Models;
using Zaide.Services;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Language.Contracts;

namespace Zaide.Features.Editor.Presentation;

/// <summary>
/// ViewModel for a single editor tab. Delegates all file state to
/// <see cref="Models.Document"/>. One instance per open tab (Transient).
/// </summary>
public class EditorViewModel : ReactiveObject
{
    private readonly IFileService _fileService;
    private readonly ISettingsService? _settingsService;
    private readonly ILanguageFormattingService? _formattingService;
    private int _formatOnSaveInFlight;
    private Document _document;

    public Document Document
    {
        get => _document;
        private set => this.RaiseAndSetIfChanged(ref _document, value);
    }

    /// <summary>
    /// Full path to the open file, or empty for new unsaved tabs.
    /// Delegates to <see cref="Models.Document.FilePath"/>.
    /// </summary>
    public string FilePath
    {
        get => _document.FilePath;
        set
        {
            if (_document.FilePath == value) return;
            _document.FilePath = value;
            this.RaisePropertyChanged(nameof(FilePath));
            this.RaisePropertyChanged(nameof(FileName));
            this.RaisePropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Display name for the tab. Derived from FilePath.
    /// </summary>
    public string FileName =>
        string.IsNullOrEmpty(Document.FilePath)
            ? "Untitled"
            : Path.GetFileName(Document.FilePath);

    /// <summary>
    /// Tab label shown in the tab bar. Prefixed with ● when the tab is dirty.
    /// Source Control diff tabs show the repository path and comparison state.
    /// </summary>
    public string DisplayName
    {
        get
        {
            if (IsSourceControlDiff)
            {
                var path = SourceControlDiffKey ?? FileName;
                var state = SourceControlComparisonState ?? "Diff";
                return $"{path} — {state}";
            }

            return IsDirty ? $"● {FileName}" : FileName;
        }
    }

    /// <summary>
    /// When true, the editor surface is read-only and cannot alter git or disk state.
    /// </summary>
    private bool _isReadOnly;
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => this.RaiseAndSetIfChanged(ref _isReadOnly, value);
    }

    /// <summary>
    /// True for tabs opened from Source Control diff selection.
    /// </summary>
    private bool _isSourceControlDiff;
    public bool IsSourceControlDiff
    {
        get => _isSourceControlDiff;
        set
        {
            this.RaiseAndSetIfChanged(ref _isSourceControlDiff, value);
            this.RaisePropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Repository-relative path used to reuse a Source Control diff tab.
    /// </summary>
    private string? _sourceControlDiffKey;
    public string? SourceControlDiffKey
    {
        get => _sourceControlDiffKey;
        set
        {
            this.RaiseAndSetIfChanged(ref _sourceControlDiffKey, value);
            this.RaisePropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Human-readable comparison state for a Source Control diff tab.
    /// </summary>
    private string? _sourceControlComparisonState;
    public string? SourceControlComparisonState
    {
        get => _sourceControlComparisonState;
        set
        {
            this.RaiseAndSetIfChanged(ref _sourceControlComparisonState, value);
            this.RaisePropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Current text content of the editor. Delegates to <see cref="Models.Document.Content"/>.
    /// Setting this value marks the tab as dirty via the Document model.
    /// </summary>
    public string TextContent
    {
        get => Document.Content;
        set => Document.Content = value;
    }

    /// <summary>
    /// True when content has been modified since the last save.
    /// Delegates to <see cref="Models.Document.IsDirty"/>.
    /// </summary>
    public bool IsDirty => Document.IsDirty;

    /// <summary>
    /// Read-only convenience flag — inverse of IsDirty.
    /// </summary>
    public bool IsSaved => !IsDirty;

    /// <summary>
    /// Error message from the last failed save. null when the last save
    /// succeeded or no save has been attempted yet.
    /// Delegates to <see cref="Models.Document.LastSaveError"/>.
    /// </summary>
    public string? LastSaveError => Document.LastSaveError;

    /// <summary>
    /// Current caret line position (1-based). Updated by EditorView.
    /// </summary>
    private int _caretLine = 1;
    public int CaretLine
    {
        get => _caretLine;
        set => this.RaiseAndSetIfChanged(ref _caretLine, value);
    }

    /// <summary>
    /// Current caret column position (1-based). Updated by EditorView.
    /// </summary>
    private int _caretColumn = 1;
    public int CaretColumn
    {
        get => _caretColumn;
        set => this.RaiseAndSetIfChanged(ref _caretColumn, value);
    }

    /// <summary>
    /// Monotonic token for pending editor navigation requests (Problems, etc.).
    /// EditorView observes this and applies selection/caret when it changes.
    /// </summary>
    private long _navigationRequestId;
    public long NavigationRequestId
    {
        get => _navigationRequestId;
        private set => this.RaiseAndSetIfChanged(ref _navigationRequestId, value);
    }

    /// <summary>Pending navigation start offset, or null when none.</summary>
    private int? _pendingNavigationOffset;
    public int? PendingNavigationOffset
    {
        get => _pendingNavigationOffset;
        private set => this.RaiseAndSetIfChanged(ref _pendingNavigationOffset, value);
    }

    /// <summary>Pending navigation selection length (0 = caret only).</summary>
    private int _pendingNavigationLength;
    public int PendingNavigationLength
    {
        get => _pendingNavigationLength;
        private set => this.RaiseAndSetIfChanged(ref _pendingNavigationLength, value);
    }

    /// <summary>
    /// Requests that the shared EditorView select <paramref name="offset"/> with
    /// <paramref name="length"/> characters. Validated again by the view against
    /// the live document length before applying.
    /// </summary>
    public void RequestNavigate(int offset, int length)
    {
        if (offset < 0)
            return;

        PendingNavigationOffset = offset;
        PendingNavigationLength = Math.Max(0, length);
        NavigationRequestId++;
    }

    /// <summary>
    /// Clears a consumed navigation request so it is not re-applied on tab switch.
    /// </summary>
    public void ClearNavigationRequest()
    {
        PendingNavigationOffset = null;
        PendingNavigationLength = 0;
    }

    // ── Selection state — updated by EditorView ───────────────────────────

    /// <summary>
    /// Offset of the selection start in the document. 0 when no selection.
    /// </summary>
    private int _selectionStart;
    public int SelectionStart
    {
        get => _selectionStart;
        set => this.RaiseAndSetIfChanged(ref _selectionStart, value);
    }

    /// <summary>
    /// Length of the selection in characters. 0 when no selection.
    /// </summary>
    private int _selectionLength;
    public int SelectionLength
    {
        get => _selectionLength;
        set => this.RaiseAndSetIfChanged(ref _selectionLength, value);
    }

    /// <summary>
    /// The currently selected text. null when no selection.
    /// </summary>
    private string? _selectionText;
    public string? SelectionText
    {
        get => _selectionText;
        set => this.RaiseAndSetIfChanged(ref _selectionText, value);
    }

    /// <summary>
    /// ReactiveCommand for saving the file.
    /// </summary>
    public ReactiveCommand<Unit, bool> SaveCommand { get; }

    /// <summary>
    /// Creates a tab view model. Settings and formatting services are optional so
    /// existing tests keep working; production wires both for Format on Save.
    /// </summary>
    public EditorViewModel(
        Document document,
        IFileService fileService,
        ISettingsService? settingsService = null,
        ILanguageFormattingService? formattingService = null)
    {
        _document = document;
        _fileService = fileService;
        _settingsService = settingsService;
        _formattingService = formattingService;
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);

        // Subscribe to Document events to propagate changes to reactive properties.
        // Document is a plain model (not ReactiveObject), so we bridge via events.
        _document.ContentChanged += (_, _) => this.RaisePropertyChanged(nameof(TextContent));
        _document.DirtyStateChanged += (_, _) =>
        {
            this.RaisePropertyChanged(nameof(IsDirty));
            this.RaisePropertyChanged(nameof(IsSaved));
            this.RaisePropertyChanged(nameof(DisplayName));
        };
        _document.SaveErrorChanged += (_, _) => this.RaisePropertyChanged(nameof(LastSaveError));
    }

    /// <summary>
    /// Loads file content without marking the tab as dirty.
    /// Sets TextContent while the dirty-tracking subscription is suppressed.
    /// </summary>
    public void LoadFileContent(string content)
    {
        var wasDirty = Document.IsDirty;
        Document.Content = content;
        if (!wasDirty)
        {
            Document.MarkClean();
        }
    }

    /// <summary>
    /// Writes TextContent to FilePath via the file service, then clears the
    /// dirty flag. When Format on Save is enabled, requests formatting before
    /// the write (M0 locked contract): accepted formatting updates in-memory
    /// content first; failure/cancellation/unsupported still saves current text.
    /// Formatting never re-triggers save.
    /// Returns true on success, false on failure or empty path.
    /// </summary>
    private async Task<bool> SaveAsync()
    {
        if (string.IsNullOrEmpty(FilePath))
            return false;

        // Guard against recursive save if formatting somehow re-entered Save.
        if (Interlocked.CompareExchange(ref _formatOnSaveInFlight, 1, 0) != 0)
            return false;

        try
        {
            await TryFormatOnSaveAsync().ConfigureAwait(false);

            await _fileService.WriteAllTextAsync(FilePath, TextContent).ConfigureAwait(false);
            Document.MarkClean();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Document.RecordSaveError(ex.Message);
            return false;
        }
        finally
        {
            Interlocked.Exchange(ref _formatOnSaveInFlight, 0);
        }
    }

    /// <summary>
    /// Format-on-Save coordination: format before write when enabled. Never
    /// blocks save on formatting failure; never re-enters <see cref="SaveAsync"/>.
    /// </summary>
    private async Task TryFormatOnSaveAsync()
    {
        if (_settingsService is null || _formattingService is null)
            return;

        if (!_settingsService.Current.Editor.FormatOnSave)
            return;

        if (string.IsNullOrEmpty(FilePath))
            return;

        try
        {
            var outcome = await _formattingService
                .FormatDocumentAsync(FilePath, CancellationToken.None)
                .ConfigureAwait(false);

            if (outcome.HasTextChange && outcome.FormattedText is not null)
            {
                // Update in-memory document only; dirty state stays authoritative.
                // Save continues with the updated TextContent in a single write.
                Document.Content = outcome.FormattedText;
            }
        }
        catch (OperationCanceledException)
        {
            // Still save current content.
        }
        catch
        {
            // Formatting is a presentation enhancement; never block save.
        }
    }

    /// <summary>
    /// Resets the dirty flag without writing to disk. Used by tests
    /// and internal logic where file I/O is not needed.
    /// </summary>
    public void MarkClean()
    {
        Document.MarkClean();
    }
}
