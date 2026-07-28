using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// Applies a minimal explicit environment allowlist and denies inherited secrets.
/// </summary>
internal static class AcpProcessEnvironmentPolicy
{
    private static readonly HashSet<string> DeniedEnvironmentKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "AWS_ACCESS_KEY_ID",
        "AWS_SECRET_ACCESS_KEY",
        "AWS_SESSION_TOKEN",
        "AZURE_CLIENT_SECRET",
        "GITHUB_TOKEN",
        "GH_TOKEN",
        "NPM_TOKEN",
        "OPENAI_API_KEY",
        "ANTHROPIC_API_KEY",
        "GOOGLE_API_KEY",
        "PASSWORD",
        "SECRET",
        "TOKEN",
    };

    public static void Apply(ProcessStartInfo startInfo, AcpProcessLaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        ArgumentNullException.ThrowIfNull(options);

        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.CreateNoWindow = true;

        foreach (var key in startInfo.Environment.Keys.ToArray())
        {
            startInfo.Environment.Remove(key);
        }

        foreach (var (key, value) in options.AllowlistedEnvironment)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new AcpProcessLifecycleException(
                    AcpProcessLifecycleFailureKind.ProtocolFailure,
                    "ACP environment key is required.");
            }

            if (IsDeniedKey(key))
            {
                throw new AcpProcessLifecycleException(
                    AcpProcessLifecycleFailureKind.ProtocolFailure,
                    "ACP environment contains a denied secret-bearing key.");
            }

            startInfo.Environment[key] = value ?? string.Empty;
        }
    }

    public static bool IsDeniedKey(string key) =>
        DeniedEnvironmentKeys.Contains(key)
        || key.Contains("SECRET", StringComparison.OrdinalIgnoreCase)
        || key.Contains("TOKEN", StringComparison.OrdinalIgnoreCase)
        || key.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase)
        || key.Contains("API_KEY", StringComparison.OrdinalIgnoreCase);
}
