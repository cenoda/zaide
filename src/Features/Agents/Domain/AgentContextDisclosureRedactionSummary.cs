using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Redaction metadata for one context disclosure event. Contains counts only.
/// </summary>
internal sealed class AgentContextDisclosureRedactionSummary
{
    public AgentContextDisclosureRedactionSummary(
        int itemsWithNoRedaction,
        int itemsWithPartialRedaction,
        int itemsWithFullRedaction,
        int itemsDroppedAfterProcessingFailure)
    {
        if (itemsWithNoRedaction < 0
            || itemsWithPartialRedaction < 0
            || itemsWithFullRedaction < 0
            || itemsDroppedAfterProcessingFailure < 0)
        {
            throw new ArgumentOutOfRangeException(
                "Redaction summary counts cannot be negative.");
        }

        ItemsWithNoRedaction = itemsWithNoRedaction;
        ItemsWithPartialRedaction = itemsWithPartialRedaction;
        ItemsWithFullRedaction = itemsWithFullRedaction;
        ItemsDroppedAfterProcessingFailure = itemsDroppedAfterProcessingFailure;
    }

    public int ItemsWithNoRedaction { get; }

    public int ItemsWithPartialRedaction { get; }

    public int ItemsWithFullRedaction { get; }

    public int ItemsDroppedAfterProcessingFailure { get; }
}
