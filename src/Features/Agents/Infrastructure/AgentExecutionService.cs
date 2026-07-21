using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Infrastructure;

/// <summary>
/// Built-in <c>HttpClient</c> implementation of <see cref="IAgentExecutionService"/>
/// using manual JSON against a single OpenAI-compatible <c>/chat/completions</c> endpoint.
/// Non-streaming only. No provider registry, no tool calling, no retries.
///
/// <para>Effective LLM configuration is resolved <b>live</b> on every
/// <see cref="ExecuteAsync"/> call from <see cref="ISettingsService"/>,
/// <see cref="ISecretStore"/>, and environment variables. Precedence:
/// environment variable → secret store → saved settings → empty.</para>
/// </summary>
internal sealed class AgentExecutionService : IAgentExecutionService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settings;
    private readonly ISecretStore _secretStore;

    public AgentExecutionService(HttpClient httpClient, ISettingsService settings, ISecretStore secretStore)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    }

    public async Task<AgentExecutionResult> ExecuteAsync(string userMessage, CancellationToken ct = default)
    {
        // Outer guard: never throw into the coordinator. Unexpected NREs were
        // previously surfaced as opaque "Object reference not set..." chat errors.
        try
        {
            return await ExecuteCoreAsync(userMessage, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return AgentExecutionResult.Failure(
                "Request was cancelled.",
                AgentFailureKind.Cancellation);
        }
        catch (OperationCanceledException)
        {
            return AgentExecutionResult.Failure(
                "Request was cancelled.",
                AgentFailureKind.Timeout);
        }
        catch (Exception)
        {
            return AgentExecutionResult.Failure(
                "Unexpected error during execution.",
                AgentFailureKind.Indeterminate);
        }
    }

    private async Task<AgentExecutionResult> ExecuteCoreAsync(
        string userMessage,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return AgentExecutionResult.Failure(
                "User message must not be empty.",
                AgentFailureKind.Execution);

        var options = ResolveEffectiveOptions();
        if (options is null)
        {
            return AgentExecutionResult.Failure(
                "Failed to resolve LLM configuration.",
                AgentFailureKind.Indeterminate);
        }

        // --- Validate configuration ---
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            return Failure(
                options,
                "API key is missing. Set AGENT_API_KEY.",
                AgentFailureKind.Execution);

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
            return Failure(
                options,
                "Base URL is missing. Set AGENT_API_URL.",
                AgentFailureKind.Execution);

        if (string.IsNullOrWhiteSpace(options.Model))
            return Failure(
                options,
                "Model is missing. Set AGENT_MODEL.",
                AgentFailureKind.Execution);

        // --- Build request body ---
        var requestBody = new
        {
            model = options.Model,
            // Cline's OpenAI-compatible endpoint defaults to SSE streaming.
            // This service intentionally supports the non-streaming response
            // contract only, so make the mode explicit.
            stream = false,
            messages = new[]
            {
                new { role = "user", content = userMessage }
            }
        };

        string jsonBody;
        try
        {
            jsonBody = JsonSerializer.Serialize(requestBody);
        }
        catch (Exception ex)
        {
            return Failure(
                options,
                $"Failed to serialize request: {ex.Message}",
                AgentFailureKind.Indeterminate);
        }

        // --- Build HTTP request ---
        var baseUrl = options.BaseUrl.TrimEnd('/');
        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions")
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", $"Bearer {options.ApiKey}");

        // --- Send request ---
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return Failure(
                options,
                $"Request failed: {ex.Message}",
                AgentFailureKind.Transport);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return Failure(
                options,
                "Request was cancelled.",
                AgentFailureKind.Cancellation);
        }
        catch (TaskCanceledException)
        {
            return Failure(
                options,
                "Request was cancelled.",
                AgentFailureKind.Timeout);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Failure(
                options,
                "Request was cancelled.",
                AgentFailureKind.Cancellation);
        }
        catch (OperationCanceledException)
        {
            return Failure(
                options,
                "Request was cancelled.",
                AgentFailureKind.Timeout);
        }

        // --- Read response body ---
        if (response.Content is null)
        {
            return Failure(
                options,
                "HTTP response had no content.",
                AgentFailureKind.Indeterminate);
        }

        string responseBody;
        try
        {
            responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
        {
            return Failure(
                options,
                $"Failed to read response: {ex.Message}",
                AgentFailureKind.Transport);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return Failure(
                options,
                "Request was cancelled.",
                AgentFailureKind.Cancellation);
        }
        catch (TaskCanceledException)
        {
            return Failure(
                options,
                "Request was cancelled.",
                AgentFailureKind.Timeout);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Failure(
                options,
                "Request was cancelled.",
                AgentFailureKind.Cancellation);
        }
        catch (OperationCanceledException)
        {
            return Failure(
                options,
                "Request was cancelled.",
                AgentFailureKind.Timeout);
        }

        // --- Check HTTP status ---
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            var shape = DescribeResponseShape(responseBody);
            return Failure(
                options,
                $"HTTP {statusCode} at {request.RequestUri?.AbsolutePath ?? "<unknown>"} " +
                $"for model '{options.Model}'. Response shape: {shape}",
                AgentFailureKind.Execution);
        }

        // --- Parse response JSON ---
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(responseBody);
        }
        catch (JsonException ex)
        {
            return Failure(
                options,
                $"Invalid JSON response: {ex.Message}",
                AgentFailureKind.Indeterminate);
        }

        using (doc)
        {
            // --- Extract assistant content ---
            try
            {
                // Cline Pass may wrap the OpenAI-compatible response in a
                // `{ "data": { ... }, "success": true }` envelope. Preserve the
                // standard top-level response path and unwrap only this observed
                // object envelope.
                var responseRoot = doc.RootElement;
                if (!responseRoot.TryGetProperty("choices", out _) &&
                    responseRoot.TryGetProperty("data", out var data) &&
                    data.ValueKind == JsonValueKind.Object)
                {
                    responseRoot = data;
                }

                if (!responseRoot.TryGetProperty("choices", out var choices) ||
                    choices.ValueKind != JsonValueKind.Array)
                {
                    return Failure(
                        options,
                        $"Unexpected response structure at {request.RequestUri?.AbsolutePath ?? "<unknown>"} " +
                        $"for model '{options.Model}': missing choices array. " +
                        $"Response shape: {DescribeResponseShape(doc)}",
                        AgentFailureKind.Indeterminate);
                }

                if (choices.GetArrayLength() == 0)
                    return Failure(
                        options,
                        "Response contains no choices.",
                        AgentFailureKind.Indeterminate);

                var firstChoice = choices[0];
                if (!firstChoice.TryGetProperty("message", out var message) ||
                    message.ValueKind != JsonValueKind.Object)
                {
                    return Failure(
                        options,
                        $"Unexpected response structure at {request.RequestUri?.AbsolutePath ?? "<unknown>"} " +
                        $"for model '{options.Model}': missing message object. " +
                        $"Response shape: {DescribeResponseShape(doc)}",
                        AgentFailureKind.Indeterminate);
                }

                if (!TryExtractAssistantContent(message, out var content) ||
                    string.IsNullOrWhiteSpace(content))
                {
                    return Failure(
                        options,
                        "Response contains no assistant content.",
                        AgentFailureKind.Indeterminate);
                }

                return AgentExecutionResult.Success(content);
            }
            catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or NotSupportedException)
            {
                return Failure(
                    options,
                    $"Unexpected response structure at {request.RequestUri?.AbsolutePath ?? "<unknown>"} " +
                    $"for model '{options.Model}': {ex.Message} Response shape: {DescribeResponseShape(doc)}",
                    AgentFailureKind.Indeterminate);
            }
        }
    }

    /// <summary>
    /// Extracts assistant text from OpenAI-compatible message.content (string or
    /// multi-part text array).
    /// </summary>
    private static bool TryExtractAssistantContent(JsonElement message, out string content)
    {
        content = string.Empty;
        if (!message.TryGetProperty("content", out var contentElement))
        {
            return false;
        }

        if (contentElement.ValueKind == JsonValueKind.String)
        {
            content = contentElement.GetString() ?? string.Empty;
            return true;
        }

        if (contentElement.ValueKind == JsonValueKind.Array)
        {
            var builder = new StringBuilder();
            foreach (var part in contentElement.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.Object &&
                    part.TryGetProperty("text", out var textElement) &&
                    textElement.ValueKind == JsonValueKind.String)
                {
                    builder.Append(textElement.GetString());
                }
                else if (part.ValueKind == JsonValueKind.String)
                {
                    builder.Append(part.GetString());
                }
            }

            content = builder.ToString();
            return true;
        }

        return false;
    }

    private static string DescribeResponseShape(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            return DescribeResponseShape(doc);
        }
        catch (JsonException)
        {
            return "invalid JSON";
        }
    }

    private static string DescribeResponseShape(JsonDocument doc)
    {
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return $"root={root.ValueKind}";

        var properties = root.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var description = properties.Length == 0
            ? "object with no fields"
            : $"top-level fields: {string.Join(", ", properties)}";

        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            var dataFields = data.EnumerateObject()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal);
            description += $"; data fields: {string.Join(", ", dataFields)}";
        }

        return description;
    }

    private AgentExecutionOptions? ResolveEffectiveOptions()
    {
        try
        {
            return BuildEffectiveOptions();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Builds the effective per-call options.
    /// Precedence: env var → secret store → saved settings → empty/default.
    /// </summary>
    internal AgentExecutionOptions BuildEffectiveOptions()
    {
        var llm = _settings.Current.Llm;

        // BaseUrl: env var overrides settings
        var baseUrl = Environment.GetEnvironmentVariable("AGENT_API_URL");
        if (string.IsNullOrEmpty(baseUrl))
            baseUrl = llm.BaseUrl;

        // Model: env var overrides settings
        var model = Environment.GetEnvironmentVariable("AGENT_MODEL");
        if (string.IsNullOrEmpty(model))
            model = llm.Model;

        // ApiKey: env var → secret store → empty
        var apiKey = Environment.GetEnvironmentVariable("AGENT_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            apiKey = _secretStore.Get("llm.apiKey");
        }
        if (apiKey is null)
            apiKey = string.Empty;

        return new AgentExecutionOptions
        {
            BaseUrl = baseUrl,
            ApiKey = apiKey,
            Model = model,
        };
    }

    private static AgentExecutionResult Failure(
        AgentExecutionOptions options,
        string message,
        AgentFailureKind kind) =>
        AgentExecutionResult.Failure(RedactSecrets(message, options.ApiKey), kind);

    private static string RedactSecrets(string value, string apiKey)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(apiKey))
        {
            return value;
        }

        return value.Replace(apiKey, "[REDACTED]", StringComparison.Ordinal);
    }
}
