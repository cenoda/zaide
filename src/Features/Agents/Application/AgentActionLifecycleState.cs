using System;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Enforces valid action lifecycle transitions for one admitted request.
/// </summary>
internal sealed class AgentActionLifecycleState
{
    public AgentActionLifecycleState(AgentActionStatus initialStatus = AgentActionStatus.Admitted)
    {
        if (!Enum.IsDefined(initialStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialStatus),
                initialStatus,
                "Action status is invalid.");
        }

        Status = initialStatus;
    }

    public AgentActionStatus Status { get; private set; }

    public bool IsTerminal => AgentActionStatusTransitions.IsTerminal(Status);

    public bool IsNonTerminal => AgentActionStatusTransitions.IsNonTerminal(Status);

    public void TransitionTo(AgentActionStatus nextStatus)
    {
        AgentActionStatusTransitions.ValidateTransition(Status, nextStatus);
        Status = nextStatus;
    }
}
