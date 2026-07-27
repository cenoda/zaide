using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// One model-issued tool call from a provider completion response.
/// </summary>
internal sealed class NativeHarnessProviderToolCall
{
    public NativeHarnessProviderToolCall(
        NativeHarnessToolCallId toolCallId,
        string modelToolName,
        string argumentsJson)
    {
        if (toolCallId == default)
        {
            throw new ArgumentException("Tool call id is required.", nameof(toolCallId));
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
        ModelToolName = modelToolName.Trim();
        ArgumentsJson = argumentsJson;
    }

    public NativeHarnessToolCallId ToolCallId { get; }

    public string ModelToolName { get; }

    public string ArgumentsJson { get; }
}
