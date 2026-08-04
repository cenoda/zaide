using System;
using System.Collections.Generic;
using System.Linq;
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
/// OpenAI-compatible SSE provider transport using the shared <see cref="HttpClient"/>.
/// </summary>
internal sealed class NativeHarnessProviderClient : INativeHarnessProviderTransport
{
    private readonly HttpClient _httpClient;

    public NativeHarnessProviderClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<NativeHarnessProviderResponse> CompleteChatAsync(
        AgentExecutionOptions options,
        NativeHarnessProviderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request);

        var requestBody = new
        {
            model = options.Model,
            stream = true,
            messages = request.Messages.Select(ToProviderMessage).ToArray(),
            tools = CreateToolDefinitions(),
        };

        string jsonBody;
        try
        {
            jsonBody = JsonSerializer.Serialize(requestBody);
        }
        catch (Exception ex)
        {
            return NativeHarnessProviderResponse.Failure(
                $"Failed to serialize provider request: {ex.GetType().Name}",
                AgentFailureKind.Indeterminate);
        }

        var baseUrl = options.BaseUrl.TrimEnd('/');
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl}{NativeHarnessProviderProtocol.ChatCompletionsPath}")
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
        };
        httpRequest.Headers.Add("Authorization", $"Bearer {options.ApiKey}");

        AgentPathEvidenceInvocationCounters.RecordNativeHarnessProviderRequest();

        HttpResponseMessage response;
        try
        {
            response = await _httpClient
                .SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return NativeHarnessProviderResponse.Failure(
                "Provider request was cancelled.",
                AgentFailureKind.Cancellation);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return NativeHarnessProviderResponse.Failure(
                "Provider request was cancelled.",
                AgentFailureKind.Cancellation);
        }
        catch (TaskCanceledException)
        {
            return NativeHarnessProviderResponse.Failure(
                "Provider request timed out.",
                AgentFailureKind.Timeout);
        }
        catch (HttpRequestException ex)
        {
            return NativeHarnessProviderResponse.Failure(
                SanitizeFailureMessage($"Provider transport failed: {ex.GetType().Name}", options.ApiKey),
                AgentFailureKind.Transport);
        }

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            var body = response.Content is null
                ? string.Empty
                : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return NativeHarnessProviderResponse.Failure(
                SanitizeFailureMessage(
                    $"Provider returned HTTP {statusCode}.",
                    options.ApiKey),
                statusCode == 408 ? AgentFailureKind.Timeout : AgentFailureKind.Execution);
        }

        if (response.Content is null)
        {
            return NativeHarnessProviderResponse.Failure(
                "Provider response had no content.",
                AgentFailureKind.Indeterminate);
        }

        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return await NativeHarnessSseReader.ReadCompletionAsync(stream, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return NativeHarnessProviderResponse.Failure(
                "Provider stream was cancelled.",
                AgentFailureKind.Cancellation);
        }
        catch (JsonException)
        {
            return NativeHarnessProviderResponse.Failure(
                "Provider stream contained invalid JSON.",
                AgentFailureKind.Indeterminate);
        }
    }

    private static object ToProviderMessage(NativeHarnessChatMessage message)
    {
        if (message.Role == "tool")
        {
            return new
            {
                role = "tool",
                tool_call_id = message.ToolCallId!.Value.Value,
                content = message.Content,
            };
        }

        if (message.ToolCalls is { Count: > 0 })
        {
            return new
            {
                role = "assistant",
                content = message.Content,
                tool_calls = message.ToolCalls.Select(toolCall => new
                {
                    id = toolCall.ToolCallId.Value,
                    type = "function",
                    function = new
                    {
                        name = toolCall.ModelToolName,
                        arguments = toolCall.ArgumentsJson,
                    },
                }).ToArray(),
            };
        }

        return new
        {
            role = message.Role,
            content = message.Content,
        };
    }

    private static object[] CreateToolDefinitions() =>
        new object[]
        {
            CreateTool(
                NativeHarnessProviderProtocol.ReadFileToolName,
                "Read one workspace file.",
                new { type = "object", properties = new { path = new { type = "string" } }, required = new[] { "path" } }),
            CreateTool(
                NativeHarnessProviderProtocol.CreateFileToolName,
                "Create one workspace file.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string" },
                        content = new { type = "string" },
                    },
                    required = new[] { "path", "content" },
                }),
            CreateTool(
                NativeHarnessProviderProtocol.ReplaceFileToolName,
                "Replace one workspace file.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string" },
                        base_revision = new { type = "string" },
                        content = new { type = "string" },
                    },
                    required = new[] { "path", "base_revision", "content" },
                }),
            CreateTool(
                NativeHarnessProviderProtocol.DeleteFileToolName,
                "Delete one workspace file.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string" },
                        base_revision = new { type = "string" },
                    },
                    required = new[] { "path", "base_revision" },
                }),
            CreateTool(
                NativeHarnessProviderProtocol.ExecuteCommandToolName,
                "Execute one approved command in the workspace.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        executable = new { type = "string" },
                        arguments = new { type = "array", items = new { type = "string" } },
                        working_directory = new { type = "string" },
                    },
                    required = new[] { "executable", "arguments", "working_directory" },
                }),
        };

    private static object CreateTool(string name, string description, object parameters) =>
        new
        {
            type = "function",
            function = new
            {
                name,
                description,
                parameters,
            },
        };

    private static string SanitizeFailureMessage(string message, string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return message;
        }

        return message.Replace(apiKey, "[REDACTED]", StringComparison.Ordinal);
    }
}
