namespace Zaide.Models;

/// <summary>
/// Type of change for a file in the Source Control panel.
/// Separate from ChangeType in FileChangeEvent.cs to avoid collision.
/// </summary>
public enum GitChangeType
{
    Added,
    Modified,
    Deleted
}

/// <summary>
/// Represents a file change in the Source Control panel.
/// Used for static/demo data — no real git operations.
/// </summary>
public class FileChange
{
    public string FilePath { get; }
    public GitChangeType ChangeType { get; }
    public bool IsStaged { get; set; }

    public FileChange(string filePath, GitChangeType changeType, bool isStaged = false)
    {
        FilePath = filePath;
        ChangeType = changeType;
        IsStaged = isStaged;
    }
}