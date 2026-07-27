using System;
using System.Collections.Generic;
using System.Linq;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Immutable append-only in-run model/tool loop history for one admitted run.
/// Not persisted and not projected to Townhall directly.
/// </summary>
internal sealed class NativeHarnessLoopHistory
{
    private readonly NativeHarnessLoopHistoryRecord[] _records;

    private NativeHarnessLoopHistory(NativeHarnessLoopHistoryRecord[] records)
    {
        _records = records;
        Records = Array.AsReadOnly(_records);
    }

    public IReadOnlyList<NativeHarnessLoopHistoryRecord> Records { get; }

    public int Count => _records.Length;

    public static NativeHarnessLoopHistory Empty { get; } = new(Array.Empty<NativeHarnessLoopHistoryRecord>());

    public NativeHarnessLoopHistory Append(NativeHarnessLoopHistoryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (_records.Length > 0)
        {
            var last = _records[^1];
            if (record.TurnIndex < last.TurnIndex)
            {
                throw new InvalidOperationException("Turn index must not decrease.");
            }

            if (record is NativeHarnessToolResultRecord toolResult)
            {
                var matchingCall = _records
                    .OfType<NativeHarnessToolCallRecord>()
                    .LastOrDefault(call => call.ToolCallId == toolResult.ToolCallId);

                if (matchingCall is null)
                {
                    throw new InvalidOperationException(
                        "Tool result must reference a preceding tool call in the same history.");
                }
            }
        }

        var next = new NativeHarnessLoopHistoryRecord[_records.Length + 1];
        _records.CopyTo(next, 0);
        next[^1] = record;
        return new NativeHarnessLoopHistory(next);
    }

    public bool TryGetLatestToolCall(
        NativeHarnessToolCallId toolCallId,
        out NativeHarnessToolCallRecord? toolCall)
    {
        toolCall = _records
            .OfType<NativeHarnessToolCallRecord>()
            .LastOrDefault(record => record.ToolCallId == toolCallId);

        return toolCall is not null;
    }
}
