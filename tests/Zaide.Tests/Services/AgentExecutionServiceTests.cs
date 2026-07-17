using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Tests.Features.Settings.Infrastructure;

namespace Zaide.Tests.Services;

public sealed class AgentExecutionServiceTests : IDisposable
{
    private readonly string _tempDir;

    public AgentExecutionServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ZaideAgentExecSvcTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private static AgentExecutionOptions DefaultOptions() => new()
    {
        BaseUrl = "https://api.test.com/v1",
        ApiKey = "test-key-123",
        Model = "test-model"
    };

    /// <summary>
    /// Creates an <see cref="AgentExecutionService"/> with a fake message handler
    /// that returns the given status code and body.
    /// </summary>
    private AgentExecutionService CreateService(
        HttpStatusCode statusCode,
        string responseBody,
        AgentExecutionOptions? options = null)
    {
        var handler = new FakeMessageHandler(statusCode, responseBody);
        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets(options ?? DefaultOptions());
        return new AgentExecutionService(httpClient, settings, secrets);
    }

    /// <summary>
    /// Creates a SettingsService + TestSecretStore pair configured to match the
    /// given <see cref="AgentExecutionOptions"/>.
    /// </summary>
    private (SettingsService settings, TestSecretStore secrets) CreateSettingsAndSecrets(
        AgentExecutionOptions options)
    {
        var settingsPath = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + "_settings.json");
        var lkgPath = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + "_lkg.json");
        var tmpPath = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + "_tmp.json");

        var llm = new LlmSettings(
            BaseUrl: options.BaseUrl,
            Model: options.Model,
            ApiKeySource: "secret-store");
        var model = SettingsModel.Defaults with { Llm = llm };
        var json = SettingsSerializer.Serialize(model);
        File.WriteAllText(settingsPath, json);

        var settingsService = new SettingsService(settingsPath, lkgPath, tmpPath,
            new SettingsMigrator(Array.Empty<ISettingsMigration>()));

        var secrets = new TestSecretStore();
        if (!string.IsNullOrEmpty(options.ApiKey))
            secrets.Set("llm.apiKey", options.ApiKey);

        return (settingsService, secrets);
    }

    // ── Request construction ────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_UsesConfiguredUrlAndModelAndIncludesUserMessage()
    {
        string? capturedRequestUrl = null;
        string? capturedRequestBody = null;

        var handler = new CaptureMessageHandler(req =>
        {
            capturedRequestUrl = req.RequestUri?.ToString();
            capturedRequestBody = req.Content is not null
                ? req.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                : null;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    choices = new[]
                    {
                        new
                        {
                            message = new { content = "Hello from test" },
                            finish_reason = "stop"
                        }
                    }
                }), Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets(DefaultOptions());
        var service = new AgentExecutionService(httpClient, settings, secrets);
        var result = await service.ExecuteAsync("Hi there");

        Assert.True(result.IsSuccess);
        Assert.Equal("Hello from test", result.ResponseText);

        Assert.NotNull(capturedRequestUrl);
        Assert.Contains("https://api.test.com/v1/chat/completions", capturedRequestUrl);

        Assert.NotNull(capturedRequestBody);
        using var doc = JsonDocument.Parse(capturedRequestBody);
        var root = doc.RootElement;

        // Model
        Assert.Equal("test-model", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());

        // Messages
        var messages = root.GetProperty("messages");
        Assert.Single(messages.EnumerateArray());
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.Equal("Hi there", messages[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_SendsAuthHeader()
    {
        string? capturedAuth = null;

        var handler = new CaptureMessageHandler(req =>
        {
            capturedAuth = req.Headers.Authorization?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    choices = new[]
                    {
                        new { message = new { content = "ok" }, finish_reason = "stop" }
                    }
                }), Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets(DefaultOptions());
        var service = new AgentExecutionService(httpClient, settings, secrets);
        await service.ExecuteAsync("test");

        Assert.Equal("Bearer test-key-123", capturedAuth);
    }

    // ── Valid success response ──────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ValidSuccessResponse_ReturnsAssistantText()
    {
        var service = CreateService(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content = "Assistant reply" }, finish_reason = "stop" }
            }
        }));

        var result = await service.ExecuteAsync("Hello");

        Assert.True(result.IsSuccess);
        Assert.Equal("Assistant reply", result.ResponseText);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ClineDataEnvelope_ReturnsAssistantText()
    {
        var service = CreateService(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            success = true,
            data = new
            {
                choices = new[]
                {
                    new { message = new { content = "Cline envelope reply" }, finish_reason = "stop" }
                }
            }
        }));

        var result = await service.ExecuteAsync("Hello");

        Assert.True(result.IsSuccess);
        Assert.Equal("Cline envelope reply", result.ResponseText);
    }

    // ── Missing API key ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_MissingApiKey_ReturnsFailure()
    {
        var options = new AgentExecutionOptions
        {
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "",
            Model = "test-model"
        };
        var service = CreateService(HttpStatusCode.OK, "{}", options);

        var result = await service.ExecuteAsync("Hello");

        Assert.False(result.IsSuccess);
        Assert.Contains("API key", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ── Invalid base URL ────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_InvalidBaseUrl_ReturnsFailure()
    {
        var options = new AgentExecutionOptions
        {
            BaseUrl = "",
            ApiKey = "key",
            Model = "test-model"
        };
        var service = CreateService(HttpStatusCode.OK, "{}", options);

        var result = await service.ExecuteAsync("Hello");

        Assert.False(result.IsSuccess);
        Assert.Contains("Base URL", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_MissingModel_ReturnsFailure()
    {
        var options = new AgentExecutionOptions
        {
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "key",
            Model = ""
        };
        var service = CreateService(HttpStatusCode.OK, "{}", options);

        var result = await service.ExecuteAsync("Hello");

        Assert.False(result.IsSuccess);
        Assert.Contains("Model", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ── Non-success HTTP status ─────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_NonSuccessHttpStatus_ReturnsFailure()
    {
        var service = CreateService(HttpStatusCode.Unauthorized, "{\"error\": {\"message\": \"Invalid credentials\"}}");

        var result = await service.ExecuteAsync("Hello");

        Assert.False(result.IsSuccess);
        Assert.Contains("401", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ServerErrorStatusCode_ReturnsFailure()
    {
        var service = CreateService(HttpStatusCode.InternalServerError, "Internal Server Error");

        var result = await service.ExecuteAsync("Hello");

        Assert.False(result.IsSuccess);
        Assert.Contains("500", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ── Malformed JSON ──────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_MalformedJson_ReturnsFailure()
    {
        var service = CreateService(HttpStatusCode.OK, "this is not json");

        var result = await service.ExecuteAsync("Hello");

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid JSON", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyBody_ReturnsFailure()
    {
        var service = CreateService(HttpStatusCode.OK, "");

        var result = await service.ExecuteAsync("Hello");

        // Empty string fails JSON parsing
        Assert.False(result.IsSuccess);
    }

    // ── Valid JSON with no assistant content ────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ValidJsonNoChoices_ReturnsFailure()
    {
        var service = CreateService(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            choices = Array.Empty<object>()
        }));

        var result = await service.ExecuteAsync("Hello");

        Assert.False(result.IsSuccess);
        Assert.Contains("no choices", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ValidJsonMissingMessage_ReturnsFailure()
    {
        // choices array exists but message property is missing
        var service = CreateService(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            choices = new[] { new { unexpected = "data" } }
        }));

        var result = await service.ExecuteAsync("Hello");

        Assert.False(result.IsSuccess);
        Assert.Contains("response structure", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ValidJsonNullContent_ReturnsFailure()
    {
        var service = CreateService(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content = (string?)null }, finish_reason = "stop" }
            }
        }));

        var result = await service.ExecuteAsync("Hello");

        Assert.False(result.IsSuccess);
        Assert.Contains("no assistant content", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ValidJsonEmptyContent_ReturnsFailure()
    {
        var service = CreateService(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content = "" }, finish_reason = "stop" }
            }
        }));

        var result = await service.ExecuteAsync("Hello");

        Assert.False(result.IsSuccess);
        Assert.Contains("no assistant content", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ── Empty user message ──────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_EmptyUserMessage_ReturnsFailure()
    {
        var service = CreateService(HttpStatusCode.OK, "{}");

        var result = await service.ExecuteAsync("");

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ── HTTP request failure ────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_HttpRequestException_ReturnsFailure()
    {
        var handler = new FaultMessageHandler(new HttpRequestException("Connection refused"));
        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets(DefaultOptions());
        var service = new AgentExecutionService(httpClient, settings, secrets);

        var result = await service.ExecuteAsync("Hello");

        Assert.False(result.IsSuccess);
        Assert.Contains("Connection refused", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_TaskCancelled_ReturnsFailure()
    {
        var handler = new FaultMessageHandler(new TaskCanceledException("Cancelled"));
        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets(DefaultOptions());
        var service = new AgentExecutionService(httpClient, settings, secrets);

        var result = await service.ExecuteAsync("Hello");

        Assert.False(result.IsSuccess);
        Assert.Contains("cancelled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_OperationCancelled_ReturnsFailure()
    {
        var handler = new FaultMessageHandler(new OperationCanceledException("Cancelled by token"));
        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets(DefaultOptions());
        var service = new AgentExecutionService(httpClient, settings, secrets);

        var result = await service.ExecuteAsync("Hello");

        Assert.False(result.IsSuccess);
        Assert.Contains("cancelled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ── Missing choices property entirely ───────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ValidJsonMissingChoicesProperty_ReturnsFailure()
    {
        var service = CreateService(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            id = "chatcmpl-xxx",
            model = "test"
        }));

        var result = await service.ExecuteAsync("Hello");

        Assert.False(result.IsSuccess);
        Assert.Contains("response structure", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("top-level fields", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("model", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ErrorEnvelopeWithHttpSuccess_ReportsSafeResponseShape()
    {
        var service = CreateService(HttpStatusCode.OK, "{\"error\":\"Not Found\",\"success\":false}");

        var result = await service.ExecuteAsync("Hello");

        Assert.False(result.IsSuccess);
        Assert.Contains("top-level fields", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("error", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("success", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Not Found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ── Trimming of base URL trailing slash ─────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_TrimsTrailingSlashFromBaseUrl()
    {
        string? capturedUrl = null;
        var handler = new CaptureMessageHandler(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    choices = new[] { new { message = new { content = "ok" }, finish_reason = "stop" } }
                }), Encoding.UTF8, "application/json")
            };
        });

        var options = new AgentExecutionOptions
        {
            BaseUrl = "https://api.test.com/v1/",
            ApiKey = "key",
            Model = "m"
        };
        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets(options);
        var service = new AgentExecutionService(httpClient, settings, secrets);

        await service.ExecuteAsync("test");

        Assert.NotNull(capturedUrl);
        // Should have exactly one slash between v1 and chat/completions
        Assert.Equal("https://api.test.com/v1/chat/completions", capturedUrl);
    }
}

// ── Fake message handlers ──────────────────────────────────────────────────

/// <summary>
/// Returns a fixed status code and response body for every request.
/// </summary>
internal sealed class FakeMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _responseBody;

    public FakeMessageHandler(HttpStatusCode statusCode, string responseBody)
    {
        _statusCode = statusCode;
        _responseBody = responseBody;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}

/// <summary>
/// Captures the request for inspection, then returns the configured response.
/// </summary>
internal sealed class CaptureMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public CaptureMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = _responder(request);
        return Task.FromResult(response);
    }
}

/// <summary>
/// Always throws the specified exception.
/// </summary>
internal sealed class FaultMessageHandler : HttpMessageHandler
{
    private readonly Exception _exception;

    public FaultMessageHandler(Exception exception)
    {
        _exception = exception;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw _exception;
    }
}
