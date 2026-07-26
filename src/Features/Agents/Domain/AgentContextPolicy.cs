using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Resolved IDE context policy with application default and optional session override.
/// </summary>
internal sealed class AgentContextPolicy
{
    public AgentContextPolicy(
        AgentContextPolicyLevel applicationDefaultLevel,
        AgentContextSessionOverride? sessionOverride = null)
    {
        if (!Enum.IsDefined(applicationDefaultLevel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(applicationDefaultLevel),
                applicationDefaultLevel,
                "Application default level is invalid.");
        }

        ApplicationDefaultLevel = applicationDefaultLevel;
        SessionOverride = sessionOverride;
    }

    public AgentContextPolicyLevel ApplicationDefaultLevel { get; }

    public AgentContextSessionOverride? SessionOverride { get; }

    public AgentContextPolicyLevel EffectiveLevel =>
        SessionOverride?.Level ?? ApplicationDefaultLevel;

    public static AgentContextPolicy CreateApplicationDefault() =>
        new(AgentContextApplicationDefault.Level);
}
