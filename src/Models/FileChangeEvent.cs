namespace Zaide.Models;

public enum ChangeType
{
    Created,
    Deleted,
    Renamed,
    Changed,
    /// <summary>
    /// Raised when the underlying FileSystemWatcher encounters an internal error
    /// (e.g. buffer overflow). The consumer should restart observation and
    /// re-enumerate to reconcile any missed events.
    /// </summary>
    WatcherError
}

/// <summary>
/// Represents a file system change event from FileSystemWatcher.
/// </summary>
public record FileChangeEvent(ChangeType Type, string FullPath, string? OldPath = null);
