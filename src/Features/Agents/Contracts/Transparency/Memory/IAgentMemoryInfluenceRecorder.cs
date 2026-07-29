using System.Collections.Generic;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;

namespace Zaide.Features.Agents.Contracts.Transparency.Memory;

internal interface IAgentMemoryInfluenceRecorder
{
    void RecordInfluence(
        AgentDurableWorkspaceStorageKey workspaceKey,
        ExecutionRunId runId,
        AgentSessionId sessionId,
        AgentMemoryInfluenceState state,
        IReadOnlyList<AgentMemoryInfluenceRevision> revisions,
        string? unavailableReason = null);
}
