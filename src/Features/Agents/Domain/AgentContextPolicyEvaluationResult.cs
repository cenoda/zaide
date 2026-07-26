using System;
using System.Collections.Generic;
using System.Linq;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Resolved policy outcome for one assembly pass.
/// </summary>
internal sealed class AgentContextPolicyEvaluationResult
{
    public AgentContextPolicyEvaluationResult(
        AgentContextPolicyLevel effectiveLevel,
        IEnumerable<AgentContextSourceId> includedSources,
        IEnumerable<AgentContextExclusionDecision> policyExclusionDecisions)
    {
        if (!Enum.IsDefined(effectiveLevel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(effectiveLevel),
                effectiveLevel,
                "Policy level is invalid.");
        }

        ArgumentNullException.ThrowIfNull(includedSources);
        ArgumentNullException.ThrowIfNull(policyExclusionDecisions);

        var normalizedIncluded = includedSources.ToArray();
        if (normalizedIncluded.Any(source => source == default))
        {
            throw new ArgumentException(
                "Included sources cannot contain default ids.",
                nameof(includedSources));
        }

        var normalizedExclusions = policyExclusionDecisions.ToArray();
        if (normalizedExclusions.Any(decision => decision is null))
        {
            throw new ArgumentException(
                "Policy exclusion decisions cannot contain null entries.",
                nameof(policyExclusionDecisions));
        }

        EffectiveLevel = effectiveLevel;
        IncludedSources = Array.AsReadOnly(normalizedIncluded);
        PolicyExclusionDecisions = Array.AsReadOnly(normalizedExclusions);
    }

    public AgentContextPolicyLevel EffectiveLevel { get; }

    public IReadOnlyList<AgentContextSourceId> IncludedSources { get; }

    public IReadOnlyList<AgentContextExclusionDecision> PolicyExclusionDecisions { get; }
}
