using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Services;

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
public sealed class AgentExecutionService : IAgentExecutionService
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
        if (string.IsNullOrWhiteSpace(userMessage))
            return AgentExecutionResult.Failure("User message must not be empty.");

        var options = BuildEffectiveOptions();

        // --- Validate configuration ---
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            return AgentExecutionResult.Failure("API key is missing. Set AGENT_API_KEY.");

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
            return AgentExecutionResult.Failure("Base URL is missing. Set AGENT_API_URL.");

        if (string.IsNullOrWhiteSpace(options.Model))
            return AgentExecutionResult.Failure("Model is missing. Set AGENT_MODEL.");

        // --- Build request body ---
        var requestBody = new
        {
            model = options.Model,
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
            return AgentExecutionResult.Failure($"Failed to serialize request: {ex.Message}");
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
            return AgentExecutionResult.Failure($"Request failed: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return AgentExecutionResult.Failure("Request was cancelled.");
        }
        catch (OperationCanceledException)
        {
            return AgentExecutionResult.Failure("Request was cancelled.");
        }

        // --- Read response body ---
        string responseBody;
        try
        {
            responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return AgentExecutionResult.Failure($"Failed to read response: {ex.Message}");
        }

        // --- Check HTTP status ---
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            var snippet = responseBody.Length > 200 ? responseBody[..200] + "..." : responseBody;
            return AgentExecutionResult.Failure($"HTTP {statusCode}: {snippet}");
        }

        // --- Parse response JSON ---
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(responseBody);
        }
        catch (JsonException ex)
        {
            return AgentExecutionResult.Failure($"Invalid JSON response: {ex.Message}");
        }

        // --- Extract assistant content ---
        try
        {
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0)
                return AgentExecutionResult.Failure("Response contains no choices.");

            var firstChoice = choices[0];
            var message = firstChoice.GetProperty("message");
            var content = message.GetProperty("content").GetString();

            if (string.IsNullOrWhiteSpace(content))
                return AgentExecutionResult.Failure("Response contains no assistant content.");

            return AgentExecutionResult.Success(content);
        }
        catch (InvalidOperationException ex)
        {
            return AgentExecutionResult.Failure($"Unexpected response structure: {ex.Message}");
        }
        catch (KeyNotFoundException ex)
        {
            return AgentExecutionResult.Failure($"Unexpected response structure: {ex.Message}");
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
}
