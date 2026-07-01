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
    /// </summary>
    void StartWatching(string path, bool includeHidden = false);

    /// <summary>
    /// Stop monitoring.
    /// </summary>
    void StopWatching();

    /// <summary>
    /// Observable of file change events. Null when not watching.
    /// </summary>
    IObservable<FileChangeEvent>? FileChanges { get; }

    /// <summary>
    /// Creates an empty file at the given path.
    /// </summary>
    void CreateFile(string path);

    /// <summary>
    /// Creates a directory at the given path.
    /// </summary>
    void CreateDirectory(string path);
}