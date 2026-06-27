using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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
public class FileTreeViewModel : ReactiveObject
{
    private readonly FileTreeService _fileTreeService;
    private FileTreeNode? _selectedFile;
    private string? _rootPath;
    private IDisposable? _watcherSubscription;
    private string? _statusText;
    private bool _showHiddenFiles;
    private readonly Subject<FileTreeNode> _openFileSubject = new();

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
        set => this.RaiseAndSetIfChanged(ref _rootPath, value);
    }

    public FileTreeViewModel(FileTreeService fileTreeService)
    {
        _fileTreeService = fileTreeService;

        // Wrap folder opening in try/catch and set StatusText on failure.
        // Watcher is only torn down AFTER validation — if EnumerateDirectory fails,
        // the existing watcher stays active so the user doesn't lose live updates.
        OpenFolderCommand = ReactiveCommand.Create<string>(path =>
        {
            StatusText = null; // Clear status on new operation attempt

            try
            {
                var nodes = _fileTreeService.EnumerateDirectory(path);

                // Validation succeeded — safely tear down old watcher
                _watcherSubscription?.Dispose();
                _fileTreeService.StopWatching();

                RootPath = path;
                RootNodes.Clear();
                foreach (var node in nodes)
                    RootNodes.Add(node);

                // Start watching for live changes
                _fileTreeService.StartWatching(path);
                _watcherSubscription = _fileTreeService.FileChanges!
                    .ObserveOn(AvaloniaScheduler.Instance)
                    .Subscribe(HandleFileChange);
            }
            catch (DirectoryNotFoundException ex)
            {
                // B2 Fix: Set status text and leave RootNodes unchanged on failure
                StatusText = $"Error: Directory not found at '{path}'. Details: {ex.Message}";
            }
            catch (UnauthorizedAccessException ex)
            {
                // B2 Fix: Set status text and leave RootNodes unchanged on failure
                StatusText = $"Access Denied: Cannot access directory '{path}'. Details: {ex.Message}";
            }
            catch (NotSupportedException ex)
            {
                // B2 Fix: Catch other unexpected IO errors gracefully
                StatusText = $"Operation Failed: The path provided is not supported or invalid. Details: {ex.Message}";
            }
            catch (ArgumentException ex)
            {
                StatusText = $"Invalid Argument: Invalid file path format provided. Details: {ex.Message}";
            }
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
        // Re-enumerates RootPath with the new flag and restarts the watcher.
        // ShowHiddenFiles is only flipped AFTER successful re-enumeration, so
        // a failure leaves UI state, tree, and watcher in sync.
        ToggleHiddenFilesCommand = ReactiveCommand.Create(() =>
        {
            if (RootPath is null) return;

            var newValue = !ShowHiddenFiles;

            try
            {
                var nodes = _fileTreeService.EnumerateDirectory(RootPath, newValue);

                // Validation succeeded — safely tear down old watcher
                _watcherSubscription?.Dispose();
                _fileTreeService.StopWatching();

                RootNodes.Clear();
                foreach (var node in nodes)
                    RootNodes.Add(node);

                _fileTreeService.StartWatching(RootPath, newValue);
                _watcherSubscription = _fileTreeService.FileChanges!
                    .ObserveOn(AvaloniaScheduler.Instance)
                    .Subscribe(HandleFileChange);

                ShowHiddenFiles = newValue; // Flip only after everything succeeded
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to toggle hidden files: {ex.Message}";
            }
        });

        // M3: CopyPathCommand — copies the absolute path of a node to clipboard
        CopyPathCommand = ReactiveCommand.Create<FileTreeNode>(node =>
        {
            if (node is null) return;
            CopyToClipboard.Handle(node.FullPath).Subscribe();
        });

        // M3: CopyRelativePathCommand — copies path relative to RootPath
        CopyRelativePathCommand = ReactiveCommand.Create<FileTreeNode>(node =>
        {
            if (node is null || RootPath is null) return;
            var relative = Path.GetRelativePath(RootPath, node.FullPath);
            CopyToClipboard.Handle(relative).Subscribe();
        });
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

    private void HandleFileChange(FileChangeEvent change)
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
        }
    }

    private void HandleCreated(string fullPath)
    {
        var parentDir = Path.GetDirectoryName(fullPath);

        // Root-level files go directly into RootNodes
        if (parentDir == RootPath)
        {
            var name = Path.GetFileName(fullPath);
            var isDir = Directory.Exists(fullPath);
            RootNodes.Add(new FileTreeNode
            {
                Name = name,
                FullPath = fullPath,
                IsDirectory = isDir
            });
            return;
        }

        var parent = FindNodeByPath(parentDir!);
        if (parent is null) return;

        var nodeName = Path.GetFileName(fullPath);
        var nodeIsDir = Directory.Exists(fullPath);

        parent.Children.Add(new FileTreeNode
        {
            Name = nodeName,
            FullPath = fullPath,
            IsDirectory = nodeIsDir
        });
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

        // 1. Update basic node properties
        node.Name = Path.GetFileName(newPath);
        node.FullPath = newPath;

        // B1 Fix: Recursively update FullPath for all descendants if the old/new paths represent a directory rename
        UpdateDescendantPaths(node, oldPath, newPath);
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