using System;
using System.IO;
using System.Text.Json;
using Phase16NativeHarnessEvaluation;
using Xunit;

namespace Zaide.Tests.Phase16Evaluation;

public sealed class Phase16M3QualificationModelProvidersTests
{
    [Fact]
    public void TcT01WorkspaceSettings_WiresDeepSeekCredentialEnvKey()
    {
        var settingsPath = Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "Phase16NativeHarnessEvaluation",
            "Fixtures",
            "TC-T01",
            "workspace",
            ".qwen",
            "settings.json");

        Assert.True(File.Exists(settingsPath), $"Expected fixture settings at {settingsPath}");

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("modelProviders", out var modelProviders));

        Assert.True(modelProviders.TryGetProperty("openai", out var openAiProviders));
        Assert.Equal(JsonValueKind.Array, openAiProviders.ValueKind);
        Assert.Equal(1, openAiProviders.GetArrayLength());

        var provider = openAiProviders[0];
        Assert.Equal(
            Phase16M3QualificationPolicy.AllowedModel,
            provider.GetProperty("id").GetString());
        Assert.Equal(
            Phase16M3QualificationPolicy.AllowedCredentialEnvVar,
            provider.GetProperty("envKey").GetString());
        Assert.Equal(
            Phase16M3QualificationPolicy.AllowedServiceUrl,
            provider.GetProperty("baseUrl").GetString());
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Zaide.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root (Zaide.slnx).");
    }
}
