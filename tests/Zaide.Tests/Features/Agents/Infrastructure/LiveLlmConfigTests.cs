using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Infrastructure;

namespace Zaide.Tests.Features.Agents.Infrastructure;

/// <summary>
/// Phase 8.1.2 / M2 tests proving that <see cref="AgentExecutionService"/>
/// resolves effective LLM configuration <b>live</b> per request from
/// <see cref="ISettingsService"/> + <see cref="ISecretStore"/> + env vars,
/// without requiring service recreation.
/// </summary>
public sealed class LiveLlmConfigTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;
    private readonly string _lkgPath;
    private readonly string _tmpPath;
    private readonly string _secretsPath;

    public LiveLlmConfigTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ZaideLiveLlmTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
        _lkgPath = Path.Combine(_tempDir, "settings.json.lastknowngood");
        _tmpPath = Path.Combine(_tempDir, "settings.json.tmp");
        _secretsPath = Path.Combine(_tempDir, "secrets.json");

        // Seed a default settings file
        var json = SettingsSerializer.Serialize(SettingsModel.Defaults);
        File.WriteAllText(_settingsPath, json);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private SettingsService CreateSettingsService()
    {
        return new SettingsService(_settingsPath, _lkgPath, _tmpPath,
            new SettingsMigrator(Array.Empty<ISettingsMigration>()));
    }

    /// <summary>
    /// Handler that captures the request URL, body, and auth header, then
    /// returns a configurable response.
    /// </summary>
    private sealed class InspectingHandler : HttpMessageHandler
    {
        public string? CapturedUrl;
        public string? CapturedAuth;
        public string? CapturedModel;
        public string ResponseContent { get; set; } = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = "ok" }, finish_reason = "stop" } }
        });

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            CapturedUrl = request.RequestUri?.ToString();
            CapturedAuth = request.Headers.Authorization?.ToString();

            // Read body to extract model
            if (request.Content is not null)
            {
                var body = request.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult();
                using var doc = JsonDocument.Parse(body);
                CapturedModel = doc.RootElement.GetProperty("model").GetString();
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponseContent, Encoding.UTF8, "application/json")
            });
        }
    }

    // ── Live settings change ────────────────────────────────────────────

    [Fact]
    public async Task CommittedSettingsChange_NextExecuteUsesNewUrlAndModel()
    {
        using var settings = CreateSettingsService();
        var secrets = new FileSecretStore(_secretsPath);
        secrets.Set("llm.apiKey", "initial-key");

        var handler = new InspectingHandler();
        var httpClient = new HttpClient(handler);
        var service = new AgentExecutionService(httpClient, settings, secrets);

        // First call uses defaults
        await service.ExecuteAsync("first");
        Assert.Equal("https://api.openai.com/v1/chat/completions", handler.CapturedUrl);
        Assert.Equal("gpt-4o-mini", handler.CapturedModel);

        // Commit a settings change: new URL and model
        var result = await settings.UpdateAsync(current => current with
        {
            Llm = current.Llm with
            {
                BaseUrl = "https://new-api.example.com/v2",
                Model = "new-model-x",
            }
        });
        Assert.IsType<SettingsMutationResult.Applied>(result);

        // Second call — same service instance — must use the new values
        await service.ExecuteAsync("second");
        Assert.Equal("https://new-api.example.com/v2/chat/completions", handler.CapturedUrl);
        Assert.Equal("new-model-x", handler.CapturedModel);
    }

    // ── Live secret change ──────────────────────────────────────────────

    [Fact]
    public async Task CommittedSecretChange_NextExecuteUsesNewApiKey()
    {
        using var settings = CreateSettingsService();
        var secrets = new FileSecretStore(_secretsPath);
        secrets.Set("llm.apiKey", "initial-key");

        var handler = new InspectingHandler();
        var httpClient = new HttpClient(handler);
        var service = new AgentExecutionService(httpClient, settings, secrets);

        // First call uses initial key
        await service.ExecuteAsync("first");
        Assert.Equal("Bearer initial-key", handler.CapturedAuth);

        // Change the secret
        secrets.Set("llm.apiKey", "rotated-key-456");

        // Second call — same service instance — must use the new key
        await service.ExecuteAsync("second");
        Assert.Equal("Bearer rotated-key-456", handler.CapturedAuth);
    }

    // ── Environment variable overrides ──────────────────────────────────

    [Fact]
    public async Task EnvVar_OverridesPersistedUrl()
    {
        using var settings = CreateSettingsService();
        var secrets = new FileSecretStore(_secretsPath);
        secrets.Set("llm.apiKey", "test-key");

        var handler = new InspectingHandler();
        var httpClient = new HttpClient(handler);
        var service = new AgentExecutionService(httpClient, settings, secrets);

        var prevUrl = Environment.GetEnvironmentVariable("AGENT_API_URL");
        try
        {
            Environment.SetEnvironmentVariable("AGENT_API_URL", "https://env-override.example.com/v1");

            await service.ExecuteAsync("test");

            Assert.Equal("https://env-override.example.com/v1/chat/completions", handler.CapturedUrl);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENT_API_URL", prevUrl);
        }
    }

    [Fact]
    public async Task EnvVar_OverridesPersistedModel()
    {
        using var settings = CreateSettingsService();
        var secrets = new FileSecretStore(_secretsPath);
        secrets.Set("llm.apiKey", "test-key");

        var handler = new InspectingHandler();
        var httpClient = new HttpClient(handler);
        var service = new AgentExecutionService(httpClient, settings, secrets);

        var prevModel = Environment.GetEnvironmentVariable("AGENT_MODEL");
        try
        {
            Environment.SetEnvironmentVariable("AGENT_MODEL", "env-model-override");

            await service.ExecuteAsync("test");

            Assert.Equal("env-model-override", handler.CapturedModel);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENT_MODEL", prevModel);
        }
    }

    [Fact]
    public async Task EnvVar_OverridesSecretStoreApiKey()
    {
        using var settings = CreateSettingsService();
        var secrets = new FileSecretStore(_secretsPath);
        secrets.Set("llm.apiKey", "secret-store-key");

        var handler = new InspectingHandler();
        var httpClient = new HttpClient(handler);
        var service = new AgentExecutionService(httpClient, settings, secrets);

        var prevKey = Environment.GetEnvironmentVariable("AGENT_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("AGENT_API_KEY", "env-api-key");

            await service.ExecuteAsync("test");

            Assert.Equal("Bearer env-api-key", handler.CapturedAuth);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENT_API_KEY", prevKey);
        }
    }

    // ── Combined live change ────────────────────────────────────────────

    [Fact]
    public async Task SettingsAndSecretChanged_NextExecuteUsesAllNew()
    {
        using var settings = CreateSettingsService();
        var secrets = new FileSecretStore(_secretsPath);
        secrets.Set("llm.apiKey", "old-key");

        var handler = new InspectingHandler();
        var httpClient = new HttpClient(handler);
        var service = new AgentExecutionService(httpClient, settings, secrets);

        // First call
        await service.ExecuteAsync("first");
        Assert.Equal("https://api.openai.com/v1/chat/completions", handler.CapturedUrl);
        Assert.Equal("gpt-4o-mini", handler.CapturedModel);
        Assert.Equal("Bearer old-key", handler.CapturedAuth);

        // Change both settings and secret
        await settings.UpdateAsync(current => current with
        {
            Llm = current.Llm with
            {
                BaseUrl = "https://changed.example.com/v3",
                Model = "changed-model",
            }
        });
        secrets.Set("llm.apiKey", "new-key");

        // Second call — same service — picks up everything
        await service.ExecuteAsync("second");
        Assert.Equal("https://changed.example.com/v3/chat/completions", handler.CapturedUrl);
        Assert.Equal("changed-model", handler.CapturedModel);
        Assert.Equal("Bearer new-key", handler.CapturedAuth);
    }
}
