using System.Collections.Generic;
using Zaide.Features.Agents.Domain.Transparency.Usage;

namespace Zaide.Features.Agents.Contracts.Transparency.Usage;

internal interface IAgentUsageInspector
{
    AgentUsageInspectionSummary GetSummary(
        Zaide.Features.Agents.Domain.Transparency.AgentDurableWorkspaceStorageKey workspaceKey);

    IReadOnlyList<AgentUsageRecord> GetRecords(
        Zaide.Features.Agents.Domain.Transparency.AgentDurableWorkspaceStorageKey workspaceKey,
        long afterOrderingSequence,
        int maxRecords);
}
