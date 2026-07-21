using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Session lifecycle states for one in-memory Agent Session owner.
/// </summary>
internal enum AgentSessionStatus
{
    Ready,
    Running,
    Ending,
    Ended,
}

/// <summary>
/// Valid session status transitions enforced by the session lifecycle owner.
/// </summary>
internal static class AgentSessionStatusTransitions
{
    public static bool CanTransition(AgentSessionStatus from, AgentSessionStatus to)
    {
        if (from == to)
        {
            return false;
        }

        return from switch
        {
            AgentSessionStatus.Ready => to is AgentSessionStatus.Running
                or AgentSessionStatus.Ending
                or AgentSessionStatus.Ended,
            AgentSessionStatus.Running => to is AgentSessionStatus.Ready
                or AgentSessionStatus.Ending,
            AgentSessionStatus.Ending => to == AgentSessionStatus.Ended,
            AgentSessionStatus.Ended => false,
            _ => false,
        };
    }

    public static void ValidateTransition(AgentSessionStatus from, AgentSessionStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException(
                $"Invalid agent session transition from {from} to {to}.");
        }
    }
}
