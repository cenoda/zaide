namespace Zaide.Services;

/// <summary>
/// Result of a commit mutation attempt. Truthfully represents validation
/// failures (empty message, nothing staged, missing git identity) and
/// service failures (IO, repo corruption) instead of throwing across
/// the seam boundary.
/// </summary>
public sealed class CommitResult
{
    /// <summary>True when the commit completed successfully.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>The SHA of the newly created commit, or null on failure.</summary>
    public string? CommitSha { get; init; }

    /// <summary>Human-readable failure reason, or null on success.</summary>
    public string? ErrorMessage { get; init; }

    public static CommitResult Success(string sha) =>
        new() { IsSuccess = true, CommitSha = sha };

    public static CommitResult Failure(string message) =>
        new() { IsSuccess = false, ErrorMessage = message };
}
