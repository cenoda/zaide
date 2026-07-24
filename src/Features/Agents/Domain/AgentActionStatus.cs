using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Lifecycle states for one admitted action request.
/// </summary>
internal enum AgentActionStatus
{
    Admitted,
    Classified,
    AwaitingPermissionDecision,
    PermissionGranted,
    PermissionDenied,
    ReadyToExecute,
    Executing,
    Succeeded,
    Failed,
    Denied,
    Revoked,
    Cancelled,
    Conflict,
    Indeterminate,
}

/// <summary>
/// Valid action lifecycle transitions enforced by the control plane.
/// </summary>
internal static class AgentActionStatusTransitions
{
    public static bool IsTerminal(AgentActionStatus status) =>
        status is AgentActionStatus.Succeeded
            or AgentActionStatus.Failed
            or AgentActionStatus.Denied
            or AgentActionStatus.Revoked
            or AgentActionStatus.Cancelled
            or AgentActionStatus.Conflict
            or AgentActionStatus.Indeterminate;

    public static bool IsNonTerminal(AgentActionStatus status) => !IsTerminal(status);

    public static bool CanTransition(AgentActionStatus from, AgentActionStatus to)
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
            AgentActionStatus.Admitted => to is AgentActionStatus.Classified
                or AgentActionStatus.Denied
                or AgentActionStatus.Revoked
                or AgentActionStatus.Cancelled,
            AgentActionStatus.Classified => to is AgentActionStatus.AwaitingPermissionDecision
                or AgentActionStatus.ReadyToExecute
                or AgentActionStatus.Denied
                or AgentActionStatus.Revoked
                or AgentActionStatus.Cancelled,
            AgentActionStatus.AwaitingPermissionDecision => to is AgentActionStatus.PermissionGranted
                or AgentActionStatus.PermissionDenied
                or AgentActionStatus.Denied
                or AgentActionStatus.Revoked
                or AgentActionStatus.Cancelled,
            AgentActionStatus.PermissionGranted => to is AgentActionStatus.ReadyToExecute
                or AgentActionStatus.Revoked
                or AgentActionStatus.Cancelled,
            AgentActionStatus.PermissionDenied => to is AgentActionStatus.Denied
                or AgentActionStatus.Revoked,
            AgentActionStatus.ReadyToExecute => to is AgentActionStatus.Executing
                or AgentActionStatus.Revoked
                or AgentActionStatus.Cancelled
                or AgentActionStatus.Conflict,
            AgentActionStatus.Executing => to is AgentActionStatus.Succeeded
                or AgentActionStatus.Failed
                or AgentActionStatus.Revoked
                or AgentActionStatus.Cancelled
                or AgentActionStatus.Conflict
                or AgentActionStatus.Indeterminate,
            _ => false,
        };
    }

    public static void ValidateTransition(AgentActionStatus from, AgentActionStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException(
                $"Invalid agent action transition from {from} to {to}.");
        }
    }
}
