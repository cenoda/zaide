namespace Zaide.Services;

/// <summary>
/// Terminal outcome of a workflow operation start attempt or run.
/// </summary>
public enum ProjectWorkflowOutcomeKind
{
    Succeeded,
    Failed,
    StartupFailed,
    Cancelled,
    RejectedConcurrent,
    RejectedContext,
}
