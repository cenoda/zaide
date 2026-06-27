using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using Zaide.Models;

namespace Zaide.Services;

/// <summary>
/// Enumerates directories and files into a nested FileTreeNode tree.
/// Applies an ignore list, skips hidden entries, and monitors file system changes.
/// </summary>
public class FileTreeService : IDisposable
{
    private static readonly HashSet<string> DefaultIgnores = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj", ".git", ".vs", ".idea",
        "__pycache__", ".DS_Store", "Thumbs.db"
    };

    private FileSystemWatcher? _watcher;

    public IObservable<FileChangeEvent>? FileChanges { get; private set; }

    /// <summary>
    /// Recursively enumerate a directory into a list of FileTreeNode.
    /// Directories are sorted first, then files. Both are sorted alphabetically.
    /// </summary>
    public List<FileTreeNode> EnumerateDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty.", nameof(path));

        if (path.IndexOf('\0') >= 0)
            throw new ArgumentException("Path contains invalid characters.", nameof(path));

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");

        var root = new DirectoryInfo(path);
        var nodes = new List<FileTreeNode>();

        foreach (var dir in EnumerateDirectoriesSafe(root))
        {
            if (IsIgnored(dir.Name))
                continue;

            var node = new FileTreeNode
            {
                Name = dir.Name,
                FullPath = dir.FullName,
                IsDirectory = true,
                IsExpanded = false
            };

            try
            {
                var children = EnumerateDirectory(dir.FullName);
                foreach (var child in children)
                    node.Children.Add(child);
            }
            catch (UnauthorizedAccessException)
            {
            }

            nodes.Add(node);
        }

        foreach (var file in EnumerateFilesSafe(root))
        {
            if (IsIgnored(file.Name))
                continue;

            nodes.Add(new FileTreeNode
            {
                Name = file.Name,
                FullPath = file.FullName,
                IsDirectory = false
            });
        }

        return nodes;
    }

    public bool IsIgnored(string name)
    {
        return DefaultIgnores.Contains(name) || IsHidden(name);
    }

    /// <summary>
    /// Start monitoring a directory tree for file/directory creation, deletion, and rename.
    /// </summary>
    public void StartWatching(string path)
    {
        StopWatching();

        _watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
        };

        var created = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
            h => _watcher.Created += h,
            h => _watcher.Created -= h);

        var deleted = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
            h => _watcher.Deleted += h,
            h => _watcher.Deleted -= h);

        var renamed = Observable.FromEventPattern<RenamedEventHandler, RenamedEventArgs>(
            h => _watcher.Renamed += h,
            h => _watcher.Renamed -= h);

        FileChanges = created
            .Select(e => new FileChangeEvent(ChangeType.Created, e.EventArgs.FullPath))
            .Merge(deleted.Select(e => new FileChangeEvent(ChangeType.Deleted, e.EventArgs.FullPath)))
            .Merge(renamed.Select(e => new FileChangeEvent(ChangeType.Renamed, e.EventArgs.FullPath, e.EventArgs.OldFullPath)))
            .Where(change => !IsIgnored(Path.GetFileName(change.FullPath)))
            .Throttle(TimeSpan.FromMilliseconds(100));

        _watcher.EnableRaisingEvents = true;
    }

    public void StopWatching()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        FileChanges = null;
    }

    /// <summary>
    /// Creates an empty file at the given path. The FileSystemWatcher picks it up.
    /// </summary>
    public void CreateFile(string path)
    {
        using var _ = File.Create(path);
    }

    /// <summary>
    /// Creates a directory at the given path. The FileSystemWatcher picks it up.
    /// </summary>
    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public void Dispose()
    {
        StopWatching();
    }

    private static bool IsHidden(string name)
    {
        return name is not "." and not ".." && name.Length > 0 && name[0] == '.';
    }

    private static IEnumerable<DirectoryInfo> EnumerateDirectoriesSafe(DirectoryInfo root)
    {
        try { return root.EnumerateDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase); }
        catch (UnauthorizedAccessException) { return Array.Empty<DirectoryInfo>(); }
    }

    private static IEnumerable<FileInfo> EnumerateFilesSafe(DirectoryInfo root)
    {
        try { return root.EnumerateFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase); }
        catch (UnauthorizedAccessException) { return Array.Empty<FileInfo>(); }
    }
}
