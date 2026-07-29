using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Application.Transparency;

/// <summary>
/// Application façade over the durable record store with request validation.
/// </summary>
internal sealed class AgentDurableRecordCoordinator
{
    private readonly IAgentDurableRecordStore _store;

    public AgentDurableRecordCoordinator(IAgentDurableRecordStore store)
    {
        _store = store;
    }

    public AgentDurableRecordLoadOutcome EnsureWorkspaceLoaded(
        AgentDurableWorkspaceStorageKey workspaceKey) =>
        _store.LoadWorkspace(workspaceKey);

    public AgentDurableRecordAppendResult Append(AgentDurableRecordAppendRequest request) =>
        _store.TryAppend(request);

    public AgentDurableRecordReplayResult Replay(AgentDurableRecordReplayRequest request) =>
        _store.Replay(request);

    public void Flush() => _store.Flush();
}
