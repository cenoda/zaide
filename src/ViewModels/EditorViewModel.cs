using System;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.ViewModels;

/// <summary>
/// ViewModel for a single editor tab. Manages text content, dirty tracking,
/// file path, and save operations. One instance per open tab (Transient).
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
    /// When set, FileName is derived from the path.
    /// </summary>
    public string FilePath
    {
        get => _document.FilePath;
        set
        {
            if (_document != null && _document.FilePath != value)
            {
                _document.FilePath = value;
                this.RaisePropertyChanged(nameof(FileName));
                // Update FileName since it's derived from FilePath
                FileName = string.IsNullOrEmpty(value)
                    ? "Untitled"
                    : Path.GetFileName(value);
            }
            else if (_document == null)
            {
                var newDocument = new Document(value, string.Empty);
                _document = newDocument;
                this.RaiseAndSetIfChanged(ref _document, _document);
                InitializeReactiveProperties();
                FileName = string.IsNullOrEmpty(value)
                    ? "Untitled"
                    : Path.GetFileName(value);
            }
            else
            {
                // If path is same as current, still ensure FileName reflects it (e.g. for empty paths)
                FileName = string.IsNullOrEmpty(value)
                    ? "Untitled"
                    : Path.GetFileName(value);
            }
        }
    }

    /// <summary>
    /// Display name for the tab. Derived from FilePath.
    /// </summary>

    private string _fileName = string.Empty;
    public string FileName
    {
        get => _fileName;
        private set
        {
            this.RaiseAndSetIfChanged(ref _fileName, value);
            this.RaisePropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Tab label shown in the tab bar. Prefixed with ● when the tab is dirty.
    /// </summary>
    public string DisplayName => IsDirty ? $"● {FileName}" : FileName;

    /// <summary>
    /// Current text content of the editor. Changes mark the tab as dirty.
    /// </summary>
    private string _textContent = string.Empty;
    public string TextContent
    {
        get => _textContent;
        set
        {
            this.RaiseAndSetIfChanged(ref _textContent, value);
            Document.Content = value;
        }
    }

    /// <summary>
    /// True when content has been modified since the last save.
    /// </summary>
    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        private set => this.RaiseAndSetIfChanged(ref _isDirty, value);
    }

    /// <summary>
    /// Read-only convenience flag — inverse of IsDirty.
    /// </summary>
    public bool IsSaved => !IsDirty;

    /// <summary>
    /// Error message from the last failed save. null when the last save
    /// succeeded or no save has been attempted yet.
    /// </summary>
    private string? _lastSaveError;
    public string? LastSaveError
    {
        get => _lastSaveError;
        private set => this.RaiseAndSetIfChanged(ref _lastSaveError, value);
    }

    /// <summary>
    /// ReactiveCommand for saving the file.
    /// </summary>
    public ReactiveCommand<Unit, bool> SaveCommand { get; }

    public EditorViewModel(Document document, IFileService fileService)
    {
        _document = document;
        _fileService = fileService;
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);

        InitializeReactiveProperties();
    }

    private void InitializeReactiveProperties()
    {
        if (_document == null) return;

        _textContent = _document.Content;
        _isDirty = _document.IsDirty;
        _lastSaveError = _document.LastSaveError;

        _document.ContentChanged += HandleContentChanged;
        _document.DirtyStateChanged += HandleDirtyStateChanged;
        _document.SaveErrorChanged += HandleSaveErrorChanged;
    }

    private void HandleContentChanged(object? sender, EventArgs e)
    {
        if (_document == null) return;
        _textContent = _document.Content;
        this.RaisePropertyChanged(nameof(TextContent));
        IsDirty = _document.IsDirty;
    }

    private void HandleDirtyStateChanged(object? sender, EventArgs e)
    {
        if (_document == null) return;
        IsDirty = _document.IsDirty;
    }

    private void HandleSaveErrorChanged(object? sender, EventArgs e)
    {
        if (_document == null) return;
        LastSaveError = _document.LastSaveError;
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
            await Document.SaveAsync(_fileService);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
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
