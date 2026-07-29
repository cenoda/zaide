using System.Collections.Generic;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;

namespace Zaide.Features.Agents.Contracts.Transparency.Memory;

internal interface IAgentMemoryInspector
{
    AgentMemoryInspectionSummary GetSummary(AgentDurableWorkspaceStorageKey workspaceKey);

    IReadOnlyList<AgentMemoryRecord> GetRecords(
        AgentDurableWorkspaceStorageKey workspaceKey,
        long afterOrderingSequence,
        int maxRecords,
        bool includeDeleted = false);

    AgentMemoryRecord? TryGetRecord(
        AgentDurableWorkspaceStorageKey workspaceKey,
        AgentMemoryId memoryId);
}
