namespace Zaide.Services;

/// <summary>
/// Result of a stage or unstage mutation attempt. Truthfully represents
/// failure (file removed externally, repo error, IO error) instead of
/// throwing across the seam boundary.
/// </summary>
public sealed class StageResult
{
    /// <summary>True when the stage/unstage operation completed successfully.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Human-readable failure reason, or null on success.</summary>
    public string? ErrorMessage { get; init; }

    public static StageResult Success() => new() { IsSuccess = true };

    public static StageResult Failure(string message) =>
        new() { IsSuccess = false, ErrorMessage = message };
}
