using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;

namespace Zaide.ViewModels;

/// <summary>
/// ViewModel for a single editor tab. Manages text content, dirty tracking,
/// file path, and save operations. One instance per open tab (Transient).
/// </summary>
public class EditorViewModel : ReactiveObject
{
    private string _filePath = string.Empty;
    private string _fileName = string.Empty;
    private string _textContent = string.Empty;
    private bool _isDirty;

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
        private set => this.RaiseAndSetIfChanged(ref _fileName, value);
    }

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
        private set => this.RaiseAndSetIfChanged(ref _isDirty, value);
    }

    /// <summary>
    /// Read-only convenience flag — inverse of IsDirty.
    /// </summary>
    public bool IsSaved => !IsDirty;

    /// <summary>
    /// ReactiveCommand for saving the file. Placeholder in M1;
    /// full implementation in M4.
    /// </summary>
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public EditorViewModel()
    {
        SaveCommand = ReactiveCommand.Create(MarkClean);

        // Track dirty state: any change to TextContent marks the tab dirty.
        // M3 will add suppression logic for programmatic file loads.
        this.WhenAnyValue(x => x.TextContent)
            .Skip(1) // Skip the initial empty-string value
            .Subscribe(_ => IsDirty = true);
    }

    /// <summary>
    /// Resets the dirty flag. Called by SaveCommand (M4 will add file I/O).
    /// </summary>
    public void MarkClean()
    {
        IsDirty = false;
    }
}
