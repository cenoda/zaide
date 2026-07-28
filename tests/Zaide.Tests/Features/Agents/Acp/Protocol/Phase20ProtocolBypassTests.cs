using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Zaide.Tests.Architecture;

namespace Zaide.Tests.Features.Agents.Acp.Protocol;

public sealed class Phase20ProtocolBypassTests
{
    private static readonly string RepositoryRoot = ArchitectureInventoryReader.ResolveRepositoryRoot();

    [Fact]
    public void Phase20Protocol_AcpSources_DoNotReferenceNativeHarnessOrProcessLaunch()
    {
        var acpDir = Path.Combine(RepositoryRoot, "src/Features/Agents/Infrastructure/Acp");
        var files = Directory.GetFiles(acpDir, "*.cs", SearchOption.TopDirectoryOnly);
        Assert.NotEmpty(files);

        var forbidden = new Regex(
            @"\bNativeHarness\b|\bSystem\.Diagnostics\.Process\b|\bINativeHarness\b",
            RegexOptions.CultureInvariant);

        var violations = files
            .Select(path => (path, text: File.ReadAllText(path)))
            .Where(entry => forbidden.IsMatch(entry.text))
            .Select(entry => Path.GetFileName(entry.path))
            .ToArray();

        Assert.Empty(violations);
    }
}
