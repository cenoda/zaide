using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Contracts.Transparency.Memory;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;

namespace Zaide.Features.Agents.Application.Memory;

internal sealed class AgentMemoryInspector : IAgentMemoryInspector
{
    private readonly IAgentDurableRecordStore _store;

    public AgentMemoryInspector(IAgentDurableRecordStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public AgentMemoryInspectionSummary GetSummary(AgentDurableWorkspaceStorageKey workspaceKey)
    {
        var records = ReplayAll(workspaceKey, includeDeleted: true);
        if (records.Count == 0)
        {
            return AgentMemoryInspectionSummary.Empty(workspaceKey);
        }

        return new AgentMemoryInspectionSummary(
            workspaceKey,
            totalRecords: records.Count,
            activeRecords: records.Count(r => r.Status == AgentMemoryStatus.Active),
            disabledRecords: records.Count(r => r.Status == AgentMemoryStatus.Disabled),
            supersededRecords: records.Count(r => r.Status == AgentMemoryStatus.Superseded),
            deletedRecords: records.Count(r => r.Status == AgentMemoryStatus.Deleted),
            poisoningSuspects: records.Count(r => r.IsPoisoningSuspect),
            staleFacts: records.Count(r => r.IsStaleFact),
            conflictRecords: records.Count(r => r.ConflictKind != AgentMemoryConflictKind.None),
            oldestCreatedAtUtc: records.Min(r => r.CreatedAtUtc),
            newestUpdatedAtUtc: records.Max(r => r.UpdatedAtUtc),
            isEmpty: false);
    }

    public IReadOnlyList<AgentMemoryRecord> GetRecords(
        AgentDurableWorkspaceStorageKey workspaceKey,
        long afterOrderingSequence,
        int maxRecords,
        bool includeDeleted = false)
    {
        if (maxRecords <= 0)
        {
            return Array.Empty<AgentMemoryRecord>();
        }

        var all = ReplayAll(workspaceKey, includeDeleted);
        return all
            .Where(r => r.OrderingSequence > afterOrderingSequence)
            .OrderBy(r => r.OrderingSequence)
            .Take(maxRecords)
            .ToArray();
    }

    public AgentMemoryRecord? TryGetRecord(
        AgentDurableWorkspaceStorageKey workspaceKey,
        AgentMemoryId memoryId)
    {
        var projected = ProjectWorkspace(workspaceKey);
        return projected.TryGetValue(memoryId, out var record) ? record : null;
    }

    internal IReadOnlyList<AgentMemoryRecord> ReplayAll(
        AgentDurableWorkspaceStorageKey workspaceKey,
        bool includeDeleted)
    {
        var projected = ProjectWorkspace(workspaceKey);
        return projected.Values
            .Where(r => includeDeleted || r.Status != AgentMemoryStatus.Deleted)
            .OrderBy(r => r.OrderingSequence)
            .ToArray();
    }

    private IReadOnlyDictionary<AgentMemoryId, AgentMemoryRecord> ProjectWorkspace(
        AgentDurableWorkspaceStorageKey workspaceKey)
    {
        const int pageSize = AgentMemoryLimits.MaxRecordsPerPage;
        long cursor = 0;
        var envelopes = new List<AgentDurableRecordEnvelope>();

        while (true)
        {
            var replay = _store.Replay(new AgentDurableRecordReplayRequest(
                workspaceKey,
                AgentDurableRecordClass.Memory,
                cursor,
                pageSize));

            if (replay.Records.Count == 0)
            {
                break;
            }

            envelopes.AddRange(replay.Records);
            cursor = replay.Records[^1].OrderingSequence;
            if (replay.Records.Count < pageSize)
            {
                break;
            }
        }

        return AgentMemoryProjectionEngine.ProjectLatest(envelopes);
    }
}
