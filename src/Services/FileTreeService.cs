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
public class FileTreeService : IFileTreeService
{
    private static readonly HashSet<string> DefaultIgnores = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj", ".git", ".vs", ".idea",
        "__pycache__", ".DS_Store", "Thumbs.db"
    };

    private FileSystemWatcher? _watcher;
    private bool _includeHidden;

    /// <summary>
    /// Recursively enumerate a directory into a list of FileTreeNode.
    /// Directories are sorted first, then files. Both are sorted alphabetically.
    /// When includeHidden is true, hidden entries (.name) are included;
    /// only DefaultIgnores are still filtered out.
    /// </summary>
    public List<FileTreeNode> EnumerateDirectory(string path, bool includeHidden = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty.", nameof(path));

        if (path.IndexOf('\0') >= 0)
            throw new ArgumentException("Path contains invalid characters.", nameof(path));

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");

        return EnumerateDirectory(path, includeHidden, depth: 0);
    }

    // M3.4: Recursive overload that tags each node with its nesting
    // depth so the view can render indent guides per level.
    private List<FileTreeNode> EnumerateDirectory(string path, bool includeHidden, int depth)
    {
        var root = new DirectoryInfo(path);
        var nodes = new List<FileTreeNode>();

        foreach (var dir in EnumerateDirectoriesSafe(root))
        {
            if (ShouldSkip(dir.Name, includeHidden))
                continue;

            var node = new FileTreeNode
            {
                Name = dir.Name,
                FullPath = dir.FullName,
                IsDirectory = true,
                IsExpanded = false,
                Depth = depth
            };

            try
            {
                var children = EnumerateDirectory(dir.FullName, includeHidden, depth + 1);
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
            if (ShouldSkip(file.Name, includeHidden))
                continue;

            nodes.Add(new FileTreeNode
            {
                Name = file.Name,
                FullPath = file.FullName,
                IsDirectory = false,
                Depth = depth
            });
        }

        return nodes;
    }

    /// <summary>
    /// Returns true if name should be excluded: always filters DefaultIgnores,
    /// and additionally filters hidden entries when includeHidden is false.
    /// </summary>
    private static bool ShouldSkip(string name, bool includeHidden)
    {
        return DefaultIgnores.Contains(name) || (!includeHidden && IsHidden(name));
    }

    // Kept for backward compat with existing callers that check DefaultIgnores + hidden.
    // M2 callers should prefer ShouldSkip with an explicit includeHidden flag.
    public bool IsIgnored(string name)
    {
        return DefaultIgnores.Contains(name) || IsHidden(name);
    }

    /// <summary>
    /// Start monitoring a directory tree for file/directory creation, deletion, and rename.
    /// includeHidden controls whether the watcher's filter pipeline skips hidden entries.
    /// Returns an observable that emits file change events.
    /// </summary>
    public IObservable<FileChangeEvent> StartWatching(string path, bool includeHidden = false)
    {
        StopWatching();
        _includeHidden = includeHidden;

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

        // Capture the current flag so the closure matches the toggle state at watch time
        var currentIncludeHidden = _includeHidden;

        var observable = created
            .Select(e => new FileChangeEvent(ChangeType.Created, e.EventArgs.FullPath))
            .Merge(deleted.Select(e => new FileChangeEvent(ChangeType.Deleted, e.EventArgs.FullPath)))
            .Merge(renamed.Select(e => new FileChangeEvent(ChangeType.Renamed, e.EventArgs.FullPath, e.EventArgs.OldFullPath)))
            .Where(change => !ShouldSkip(Path.GetFileName(change.FullPath), currentIncludeHidden))
            .Throttle(TimeSpan.FromMilliseconds(100));

        _watcher.EnableRaisingEvents = true;
        return observable;
    }

    public void StopWatching()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
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
