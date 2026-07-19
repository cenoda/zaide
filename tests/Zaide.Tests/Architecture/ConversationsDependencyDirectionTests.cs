using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Refactor 7 M1: Conversations must remain agent-neutral and must not depend on
/// Agents, Townhall, App shell, or presentation frameworks.
/// </summary>
public sealed class ConversationsDependencyDirectionTests
{
    private static readonly string[] ForbiddenNamespaceFragments =
    [
        "Zaide.Features.Agents",
        "Zaide.Features.Townhall",
        "Zaide.App",
        "Avalonia",
        "ReactiveUI",
    ];

    [Fact]
    public void ConversationsProductionSources_DoNotReferenceForbiddenNamespaces()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(
                Path.Combine(root, "src/Features/Conversations"),
                "*.cs",
                SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var forbidden in ForbiddenNamespaceFragments)
            {
                Assert.DoesNotContain(
                    $"using {forbidden}",
                    text,
                    StringComparison.Ordinal);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Zaide.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root (Zaide.slnx).");
    }
}
