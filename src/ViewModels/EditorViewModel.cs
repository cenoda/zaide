using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Zaide.Services;

namespace Zaide.ViewModels;

/// <summary>
/// ViewModel for a single editor tab. Manages text content, dirty tracking,
/// file path, and save operations. One instance per open tab (Transient).
/// </summary>
public class EditorViewModel : ReactiveObject
{
    private readonly IFileService _fileService;
    private string _filePath = string.Empty;
    private string _fileName = string.Empty;
    private string _textContent = string.Empty;
    private bool _isDirty;
    private bool _suppressDirty;

    /// <summary>
    /// Full path to the open file, or empty for new unsaved tabs.
    /// When set, FileName is derived from the path.
    /// </summary>
    public string FilePath
    {
        get => _filePath;
        set
        {
            this.RaiseAndSetIfChanged(ref _filePath, value);
            FileName = string.IsNullOrEmpty(value)
                ? "Untitled"
                : Path.GetFileName(value);
        }
    }

    /// <summary>
    /// Display name for the tab. Derived from FilePath.
    /// </summary>
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
    /// Current text content of the editor. Changes in M1 mark the tab as dirty.
    /// M3 will add file-load suppression to avoid dirty-on-open.
    /// </summary>
    public string TextContent
    {
        get => _textContent;
        set => this.RaiseAndSetIfChanged(ref _textContent, value);
    }

    /// <summary>
    /// True when content has been modified since the last save.
    /// </summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isDirty, value);
            this.RaisePropertyChanged(nameof(DisplayName));
            this.RaisePropertyChanged(nameof(IsSaved));
        }
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

    public EditorViewModel(IFileService fileService)
    {
        _fileService = fileService;
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);

        // Track dirty state: any change to TextContent marks the tab dirty,
        // unless LoadFileContent has temporarily suppressed tracking.
        this.WhenAnyValue(x => x.TextContent)
            .Skip(1) // Skip the initial empty-string value
            .Subscribe(_ =>
            {
                if (!_suppressDirty)
                    IsDirty = true;
            });
    }

    /// <summary>
    /// Loads file content without marking the tab as dirty.
    /// Sets TextContent while the dirty-tracking subscription is suppressed.
    /// </summary>
    public void LoadFileContent(string content)
    {
        _suppressDirty = true;
        TextContent = content;
        _suppressDirty = false;
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
            IsDirty = false;
            LastSaveError = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastSaveError = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Resets the dirty flag without writing to disk. Used by tests
    /// and internal logic where file I/O is not needed.
    /// </summary>
    public void MarkClean()
    {
        IsDirty = false;
    }
}
