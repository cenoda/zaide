using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Records one deterministic truncation outcome for manifest assembly.
/// </summary>
internal sealed class AgentContextTruncationDecision
{
    public AgentContextTruncationDecision(
        AgentContextSourceId sourceId,
        string reason,
        bool itemDropped,
        bool itemTruncated)
    {
        if (sourceId == default)
        {
            throw new ArgumentException("Context source id is required.", nameof(sourceId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Truncation reason is required.", nameof(reason));
        }

        if (itemDropped == itemTruncated)
        {
            throw new ArgumentException(
                "Exactly one of itemDropped or itemTruncated must be true.",
                nameof(itemDropped));
        }

        SourceId = sourceId;
        Reason = reason;
        ItemDropped = itemDropped;
        ItemTruncated = itemTruncated;
    }

    public AgentContextSourceId SourceId { get; }

    public string Reason { get; }

    public bool ItemDropped { get; }

    public bool ItemTruncated { get; }
}
