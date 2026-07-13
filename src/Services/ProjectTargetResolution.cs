namespace Zaide.Services;

/// <summary>
/// Result of resolving a workflow target from a <see cref="ProjectContext"/> snapshot.
/// </summary>
public sealed class ProjectTargetResolution
{
    private ProjectTargetResolution(bool isSuccess, ResolvedProjectTarget? target)
    {
        IsSuccess = isSuccess;
        Target = target;
    }

    /// <summary>
    /// <c>true</c> when a target was resolved; otherwise the caller must treat the
    /// attempt as <see cref="ProjectWorkflowOutcomeKind.RejectedContext"/>.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// The resolved target when <see cref="IsSuccess"/> is <c>true</c>.
    /// </summary>
    public ResolvedProjectTarget? Target { get; }

    /// <summary>Creates a successful resolution.</summary>
    public static ProjectTargetResolution Success(ResolvedProjectTarget target) =>
        new(true, target ?? throw new System.ArgumentNullException(nameof(target)));

    /// <summary>Creates a rejected-context resolution.</summary>
    public static ProjectTargetResolution RejectedContext() =>
        new(false, null);
}
