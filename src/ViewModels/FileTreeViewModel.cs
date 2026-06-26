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

    public ObservableCollection<FileTreeNode> RootNodes { get; } = new();

    public ReactiveCommand<string, Unit> OpenFolderCommand { get; }

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

        OpenFolderCommand = ReactiveCommand.Create<string>(path =>
        {
            // Stop previous watcher
            _watcherSubscription?.Dispose();
            _fileTreeService.StopWatching();

            RootPath = path;
            var nodes = _fileTreeService.EnumerateDirectory(path);
            RootNodes.Clear();
            foreach (var node in nodes)
                RootNodes.Add(node);

            // Start watching for live changes
            _fileTreeService.StartWatching(path);
            _watcherSubscription = _fileTreeService.FileChanges!
                .ObserveOn(AvaloniaScheduler.Instance)
                .Subscribe(HandleFileChange);
        });
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

    private void HandleRenamed(string newPath, string oldPath)
    {
        var node = FindNodeByPath(oldPath);
        if (node is null) return;

        node.Name = Path.GetFileName(newPath);
        node.FullPath = newPath;
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
