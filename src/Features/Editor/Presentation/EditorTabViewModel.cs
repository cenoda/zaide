using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Zaide.Models;
using Zaide.Services;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Editor.Contracts;

namespace Zaide.Features.Editor.Presentation;

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
    private readonly global::Zaide.Features.Workspace.Domain.Workspace _workspace;
    private EditorViewModel? _activeTab;

    public ObservableCollection<EditorViewModel> OpenTabs { get; } = new();

    /// <summary>
    /// The folding-operations seam for the active editor. Set by the View
    /// on activation. Commands read this to execute fold operations.
    /// </summary>
    private IFoldingOperations? _foldingEditor;
    public IFoldingOperations? FoldingEditor
    {
        get => _foldingEditor;
        set => this.RaiseAndSetIfChanged(ref _foldingEditor, value);
    }

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

    /// <summary>
    /// Phase 9 M6: Status message from the last folding command execution.
    /// Set by fold command handlers; consumed by MainWindowViewModel to
    /// surface in the status bar.
    /// </summary>
    private string? _foldStatusMessage;
    public string? FoldStatusMessage
    {
        get => _foldStatusMessage;
        set => this.RaiseAndSetIfChanged(ref _foldStatusMessage, value);
    }

    public ReactiveCommand<string, bool> OpenFileCommand { get; }
    public ReactiveCommand<EditorViewModel, Unit> CloseTabCommand { get; }

    /// <summary>
    /// Phase 9 M4: Toggles folding for the brace region at the current caret position.
    /// Availability: active tab exists AND folding is available.
    /// Default gesture: unbound.
    /// </summary>
    public ICommand FoldToggleCommand { get; }

    /// <summary>
    /// Phase 9 M4: Folds all discovered brace regions.
    /// Availability: active tab exists AND folding is available.
    /// Default gesture: unbound.
    /// </summary>
    public ICommand FoldAllCommand { get; }

    /// <summary>
    /// Phase 9 M4: Unfolds all folding sections.
    /// Availability: active tab exists AND folding is available.
    /// Default gesture: unbound.
    /// </summary>
    public ICommand UnfoldAllCommand { get; }

    // ── Phase 9 M5a: Tab Lifecycle Commands ─────────────────────────────

    /// <summary>
    /// Navigates to the next tab. Wraps from last to first.
    /// Availability: at least 2 open tabs. Default gesture: Ctrl+Tab.
    /// </summary>
    public ReactiveCommand<Unit, Unit> TabNextCommand { get; }

    /// <summary>
    /// Navigates to the previous tab. Wraps from first to last.
    /// Availability: at least 2 open tabs. Default gesture: Ctrl+Shift+Tab.
    /// </summary>
    public ReactiveCommand<Unit, Unit> TabPreviousCommand { get; }

    /// <summary>
    /// Closes the active tab. Uses existing unsaved-change confirmation.
    /// Availability: at least 1 open tab. Default gestures: Ctrl+W, Ctrl+F4.
    /// </summary>
    public ReactiveCommand<Unit, Unit> TabCloseActiveCommand { get; }

    /// <summary>
    /// Closes all tabs except the active tab. Preserves the active tab.
    /// Availability: at least 2 open tabs. Default gesture: unbound.
    /// Processes non-active tabs in left-to-right (ascending index) order.
    /// On cancel or save failure, stops immediately; already-closed tabs
    /// remain closed and not-yet-processed tabs are untouched.
    /// </summary>
    public ReactiveCommand<Unit, Unit> TabCloseOthersCommand { get; }

    /// <summary>
    /// Closes all open tabs.
    /// Availability: at least 1 open tab. Default gesture: unbound.
    /// Processes tabs in reverse index order (right-to-left).
    /// On cancel or save failure, stops immediately; already-closed tabs
    /// remain closed; the next still-open tab becomes active deterministically.
    /// If all close successfully, ActiveTab and Workspace.ActiveDocument are null.
    /// </summary>
    public ReactiveCommand<Unit, Unit> TabCloseAllCommand { get; }

    public EditorTabViewModel(IServiceProvider services, IFileService fileService, global::Zaide.Features.Workspace.Domain.Workspace workspace,
        ICommandRegistry? commandRegistry = null)
    {
        _services = services;
        _fileService = fileService;
        _workspace = workspace;

        OpenFileCommand = ReactiveCommand.CreateFromTask<string, bool>(OpenFileAsync);
        CloseTabCommand = ReactiveCommand.CreateFromTask<EditorViewModel>(CloseTabAsync);

        // Phase 9 M4: folding commands.
        // Availability: active tab exists AND folding is available.
        var canFold = this.WhenAnyValue(
            x => x.ActiveTab,
            x => x.FoldingEditor,
            (tab, fold) => tab is not null && fold is not null && fold.IsAvailable);

        FoldToggleCommand = ReactiveCommand.Create(
            () =>
            {
                var result = _foldingEditor?.ToggleCurrent();
                FoldStatusMessage = result == true
                    ? "Toggled fold"
                    : "No foldable region at caret";
            },
            canFold);

        FoldAllCommand = ReactiveCommand.Create(
            () =>
            {
                _foldingEditor?.FoldAll();
                FoldStatusMessage = "Folded all regions";
            },
            canFold);

        UnfoldAllCommand = ReactiveCommand.Create(
            () =>
            {
                _foldingEditor?.UnfoldAll();
                FoldStatusMessage = "Unfolded all regions";
            },
            canFold);

        // Phase 9 M5a: tab lifecycle commands.
        // Availability observables: tab count (from collection changes) and
        // active-tab non-null.

        var tabCountChanged = Observable.FromEventPattern<
            NotifyCollectionChangedEventHandler,
            NotifyCollectionChangedEventArgs>(
            h => OpenTabs.CollectionChanged += h,
            h => OpenTabs.CollectionChanged -= h)
            .Select(_ => OpenTabs.Count)
            .StartWith(OpenTabs.Count);

        var hasMultipleTabs = tabCountChanged.Select(c => c >= 2);
        var hasActiveTab = this.WhenAnyValue(x => x.ActiveTab)
            .Select(tab => tab is not null);

        TabNextCommand = ReactiveCommand.Create(TabNext, hasMultipleTabs);
        TabPreviousCommand = ReactiveCommand.Create(TabPrevious, hasMultipleTabs);
        TabCloseActiveCommand = ReactiveCommand.CreateFromTask(
            TabCloseActiveAsync, hasActiveTab);
        TabCloseOthersCommand = ReactiveCommand.CreateFromTask(
            TabCloseOthersAsync, hasMultipleTabs);
        TabCloseAllCommand = ReactiveCommand.CreateFromTask(
            TabCloseAllAsync, hasActiveTab);

        // Register folding commands with the same Phase 8.2 lifecycle.
        // All three are unbound (no default gesture).
        commandRegistry?.Register(new CommandDescriptor(
            "editor.foldToggle", "Toggle Current Fold", "Editor",
            Array.Empty<string>(), FoldToggleCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "editor.foldAll", "Fold All", "Editor",
            Array.Empty<string>(), FoldAllCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "editor.unfoldAll", "Unfold All", "Editor",
            Array.Empty<string>(), UnfoldAllCommand));

        // Phase 9 M5a: register tab lifecycle commands.
        // M0-locked IDs, categories, gestures, and availability.
        commandRegistry?.Register(new CommandDescriptor(
            "tab.next", "Next Tab", "Tab",
            new[] { "Ctrl+Tab" }, TabNextCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "tab.previous", "Previous Tab", "Tab",
            new[] { "Ctrl+Shift+Tab" }, TabPreviousCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "tab.close", "Close Tab", "Tab",
            new[] { "Ctrl+W", "Ctrl+F4" }, TabCloseActiveCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "tab.closeOthers", "Close Other Tabs", "Tab",
            Array.Empty<string>(), TabCloseOthersCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "tab.closeAll", "Close All Tabs", "Tab",
            Array.Empty<string>(), TabCloseAllCommand));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Phase 9 M5a: Tab Lifecycle Command Executions
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Navigates to the next tab. Wraps deterministically from last to first.
    /// Changes only ActiveTab; preserves tab order, content, and dirty state.
    /// Guarded by hasMultipleTabs — caller must verify count >= 2.
    /// </summary>
    private void TabNext()
    {
        if (ActiveTab is null) return;
        var index = OpenTabs.IndexOf(ActiveTab);
        if (index < 0) return;
        ActiveTab = OpenTabs[(index + 1) % OpenTabs.Count];
    }

    /// <summary>
    /// Navigates to the previous tab. Wraps deterministically from first to last.
    /// Changes only ActiveTab; preserves tab order, content, and dirty state.
    /// Guarded by hasMultipleTabs — caller must verify count >= 2.
    /// </summary>
    private void TabPrevious()
    {
        if (ActiveTab is null) return;
        var index = OpenTabs.IndexOf(ActiveTab);
        if (index < 0) return;
        ActiveTab = OpenTabs[(index - 1 + OpenTabs.Count) % OpenTabs.Count];
    }

    /// <summary>
    /// Closes the active tab. Delegates to CloseTabAsync, which handles
    /// dirty confirmation, save-on-request, neighbor selection, and
    /// workspace cleanup. Guarded by hasActiveTab.
    /// </summary>
    private async Task TabCloseActiveAsync()
    {
        if (ActiveTab is null) return;
        await CloseTabAsync(ActiveTab);
    }

    /// <summary>
    /// Closes all tabs except the active tab.
    ///
    /// <para><b>Order:</b> Processes non-active tabs in left-to-right
    /// (ascending index / visual) order. The list is captured once before
    /// iteration, so index shifts from removal do not affect which tabs
    /// are visited.</para>
    ///
    /// <para><b>Partial completion:</b> If a dirty tab's confirmation
    /// returns cancel or save-failure, iteration stops immediately. All
    /// already-closed tabs remain closed. Not-yet-processed tabs are
    /// untouched. The active tab is preserved throughout.</para>
    ///
    /// <para><b>Active tab:</b> Never closed. Workspace.ActiveDocument
    /// always equals the preserved active tab's Document.</para>
    ///
    /// Guarded by hasMultipleTabs — caller must verify count >= 2.
    /// </summary>
    private async Task TabCloseOthersAsync()
    {
        var active = ActiveTab;
        if (active is null) return;

        // Capture non-active tabs in visual (left-to-right) order.
        var others = OpenTabs.Where(t => !ReferenceEquals(t, active)).ToList();

        foreach (var tab in others)
        {
            await CloseTabAsync(tab);

            // If CloseTabAsync did NOT remove the tab (cancel or save
            // failure), stop immediately. Already-closed tabs remain closed.
            if (OpenTabs.Contains(tab))
                return;
        }
    }

    /// <summary>
    /// Closes all open tabs.
    ///
    /// <para><b>Order:</b> Processes tabs in reverse index order
    /// (right-to-left, highest index first). This is deterministic and
    /// avoids index-shifting complications during iteration.</para>
    ///
    /// <para><b>Partial completion:</b> If a dirty tab's confirmation
    /// returns cancel or save-failure, iteration stops immediately. All
    /// already-closed tabs remain closed. The next still-open tab becomes
    /// active deterministically via CloseTabAsync's neighbor selection.</para>
    ///
    /// <para><b>Full completion:</b> When all tabs close successfully,
    /// ActiveTab and Workspace.ActiveDocument are null.</para>
    ///
    /// Guarded by hasActiveTab — caller must verify at least one tab exists.
    /// </summary>
    private async Task TabCloseAllAsync()
    {
        if (OpenTabs.Count == 0) return;

        // Process in reverse index order (right-to-left) so index shifts
        // from earlier removals never affect remaining tabs' positions.
        while (OpenTabs.Count > 0)
        {
            var tab = OpenTabs[^1];
            await CloseTabAsync(tab);

            // If CloseTabAsync did NOT remove the tab (cancel or save
            // failure), stop immediately.
            if (OpenTabs.Contains(tab))
                return;
        }
    }

    /// <summary>
    /// Moves a tab from one index to another within <see cref="OpenTabs"/>.
    /// All inputs are validated; invalid or no-op moves are safe.
    ///
    /// <para><b>Active-tab preservation:</b> The same <see cref="ActiveTab"/>
    /// object remains active after moving — only its index in the collection
    /// changes. <see cref="Workspace.ActiveDocument"/> is unchanged.</para>
    ///
    /// <para><b>Dirty-state / display-name preservation:</b> Tab content,
    /// dirty state, and display name are properties on the ViewModel and are
    /// not affected by collection reordering.</para>
    ///
    /// <para><b>CollectionChanged:</b> Fires a <c>Move</c> notification so
    /// the View can reconcile visual order without full rebuild.</para>
    /// </summary>
    public void MoveTab(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= OpenTabs.Count) return;
        if (toIndex < 0 || toIndex >= OpenTabs.Count) return;
        if (fromIndex == toIndex) return;

        OpenTabs.Move(fromIndex, toIndex);

        // ActiveTab reference preserved by object identity. Neither its
        // content nor its dirty state changed. Workspace unchanged.
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
        var tab = new EditorViewModel(
            document,
            _services.GetRequiredService<IFileService>(),
            _services.GetService<ISettingsService>(),
            _services.GetService<ILanguageFormattingService>());

        OpenTabs.Add(tab);
        ActiveTab = tab;
        LastOpenError = null;
        return true;
    }

    /// <summary>
    /// Saves every dirty open tab. Stops on the first failure and sets
    /// <see cref="LastSaveError"/> from the failing tab. Returns true when
    /// every dirty tab was saved successfully or no tabs were dirty.
    /// </summary>
    public async Task<bool> SaveAllDirtyTabsAsync()
    {
        foreach (var tab in OpenTabs)
        {
            if (!tab.IsDirty)
                continue;

            var saved = await tab.SaveCommand.Execute();
            if (!saved)
            {
                LastSaveError = tab.LastSaveError;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Closes a tab. If the tab is dirty, raises ConfirmClose to prompt
    /// the user via the unsaved-changes dialog. Removes the tab from
    /// the collection and activates a neighbor if needed.
    /// </summary>
    private async Task CloseTabAsync(EditorViewModel tab)
    {
        if (tab.IsSourceControlDiff && tab.IsDirty)
        {
            // Read-only diff tabs should never be dirty; if they are, discard safely
            // without prompting or writing to disk.
            tab.MarkClean();
        }

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
