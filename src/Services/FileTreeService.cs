using System;
using System.Collections.Generic;
using System.IO;
using Zaide.Models;

namespace Zaide.Services;

/// <summary>
/// Enumerates directories and files into a nested FileTreeNode tree.
/// Applies an ignore list and skips hidden entries.
/// </summary>
public class FileTreeService
{
    private static readonly HashSet<string> DefaultIgnores = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj", ".git", ".vs", ".idea",
        "__pycache__", ".DS_Store", "Thumbs.db"
    };

    /// <summary>
    /// Recursively enumerate a directory into a list of FileTreeNode.
    /// Directories are sorted first, then files. Both are sorted alphabetically.
    /// </summary>
    public List<FileTreeNode> EnumerateDirectory(string path)
    {
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");

        var root = new DirectoryInfo(path);
        var nodes = new List<FileTreeNode>();

        // Directories first
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
                // Skip directories we can't access
            }

            nodes.Add(node);
        }

        // Files second
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

    /// <summary>
    /// Returns true if the file/directory name should be hidden from the tree.
    /// </summary>
    public bool IsIgnored(string name)
    {
        return DefaultIgnores.Contains(name) || IsHidden(name);
    }

    /// <summary>
    /// Returns true if the name starts with a dot (hidden on Unix).
    /// </summary>
    private static bool IsHidden(string name)
    {
        return name.Length > 0 && name[0] == '.';
    }

    private static IEnumerable<DirectoryInfo> EnumerateDirectoriesSafe(DirectoryInfo root)
    {
        try
        {
            return root.EnumerateDirectories();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<DirectoryInfo>();
        }
    }

    private static IEnumerable<FileInfo> EnumerateFilesSafe(DirectoryInfo root)
    {
        try
        {
            return root.EnumerateFiles();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<FileInfo>();
        }
    }
}
