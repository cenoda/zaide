using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Run lifecycle states for one admitted execution attempt.
/// </summary>
internal enum AgentRunStatus
{
    Created,
    Accepted,
    Rejected,
    Running,
    CancellationRequested,
    Completed,
    Failed,
    Cancelled,
    TimedOut,
    Disconnected,
    Indeterminate,
}

/// <summary>
/// Valid run status transitions enforced by the run lifecycle owner.
/// </summary>
internal static class AgentRunStatusTransitions
{
    public static bool IsTerminal(AgentRunStatus status) =>
        status is AgentRunStatus.Rejected
            or AgentRunStatus.Completed
            or AgentRunStatus.Failed
            or AgentRunStatus.Cancelled
            or AgentRunStatus.TimedOut
            or AgentRunStatus.Disconnected
            or AgentRunStatus.Indeterminate;

    public static bool CanTransition(AgentRunStatus from, AgentRunStatus to)
    {
        if (from == to)
        {
            return false;
        }

        if (IsTerminal(from))
        {
            return false;
        }

        return from switch
        {
            AgentRunStatus.Created => to is AgentRunStatus.Accepted or AgentRunStatus.Rejected,
            AgentRunStatus.Accepted => to == AgentRunStatus.Running,
            AgentRunStatus.Running => to is AgentRunStatus.CancellationRequested
                or AgentRunStatus.Completed
                or AgentRunStatus.Failed
                or AgentRunStatus.Cancelled
                or AgentRunStatus.TimedOut
                or AgentRunStatus.Disconnected
                or AgentRunStatus.Indeterminate,
            AgentRunStatus.CancellationRequested => to is AgentRunStatus.Completed
                or AgentRunStatus.Failed
                or AgentRunStatus.Cancelled
                or AgentRunStatus.TimedOut
                or AgentRunStatus.Disconnected
                or AgentRunStatus.Indeterminate,
            _ => false,
        };
    }

    public static void ValidateTransition(AgentRunStatus from, AgentRunStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException(
                $"Invalid agent run transition from {from} to {to}.");
        }
    }
}
