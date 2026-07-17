using System.IO;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Features.ProjectSystem.Infrastructure;

/// <summary>
/// Resolves workflow targets from authoritative <see cref="ProjectContext"/> snapshots.
/// </summary>
public static class ProjectTargetResolver
{
    /// <summary>
    /// Returns whether the context can host build/run/test operations.
    /// Mirrors Phase 10 language-session eligibility.
    /// </summary>
    public static bool IsEligible(ProjectContext context) =>
        context.SelectedProject is not null &&
        context.State is ProjectContextState.SingleProject or ProjectContextState.Selected;

    /// <summary>
    /// Resolves the workflow target for <paramref name="operation"/> from
    /// <paramref name="context"/>.
    /// </summary>
    public static ProjectTargetResolution Resolve(
        ProjectContext context,
        ProjectWorkflowOperation operation)
    {
        if (!IsEligible(context))
            return ProjectTargetResolution.RejectedContext();

        var candidate = context.SelectedProject!;
        if (operation == ProjectWorkflowOperation.Run &&
            candidate.Kind is ProjectKind.Solution or ProjectKind.SolutionX)
        {
            return ProjectTargetResolution.RejectedContext();
        }

        var filePath = Path.GetFullPath(candidate.FilePath);
        var workingDirectory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidDataException($"Project file has no parent directory: {filePath}");

        return ProjectTargetResolution.Success(
            new ResolvedProjectTarget(
                filePath,
                workingDirectory,
                candidate.Kind,
                candidate.DisplayName));
    }
}
