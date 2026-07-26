using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Deterministic token budget contract for one policy level.
/// </summary>
internal sealed class AgentContextTokenBudget
{
    public AgentContextTokenBudget(
        AgentContextPolicyLevel policyLevel,
        int requestedBudget,
        int actualTokenCount)
    {
        if (!Enum.IsDefined(policyLevel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(policyLevel),
                policyLevel,
                "Policy level is invalid.");
        }

        if (requestedBudget < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedBudget),
                requestedBudget,
                "Requested budget cannot be negative.");
        }

        if (actualTokenCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actualTokenCount),
                actualTokenCount,
                "Actual token count cannot be negative.");
        }

        PolicyLevel = policyLevel;
        RequestedBudget = requestedBudget;
        ActualTokenCount = actualTokenCount;
    }

    public AgentContextPolicyLevel PolicyLevel { get; }

    public int RequestedBudget { get; }

    public int ActualTokenCount { get; }
}
