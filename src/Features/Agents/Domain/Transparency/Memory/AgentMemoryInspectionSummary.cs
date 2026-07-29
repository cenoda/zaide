using System;
using System.Collections.Generic;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Domain.Transparency.Memory;

internal sealed class AgentMemoryInspectionSummary
{
    public AgentMemoryInspectionSummary(
        AgentDurableWorkspaceStorageKey workspaceKey,
        int totalRecords,
        int activeRecords,
        int disabledRecords,
        int supersededRecords,
        int deletedRecords,
        int poisoningSuspects,
        int staleFacts,
        int conflictRecords,
        DateTimeOffset? oldestCreatedAtUtc,
        DateTimeOffset? newestUpdatedAtUtc,
        bool isEmpty)
    {
        WorkspaceKey = workspaceKey;
        TotalRecords = totalRecords;
        ActiveRecords = activeRecords;
        DisabledRecords = disabledRecords;
        SupersededRecords = supersededRecords;
        DeletedRecords = deletedRecords;
        PoisoningSuspects = poisoningSuspects;
        StaleFacts = staleFacts;
        ConflictRecords = conflictRecords;
        OldestCreatedAtUtc = oldestCreatedAtUtc;
        NewestUpdatedAtUtc = newestUpdatedAtUtc;
        IsEmpty = isEmpty;
    }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public int TotalRecords { get; }

    public int ActiveRecords { get; }

    public int DisabledRecords { get; }

    public int SupersededRecords { get; }

    public int DeletedRecords { get; }

    public int PoisoningSuspects { get; }

    public int StaleFacts { get; }

    public int ConflictRecords { get; }

    public DateTimeOffset? OldestCreatedAtUtc { get; }

    public DateTimeOffset? NewestUpdatedAtUtc { get; }

    public bool IsEmpty { get; }

    public static AgentMemoryInspectionSummary Empty(AgentDurableWorkspaceStorageKey workspaceKey) =>
        new(
            workspaceKey,
            totalRecords: 0,
            activeRecords: 0,
            disabledRecords: 0,
            supersededRecords: 0,
            deletedRecords: 0,
            poisoningSuspects: 0,
            staleFacts: 0,
            conflictRecords: 0,
            oldestCreatedAtUtc: null,
            newestUpdatedAtUtc: null,
            isEmpty: true);
}
