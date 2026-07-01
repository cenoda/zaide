namespace Zaide.Models;

public enum WorkspaceRole
{
    User,
    Agent
}

public enum WorkspaceAgentStatus
{
    Active,
    Busy,
    Idle
}

public class WorkspaceAgent
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public WorkspaceRole Role { get; init; }
    public WorkspaceAgentStatus Status { get; set; }
}
