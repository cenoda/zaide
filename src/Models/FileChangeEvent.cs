namespace Zaide.Models;

public enum ChangeType
{
    Created,
    Deleted,
    Renamed,
    Changed
}

/// <summary>
/// Represents a file system change event from FileSystemWatcher.
/// </summary>
public record FileChangeEvent(ChangeType Type, string FullPath, string? OldPath = null);
