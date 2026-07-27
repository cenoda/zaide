using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// One model-issued tool call request before broker dispatch.
/// </summary>
internal sealed class NativeHarnessToolCallRecord : NativeHarnessLoopHistoryRecord
{
    public NativeHarnessToolCallRecord(
        int turnIndex,
        DateTimeOffset recordedAtUtc,
        NativeHarnessToolCallId toolCallId,
        AgentActionKind actionKind,
        string modelToolName,
        string argumentsJson)
        : base(turnIndex, recordedAtUtc)
    {
        if (toolCallId == default)
        {
            throw new ArgumentException("Tool call id is required.", nameof(toolCallId));
        }

        if (!Enum.IsDefined(actionKind))
        {
            throw new ArgumentOutOfRangeException(nameof(actionKind), actionKind, "Action kind is invalid.");
        }

        if (string.IsNullOrWhiteSpace(modelToolName))
        {
            throw new ArgumentException("Model tool name is required.", nameof(modelToolName));
        }

        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            throw new ArgumentException("Arguments JSON is required.", nameof(argumentsJson));
        }

        ToolCallId = toolCallId;
        ActionKind = actionKind;
        ModelToolName = modelToolName;
        ArgumentsJson = argumentsJson;
    }

    public NativeHarnessToolCallId ToolCallId { get; }

    public AgentActionKind ActionKind { get; }

    public string ModelToolName { get; }

    public string ArgumentsJson { get; }
}
