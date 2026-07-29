using System;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Domain.Transparency.Memory;

internal sealed class AgentMemoryOperationResult
{
    public AgentMemoryOperationResult(
        AgentMemoryOperationStatus status,
        AgentMemoryOperationKind operationKind,
        AgentMemoryId? memoryId = null,
        long orderingSequence = 0,
        string? reason = null,
        AgentMemoryConflictKind conflictKind = AgentMemoryConflictKind.None)
    {
        Status = status;
        OperationKind = operationKind;
        MemoryId = memoryId;
        OrderingSequence = orderingSequence;
        Reason = reason;
        ConflictKind = conflictKind;
    }

    public AgentMemoryOperationStatus Status { get; }

    public AgentMemoryOperationKind OperationKind { get; }

    public AgentMemoryId? MemoryId { get; }

    public long OrderingSequence { get; }

    public string? Reason { get; }

    public AgentMemoryConflictKind ConflictKind { get; }
}
