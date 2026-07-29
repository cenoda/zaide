namespace Zaide.Features.Agents.Domain.Transparency;

/// <summary>
/// Replay request for one workspace partition and record class.
/// </summary>
internal sealed class AgentDurableRecordReplayRequest
{
    public AgentDurableRecordReplayRequest(
        AgentDurableWorkspaceStorageKey workspaceKey,
        AgentDurableRecordClass recordClass,
        long afterOrderingSequence = 0,
        int maxRecords = int.MaxValue)
    {
        WorkspaceKey = workspaceKey;
        RecordClass = recordClass;
        AfterOrderingSequence = afterOrderingSequence;
        MaxRecords = maxRecords;
    }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public AgentDurableRecordClass RecordClass { get; }

    public long AfterOrderingSequence { get; }

    public int MaxRecords { get; }
}
