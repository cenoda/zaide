namespace Zaide.Services;

/// <summary>
/// Structured rejection reason when <see cref="IProjectOperationGate"/> denies admission.
/// </summary>
public enum ProjectOperationRejectionReason
{
    WorkflowBusy,
    DebugSessionActive,
}