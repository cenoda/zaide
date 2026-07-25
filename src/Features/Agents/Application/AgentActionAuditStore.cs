using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// In-memory audit store for the current application lifetime. Not durable.
/// </summary>
internal sealed class AgentActionAuditStore : IAgentActionAuditStore
{
    internal const int DefaultMaxSnapshotRecords = 256;

    private readonly LinkedList<AgentActionAuditRecord> _records = new();
    private readonly object _sync = new();

    public void Record(AgentActionAuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_sync)
        {
            _records.AddLast(record);
            if (_records.Count > DefaultMaxSnapshotRecords)
            {
                _records.RemoveFirst();
            }
        }
    }

    public IReadOnlyList<AgentActionAuditRecord> GetRunSnapshot(ExecutionRunId runId, int maxRecords)
    {
        if (runId == default)
        {
            throw new ArgumentException("Run id is required.", nameof(runId));
        }

        if (maxRecords < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRecords), maxRecords, "Max records must be positive.");
        }

        lock (_sync)
        {
            return _records
                .Where(record => record.RunId == runId)
                .OrderBy(record => record.Sequence)
                .Take(maxRecords)
                .ToArray();
        }
    }

    public IReadOnlyList<AgentActionAuditRecord> GetCurrentLifetimeSnapshot(int maxRecords)
    {
        if (maxRecords < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRecords), maxRecords, "Max records must be positive.");
        }

        lock (_sync)
        {
            return _records
                .OrderBy(record => record.Sequence)
                .TakeLast(maxRecords)
                .ToArray();
        }
    }
}
