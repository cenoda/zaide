using System.Collections.Generic;

namespace Zaide.Features.Agents.Domain.Transparency;

/// <summary>
/// Ordered replay result with the next cursor for incremental replay.
/// </summary>
internal readonly struct AgentDurableRecordReplayResult
{
    public AgentDurableRecordReplayResult(
        IReadOnlyList<AgentDurableRecordEnvelope> records,
        AgentDurableRecordReplayCursor nextCursor)
    {
        Records = records;
        NextCursor = nextCursor;
    }

    public IReadOnlyList<AgentDurableRecordEnvelope> Records { get; }

    public AgentDurableRecordReplayCursor NextCursor { get; }
}
