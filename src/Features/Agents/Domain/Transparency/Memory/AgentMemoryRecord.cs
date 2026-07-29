using System;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Domain.Transparency.Memory;

/// <summary>
/// Projected durable memory record after replaying append-only revisions.
/// </summary>
internal sealed class AgentMemoryRecord
{
    public AgentMemoryRecord(
        AgentMemoryId memoryId,
        AgentDurableRecordId durableRecordId,
        long orderingSequence,
        AgentDurableWorkspaceStorageKey workspaceKey,
        AgentMemoryScopeTarget scopeTarget,
        string content,
        AgentMemoryProvenance provenance,
        AgentMemoryStatus status,
        int schemaVersion,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? lastValidatedAtUtc,
        AgentMemoryId? supersededByMemoryId,
        AgentMemoryId? supersedesMemoryId,
        AgentMemoryConflictKind conflictKind,
        bool isPoisoningSuspect,
        bool isStaleFact,
        DateTimeOffset recordedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content is required.", nameof(content));
        }

        MemoryId = memoryId;
        DurableRecordId = durableRecordId;
        OrderingSequence = orderingSequence;
        WorkspaceKey = workspaceKey;
        ScopeTarget = scopeTarget;
        Content = content;
        Provenance = provenance;
        Status = status;
        SchemaVersion = schemaVersion;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        LastValidatedAtUtc = lastValidatedAtUtc;
        SupersededByMemoryId = supersededByMemoryId;
        SupersedesMemoryId = supersedesMemoryId;
        ConflictKind = conflictKind;
        IsPoisoningSuspect = isPoisoningSuspect;
        IsStaleFact = isStaleFact;
        RecordedAtUtc = recordedAtUtc;
    }

    public AgentMemoryId MemoryId { get; }

    public AgentDurableRecordId DurableRecordId { get; }

    public long OrderingSequence { get; }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public AgentMemoryScopeTarget ScopeTarget { get; }

    public string Content { get; }

    public AgentMemoryProvenance Provenance { get; }

    public AgentMemoryStatus Status { get; }

    public int SchemaVersion { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    public DateTimeOffset? LastValidatedAtUtc { get; }

    public AgentMemoryId? SupersededByMemoryId { get; }

    public AgentMemoryId? SupersedesMemoryId { get; }

    public AgentMemoryConflictKind ConflictKind { get; }

    public bool IsPoisoningSuspect { get; }

    public bool IsStaleFact { get; }

    public DateTimeOffset RecordedAtUtc { get; }

    public bool IsRetrievable =>
        Status == AgentMemoryStatus.Active
        && !IsPoisoningSuspect
        && ConflictKind != AgentMemoryConflictKind.Superseded;
}
