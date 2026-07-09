namespace Zaide.Services;

/// <summary>
/// Result of a repository-root discovery attempt. Truthfully represents the
/// "not inside a git repository" case instead of hiding it.
/// </summary>
public sealed class RepositoryDiscoveryResult
{
    /// <summary>
    /// True when the starting path is inside a git repository.
    /// </summary>
    public bool IsRepository { get; init; }

    /// <summary>
    /// The discovered repository root (normalized, ending in .git/), or null
    /// when <see cref="IsRepository"/> is false.
    /// </summary>
    public string? RepositoryRoot { get; init; }

    /// <summary>
    /// The path discovery started from.
    /// </summary>
    public string StartingPath { get; init; } = string.Empty;

    public static RepositoryDiscoveryResult NotFound(string startingPath) =>
        new() { IsRepository = false, StartingPath = startingPath };

    public static RepositoryDiscoveryResult Found(string startingPath, string root) =>
        new() { IsRepository = true, StartingPath = startingPath, RepositoryRoot = root };
}