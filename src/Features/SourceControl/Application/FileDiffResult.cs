namespace Zaide.Features.SourceControl.Application;

/// <summary>
/// Result of a single-file diff operation produced by <see cref="IFileDiffService"/>.
/// When <see cref="IsBinary"/> is true, <see cref="DiffText"/> is null and the
/// view should show a "Binary file — diff not available" notice.
/// </summary>
public sealed class FileDiffResult
{
    /// <summary>
    /// Repository-relative path of the file that was diffed.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// True when the file is binary and no diff text can be produced.
    /// </summary>
    public bool IsBinary { get; init; }

    /// <summary>
    /// Unified diff text, or null when <see cref="IsBinary"/> is true.
    /// </summary>
    public string? DiffText { get; init; }

    /// <summary>
    /// Number of added lines reported by the diff engine.
    /// </summary>
    public int AddedLines { get; init; }

    /// <summary>
    /// Number of deleted lines reported by the diff engine.
    /// </summary>
    public int DeletedLines { get; init; }
}
