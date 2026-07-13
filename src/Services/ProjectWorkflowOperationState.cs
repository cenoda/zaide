namespace Zaide.Services;

/// <summary>
/// Operational state of the project workflow service slot.
/// </summary>
public enum ProjectWorkflowOperationState
{
    /// <summary>No operation is active.</summary>
    Idle,

    /// <summary>A process is being started.</summary>
    Starting,

    /// <summary>A managed process is running.</summary>
    Running,
}
