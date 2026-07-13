namespace Zaide.Services;

/// <summary>
/// Maps workflow output snapshots to operator-facing status text for the Output surface.
/// Single owner for Build / Run / Test progress and terminal outcome strings (Phase 11 U6).
/// Does not write <c>MainWindowViewModel.StatusText</c>; hosts that need main-bar merge
/// must document a policy separately (Option A: panel-local only).
/// </summary>
public static class ProjectWorkflowStatusPolicy
{
    /// <summary>
    /// Builds a concise status line for the current workflow/output snapshot.
    /// In-progress text uses <see cref="ProjectOutputSnapshot.ActiveOperation"/>;
    /// terminal outcomes use <see cref="ProjectOutputSnapshot.LastOperation"/>
    /// (falling back to active when set).
    /// </summary>
    public static string? MapOutputStatusMessage(ProjectOutputSnapshot snapshot)
    {
        if (snapshot.State is ProjectWorkflowOperationState.Starting
            or ProjectWorkflowOperationState.Running)
        {
            var verb = VerbProgress(snapshot.ActiveOperation);

            return snapshot.TargetFilePath is { Length: > 0 } target
                ? $"{verb} {target}…"
                : $"{verb}…";
        }

        if (snapshot.LastOutcome is null)
            return null;

        var noun = Noun(snapshot.LastOperation ?? snapshot.ActiveOperation);
        return snapshot.LastOutcome switch
        {
            ProjectWorkflowOutcomeKind.Succeeded => $"{noun} succeeded.",
            ProjectWorkflowOutcomeKind.Failed => $"{noun} failed.",
            ProjectWorkflowOutcomeKind.StartupFailed => $"{noun} could not start.",
            ProjectWorkflowOutcomeKind.Cancelled => $"{noun} cancelled.",
            _ => null,
        };
    }

    private static string VerbProgress(ProjectWorkflowOperation? operation) =>
        operation switch
        {
            ProjectWorkflowOperation.Build => "Building",
            ProjectWorkflowOperation.Run => "Running",
            ProjectWorkflowOperation.Test => "Testing",
            _ => "Working",
        };

    /// <summary>
    /// Operator-facing noun for terminal outcomes. Test uses plural "Tests"
    /// for natural "Tests succeeded." phrasing.
    /// </summary>
    private static string Noun(ProjectWorkflowOperation? operation) =>
        operation switch
        {
            ProjectWorkflowOperation.Build => "Build",
            ProjectWorkflowOperation.Run => "Run",
            ProjectWorkflowOperation.Test => "Tests",
            _ => "Build",
        };
}
