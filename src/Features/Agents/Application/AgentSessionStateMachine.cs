using System;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Enforces valid session status transitions for one in-memory session owner.
/// </summary>
internal sealed class AgentSessionStateMachine
{
    public AgentSessionStateMachine(AgentSessionStatus initialStatus = AgentSessionStatus.Ready)
    {
        if (!Enum.IsDefined(initialStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialStatus),
                initialStatus,
                "Session status is invalid.");
        }

        Status = initialStatus;
    }

    public AgentSessionStatus Status { get; private set; }

    public void TransitionTo(AgentSessionStatus nextStatus)
    {
        AgentSessionStatusTransitions.ValidateTransition(Status, nextStatus);
        Status = nextStatus;
    }
}
