using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Provenance metadata for one assembled context item.
/// </summary>
internal sealed class AgentContextProvenance
{
    public AgentContextProvenance(
        string sourceServiceIdentity,
        long snapshotGeneration,
        bool wasLiveSnapshot,
        bool redactionApplied,
        AgentContextRedactionReason? redactionReason = null)
    {
        if (string.IsNullOrWhiteSpace(sourceServiceIdentity))
        {
            throw new ArgumentException(
                "Source service identity is required.",
                nameof(sourceServiceIdentity));
        }

        if (snapshotGeneration < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(snapshotGeneration),
                snapshotGeneration,
                "Snapshot generation cannot be negative.");
        }

        if (redactionApplied && redactionReason is null)
        {
            throw new ArgumentException(
                "Redaction reason is required when redaction was applied.",
                nameof(redactionReason));
        }

        SourceServiceIdentity = sourceServiceIdentity;
        SnapshotGeneration = snapshotGeneration;
        WasLiveSnapshot = wasLiveSnapshot;
        RedactionApplied = redactionApplied;
        RedactionReason = redactionReason;
    }

    public string SourceServiceIdentity { get; }

    public long SnapshotGeneration { get; }

    public bool WasLiveSnapshot { get; }

    public bool RedactionApplied { get; }

    public AgentContextRedactionReason? RedactionReason { get; }
}
