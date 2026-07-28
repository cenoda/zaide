using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Zaide.Tests.Architecture;

namespace Zaide.Tests.Features.Agents.Acp.Actions;

public sealed class Phase20ActionBridgeBypassTests
{
    private static readonly string RepositoryRoot = ArchitectureInventoryReader.ResolveRepositoryRoot();

    [Fact]
    public void Phase20ActionBridge_AcpApplicationSources_DoNotAccessFilesystemDirectly()
    {
        var root = Path.Combine(RepositoryRoot, "src/Features/Agents/Application/Acp");
        var forbidden = new Regex(
            @"\bSystem\.IO\.(File|Directory)\b|\bSystem\.Diagnostics\.Process\b",
            RegexOptions.CultureInvariant);

        var violations = Directory.GetFiles(root, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => (path, text: File.ReadAllText(path)))
            .Where(entry => forbidden.IsMatch(entry.text))
            .Select(entry => Path.GetRelativePath(RepositoryRoot, entry.path).Replace('\\', '/'))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Phase20ActionBridge_AcpApplicationSources_DoNotReferenceNativeHarness()
    {
        var root = Path.Combine(RepositoryRoot, "src/Features/Agents/Application/Acp");
        var forbidden = new Regex(
            @"\bNativeHarness\b|\bINativeHarness\b",
            RegexOptions.CultureInvariant);

        var violations = Directory.GetFiles(root, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => (path, text: File.ReadAllText(path)))
            .Where(entry => forbidden.IsMatch(entry.text))
            .Select(entry => Path.GetRelativePath(RepositoryRoot, entry.path).Replace('\\', '/'))
            .ToArray();

        Assert.Empty(violations);
    }
}
