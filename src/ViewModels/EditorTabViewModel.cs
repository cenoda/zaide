using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.ViewModels;

/// <summary>
/// Manages the collection of open editor tabs. Handles opening files,
/// switching tabs, and closing tabs.
/// 
/// Registered as Singleton — one tab manager for the app lifetime.
/// Individual EditorViewModels are resolved as Transient via IServiceProvider.
/// </summary>
public class EditorTabViewModel : ReactiveObject
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private readonly IServiceProvider _services;
    private readonly IFileService _fileService;
    private readonly Workspace _workspace;
    private EditorViewModel? _activeTab;

    public ObservableCollection<EditorViewModel> OpenTabs { get; } = new();

    public EditorViewModel? ActiveTab
    {
        get => _activeTab;
        set
        {
            this.RaiseAndSetIfChanged(ref _activeTab, value);
            if (value != null)
            {
                _workspace.SetActiveDocument(value.Document);
            }
            else
            {
                _workspace.SetActiveDocument(null);
            }
        }
    }

    /// <summary>
    /// Raised when a dirty tab is about to close. The View subscribes and
    /// shows the unsaved-changes dialog. Returns: true = save then close,
    /// false = close without saving, null = cancel (don't close).
    /// </summary>
    public Interaction<EditorViewModel, bool?> ConfirmClose { get; } = new();

    /// <summary>
    /// Error from the most recent failed save, surfaced so the UI can display it.
    /// </summary>
    private string? _lastSaveError;
    public string? LastSaveError
    {
        get => _lastSaveError;
        set => this.RaiseAndSetIfChanged(ref _lastSaveError, value);
    }

    /// <summary>
    /// Error from the most recent failed file-open attempt, surfaced so the UI
    /// can display it instead of claiming success.
    /// </summary>
    private string? _lastOpenError;
    public string? LastOpenError
    {
        get => _lastOpenError;
        set => this.RaiseAndSetIfChanged(ref _lastOpenError, value);
    }

    public ReactiveCommand<string, bool> OpenFileCommand { get; }
    public ReactiveCommand<EditorViewModel, Unit> CloseTabCommand { get; }

    public EditorTabViewModel(IServiceProvider services, IFileService fileService, Workspace workspace)
    {
        _services = services;
        _fileService = fileService;
        _workspace = workspace;

        OpenFileCommand = ReactiveCommand.CreateFromTask<string, bool>(OpenFileAsync);
        CloseTabCommand = ReactiveCommand.CreateFromTask<EditorViewModel>(CloseTabAsync);
    }

    /// <summary>
    /// Opens a file in a new or existing tab. If a tab for the same path
    /// already exists, activates it instead of creating a duplicate.
    /// </summary>
    private async Task<bool> OpenFileAsync(string path)
    {
        var normalizedPath = NormalizePath(path);

        // Check if already open — activate existing tab
        var existing = OpenTabs.FirstOrDefault(t =>
            string.Equals(NormalizePath(t.FilePath), normalizedPath, PathComparison));
        if (existing is not null)
        {
            ActiveTab = existing;
            _workspace.SetActiveDocument(existing.Document);
            LastOpenError = null;
            return true;
        }

        // Read file content via service (async, no UI-thread blocking)
        string content;
        try
        {
            content = await _fileService.ReadAllTextAsync(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastOpenError = ex.Message;
            return false;
        }

        // Open document via Workspace
        var document = _workspace.OpenDocument(normalizedPath, content);
        var tab = new EditorViewModel(document, _services.GetRequiredService<IFileService>());

        OpenTabs.Add(tab);
        ActiveTab = tab;
        LastOpenError = null;
        return true;
    }

    /// <summary>
    /// Closes a tab. If the tab is dirty, raises ConfirmClose to prompt
    /// the user via the unsaved-changes dialog. Removes the tab from
    /// the collection and activates a neighbor if needed.
    /// </summary>
    private async Task CloseTabAsync(EditorViewModel tab)
    {
        if (tab.IsDirty)
        {
            var shouldSave = await ConfirmClose.Handle(tab);
            if (shouldSave == true)
            {
                // Save then close — only close if save succeeded
                var saved = await tab.SaveCommand.Execute();
                if (!saved)
                {
                    LastSaveError = tab.LastSaveError;
                    return;
                }
            }
            else if (shouldSave == false)
            {
                // Close without saving
            }
            else
            {
                // null — user cancelled, don't close
                return;
            }
        }

        var index = OpenTabs.IndexOf(tab);
        if (index < 0) return;

        _workspace.CloseDocument(tab.Document.FilePath);
        OpenTabs.RemoveAt(index);

        if (ReferenceEquals(ActiveTab, tab))
        {
            if (OpenTabs.Count == 0)
            {
                ActiveTab = null;
                _workspace.SetActiveDocument(null);
            }
            else if (index < OpenTabs.Count)
                ActiveTab = OpenTabs[index];
            else
                ActiveTab = OpenTabs[index - 1];
        }
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetFullPath(path);
    }
}
