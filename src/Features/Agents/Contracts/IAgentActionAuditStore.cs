using System.Collections.Generic;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Current-lifetime bounded in-memory audit snapshots for action facts.
/// </summary>
internal interface IAgentActionAuditStore
{
    void Record(AgentActionAuditRecord record);

    IReadOnlyList<AgentActionAuditRecord> GetRunSnapshot(ExecutionRunId runId, int maxRecords);

    IReadOnlyList<AgentActionAuditRecord> GetCurrentLifetimeSnapshot(int maxRecords);
}
