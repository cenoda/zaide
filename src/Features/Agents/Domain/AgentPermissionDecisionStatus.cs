using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Lifecycle states for one permission decision publication.
/// </summary>
internal enum AgentPermissionDecisionStatus
{
    Published,
    Consumed,
    Expired,
    Revoked,
    Denied,
}

/// <summary>
/// Valid permission decision status transitions.
/// </summary>
internal static class AgentPermissionDecisionStatusTransitions
{
    public static bool IsTerminal(AgentPermissionDecisionStatus status) =>
        status is AgentPermissionDecisionStatus.Consumed
            or AgentPermissionDecisionStatus.Expired
            or AgentPermissionDecisionStatus.Revoked
            or AgentPermissionDecisionStatus.Denied;

    public static bool CanTransition(AgentPermissionDecisionStatus from, AgentPermissionDecisionStatus to)
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
            AgentPermissionDecisionStatus.Published => to is AgentPermissionDecisionStatus.Consumed
                or AgentPermissionDecisionStatus.Expired
                or AgentPermissionDecisionStatus.Revoked
                or AgentPermissionDecisionStatus.Denied,
            _ => false,
        };
    }

    public static void ValidateTransition(AgentPermissionDecisionStatus from, AgentPermissionDecisionStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException(
                $"Invalid permission decision transition from {from} to {to}.");
        }
    }
}
