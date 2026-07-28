using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Zaide.Tests.Architecture;

namespace Zaide.Tests.Features.Agents.Acp.Backend;

public sealed class Phase20BackendBypassTests
{
    private static readonly string RepositoryRoot = ArchitectureInventoryReader.ResolveRepositoryRoot();

    [Fact]
    public void Phase20Backend_AcpSources_DoNotReferenceNativeHarness()
    {
        var directories = new[]
        {
            Path.Combine(RepositoryRoot, "src/Features/Agents/Infrastructure/Acp"),
            Path.Combine(RepositoryRoot, "src/Features/Agents/Application/Acp"),
        };

        var forbidden = new Regex(
            @"\bNativeHarness\b|\bINativeHarness\b",
            RegexOptions.CultureInvariant);

        var violations = directories
            .SelectMany(directory => Directory.Exists(directory)
                ? Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
                : Array.Empty<string>())
            .Select(path => (path, text: File.ReadAllText(path)))
            .Where(entry => forbidden.IsMatch(entry.text))
            .Select(entry => Path.GetRelativePath(RepositoryRoot, entry.path).Replace('\\', '/'))
            .ToArray();

        Assert.Empty(violations);
    }
}
