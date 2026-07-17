using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Zaide.Models;
using Zaide.Services;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Tests.Features.Settings.Infrastructure;

namespace Zaide.Tests;

/// <summary>
/// Shared test helper that creates an <see cref="ISettingsService"/> and
/// <see cref="ISecretStore"/> configured with specific LLM values.
/// Clears LLM-related environment variables on construction so that
/// settings/secret values are the effective source (not ambient env vars).
/// </summary>
internal sealed class TestLlmFixture : IDisposable
{
    private readonly string _tempDir;
    private readonly SettingsService _settingsService;
    private readonly TestSecretStore _secretStore;

    private readonly string? _savedApiUrl;
    private readonly string? _savedApiKey;
    private readonly string? _savedModel;

    public ISettingsService Settings => _settingsService;
    public TestSecretStore Secrets => _secretStore;

    public TestLlmFixture(
        string baseUrl = "https://api.test.com/v1",
        string apiKey = "test-key-123",
        string model = "test-model")
    {
        // Save and clear env vars so they don't interfere with settings-based config.
        _savedApiUrl = Environment.GetEnvironmentVariable("AGENT_API_URL");
        _savedApiKey = Environment.GetEnvironmentVariable("AGENT_API_KEY");
        _savedModel = Environment.GetEnvironmentVariable("AGENT_MODEL");
        Environment.SetEnvironmentVariable("AGENT_API_URL", null);
        Environment.SetEnvironmentVariable("AGENT_API_KEY", null);
        Environment.SetEnvironmentVariable("AGENT_MODEL", null);

        _tempDir = Path.Combine(Path.GetTempPath(), "ZaideTestLlm_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var settingsPath = Path.Combine(_tempDir, "settings.json");
        var lkgPath = Path.Combine(_tempDir, "settings.json.lastknowngood");
        var tmpPath = Path.Combine(_tempDir, "settings.json.tmp");

        _settingsService = new SettingsService(settingsPath, lkgPath, tmpPath,
            new SettingsMigrator(Array.Empty<ISettingsMigration>()));

        _secretStore = new TestSecretStore();

        // Commit the desired LLM settings.
        var current = _settingsService.Current;
        var next = current with
        {
            Llm = current.Llm with
            {
                BaseUrl = baseUrl,
                Model = model,
            }
        };
        _settingsService.UpdateAsync(_ => next).GetAwaiter().GetResult();

        if (!string.IsNullOrEmpty(apiKey))
        {
            _secretStore.Set("llm.apiKey", apiKey);
        }
    }

    public void Dispose()
    {
        _settingsService.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }

        Environment.SetEnvironmentVariable("AGENT_API_URL", _savedApiUrl);
        Environment.SetEnvironmentVariable("AGENT_API_KEY", _savedApiKey);
        Environment.SetEnvironmentVariable("AGENT_MODEL", _savedModel);
    }
}
