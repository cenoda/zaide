using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Infrastructure;

namespace Zaide.Tests.Features.Settings.Infrastructure;

/// <summary>
/// Phase 23 F5: Agents settings schema migration, validation, and persistence.
/// </summary>
public sealed class Phase23SettingsAgentsTests : IDisposable
{
    private readonly string _dir;

    public Phase23SettingsAgentsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "Phase23SettingsAgents_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [Fact]
    public void Migration_V3ToV4_PreservesExistingAndFillsAgentDefaults()
    {
        var settingsPath = Path.Combine(_dir, "settings.json");
        var lkgPath = Path.Combine(_dir, "settings.last-known-good.json");
        var tempPath = Path.Combine(_dir, "settings.tmp");

        File.WriteAllText(settingsPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 3,
            editor = new
            {
                codeFontFamily = "Cascadia Code, Consolas, monospace",
                codeFontSize = 18,
                proseFontFamily = "Georgia, serif",
                terminalFontFamily = "monospace",
                terminalFontSize = 14,
                tabSize = 4,
                insertSpaces = true,
                showWhitespace = false,
                showTabs = false,
                showSpaces = false,
                formatOnSave = true,
            },
            llm = new
            {
                baseUrl = "https://custom.example/v1",
                model = "custom-model",
                apiKeySource = "secret-store",
            },
            keybindings = new { },
            debug = new { breakpointsByWorkspaceRoot = new { } },
        }));

        var migrator = new SettingsMigrator(new ISettingsMigration[]
        {
            new SettingsMigrationV1ToV2(),
            new SettingsMigrationV2ToV3(),
            new SettingsMigrationV3ToV4(),
        });
        using var service = new SettingsService(settingsPath, lkgPath, tempPath, migrator);

        Assert.Equal(4, service.Current.SchemaVersion);
        Assert.True(service.Current.Editor.FormatOnSave);
        Assert.Equal(18, service.Current.Editor.CodeFontSize);
        Assert.Equal("custom-model", service.Current.Llm.Model);
        Assert.False(service.Current.Agents.TraceCaptureEnabled);
        Assert.False(service.Current.Agents.UsageCaptureEnabled);
        Assert.Equal(64, service.Current.Agents.TracePageSize);
        Assert.Equal(256, service.Current.Agents.TraceMaxPageSize);
        Assert.Equal(string.Empty, service.Current.Agents.AcpExecutablePath);
        Assert.Equal(string.Empty, service.Current.Agents.AcpArguments);
        Assert.Equal(string.Empty, service.Current.Agents.AcpExpectedAgentName);
        Assert.Equal(string.Empty, service.Current.Agents.AcpExpectedAgentVersion);
        Assert.Equal("Standard", service.Current.Agents.DefaultContextPolicyLevel);
    }

    [Fact]
    public async Task AgentsFields_RoundTripThroughSettingsService()
    {
        var settingsPath = Path.Combine(_dir, "roundtrip-settings.json");
        var lkgPath = Path.Combine(_dir, "roundtrip-lkg.json");
        var tempPath = Path.Combine(_dir, "roundtrip.tmp");
        var migrator = new SettingsMigrator(new ISettingsMigration[]
        {
            new SettingsMigrationV1ToV2(),
            new SettingsMigrationV2ToV3(),
            new SettingsMigrationV3ToV4(),
        });

        using var service = new SettingsService(settingsPath, lkgPath, tempPath, migrator);
        var next = service.Current with
        {
            Agents = new AgentsSettings(
                TraceCaptureEnabled: true,
                UsageCaptureEnabled: true,
                TracePageSize: 32,
                TraceMaxPageSize: 128,
                AcpExecutablePath: "/usr/bin/acp-agent",
                AcpArguments: "--stdio healthy",
                AcpExpectedAgentName: "acp-fake-agent",
                AcpExpectedAgentVersion: "1.0.0",
                DefaultContextPolicyLevel: "Detailed"),
        };

        var applied = await service.ApplyAsync(service.Current, next);
        Assert.IsType<SettingsMutationResult.Applied>(applied);

        using var reloaded = new SettingsService(settingsPath, lkgPath, tempPath, migrator);
        Assert.True(reloaded.Current.Agents.TraceCaptureEnabled);
        Assert.True(reloaded.Current.Agents.UsageCaptureEnabled);
        Assert.Equal(32, reloaded.Current.Agents.TracePageSize);
        Assert.Equal(128, reloaded.Current.Agents.TraceMaxPageSize);
        Assert.Equal("/usr/bin/acp-agent", reloaded.Current.Agents.AcpExecutablePath);
        Assert.Equal("--stdio healthy", reloaded.Current.Agents.AcpArguments);
        Assert.Equal("acp-fake-agent", reloaded.Current.Agents.AcpExpectedAgentName);
        Assert.Equal("1.0.0", reloaded.Current.Agents.AcpExpectedAgentVersion);
        Assert.Equal("Detailed", reloaded.Current.Agents.DefaultContextPolicyLevel);
    }

    [Fact]
    public async Task SecretShapedAcpArguments_RejectedByValidator()
    {
        var settingsPath = Path.Combine(_dir, "secret-guard.json");
        var lkgPath = Path.Combine(_dir, "secret-guard-lkg.json");
        var tempPath = Path.Combine(_dir, "secret-guard.tmp");
        using var service = new SettingsService(
            settingsPath,
            lkgPath,
            tempPath,
            new SettingsMigrator(new ISettingsMigration[]
            {
                new SettingsMigrationV1ToV2(),
                new SettingsMigrationV2ToV3(),
                new SettingsMigrationV3ToV4(),
            }));

        var invalid = service.Current with
        {
            Agents = service.Current.Agents with { AcpArguments = "Bearer sk-test-token" },
        };

        var result = await service.ApplyAsync(service.Current, invalid);
        var rejected = Assert.IsType<SettingsMutationResult.Invalid>(result);
        Assert.Contains(
            rejected.Errors,
            error => error.PropertyPath == "Agents.AcpArguments");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
