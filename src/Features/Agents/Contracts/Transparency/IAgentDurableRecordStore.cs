using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Contracts.Transparency;

/// <summary>
/// Backend-neutral durable record store owned by the Agents feature.
/// </summary>
internal interface IAgentDurableRecordStore
{
    AgentDurableRecordLoadOutcome LoadWorkspace(AgentDurableWorkspaceStorageKey workspaceKey);

    AgentDurableRecordAppendResult TryAppend(AgentDurableRecordAppendRequest request);

    AgentDurableRecordReplayResult Replay(AgentDurableRecordReplayRequest request);

    void Flush();
}
