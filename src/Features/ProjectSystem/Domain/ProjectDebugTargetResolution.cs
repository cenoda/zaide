namespace Zaide.Features.ProjectSystem.Domain;

/// <summary>
/// Result of resolving one debug launch target from a built <c>.csproj</c>.
/// </summary>
/// <param name="Kind">Structured resolution outcome.</param>
/// <param name="TargetPath">
/// Normalized absolute existing <c>.dll</c> path when <see cref="Kind"/> is
/// <see cref="ProjectDebugTargetResolutionKind.Succeeded"/>.
/// </param>
/// <param name="Message">Diagnostic message when resolution failed.</param>
public sealed record ProjectDebugTargetResolution(
    ProjectDebugTargetResolutionKind Kind,
    string? TargetPath,
    string? Message)
{
    public bool IsSuccess => Kind == ProjectDebugTargetResolutionKind.Succeeded;

    public static ProjectDebugTargetResolution Success(string targetPath) =>
        new(ProjectDebugTargetResolutionKind.Succeeded, targetPath, null);

    public static ProjectDebugTargetResolution Unsupported(string message) =>
        new(ProjectDebugTargetResolutionKind.UnsupportedLaunchTarget, null, message);
}