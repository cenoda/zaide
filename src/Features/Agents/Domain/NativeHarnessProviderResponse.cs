using System;
using System.Collections.Generic;
using System.Linq;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Aggregated provider completion for one model round.
/// </summary>
internal sealed class NativeHarnessProviderResponse
{
    private NativeHarnessProviderResponse(
        string? assistantContent,
        IReadOnlyList<NativeHarnessProviderToolCall> toolCalls,
        string? finishReason,
        bool isFailure,
        string? failureReason,
        AgentFailureKind? failureKind)
    {
        AssistantContent = assistantContent;
        ToolCalls = toolCalls;
        FinishReason = finishReason;
        IsFailure = isFailure;
        FailureReason = failureReason;
        FailureKind = failureKind;
    }

    public string? AssistantContent { get; }

    public IReadOnlyList<NativeHarnessProviderToolCall> ToolCalls { get; }

    public string? FinishReason { get; }

    public bool IsFailure { get; }

    public string? FailureReason { get; }

    public AgentFailureKind? FailureKind { get; }

    public bool HasToolCalls => ToolCalls.Count > 0;

    public static NativeHarnessProviderResponse Success(
        string? assistantContent,
        IReadOnlyList<NativeHarnessProviderToolCall>? toolCalls = null,
        string? finishReason = null)
    {
        var normalizedToolCalls = toolCalls?.ToArray() ?? Array.Empty<NativeHarnessProviderToolCall>();
        if (string.IsNullOrWhiteSpace(assistantContent) && normalizedToolCalls.Length == 0)
        {
            throw new ArgumentException(
                "Successful provider responses require assistant content or tool calls.");
        }

        return new NativeHarnessProviderResponse(
            assistantContent,
            normalizedToolCalls,
            finishReason,
            isFailure: false,
            failureReason: null,
            failureKind: null);
    }

    public static NativeHarnessProviderResponse Failure(
        string failureReason,
        AgentFailureKind failureKind) =>
        new(
            assistantContent: null,
            toolCalls: Array.Empty<NativeHarnessProviderToolCall>(),
            finishReason: null,
            isFailure: true,
            failureReason: failureReason,
            failureKind: failureKind);
}
