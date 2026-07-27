using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Broker tool result summary bound to one model tool call within the in-run loop.
/// </summary>
internal sealed class NativeHarnessToolResultRecord : NativeHarnessLoopHistoryRecord
{
    public NativeHarnessToolResultRecord(
        int turnIndex,
        DateTimeOffset recordedAtUtc,
        NativeHarnessToolCallId toolCallId,
        AgentActionResultKind resultKind,
        string summary)
        : base(turnIndex, recordedAtUtc)
    {
        if (toolCallId == default)
        {
            throw new ArgumentException("Tool call id is required.", nameof(toolCallId));
        }

        if (!Enum.IsDefined(resultKind))
        {
            throw new ArgumentOutOfRangeException(nameof(resultKind), resultKind, "Result kind is invalid.");
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Tool result summary is required.", nameof(summary));
        }

        ToolCallId = toolCallId;
        ResultKind = resultKind;
        Summary = summary;
    }

    public NativeHarnessToolCallId ToolCallId { get; }

    public AgentActionResultKind ResultKind { get; }

    public string Summary { get; }
}
