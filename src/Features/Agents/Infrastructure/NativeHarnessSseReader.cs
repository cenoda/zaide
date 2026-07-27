using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Infrastructure;

/// <summary>
/// Incremental SSE reader for OpenAI-compatible chat completion streams.
/// </summary>
internal static class NativeHarnessSseReader
{
    public static async Task<NativeHarnessProviderResponse> ReadCompletionAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var contentBuilder = new StringBuilder();
        var toolCalls = new Dictionary<int, ToolCallAccumulator>();

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (line.Length == 0)
            {
                continue;
            }

            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var payload = line["data:".Length..].Trim();
            if (payload.Length == 0 || payload == "[DONE]")
            {
                continue;
            }

            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                continue;
            }

            var choice = choices[0];
            if (!choice.TryGetProperty("delta", out var delta)
                || delta.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (delta.TryGetProperty("content", out var contentElement)
                && contentElement.ValueKind == JsonValueKind.String)
            {
                contentBuilder.Append(contentElement.GetString());
            }

            if (delta.TryGetProperty("tool_calls", out var toolCallsElement)
                && toolCallsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var toolCallElement in toolCallsElement.EnumerateArray())
                {
                    if (!toolCallElement.TryGetProperty("index", out var indexElement)
                        || indexElement.ValueKind != JsonValueKind.Number)
                    {
                        continue;
                    }

                    var index = indexElement.GetInt32();
                    if (!toolCalls.TryGetValue(index, out var accumulator))
                    {
                        accumulator = new ToolCallAccumulator();
                        toolCalls[index] = accumulator;
                    }

                    if (toolCallElement.TryGetProperty("id", out var idElement)
                        && idElement.ValueKind == JsonValueKind.String)
                    {
                        accumulator.Id = idElement.GetString();
                    }

                    if (toolCallElement.TryGetProperty("function", out var functionElement)
                        && functionElement.ValueKind == JsonValueKind.Object)
                    {
                        if (functionElement.TryGetProperty("name", out var nameElement)
                            && nameElement.ValueKind == JsonValueKind.String)
                        {
                            accumulator.Name = nameElement.GetString();
                        }

                        if (functionElement.TryGetProperty("arguments", out var argumentsElement)
                            && argumentsElement.ValueKind == JsonValueKind.String)
                        {
                            accumulator.ArgumentsBuilder.Append(argumentsElement.GetString());
                        }
                    }
                }
            }
        }

        var normalizedToolCalls = new List<NativeHarnessProviderToolCall>();
        foreach (var pair in toolCalls)
        {
            var accumulator = pair.Value;
            if (string.IsNullOrWhiteSpace(accumulator.Id)
                || string.IsNullOrWhiteSpace(accumulator.Name))
            {
                continue;
            }

            normalizedToolCalls.Add(new NativeHarnessProviderToolCall(
                NativeHarnessToolCallId.FromValue(accumulator.Id!),
                accumulator.Name!,
                accumulator.ArgumentsBuilder.ToString()));
        }

        var assistantContent = contentBuilder.Length == 0 ? null : contentBuilder.ToString();
        if (string.IsNullOrWhiteSpace(assistantContent) && normalizedToolCalls.Count == 0)
        {
            return NativeHarnessProviderResponse.Failure(
                "Provider stream completed without assistant content or tool calls.",
                AgentFailureKind.Indeterminate);
        }

        return NativeHarnessProviderResponse.Success(assistantContent, normalizedToolCalls);
    }

    private sealed class ToolCallAccumulator
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public StringBuilder ArgumentsBuilder { get; } = new();
    }
}
