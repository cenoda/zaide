namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Deterministic outcome categories for one bounded workspace file mutation.
/// </summary>
internal enum AgentFileMutationOutcome
{
    /// <summary>The requested mutation completed and was confirmed on disk.</summary>
    Succeeded,

    /// <summary>
    /// The target state no longer matches the captured base revision, or a create
    /// target is no longer absent.
    /// </summary>
    Conflict,

    /// <summary>The requested path does not exist when replace/delete requires it.</summary>
    NotFound,

    /// <summary>
    /// The path resolves outside the captured workspace root (traversal,
    /// alternate root, symbolic-link escape, or root replacement).
    /// </summary>
    PathEscaped,

    /// <summary>The path exists but is not a regular file.</summary>
    NotRegularFile,

    /// <summary>The file or workspace could not be accessed (permission or I/O failure).</summary>
    Unreadable,

    /// <summary>The mutation was cancelled before or during the operation.</summary>
    Cancelled,

    /// <summary>
    /// The mutation failed after validation (partial write, rename/delete failure,
    /// or cleanup could not restore prior state).
    /// </summary>
    Failed,
}
