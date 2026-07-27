using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Base type for one in-run model/tool loop history record. These records are
/// private to the harness, not normalized <see cref="AgentEvent"/>s or
/// conversation-store entries.
/// </summary>
internal abstract class NativeHarnessLoopHistoryRecord
{
    protected NativeHarnessLoopHistoryRecord(int turnIndex, DateTimeOffset recordedAtUtc)
    {
        if (turnIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(turnIndex), turnIndex, "Turn index cannot be negative.");
        }

        TurnIndex = turnIndex;
        RecordedAtUtc = recordedAtUtc;
    }

    public int TurnIndex { get; }

    public DateTimeOffset RecordedAtUtc { get; }
}
