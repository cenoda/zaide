namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Deterministic outcome categories for one bounded workspace file read.
/// </summary>
internal enum AgentFileReadOutcome
{
    /// <summary>A bounded regular file was read and digested.</summary>
    Succeeded,

    /// <summary>The requested path does not exist within the workspace.</summary>
    NotFound,

    /// <summary>
    /// The path exists but is not a regular file (directory, FIFO, socket,
    /// device, or other special file).
    /// </summary>
    NotRegularFile,

    /// <summary>
    /// The path resolves outside the captured workspace root (traversal,
    /// alternate root, or symbolic-link escape, including a link retargeted
    /// between validation and open).
    /// </summary>
    PathEscaped,

    /// <summary>The file contains non-text (NUL bytes or invalid UTF-8).</summary>
    Binary,

    /// <summary>The file exceeds the locked regular-file read budget.</summary>
    TooLarge,

    /// <summary>The file could not be read (permission or I/O failure).</summary>
    Unreadable,

    /// <summary>The read was cancelled before or during the operation.</summary>
    Cancelled,
}
