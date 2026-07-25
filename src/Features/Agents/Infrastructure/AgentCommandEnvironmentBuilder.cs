using System;
using System.Collections.Generic;
using Zaide.Features.Settings.Contracts;

namespace Zaide.Features.Agents.Infrastructure;

/// <summary>
/// Constructs the locked Phase 17 command environment from the parent process.
/// </summary>
internal static class AgentCommandEnvironmentBuilder
{
    private static readonly string[] InheritedNames =
    {
        "PATH",
        "LANG",
        "LC_ALL",
        "TZ",
        "HOME",
        "TMPDIR",
    };

    private static readonly (string Name, string Value)[] ForcedValues =
    {
        ("NO_COLOR", "1"),
        ("DOTNET_NOLOGO", "1"),
        ("DOTNET_CLI_TELEMETRY_OPTOUT", "1"),
    };

    public static IReadOnlyDictionary<string, string> Build(ISecretStore? secretStore = null)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        var secretValues = CollectSecretValues(secretStore);

        foreach (var name in InheritedNames)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            if (IsSecretName(name) || secretValues.Contains(value))
            {
                continue;
            }

            environment[name] = value;
        }

        foreach (var (name, value) in ForcedValues)
        {
            environment[name] = value;
        }

        return environment;
    }

    private static HashSet<string> CollectSecretValues(ISecretStore? secretStore)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        if (secretStore is null)
        {
            return values;
        }

        foreach (var key in new[] { "openai_api_key", "api_key", "token", "password" })
        {
            var value = secretStore.Get(key);
            if (!string.IsNullOrEmpty(value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static bool IsSecretName(string name) =>
        name.Contains("SECRET", StringComparison.OrdinalIgnoreCase)
        || name.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase)
        || name.Contains("TOKEN", StringComparison.OrdinalIgnoreCase)
        || name.Contains("API_KEY", StringComparison.OrdinalIgnoreCase);
}
