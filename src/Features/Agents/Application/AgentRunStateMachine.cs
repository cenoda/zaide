using System;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Enforces valid run status transitions for one admitted execution attempt.
/// </summary>
internal sealed class AgentRunStateMachine
{
    public AgentRunStateMachine(AgentRunStatus initialStatus = AgentRunStatus.Created)
    {
        if (!Enum.IsDefined(initialStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialStatus),
                initialStatus,
                "Run status is invalid.");
        }

        Status = initialStatus;
    }

    public AgentRunStatus Status { get; private set; }

    public bool IsTerminal => AgentRunStatusTransitions.IsTerminal(Status);

    public void TransitionTo(AgentRunStatus nextStatus)
    {
        AgentRunStatusTransitions.ValidateTransition(Status, nextStatus);
        Status = nextStatus;
    }
}
