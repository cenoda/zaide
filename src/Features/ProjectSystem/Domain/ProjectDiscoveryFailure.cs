namespace Zaide.Features.ProjectSystem.Domain;

/// <summary>
/// Kinds of errors that can prevent project discovery from succeeding.
/// </summary>
public enum ProjectDiscoveryFailureKind
{
    /// <summary>The workspace root path is invalid (empty, whitespace, or not a directory).</summary>
    InvalidRoot,

    /// <summary>The workspace root directory does not exist.</summary>
    NotFound,

    /// <summary>The process does not have permission to enumerate the workspace root.</summary>
    Unauthorized,

    /// <summary>An I/O error occurred during discovery.</summary>
    Io,
}

/// <summary>
/// Describes why project discovery failed.
/// </summary>
/// <param name="Kind">The category of the failure.</param>
/// <param name="Message">A human-readable description of the failure.</param>
public sealed record ProjectDiscoveryFailure(
    ProjectDiscoveryFailureKind Kind,
    string Message);
