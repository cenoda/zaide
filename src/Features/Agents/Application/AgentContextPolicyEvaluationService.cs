using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Deterministic policy evaluation for Phase 18 IDE context disclosure.
/// </summary>
internal sealed class AgentContextPolicyEvaluationService
{
    public AgentContextPolicyEvaluationResult Evaluate(AgentContextPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var effectiveLevel = policy.EffectiveLevel;
        var includedSources = new List<AgentContextSourceId>();
        var policyExclusions = new List<AgentContextExclusionDecision>();

        foreach (var sourceId in AgentContextSourceId.All.OrderBy(
                     source => AgentContextSourcePriority.GetPriority(source)))
        {
            if (AgentContextSourcePolicyMatrix.IsSourceIncluded(sourceId, effectiveLevel))
            {
                includedSources.Add(sourceId);
                continue;
            }

            policyExclusions.Add(
                new AgentContextExclusionDecision(
                    sourceId: sourceId,
                    hardExclusionId: null,
                    reason: $"Source excluded by {effectiveLevel} policy level.",
                    isHardExclusion: false));
        }

        return new AgentContextPolicyEvaluationResult(
            effectiveLevel,
            includedSources,
            policyExclusions);
    }
}
