using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Records why one source or category was excluded from automatic context.
/// </summary>
internal sealed class AgentContextExclusionDecision
{
    public AgentContextExclusionDecision(
        AgentContextSourceId? sourceId,
        AgentContextHardExclusionId? hardExclusionId,
        string reason,
        bool isHardExclusion)
    {
        if (sourceId == default && hardExclusionId == default)
        {
            throw new ArgumentException(
                "Either a context source id or a hard exclusion id is required.");
        }

        if (sourceId != default && hardExclusionId != default)
        {
            throw new ArgumentException(
                "Context source id and hard exclusion id are mutually exclusive.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Exclusion reason is required.", nameof(reason));
        }

        if (isHardExclusion)
        {
            if (hardExclusionId == default)
            {
                throw new ArgumentException(
                    "Hard exclusion id is required when isHardExclusion is true.",
                    nameof(hardExclusionId));
            }

            if (sourceId != default)
            {
                throw new ArgumentException(
                    "Hard exclusion decisions cannot reference a context source id.",
                    nameof(sourceId));
            }
        }
        else if (hardExclusionId != default)
        {
            throw new ArgumentException(
                "Hard exclusion id cannot be set when isHardExclusion is false.",
                nameof(hardExclusionId));
        }

        SourceId = sourceId == default ? null : sourceId;
        HardExclusionId = hardExclusionId == default ? null : hardExclusionId;
        Reason = reason;
        IsHardExclusion = isHardExclusion;
    }

    public AgentContextSourceId? SourceId { get; }

    public AgentContextHardExclusionId? HardExclusionId { get; }

    public string Reason { get; }

    public bool IsHardExclusion { get; }
}
