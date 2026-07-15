namespace Zaide.Services;

/// <summary>
/// Result of a push mutation attempt. Truthfully represents validation
/// failures (dirty tree, no upstream, nothing to push) and service
/// failures instead of throwing across the seam boundary.
/// </summary>
public sealed class PushResult
{
    /// <summary>True when the push completed successfully.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Human-readable failure reason, or null on success.</summary>
    public string? ErrorMessage { get; init; }

    public static PushResult Success() =>
        new() { IsSuccess = true };

    public static PushResult Failure(string message) =>
        new() { IsSuccess = false, ErrorMessage = message };
}