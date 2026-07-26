using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Optional session-level override for IDE context disclosure policy.
/// </summary>
internal sealed class AgentContextSessionOverride
{
    public AgentContextSessionOverride(AgentContextPolicyLevel? level)
    {
        if (level.HasValue && !Enum.IsDefined(level.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(level),
                level,
                "Policy level is invalid.");
        }

        Level = level;
    }

    public AgentContextPolicyLevel? Level { get; }

    public bool IsActive => Level.HasValue;
}
