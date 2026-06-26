using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

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
    private readonly IServiceProvider _services;
    private EditorViewModel? _activeTab;

    public ObservableCollection<EditorViewModel> OpenTabs { get; } = new();

    public EditorViewModel? ActiveTab
    {
        get => _activeTab;
        set => this.RaiseAndSetIfChanged(ref _activeTab, value);
    }

    public ReactiveCommand<string, Unit> OpenFileCommand { get; }
    public ReactiveCommand<EditorViewModel, Unit> CloseTabCommand { get; }

    public EditorTabViewModel(IServiceProvider services)
    {
        _services = services;

        OpenFileCommand = ReactiveCommand.Create<string>(OpenFile);
        CloseTabCommand = ReactiveCommand.Create<EditorViewModel>(CloseTab);
    }

    /// <summary>
    /// Opens a file in a new or existing tab. If a tab for the same path
    /// already exists, activates it instead of creating a duplicate.
    /// </summary>
    private void OpenFile(string path)
    {
        // Check if already open — activate existing tab
        var existing = OpenTabs.FirstOrDefault(t =>
            string.Equals(t.FilePath, path, StringComparison.Ordinal));
        if (existing is not null)
        {
            ActiveTab = existing;
            return;
        }

        // Read file content (rule 11h: I/O with error handling)
        string content;
        try
        {
            content = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // TODO: surface error to status bar in a future milestone
            return;
        }

        // Create new tab via DI (Transient — each tab gets its own instance)
        var tab = _services.GetRequiredService<EditorViewModel>();
        tab.FilePath = path;
        tab.LoadFileContent(content);

        OpenTabs.Add(tab);
        ActiveTab = tab;
    }

    /// <summary>
    /// Closes a tab and removes it from the collection.
    /// If closing the active tab, activates the nearest remaining tab.
    /// M5 will add dirty-check interaction before removal.
    /// </summary>
    private void CloseTab(EditorViewModel tab)
    {
        var index = OpenTabs.IndexOf(tab);
        if (index < 0) return;

        OpenTabs.RemoveAt(index);

        // If we closed the active tab, activate a neighbor
        if (ReferenceEquals(ActiveTab, tab))
        {
            if (OpenTabs.Count == 0)
            {
                ActiveTab = null;
            }
            else if (index < OpenTabs.Count)
            {
                ActiveTab = OpenTabs[index]; // next tab
            }
            else
            {
                ActiveTab = OpenTabs[index - 1]; // previous tab (was last)
            }
        }
    }
}
