using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
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

        // M1 Fix B2: Wrap folder opening in try/catch and set StatusText on failure
        OpenFolderCommand = ReactiveCommand.Create<string>(path =>
        {
            // Stop previous watcher
            _watcherSubscription?.Dispose();
            StatusText = null; // Clear status on new operation attempt (B2)
            _fileTreeService.StopWatching();

            try
            {
                var nodes = _fileTreeService.EnumerateDirectory(path);
                RootPath = path;  // set only after validation succeeds
                RootNodes.Clear();
                foreach (var node in nodes)
                    RootNodes.Add(node);

                // Start watching for live changes only upon successful loading
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

        // M3: RequestOpenFileCommand — single open pathway for context menu and Enter key
        RequestOpenFileCommand = ReactiveCommand.Create<FileTreeNode>(node =>
        {
            if (node is null || node.IsDirectory)
                return; // No-op on directory

            // This command is mediated by MainWindowViewModel to EditorTabs.OpenFileCommand
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
        var parent = FindNodeByPath(parentDir!);
        if (parent is null) return;

        var name = Path.GetFileName(fullPath);
        var isDir = Directory.Exists(fullPath);

        var node = new FileTreeNode
        {
            Name = name,
            FullPath = fullPath,
            IsDirectory = isDir
        };

        parent.Children.Add(node);
    }

    private void HandleDeleted(string fullPath)
    {
        var parentDir = Path.GetDirectoryName(fullPath);
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