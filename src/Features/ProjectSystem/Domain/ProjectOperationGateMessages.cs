namespace Zaide.Features.ProjectSystem.Domain;

/// <summary>
/// Operator-facing admission-control messages for the shared project-operation gate.
/// </summary>
public static class ProjectOperationGateMessages
{
    public const string WorkflowBusy = "Workflow busy";

    public const string DebugSessionActive = "Debug session active";
}