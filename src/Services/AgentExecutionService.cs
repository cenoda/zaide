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
/// </summary>
public sealed class AgentExecutionService : IAgentExecutionService
{
    private readonly HttpClient _httpClient;
    private readonly AgentExecutionOptions _options;

    public AgentExecutionService(HttpClient httpClient, AgentExecutionOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<AgentExecutionResult> ExecuteAsync(string userMessage, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return AgentExecutionResult.Failure("User message must not be empty.");

        // --- Validate configuration ---
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return AgentExecutionResult.Failure("API key is missing. Set AGENT_API_KEY.");

        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            return AgentExecutionResult.Failure("Base URL is missing. Set AGENT_API_URL.");

        if (string.IsNullOrWhiteSpace(_options.Model))
            return AgentExecutionResult.Failure("Model is missing. Set AGENT_MODEL.");

        // --- Build request body ---
        var requestBody = new
        {
            model = _options.Model,
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
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions")
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");

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
            // Truncate body to avoid leaking huge error pages
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
}