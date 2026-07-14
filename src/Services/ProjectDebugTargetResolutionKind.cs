namespace Zaide.Services;

/// <summary>
/// Structured result categories for MSBuild <c>TargetPath</c> resolution.
/// </summary>
public enum ProjectDebugTargetResolutionKind
{
    Succeeded,
    UnsupportedLaunchTarget,
}