using System.Collections.Generic;
using Zaide.Features.Agents.Domain.Transparency.Trace;

namespace Zaide.Features.Agents.Contracts.Transparency.Trace;

/// <summary>
/// Read-side inspection surface for the durable redacted trace evidence. Never
/// returns unredacted payloads. The M1 durable record store remains the
/// authoritative source.
/// </summary>
internal interface IAgentTraceInspector
{
    /// <summary>
    /// Returns a non-PII inspection summary (counts by capture state, oldest
    /// and newest record times, total size, scope) for one workspace partition.
    /// </summary>
    AgentTraceInspectionSummary GetSummary(
        Zaide.Features.Agents.Domain.Transparency.AgentDurableWorkspaceStorageKey workspaceKey);

    /// <summary>
    /// Returns ordered trace records for one workspace partition after the
    /// supplied cursor, capped by <paramref name="maxRecords"/>. The cursor is
    /// the last ordering sequence observed by the caller (0 for the first page).
    /// </summary>
    IReadOnlyList<AgentTraceRecord> GetRecords(
        Zaide.Features.Agents.Domain.Transparency.AgentDurableWorkspaceStorageKey workspaceKey,
        long afterOrderingSequence,
        int maxRecords);
}
