using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Zaide.Tests.Architecture;

namespace Zaide.Tests.Features.Agents.Acp.Transport;

public sealed class Phase20TransportBypassTests
{
    private static readonly string RepositoryRoot = ArchitectureInventoryReader.ResolveRepositoryRoot();

    [Fact]
    public void Phase20Transport_AcpSources_DoNotReferenceNativeHarness()
    {
        var acpDir = Path.Combine(RepositoryRoot, "src/Features/Agents/Infrastructure/Acp");
        var files = Directory.GetFiles(acpDir, "*.cs", SearchOption.TopDirectoryOnly);
        Assert.NotEmpty(files);

        var forbidden = new Regex(
            @"\bNativeHarness\b|\bINativeHarness\b",
            RegexOptions.CultureInvariant);

        var violations = files
            .Select(path => (path, text: File.ReadAllText(path)))
            .Where(entry => forbidden.IsMatch(entry.text))
            .Select(entry => Path.GetFileName(entry.path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Phase20Transport_ProcessLaunch_IsLimitedToAllowlistedFiles()
    {
        var acpDir = Path.Combine(RepositoryRoot, "src/Features/Agents/Infrastructure/Acp");
        var allowed = new[]
        {
            "AcpSystemDiagnosticsChildProcess.cs",
            "AcpSystemDiagnosticsProcessLauncher.cs",
            "AcpProcessTreeTerminator.cs",
        };

        var processPattern = new Regex(
            @"\bSystem\.Diagnostics\.Process\b",
            RegexOptions.CultureInvariant);

        var violations = Directory.GetFiles(acpDir, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path))
            .Where(file => !allowed.Contains(file, StringComparer.Ordinal))
            .Where(file => processPattern.IsMatch(File.ReadAllText(Path.Combine(acpDir, file))))
            .ToArray();

        Assert.Empty(violations);
    }
}
