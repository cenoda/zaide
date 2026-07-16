using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Concurrency;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.ViewModels;

/// <summary>
/// Reactive bridge between FileTreeService and the file tree view.
/// Holds root nodes, open-folder command, selected file, current path,
/// and live file system change monitoring.
/// </summary>
public class FileTreeViewModel : ReactiveObject, IDisposable
{
    private readonly IFileTreeService _fileTreeService;
    private readonly IScheduler _scheduler;
    private FileTreeNode? _selectedFile;
    private string? _rootPath;
    private IDisposable? _watcherSubscription;
    private IDisposable? _restartSubscription;
    private string? _statusText;
    private bool _showHiddenFiles;
    private readonly Subject<FileTreeNode> _openFileSubject = new();

    // Tracks Created events whose parent directory node hasn't arrived yet.
    // Retried on each subsequent HandleFileChange call; dropped after MaxDeferRetries attempts.
    private readonly List<(FileChangeEvent Event, int RetryCount)> _pendingEvents = new();
    private const int MaxDeferRetries = 5;

    /// <summary>
    /// Fires when a file open is requested. Payload is the FileTreeNode.
    /// Used by MainWindowViewModel to open the file — avoids relying on SelectedFile.
    /// </summary>
    public IObservable<FileTreeNode> OpenFileRequested => _openFileSubject;

    // B2: Reactive status property for tree-specific errors (M1)
    public string? StatusText
    {
        get => _statusText;
        private set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public ObservableCollection<FileTreeNode> RootNodes { get; } = new();

    public ReactiveCommand<string, Unit> OpenFolderCommand { get; }

    // M3: Commands for context menu and keyboard interactions
    public ReactiveCommand<FileTreeNode, Unit> RequestOpenFileCommand { get; }
    public ReactiveCommand<Unit, Unit> ExpandAllCommand { get; }
    public ReactiveCommand<Unit, Unit> CollapseAllCommand { get; }

    // M1: Create file or directory. Tuple: (parentDir, name, isDirectory).
    public ReactiveCommand<(string ParentDir, string Name, bool IsDirectory), Unit> CreateNodeCommand { get; }

    // M2: Show hidden files toggle
    public ReactiveCommand<Unit, Unit> ToggleHiddenFilesCommand { get; }

    // M3: Copy path commands — execute with a FileTreeNode payload
    public ReactiveCommand<FileTreeNode, Unit> CopyPathCommand { get; }
    public ReactiveCommand<FileTreeNode, Unit> CopyRelativePathCommand { get; }

    // Live-sync fallback: re-enumerates the tree without toggling any state.
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    /// <summary>
    /// M3: Fires when a path string should be copied to clipboard.
    /// MainWindowViewModel registers the handler that calls topLevel.Clipboard.SetTextAsync.
    /// </summary>
    public Interaction<string, Unit> CopyToClipboard { get; } = new();

    public bool ShowHiddenFiles
    {
        get => _showHiddenFiles;
        private set => this.RaiseAndSetIfChanged(ref _showHiddenFiles, value);
    }

    public FileTreeNode? SelectedFile
    {
        get => _selectedFile;
        set => this.RaiseAndSetIfChanged(ref _selectedFile, value);
    }

    public string? RootPath
    {
        get => _rootPath;
        private set => this.RaiseAndSetIfChanged(ref _rootPath, value);
    }

    /// <summary>
    /// M3 (Phase 8.1.3): Requests that the owning folder be closed.
    /// Bridged by <c>MainWindowViewModel.Activate()</c> to <c>CloseFolderCommand</c>.
    /// </summary>
    public Interaction<Unit, Unit> CloseFolderRequested { get; } = new();

    public FileTreeViewModel(IFileTreeService fileTreeService, IScheduler scheduler, ICommandRegistry? commandRegistry = null)
    {
        _fileTreeService = fileTreeService;
        _scheduler = scheduler;

        // Wrap folder opening in SetRootPath. Validation and watcher lifecycle
        // are handled there; a failed open preserves the existing tree and watcher.
        OpenFolderCommand = ReactiveCommand.Create<string>(path =>
        {
            SetRootPath(path);
        });

        // M3: RequestOpenFileCommand — single open pathway for context menu and Enter key.
        // Publishes to OpenFileRequested subject; MainWindowViewModel subscribes there
        // with the actual FileTreeNode payload (avoids dependency on SelectedFile).
        RequestOpenFileCommand = ReactiveCommand.Create<FileTreeNode>(node =>
        {
            if (node is null || node.IsDirectory)
                return;

            _openFileSubject.OnNext(node);
        });

        // M3: ExpandAllCommand — recursively expand all directory nodes
        ExpandAllCommand = ReactiveCommand.Create(() =>
        {
            foreach (var node in RootNodes)
                SetExpandedRecursive(node, true);
        });

        // M3: CollapseAllCommand — recursively collapse all directory nodes
        CollapseAllCommand = ReactiveCommand.Create(() =>
        {
            foreach (var node in RootNodes)
                SetExpandedRecursive(node, false);
        });

        // M1: CreateNodeCommand — creates a file or directory on disk.
        // Watcher picks up the change — no manual tree insert.
        // Clears StatusText on success so stale errors don't persist.
        CreateNodeCommand = ReactiveCommand.Create<(string ParentDir, string Name, bool IsDirectory)>(spec =>
        {
            var (parentDir, name, isDirectory) = spec;
            var fullPath = Path.Combine(parentDir, name);

            StatusText = null;

            try
            {
                if (isDirectory)
                    _fileTreeService.CreateDirectory(fullPath);
                else
                    _fileTreeService.CreateFile(fullPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                StatusText = $"Failed to create {(isDirectory ? "directory" : "file")} '{name}': {ex.Message}";
            }
        });

        // M2: ToggleHiddenFilesCommand — toggles show/hide of . prefixed entries.
        // Re-enumerates RootPath with the new flag via SetRootPath.
        // ShowHiddenFiles is flipped before SetRootPath; on failure it is reverted
        // so UI state, tree, and watcher remain in sync.
        ToggleHiddenFilesCommand = ReactiveCommand.Create(() =>
        {
            if (RootPath is null) return;

            var previousValue = ShowHiddenFiles;
            ShowHiddenFiles = !previousValue;

            StatusText = null;
            if (!SetRootPath(RootPath))
            {
                // Enumeration failed — SetRootPath preserved existing tree/watcher
                // and set StatusText. Revert the toggle so state stays consistent.
                 ShowHiddenFiles = previousValue;
            }
        });

        // M3: CopyPathCommand — copies the absolute path of a node to clipboard
        CopyPathCommand = ReactiveCommand.Create<FileTreeNode>(node =>
        {
            if (node is null) return;
            CopyToClipboard.Handle(node.FullPath).Subscribe();
        });

        // M3: CopyRelativePathCommand — copies path relative to RootPath.
        // Disabled when no folder is open (RootPath is null).
        var canCopyRelative = this.WhenAnyValue(x => x.RootPath).Select(p => p is not null);
        CopyRelativePathCommand = ReactiveCommand.Create<FileTreeNode>(node =>
        {
            if (node is null || RootPath is null) return;
            var relative = Path.GetRelativePath(RootPath, node.FullPath);
            CopyToClipboard.Handle(relative).Subscribe();
        }, canCopyRelative);

        // Phase 8.2 M8a: register the canonical explorer command with a stable
        // ID (D6a) after the ReactiveCommand property above is initialized.
        commandRegistry?.Register(new CommandDescriptor(
            "explorer.toggleHiddenFiles", "Toggle Hidden Files", "Explorer",
            new[] { "Ctrl+Shift+H" }, ToggleHiddenFilesCommand));

        // Live-sync fallback: re-enumerates the current root without changing
        // any toggle state. The watcher keeps running, so no observation gap.
        RefreshCommand = ReactiveCommand.Create(Refresh);
    }

    /// <summary>
    /// M3 (Phase 8.1.3): Sole public writer for <see cref="RootPath"/>.
    /// A null <paramref name="path"/> closes the workspace: disposes the watcher,
    /// clears tree/selection/status, then publishes null. A non-null path
    /// validates and enumerates before tearing down the existing watcher; a
    /// failed open preserves the current root, nodes, and watcher and returns false.
    /// </summary>
    public bool SetRootPath(string? path)
    {
        // Close transition
        if (path is null)
        {
            _watcherSubscription?.Dispose();
            _watcherSubscription = null;
            _restartSubscription?.Dispose();
            _restartSubscription = null;
            _fileTreeService.StopWatching();
            RootNodes.Clear();
            SelectedFile = null;
            StatusText = null;
            RootPath = null;
            return true;
        }

        // Open transition: validate/enumerate BEFORE tearing down old state
        try
        {
            var nodes = _fileTreeService.EnumerateDirectory(path, ShowHiddenFiles);

            // Validation succeeded — tear down old watcher and tree
            _watcherSubscription?.Dispose();
            _restartSubscription?.Dispose();
            _fileTreeService.StopWatching();

            RootPath = path;
            RootNodes.Clear();
            foreach (var node in nodes)
                RootNodes.Add(node);

            var fileChanges = _fileTreeService.StartWatching(path, ShowHiddenFiles);
            _watcherSubscription = fileChanges
                .ObserveOn(_scheduler)
                .Subscribe(HandleFileChange);

            // Re-enumerate the tree if the watcher is restarted (e.g. after buffer overflow)
            // so any events missed during the gap are reconciled.
            _restartSubscription = _fileTreeService.WhenWatcherRestarted
                .ObserveOn(_scheduler)
                .Subscribe(_ => Refresh());
            return true;
        }
        catch (DirectoryNotFoundException ex)
        {
            StatusText = $"Error: Directory not found at '{path}'. Details: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusText = $"Access Denied: Cannot access directory '{path}'. Details: {ex.Message}";
        }
        catch (NotSupportedException ex)
        {
            StatusText = $"Operation Failed: The path provided is not supported or invalid. Details: {ex.Message}";
        }
        catch (ArgumentException ex)
        {
            StatusText = $"Invalid Argument: Invalid file path format provided. Details: {ex.Message}";
        }

        return false;
    }

    /// <summary>
    /// Re-enumerates the current root directory and replaces RootNodes.
    /// The watcher is NOT stopped — it continues observing throughout the
    /// refresh so no filesystem events are missed.
    /// </summary>
    public void Refresh()
    {
        if (RootPath is null) return;

        try
        {
            var nodes = _fileTreeService.EnumerateDirectory(RootPath, ShowHiddenFiles);
            RootNodes.Clear();
            foreach (var node in nodes)
                RootNodes.Add(node);
            StatusText = null;
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or UnauthorizedAccessException)
        {
            StatusText = $"Refresh failed: {ex.Message}";
        }
    }

    private static void SetExpandedRecursive(FileTreeNode node, bool isExpanded)
    {
        node.IsExpanded = isExpanded;
        foreach (var child in node.Children)
        {
            if (child.IsDirectory)
                SetExpandedRecursive(child, isExpanded);
        }
    }

    internal void HandleFileChange(FileChangeEvent change)
    {
        switch (change.Type)
        {
            case ChangeType.Created:
                HandleCreated(change.FullPath);
                break;
            case ChangeType.Deleted:
                HandleDeleted(change.FullPath);
                break;
            case ChangeType.Renamed:
                // M1 Fix B1/B2
                HandleRenamed(change.FullPath, change.OldPath!);
                break;
            case ChangeType.Changed:
                HandleChanged(change.FullPath);
                break;
        }

        // After any tree mutation, retry deferred Created events whose
        // parent directory may now be present in the tree.
        RetryPendingEvents();
    }

    private void HandleCreated(string fullPath)
    {
        var parentDir = Path.GetDirectoryName(fullPath);

        // Root-level files go directly into RootNodes
        if (parentDir == RootPath)
        {
            var name = Path.GetFileName(fullPath);
            var isDir = Directory.Exists(fullPath);

            // Duplicate guard: watcher may fire Created more than once for the same path
            if (RootNodes.Any(n => n.FullPath == fullPath))
                return;

            var node = new FileTreeNode
            {
                Name = name,
                FullPath = fullPath,
                IsDirectory = isDir,
                Depth = 0
            };
            InsertNodeSorted(RootNodes, node);
            return;
        }

        var parent = FindNodeByPath(parentDir!);
        if (parent is null)
        {
            // Parent directory node not yet in tree (watcher may fire
            // child Created before parent Created). Defer and retry later.
            _pendingEvents.Add((new FileChangeEvent(ChangeType.Created, fullPath), 0));
            return;
        }

        var nodeName = Path.GetFileName(fullPath);
        var nodeIsDir = Directory.Exists(fullPath);

        // Duplicate guard
        if (parent.Children.Any(c => c.FullPath == fullPath))
            return;

        var newNode = new FileTreeNode
        {
            Name = nodeName,
            FullPath = fullPath,
            IsDirectory = nodeIsDir,
            Depth = parent.Depth + 1
        };
        InsertNodeSorted(parent.Children, newNode);
    }

    /// <summary>
    /// Inserts a node into a sorted collection maintaining the tree's sort order:
    /// directories first (alphabetical), then files (alphabetical).
    /// </summary>
    private static void InsertNodeSorted(ObservableCollection<FileTreeNode> collection, FileTreeNode node)
    {
        var insertIndex = 0;

        if (node.IsDirectory)
        {
            // Directories go before all files; find position among directories
            while (insertIndex < collection.Count && collection[insertIndex].IsDirectory)
            {
                if (string.Compare(node.Name, collection[insertIndex].Name, StringComparison.OrdinalIgnoreCase) < 0)
                    break;
                insertIndex++;
            }
        }
        else
        {
            // Files go after all directories
            while (insertIndex < collection.Count && collection[insertIndex].IsDirectory)
                insertIndex++;

            // Find position among files
            while (insertIndex < collection.Count)
            {
                if (string.Compare(node.Name, collection[insertIndex].Name, StringComparison.OrdinalIgnoreCase) < 0)
                    break;
                insertIndex++;
            }
        }

        collection.Insert(insertIndex, node);
    }

    private void HandleDeleted(string fullPath)
    {
        var parentDir = Path.GetDirectoryName(fullPath);

        // Root-level removal: scan RootNodes directly
        if (parentDir == RootPath)
        {
            for (var i = RootNodes.Count - 1; i >= 0; i--)
            {
                if (RootNodes[i].FullPath == fullPath)
                {
                    RootNodes.RemoveAt(i);
                    return;
                }
            }
            return;
        }

        var parent = FindNodeByPath(parentDir!);
        if (parent is null) return;

        for (var i = parent.Children.Count - 1; i >= 0; i--)
        {
            if (parent.Children[i].FullPath == fullPath)
            {
                parent.Children.RemoveAt(i);
                return;
            }
        }
    }

    public void HandleRenamed(string newPath, string oldPath)
    {
        var node = FindNodeByPath(oldPath);
        if (node is null) return;

        // 1. Find the parent collection so we can re-sort after the rename
        var parentCollection = GetParentCollection(node);

        // 2. Remove from old position before updating (to avoid stale sort position)
        if (parentCollection is not null)
            parentCollection.Remove(node);

        // 3. Update basic node properties
        node.Name = Path.GetFileName(newPath);
        node.FullPath = newPath;

        // 4. B1 Fix: Recursively update FullPath for all descendants if the old/new paths represent a directory rename
        UpdateDescendantPaths(node, oldPath, newPath);

        // 5. Re-insert in sorted order (name may have changed alphabetical position)
        if (parentCollection is not null)
            InsertNodeSorted(parentCollection, node);
    }

    /// <summary>
    /// Returns the ObservableCollection that contains the given node —
    /// either RootNodes or a parent directory's Children collection.
    /// </summary>
    private ObservableCollection<FileTreeNode>? GetParentCollection(FileTreeNode node)
    {
        // Check if the node is a direct child of RootNodes
        if (RootNodes.Contains(node))
            return RootNodes;

        // Otherwise find the parent directory that owns this node
        var parent = FindParentNode(node);
        return parent?.Children;
    }

    /// <summary>
    /// Finds the directory node whose Children collection contains <paramref name="node"/>.
    /// </summary>
    private FileTreeNode? FindParentNode(FileTreeNode node)
    {
        foreach (var rootNode in RootNodes)
        {
            if (rootNode.Children.Contains(node))
                return rootNode;

            var found = FindParentInChildren(rootNode, node);
            if (found is not null)
                return found;
        }
        return null;
    }

    private static FileTreeNode? FindParentInChildren(FileTreeNode parent, FileTreeNode target)
    {
        foreach (var child in parent.Children)
        {
            if (child.Children.Contains(target))
                return child;

            var found = FindParentInChildren(child, target);
            if (found is not null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// Handles content/metadata change events from the file system watcher.
    /// Currently a no-op — the file tree does not display content-derived state.
    /// Wired up so that when metadata display is added to FileTreeNode, the
    /// handling logic goes here without needing pipeline changes.
    /// </summary>
    internal void HandleChanged(string fullPath)
    {
        // Future: find the node by path and update any displayed metadata
        // (e.g. file size, last-modified time, git-status overlay).
        // For now, the event is consumed so it doesn't stall the buffer.
    }

    /// <summary>
    /// Retries deferred Created events whose parent directory may now exist.
    /// Called after every HandleFileChange mutation. Events that exceed
    /// <see cref="MaxDeferRetries"/> attempts are dropped with a status message.
    /// </summary>
    private void RetryPendingEvents()
    {
        for (var i = _pendingEvents.Count - 1; i >= 0; i--)
        {
            var (ev, retryCount) = _pendingEvents[i];
            var parentDir = Path.GetDirectoryName(ev.FullPath);

            // Check if the parent is now available (either root level or found in tree)
            if (parentDir is not null && (parentDir == RootPath || FindNodeByPath(parentDir) is not null))
            {
                _pendingEvents.RemoveAt(i);
                HandleCreated(ev.FullPath);
            }
            else if (retryCount >= MaxDeferRetries)
            {
                _pendingEvents.RemoveAt(i);
                StatusText = $"Could not process file creation: {ev.FullPath}";
            }
            else
            {
                _pendingEvents[i] = (ev, retryCount + 1);
            }
        }
    }

    /// <summary>
    /// Disposes watcher subscriptions. Called by the DI container on app shutdown.
    /// </summary>
    public void Dispose()
    {
        _watcherSubscription?.Dispose();
        _watcherSubscription = null;
        _restartSubscription?.Dispose();
        _restartSubscription = null;
        _pendingEvents.Clear();
    }

    /// <summary>
    /// Recursively updates descendant node full paths following a directory rename. (M1 Fix B1)
    /// </summary>
    private static void UpdateDescendantPaths(FileTreeNode node, string oldDirPath, string newDirPath)
    {
        foreach (var child in node.Children)
        {
            // Check if the path starts with the old directory path prefix
            if (child.FullPath != null && child.FullPath.StartsWith(oldDirPath))
            {
                string relativePathSegment = child.FullPath[oldDirPath.Length..];
                child.FullPath = newDirPath + relativePathSegment;
            }

            if (child.IsDirectory)
            {
                UpdateDescendantPaths(child, oldDirPath, newDirPath);
            }
        }
    }

    /// <summary>
    /// Recursively search the tree for a node matching the given path.
    /// </summary>
    private FileTreeNode? FindNodeByPath(string path)
    {
        foreach (var node in RootNodes)
        {
            if (node.FullPath == path)
                return node;

            var found = FindInChildren(node, path);
            if (found is not null)
                return found;
        }

        return null;
    }

    private static FileTreeNode? FindInChildren(FileTreeNode parent, string path)
    {
        foreach (var child in parent.Children)
        {
            if (child.FullPath == path)
                return child;

            var found = FindInChildren(child, path);
            if (found is not null)
                return found;
        }

        return null;
    }
}