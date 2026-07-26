using System;
using System.Collections.Generic;
using System.Linq;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Exclusion and truncation metadata for one context disclosure event.
/// Contains identifiers and counts only.
/// </summary>
internal sealed class AgentContextDisclosureBoundarySummary
{
    public AgentContextDisclosureBoundarySummary(
        int excludedSourceCount,
        int hardExclusionCount,
        int truncatedItemCount,
        int droppedItemCount,
        IEnumerable<AgentContextSourceId>? excludedSourceIds = null,
        IEnumerable<AgentContextHardExclusionId>? hardExclusionIds = null,
        IEnumerable<AgentContextSourceId>? truncatedSourceIds = null)
    {
        if (excludedSourceCount < 0
            || hardExclusionCount < 0
            || truncatedItemCount < 0
            || droppedItemCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                "Boundary summary counts cannot be negative.");
        }

        var normalizedExcludedSources = NormalizeSourceIds(excludedSourceIds);
        var normalizedHardExclusions = NormalizeHardExclusionIds(hardExclusionIds);
        var normalizedTruncatedSources = NormalizeSourceIds(truncatedSourceIds);

        if (normalizedExcludedSources.Count != excludedSourceCount)
        {
            throw new ArgumentException(
                "Excluded source count must match excluded source identifiers.",
                nameof(excludedSourceCount));
        }

        if (normalizedHardExclusions.Count != hardExclusionCount)
        {
            throw new ArgumentException(
                "Hard exclusion count must match hard exclusion identifiers.",
                nameof(hardExclusionCount));
        }

        if (normalizedTruncatedSources.Count != truncatedItemCount)
        {
            throw new ArgumentException(
                "Truncated item count must match truncated source identifiers.",
                nameof(truncatedItemCount));
        }

        ExcludedSourceCount = excludedSourceCount;
        HardExclusionCount = hardExclusionCount;
        TruncatedItemCount = truncatedItemCount;
        DroppedItemCount = droppedItemCount;
        ExcludedSourceIds = normalizedExcludedSources;
        HardExclusionIds = normalizedHardExclusions;
        TruncatedSourceIds = normalizedTruncatedSources;
    }

    public int ExcludedSourceCount { get; }

    public int HardExclusionCount { get; }

    public int TruncatedItemCount { get; }

    public int DroppedItemCount { get; }

    public IReadOnlyList<AgentContextSourceId> ExcludedSourceIds { get; }

    public IReadOnlyList<AgentContextHardExclusionId> HardExclusionIds { get; }

    public IReadOnlyList<AgentContextSourceId> TruncatedSourceIds { get; }

    private static IReadOnlyList<AgentContextSourceId> NormalizeSourceIds(
        IEnumerable<AgentContextSourceId>? sourceIds)
    {
        if (sourceIds is null)
        {
            return Array.Empty<AgentContextSourceId>();
        }

        var normalized = sourceIds
            .Where(sourceId => sourceId != default)
            .Distinct()
            .OrderBy(sourceId => sourceId.Value, StringComparer.Ordinal)
            .ToArray();

        return Array.AsReadOnly(normalized);
    }

    private static IReadOnlyList<AgentContextHardExclusionId> NormalizeHardExclusionIds(
        IEnumerable<AgentContextHardExclusionId>? hardExclusionIds)
    {
        if (hardExclusionIds is null)
        {
            return Array.Empty<AgentContextHardExclusionId>();
        }

        var normalized = hardExclusionIds
            .Where(exclusionId => exclusionId != default)
            .Distinct()
            .OrderBy(exclusionId => exclusionId.Value, StringComparer.Ordinal)
            .ToArray();

        return Array.AsReadOnly(normalized);
    }
}
