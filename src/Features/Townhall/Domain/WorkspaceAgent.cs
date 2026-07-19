using System;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Townhall.Domain;

/// <summary>
/// Represents a person or agent in the Workspace.
/// Identity projections are read-only views of the canonical <see cref="Actor"/> row.
/// </summary>
public class WorkspaceAgent
{
    private readonly Actor _actor;

    /// <summary>
    /// Creates a workspace roster entry bound to a canonical actor row.
    /// </summary>
    public WorkspaceAgent(Actor actor)
    {
        _actor = actor ?? throw new ArgumentNullException(nameof(actor));
    }

    /// <summary>
    /// Typed canonical actor identity for this roster entry.
    /// </summary>
    public ActorId ActorId => _actor.Id;

    /// <summary>
    /// Legacy projected identifier derived from the canonical actor row.
    /// </summary>
    public string Id => _actor.ProjectedLegacyId;

    /// <summary>
    /// Legacy projected display name derived from the canonical actor row.
    /// </summary>
    public string Name => _actor.DisplayName;

    /// <summary>
    /// Legacy projected avatar resource key derived from the canonical actor row.
    /// </summary>
    public string Avatar => _actor.AvatarResourceKey;

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
