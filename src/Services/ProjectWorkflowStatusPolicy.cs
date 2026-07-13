namespace Zaide.Services;

/// <summary>
/// Maps workflow output snapshots to operator-facing status text for the Output surface.
/// </summary>
public static class ProjectWorkflowStatusPolicy
{
    /// <summary>
    /// Builds a concise status line for the current workflow/output snapshot.
    /// </summary>
    public static string? MapOutputStatusMessage(ProjectOutputSnapshot snapshot)
    {
        if (snapshot.State is ProjectWorkflowOperationState.Starting
            or ProjectWorkflowOperationState.Running)
        {
            var verb = snapshot.ActiveOperation switch
            {
                ProjectWorkflowOperation.Build => "Building",
                ProjectWorkflowOperation.Run => "Running",
                ProjectWorkflowOperation.Test => "Testing",
                _ => "Working",
            };

            return snapshot.TargetFilePath is { Length: > 0 } target
                ? $"{verb} {target}…"
                : $"{verb}…";
        }

        return snapshot.LastOutcome switch
        {
            ProjectWorkflowOutcomeKind.Succeeded => "Build succeeded.",
            ProjectWorkflowOutcomeKind.Failed => "Build failed.",
            ProjectWorkflowOutcomeKind.StartupFailed => "Build could not start.",
            ProjectWorkflowOutcomeKind.Cancelled => "Build cancelled.",
            _ => null,
        };
    }
}
