using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Explicit capability fact value. Unknown and unavailable remain distinct
/// from supported and not-supported states.
/// </summary>
internal enum AgentCapabilityFactValue
{
    Unknown,
    Unavailable,
    Supported,
    NotSupported,
}

/// <summary>
/// Separate advertised, available, configured, permission, degradation, and
/// current usability facts for one capability row.
/// </summary>
internal sealed class AgentCapabilityState
{
    private AgentCapabilityState(
        AgentCapabilityFactValue advertised,
        AgentCapabilityFactValue available,
        AgentCapabilityFactValue configured,
        AgentCapabilityFactValue permitted,
        AgentCapabilityFactValue degraded,
        AgentCapabilityFactValue currentlyUsable)
    {
        Advertised = advertised;
        Available = available;
        Configured = configured;
        Permitted = permitted;
        Degraded = degraded;
        CurrentlyUsable = currentlyUsable;
    }

    public AgentCapabilityFactValue Advertised { get; }

    public AgentCapabilityFactValue Available { get; }

    public AgentCapabilityFactValue Configured { get; }

    public AgentCapabilityFactValue Permitted { get; }

    public AgentCapabilityFactValue Degraded { get; }

    public AgentCapabilityFactValue CurrentlyUsable { get; }

    public static AgentCapabilityState Create(
        AgentCapabilityFactValue advertised,
        AgentCapabilityFactValue available,
        AgentCapabilityFactValue configured,
        AgentCapabilityFactValue permitted,
        AgentCapabilityFactValue degraded,
        AgentCapabilityFactValue currentlyUsable)
    {
        ValidateFact(advertised, nameof(advertised));
        ValidateFact(available, nameof(available));
        ValidateFact(configured, nameof(configured));
        ValidateFact(permitted, nameof(permitted));
        ValidateFact(degraded, nameof(degraded));
        ValidateFact(currentlyUsable, nameof(currentlyUsable));

        return new AgentCapabilityState(
            advertised,
            available,
            configured,
            permitted,
            degraded,
            currentlyUsable);
    }

    private static void ValidateFact(AgentCapabilityFactValue value, string paramName)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Capability fact value is invalid.");
        }
    }
}
