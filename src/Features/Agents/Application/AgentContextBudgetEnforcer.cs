using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Deterministic token budget enforcement with atomic item truncation.
/// </summary>
internal static class AgentContextBudgetEnforcer
{
    public static AgentContextBudgetResult Apply(
        IReadOnlyList<AgentContextManifestCandidate> candidates,
        int requestedBudget)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (requestedBudget < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedBudget),
                requestedBudget,
                "Requested budget cannot be negative.");
        }

        if (candidates.Count == 0)
        {
            return new AgentContextBudgetResult(
                Array.Empty<AgentContextItem>(),
                Array.Empty<AgentContextTruncationDecision>(),
                actualTokenCount: 0);
        }

        var ordered = candidates
            .OrderBy(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.Item.SourceId.Value, StringComparer.Ordinal)
            .ToArray();

        var preBudgetTokenCount = ordered.Sum(candidate => candidate.Item.EstimatedTokenCount);
        if (preBudgetTokenCount <= requestedBudget)
        {
            return new AgentContextBudgetResult(
                ordered.Select(candidate => candidate.Item).ToArray(),
                Array.Empty<AgentContextTruncationDecision>(),
                preBudgetTokenCount);
        }

        var working = ordered.ToList();
        var dropped = new List<AgentContextTruncationDecision>();

        while (working.Count > 0
               && working.Sum(candidate => candidate.Item.EstimatedTokenCount) > requestedBudget
               && working.Select(candidate => candidate.Priority).Distinct().Count() > 1)
        {
            var lowestPriority = working.Max(candidate => candidate.Priority);
            var toDrop = working
                .Where(candidate => candidate.Priority == lowestPriority)
                .ToArray();

            foreach (var candidate in toDrop)
            {
                working.Remove(candidate);
                dropped.Add(
                    new AgentContextTruncationDecision(
                        candidate.Item.SourceId,
                        reason: "budget overflow",
                        itemDropped: true,
                        itemTruncated: false));
            }
        }

        var remainingTokenCount = working.Sum(candidate => candidate.Item.EstimatedTokenCount);
        if (remainingTokenCount <= requestedBudget)
        {
            return new AgentContextBudgetResult(
                working.Select(candidate => candidate.Item).ToArray(),
                dropped,
                remainingTokenCount);
        }

        var truncated = new List<AgentContextTruncationDecision>();
        var finalItems = new List<AgentContextItem>();

        foreach (var candidate in working
                     .OrderBy(item => item.Priority)
                     .ThenBy(item => item.Item.SourceId.Value, StringComparer.Ordinal))
        {
            var truncatedContent = candidate.Item.Content + AgentContextTokenEstimator.ExceededBudgetMarker;
            var truncatedItem = new AgentContextItem(
                candidate.Item.SourceId,
                truncatedContent,
                candidate.Item.ScopeDescriptor,
                candidate.Item.Fingerprint,
                candidate.Item.RedactionState,
                AgentContextTokenEstimator.Estimate(truncatedContent),
                candidate.Item.Provenance,
                candidate.Item.RedactionReason);

            finalItems.Add(truncatedItem);
            truncated.Add(
                new AgentContextTruncationDecision(
                    candidate.Item.SourceId,
                    reason: "single-item overflow",
                    itemDropped: false,
                    itemTruncated: true));
        }

        return new AgentContextBudgetResult(
            finalItems,
            dropped.Concat(truncated).ToArray(),
            finalItems.Sum(item => item.EstimatedTokenCount));
    }
}

/// <summary>
/// Budget enforcement output for one assembly pass.
/// </summary>
internal sealed class AgentContextBudgetResult
{
    public AgentContextBudgetResult(
        IReadOnlyList<AgentContextItem> items,
        IReadOnlyList<AgentContextTruncationDecision> truncationDecisions,
        int actualTokenCount)
    {
        Items = items;
        TruncationDecisions = truncationDecisions;
        ActualTokenCount = actualTokenCount;
    }

    public IReadOnlyList<AgentContextItem> Items { get; }

    public IReadOnlyList<AgentContextTruncationDecision> TruncationDecisions { get; }

    public int ActualTokenCount { get; }
}
