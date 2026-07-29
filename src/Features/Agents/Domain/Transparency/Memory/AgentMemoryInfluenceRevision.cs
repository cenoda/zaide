using System;

namespace Zaide.Features.Agents.Domain.Transparency.Memory;

/// <summary>
/// One memory revision that influenced a run.
/// </summary>
internal sealed class AgentMemoryInfluenceRevision
{
    public AgentMemoryInfluenceRevision(
        AgentMemoryId memoryId,
        long orderingSequence,
        int schemaVersion,
        bool isStaleFact)
    {
        MemoryId = memoryId;
        OrderingSequence = orderingSequence;
        SchemaVersion = schemaVersion;
        IsStaleFact = isStaleFact;
    }

    public AgentMemoryId MemoryId { get; }

    public long OrderingSequence { get; }

    public int SchemaVersion { get; }

    public bool IsStaleFact { get; }
}
