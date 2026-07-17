namespace Zaide.Features.ProjectSystem.Domain;

/// <summary>
/// Structured rejection reason when <see cref="IProjectOperationGate"/> denies admission.
/// </summary>
public enum ProjectOperationRejectionReason
{
    WorkflowBusy,
    DebugSessionActive,
}