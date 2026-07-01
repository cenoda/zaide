using System;
using System.Collections.Generic;
using System.IO;
using Zaide.Models;

namespace Zaide.Services;

/// <summary>
/// Interface for file tree enumeration and file system watching.
/// Separates pure tree operations from file system infrastructure.
/// </summary>
public interface IFileTreeService : IDisposable
{
    /// <summary>
    /// Recursively enumerate a directory into a list of FileTreeNode.
    /// </summary>
    List<FileTreeNode> EnumerateDirectory(string path, bool includeHidden = false);

    /// <summary>
    /// Start monitoring a directory tree for file/directory creation, deletion, and rename.
    /// Returns an observable that emits file change events.
    /// </summary>
    IObservable<FileChangeEvent> StartWatching(string path, bool includeHidden = false);

    /// <summary>
    /// Stop monitoring.
    /// </summary>
    void StopWatching();

    /// <summary>
    /// Creates an empty file at the given path.
    /// </summary>
    void CreateFile(string path);

    /// <summary>
    /// Creates a directory at the given path.
    /// </summary>
    void CreateDirectory(string path);
}