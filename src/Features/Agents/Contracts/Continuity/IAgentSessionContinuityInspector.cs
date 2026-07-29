using System.Collections.Generic;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Contracts.Continuity;

internal interface IAgentSessionContinuityInspector
{
    AgentSessionContinuityReconcileSummary GetInterruptedSessions(
        AgentDurableWorkspaceStorageKey workspaceKey,
        string workspaceRoot,
        int maxRecords = AgentSessionContinuityLimits.DefaultMaxInterruptedSessionsPerPage);
}
