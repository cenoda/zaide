using System;
using System.Collections.Generic;
using System.Linq;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// One OpenAI-compatible chat message for provider transport.
/// </summary>
internal sealed class NativeHarnessChatMessage
{
    private NativeHarnessChatMessage(
        string role,
        string? content,
        IReadOnlyList<NativeHarnessProviderToolCall>? toolCalls,
        NativeHarnessToolCallId? toolCallId)
    {
        Role = role;
        Content = content;
        ToolCalls = toolCalls;
        ToolCallId = toolCallId;
    }

    public string Role { get; }

    public string? Content { get; }

    public IReadOnlyList<NativeHarnessProviderToolCall>? ToolCalls { get; }

    public NativeHarnessToolCallId? ToolCallId { get; }

    public static NativeHarnessChatMessage System(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("System message content is required.", nameof(content));
        }

        return new NativeHarnessChatMessage("system", content, null, null);
    }

    public static NativeHarnessChatMessage User(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("User message content is required.", nameof(content));
        }

        return new NativeHarnessChatMessage("user", content, null, null);
    }

    public static NativeHarnessChatMessage Assistant(
        string? content,
        IReadOnlyList<NativeHarnessProviderToolCall>? toolCalls = null)
    {
        if (string.IsNullOrWhiteSpace(content) && (toolCalls is null || toolCalls.Count == 0))
        {
            throw new ArgumentException(
                "Assistant messages require content or tool calls.",
                nameof(content));
        }

        return new NativeHarnessChatMessage("assistant", content, toolCalls, null);
    }

    public static NativeHarnessChatMessage Tool(NativeHarnessToolCallId toolCallId, string content)
    {
        if (toolCallId == default)
        {
            throw new ArgumentException("Tool call id is required.", nameof(toolCallId));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Tool message content is required.", nameof(content));
        }

        return new NativeHarnessChatMessage("tool", content, null, toolCallId);
    }

    public static IReadOnlyList<NativeHarnessChatMessage> FromLoopHistory(
        NativeHarnessLoopHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);

        var messages = new List<NativeHarnessChatMessage>();
        var records = history.Records;
        for (var index = 0; index < records.Count; index++)
        {
            switch (records[index])
            {
                case NativeHarnessSystemPromptRecord systemPrompt:
                    messages.Add(System(systemPrompt.Text));
                    break;
                case NativeHarnessUserTurnRecord userTurn:
                    messages.Add(User(userTurn.Text));
                    break;
                case NativeHarnessAssistantTurnRecord assistantTurn:
                    messages.Add(Assistant(assistantTurn.Text));
                    break;
                case NativeHarnessToolCallRecord firstToolCall:
                {
                    var toolCalls = new List<NativeHarnessProviderToolCall> { ToProviderToolCall(firstToolCall) };
                    while (index + 1 < records.Count
                           && records[index + 1] is NativeHarnessToolCallRecord nextCall)
                    {
                        index++;
                        toolCalls.Add(ToProviderToolCall((NativeHarnessToolCallRecord)records[index]));
                    }

                    messages.Add(Assistant(content: null, toolCalls));
                    foreach (var toolCall in toolCalls)
                    {
                        index++;
                        if (index >= records.Count
                            || records[index] is not NativeHarnessToolResultRecord toolResult
                            || toolResult.ToolCallId != toolCall.ToolCallId)
                        {
                            throw new InvalidOperationException(
                                "Tool result must immediately follow its tool call batch.");
                        }

                        messages.Add(Tool(toolResult.ToolCallId, toolResult.Summary));
                    }

                    break;
                }
            }
        }

        return messages;
    }

    private static NativeHarnessProviderToolCall ToProviderToolCall(NativeHarnessToolCallRecord record) =>
        new(record.ToolCallId, record.ModelToolName, record.ArgumentsJson);
}
