using System;

namespace Zaide.Features.Townhall.Domain;

/// <summary>
/// Represents a person or agent in the Workspace.
/// </summary>
public class WorkspaceAgent
{
    /// <summary>
    /// Unique identifier for the agent.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the agent.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Avatar path or resource key for the agent.
    /// </summary>
    public string Avatar { get; set; } = string.Empty;

    /// <summary>
    /// Role of the agent (User or Agent).
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the agent.
    /// </summary>
    public AgentStatus Status { get; set; }

    /// <summary>
    /// Whether this agent has an active warning.
    /// </summary>
    public bool HasWarning { get; set; }
}

/// <summary>
/// Defines the possible statuses for a workspace agent.
/// </summary>
public enum AgentStatus
{
    /// <summary>
    /// Agent is active and available.
    /// </summary>
    Active,

    /// <summary>
    /// Agent is busy or unavailable.
    /// </summary>
    Busy,

    /// <summary>
    /// Agent is idle or away.
    /// </summary>
    Idle
}