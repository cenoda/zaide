using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Presentation;

namespace Zaide.Tests.Features.Settings.Infrastructure;

/// <summary>
/// Phase 8.1.2 / M2 tests for <see cref="FileSecretStore"/>:
/// basic get/set/delete, persistence, and the guarantee that
/// <c>settings.json</c> never contains API key values.
/// </summary>
public sealed class SecretStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _secretsPath;
    private readonly string _tempPath;

    public SecretStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ZaideSecretTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _secretsPath = Path.Combine(_tempDir, "secrets.json");
        _tempPath = Path.Combine(_tempDir, "secrets.json.tmp");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort */ }
    }

    private FileSecretStore CreateStore() => new(_secretsPath, _tempPath);

    // ── Basic operations ────────────────────────────────────────────────

    [Fact]
    public void Get_NonExistentKey_ReturnsNull()
    {
        using var store = CreateStore();
        Assert.Null(store.Get("does-not-exist"));
    }

    [Fact]
    public void Set_And_Get_RoundTrips()
    {
        using var store = CreateStore();
        store.Set("myKey", "myValue");
        Assert.Equal("myValue", store.Get("myKey"));
    }

    [Fact]
    public void Set_OverwritesExistingValue()
    {
        using var store = CreateStore();
        store.Set("key", "first");
        store.Set("key", "second");
        Assert.Equal("second", store.Get("key"));
    }

    [Fact]
    public void Delete_RemovesKey()
    {
        using var store = CreateStore();
        store.Set("key", "value");
        store.Delete("key");
        Assert.Null(store.Get("key"));
    }

    [Fact]
    public void Delete_NonExistentKey_DoesNotThrow()
    {
        using var store = CreateStore();
        store.Delete("nonexistent"); // should not throw
    }

    // ── Persistence ─────────────────────────────────────────────────────

    [Fact]
    public void Set_PersistsToDisk_AsJson()
    {
        using var store = CreateStore();
        store.Set("apiKey", "secret-123");

        Assert.True(File.Exists(_secretsPath));
        var json = File.ReadAllText(_secretsPath);
        var doc = JsonDocument.Parse(json);
        Assert.Equal("secret-123", doc.RootElement.GetProperty("apiKey").GetString());
    }

    [Fact]
    public void NewStoreInstance_ReadsExistingSecrets()
    {
        using (var store1 = CreateStore())
        {
            store1.Set("persisted", "value");
        }

        using var store2 = CreateStore();
        Assert.Equal("value", store2.Get("persisted"));
    }

    [Fact]
    public void Delete_PersistsRemoval()
    {
        using (var store1 = CreateStore())
        {
            store1.Set("key", "value");
            store1.Delete("key");
        }

        using var store2 = CreateStore();
        Assert.Null(store2.Get("key"));
    }

    // ── Settings JSON never contains API key ────────────────────────────

    [Fact]
    public async Task SettingsJson_NeverContainsApiKey_WhenSecretStoreIsUsed()
    {
        // Set up a settings service + secret store.
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        var lkgPath = Path.Combine(_tempDir, "settings.json.lastknowngood");
        var tmpPath = Path.Combine(_tempDir, "settings.json.tmp");

        using var settings = new SettingsService(settingsPath, lkgPath, tmpPath,
            new SettingsMigrator(Array.Empty<ISettingsMigration>()));
        using var secrets = new FileSecretStore(_secretsPath, _tempPath);

        // Store the API key in the secret store (not in settings).
        secrets.Set("llm.apiKey", "super-secret-key-xyz");

        // Commit an LLM settings change.
        var next = settings.Current with
        {
            Llm = settings.Current.Llm with { BaseUrl = "https://api.example.com" }
        };
        await settings.UpdateAsync(_ => next);

        // The settings file must NOT contain the API key value.
        var settingsJson = File.ReadAllText(settingsPath);
        Assert.DoesNotContain("super-secret-key-xyz", settingsJson);

        // The secrets file MUST contain it.
        var secretsJson = File.ReadAllText(_secretsPath);
        Assert.Contains("super-secret-key-xyz", secretsJson);
    }

    [Fact]
    public async Task SettingsJson_LlmSection_HasNoApiKeyField()
    {
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        var lkgPath = Path.Combine(_tempDir, "settings.json.lastknowngood");
        var tmpPath = Path.Combine(_tempDir, "settings.json.tmp");

        using var settings = new SettingsService(settingsPath, lkgPath, tmpPath,
            new SettingsMigrator(Array.Empty<ISettingsMigration>()));

        await settings.UpdateAsync(_ => settings.Current);

        var settingsJson = File.ReadAllText(settingsPath);
        var doc = JsonDocument.Parse(settingsJson);
        var llm = doc.RootElement.GetProperty("llm");

        // The LLM section should have baseUrl, model, apiKeySource — but no apiKey.
        Assert.True(llm.TryGetProperty("baseUrl", out _));
        Assert.True(llm.TryGetProperty("model", out _));
        Assert.True(llm.TryGetProperty("apiKeySource", out _));
        Assert.False(llm.TryGetProperty("apiKey", out _));
    }
}
