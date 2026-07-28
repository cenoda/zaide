using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Zaide.Features.Agents.Infrastructure.Acp;
using Zaide.Tests.Architecture;

namespace Zaide.Tests.Features.Agents.Acp.Transport;

/// <summary>
/// Resolves and builds the repository-owned ACP fake child-process fixture.
/// </summary>
internal static class AcpFakeAgentFixture
{
    private static readonly string RepositoryRoot = ArchitectureInventoryReader.ResolveRepositoryRoot();
    private static readonly string ProjectPath = Path.Combine(
        RepositoryRoot,
        "tests/fixtures/acp-fake-agent/AcpFakeAgent.csproj");
    private static readonly string OutputDirectory = Path.Combine(
        RepositoryRoot,
        "tests/fixtures/acp-fake-agent/bin/TransportFixture/net10.0");
    private static readonly string DllPath = Path.Combine(OutputDirectory, "AcpFakeAgent.dll");
    private static readonly object BuildGate = new();
    private static bool _built;

    public static AcpProcessLaunchOptions CreateLaunchOptions(string mode)
    {
        EnsureBuilt();

        var dotnetPath = Environment.ProcessPath
                         ?? throw new InvalidOperationException("dotnet host path is unavailable.");

        return new AcpProcessLaunchOptions(dotnetPath, new[] { DllPath, mode })
        {
            AllowlistedEnvironment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["DOTNET_ENVIRONMENT"] = "Development",
                ["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? string.Empty,
            },
        };
    }

    public static async Task<AcpStdioProcessHost> StartHealthyHostAsync()
    {
        var launcher = new AcpSystemDiagnosticsProcessLauncher();
        return await AcpStdioProcessHost.StartAsync(
            CreateLaunchOptions("healthy"),
            launcher,
            default).ConfigureAwait(false);
    }

    private static void EnsureBuilt()
    {
        lock (BuildGate)
        {
            if (_built && File.Exists(DllPath))
            {
                return;
            }

            Directory.CreateDirectory(OutputDirectory);
            var startInfo = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? "dotnet",
                WorkingDirectory = RepositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("build");
            startInfo.ArgumentList.Add(ProjectPath);
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(OutputDirectory);
            startInfo.ArgumentList.Add("-v");
            startInfo.ArgumentList.Add("q");

            using var process = Process.Start(startInfo)
                                  ?? throw new InvalidOperationException("Failed to build ACP fake agent fixture.");
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                var stderr = process.StandardError.ReadToEnd();
                throw new InvalidOperationException(
                    $"ACP fake agent fixture build failed with exit code {process.ExitCode}: {stderr}");
            }

            _built = true;
        }
    }
}
