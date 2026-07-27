using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Validated model tool-call descriptor mapped to the Phase 17 action taxonomy.
/// Broker dispatch uses the resolved <see cref="AgentActionPayload"/> in M3.
/// </summary>
internal sealed class NativeHarnessToolCallDescriptor
{
    public NativeHarnessToolCallDescriptor(
        NativeHarnessToolCallId toolCallId,
        AgentActionKind actionKind,
        string modelToolName,
        string argumentsJson,
        string? correlationKey = null)
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
        ModelToolName = modelToolName.Trim();
        ArgumentsJson = argumentsJson;
        CorrelationKey = string.IsNullOrWhiteSpace(correlationKey) ? null : correlationKey.Trim();
    }

    public NativeHarnessToolCallId ToolCallId { get; }

    public AgentActionKind ActionKind { get; }

    public string ModelToolName { get; }

    public string ArgumentsJson { get; }

    public string? CorrelationKey { get; }

    public static bool IsSupportedActionKind(AgentActionKind actionKind) =>
        actionKind is AgentActionKind.ReadFile
            or AgentActionKind.CreateFile
            or AgentActionKind.ReplaceFile
            or AgentActionKind.DeleteFile
            or AgentActionKind.ExecuteCommand;
}
