using System;
using System.Collections.Generic;
using Zaide.Features.Agents.Domain.Transparency.Memory;

namespace Zaide.Features.Agents.Presentation.Memory;

internal sealed class AgentMemoryAvailabilityState
{
    public AgentMemoryAvailabilityState(
        int totalRecords,
        int activeRecords,
        int disabledRecords,
        int supersededRecords,
        int deletedRecords,
        int poisoningSuspects,
        int staleFacts,
        DateTimeOffset? newestUpdatedAtUtc)
    {
        TotalRecords = totalRecords;
        ActiveRecords = activeRecords;
        DisabledRecords = disabledRecords;
        SupersededRecords = supersededRecords;
        DeletedRecords = deletedRecords;
        PoisoningSuspects = poisoningSuspects;
        StaleFacts = staleFacts;
        NewestUpdatedAtUtc = newestUpdatedAtUtc;
    }

    public int TotalRecords { get; }

    public int ActiveRecords { get; }

    public int DisabledRecords { get; }

    public int SupersededRecords { get; }

    public int DeletedRecords { get; }

    public int PoisoningSuspects { get; }

    public int StaleFacts { get; }

    public DateTimeOffset? NewestUpdatedAtUtc { get; }

    public static AgentMemoryAvailabilityState Initial { get; } = new(
        totalRecords: 0,
        activeRecords: 0,
        disabledRecords: 0,
        supersededRecords: 0,
        deletedRecords: 0,
        poisoningSuspects: 0,
        staleFacts: 0,
        newestUpdatedAtUtc: null);

    public string FormatStatusCaption()
    {
        if (TotalRecords == 0)
        {
            return "No durable memory records";
        }

        return $"{ActiveRecords} active / {TotalRecords} total";
    }
}
