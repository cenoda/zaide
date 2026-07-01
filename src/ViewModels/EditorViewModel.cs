using System;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.ViewModels;

/// <summary>
/// ViewModel for a single editor tab. Delegates all file state to
/// <see cref="Models.Document"/>. One instance per open tab (Transient).
/// </summary>
public class EditorViewModel : ReactiveObject
{
    private readonly IFileService _fileService;
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
    /// </summary>
    public string DisplayName => IsDirty ? $"● {FileName}" : FileName;

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
    /// ReactiveCommand for saving the file.
    /// </summary>
    public ReactiveCommand<Unit, bool> SaveCommand { get; }

    public EditorViewModel(Document document, IFileService fileService)
    {
        _document = document;
        _fileService = fileService;
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
    /// dirty flag. Returns true on success, false on failure or empty path.
    /// </summary>
    private async Task<bool> SaveAsync()
    {
        if (string.IsNullOrEmpty(FilePath))
            return false;

        try
        {
            await _fileService.WriteAllTextAsync(FilePath, TextContent);
            Document.MarkClean();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Document.RecordSaveError(ex.Message);
            return false;
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
