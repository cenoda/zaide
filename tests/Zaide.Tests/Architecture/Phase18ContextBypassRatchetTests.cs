using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Phase 18 M1 bypass-prevention ratchets for the IDE context disclosure boundary.
/// </summary>
public sealed class Phase18ContextBypassRatchetTests
{
    private static readonly string RepositoryRoot = ArchitectureInventoryReader.ResolveRepositoryRoot();

    private static readonly Regex ForbiddenCrossFeaturePattern = new(
        @"\bZaide\.Features\.(?:Editor|Terminal|SourceControl|ProjectSystem|Debugging|Language|Workspace)\.(?:Presentation|Infrastructure)\.",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Type[] ContextConsumptionTypes =
    {
        typeof(AgentContextManifest),
        typeof(AgentContextItem),
        typeof(AgentContextDisclosurePayload),
        typeof(AgentContextDisclosureRedactionSummary),
        typeof(AgentContextDisclosureBoundarySummary),
    };

    [Fact]
    public void ContextAssembly_DoesNotBypassPolicyBoundary()
    {
        // Structural ratchet: any class/record/struct/interface with "ContextAssembly" or "ContextService" in name
        // must go through AgentContextSourcePolicyMatrix for source inclusion control
        var agentsRoot = Path.Combine(RepositoryRoot, "src/Features/Agents");
        var forbiddenTypeNames = new[] { "ContextAssembly", "ContextService" }; // types that would bypass policy matrix
        var assemblyTypes = Directory
            .EnumerateFiles(agentsRoot, "*.cs", SearchOption.AllDirectories)
            .Select(path => File.ReadAllText(path))
            .SelectMany(text => Regex.Matches(
                text,
                @"\b(?:class|record|struct|interface)\s+(?<name>\w*(?:ContextAssembly|ContextService)\w*)\b",
                RegexOptions.CultureInvariant)
                .Select(match => match.Groups["name"].Value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(assemblyTypes);

        foreach (var source in AgentContextSourceId.All)
        {
            Assert.True(AgentContextSourcePolicyMatrix.DefinesSource(source));
        }

        var policyMatrixPath = Path.Combine(
            RepositoryRoot,
            "src/Features/Agents/Domain/AgentContextSourcePolicyMatrix.cs");
        var inclusionOwners = EnumeratePhase18ContextProductionFiles()
            .Where(relativePath => File.ReadAllText(Path.Combine(RepositoryRoot, relativePath))
                .Contains("IsSourceIncluded", StringComparison.Ordinal))
            .OrderBy(relativePath => relativePath, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { Path.GetRelativePath(RepositoryRoot, policyMatrixPath).Replace('\\', '/') },
            inclusionOwners);
    }

    [Fact]
    public void ContextAssemblyService_RequiresPolicyMatrixRegistration()
    {
        // Future context assembly services must be registered in AgentContextSourcePolicyMatrix
        // This ensures all context assembly/assembly services go through policy control

        var allAgentFiles = EnumerateAllAgentProductionFiles()
            .Order(StringComparer.Ordinal)
            .ToArray();

        foreach (var filePath in allAgentFiles)
        {
            var content = File.ReadAllText(Path.Combine(RepositoryRoot, filePath));

            // If file contains "AssemblyService" or "ContextAssembly" type definitions,
            // it must go through policy matrix
            var containsAssemblyService = Regex.IsMatch(content,
                @"\b(?:class|record|struct|interface)\s+(?<name>\w*AssemblyService\w*)\b",
                RegexOptions.CultureInvariant);
            var containsContextAssemblyPattern = Regex.IsMatch(content,
                @"\b(?:class|record|struct|interface)\s+(?<name>\w*ContextAssembly\w*|\w*ContextService\w*)\b",
                RegexOptions.CultureInvariant);

            if (containsAssemblyService || containsContextAssemblyPattern)
            {
                // Ensure this file mentions policy matrix or source inclusion
                var mentionsPolicyMatrix = content.Contains("AgentContextSource", StringComparison.Ordinal) ||
                                         content.Contains("SourcePolicy", StringComparison.Ordinal) ||
                                         content.Contains("IsSourceIncluded", StringComparison.Ordinal);

                Assert.True(mentionsPolicyMatrix,
                    $"Policy-breaking assembly type found in {filePath}. Must integrate with AgentContextSourcePolicyMatrix.");
            }
        }
    }

    [Fact]
    public void ContextManifest_DoesNotLeakToLegacyBackend()
    {
        var legacyType = typeof(LegacyOpenAiCompatibleAgentBackend);
        var violations = new List<string>();

        foreach (var contextType in ContextConsumptionTypes)
        {
            if (legacyType.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(field => ReferencesType(field.FieldType, contextType)))
            {
                violations.Add($"field:{contextType.Name}");
            }

            foreach (var method in legacyType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (ReferencesType(method.ReturnType, contextType))
                {
                    violations.Add($"return:{method.Name}:{contextType.Name}");
                }

                foreach (var parameter in method.GetParameters())
                {
                    if (ReferencesType(parameter.ParameterType, contextType))
                    {
                        violations.Add($"parameter:{method.Name}:{parameter.Name}:{contextType.Name}");
                    }
                }
            }
        }

        var executeMethod = legacyType.GetMethod(
            nameof(LegacyOpenAiCompatibleAgentBackend.ExecuteAsync),
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(executeMethod);

        var executeBody = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src/Features/Agents/Infrastructure/LegacyOpenAiCompatibleAgentBackend.cs"));
        foreach (var forbiddenName in new[]
        {
            nameof(AgentContextManifest),
            nameof(AgentContextItem),
            nameof(AgentContextDisclosurePayload),
        })
        {
            if (executeBody.Contains(forbiddenName, StringComparison.Ordinal))
            {
                violations.Add($"execute-body:{forbiddenName}");
            }
        }

        Assert.True(
            legacyType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(method => method.Name == nameof(LegacyOpenAiCompatibleAgentBackend.ExecuteAsync)),
            "Legacy backend must keep the ExecuteAsync entry point under ratchet review.");

        Assert.Empty(violations);
    }

    [Fact]
    public void ContextAssembly_DoesNotReferenceConcretePresentationOrCrossFeatureInfrastructure()
    {
        var violations = new List<string>();

        foreach (var relativePath in EnumeratePhase18ContextProductionFiles())
        {
            var text = File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));
            if (ForbiddenCrossFeaturePattern.IsMatch(text))
            {
                violations.Add(relativePath);
            }
        }

        var agentsRoot = Path.Combine(RepositoryRoot, "src/Features/Agents");
        foreach (var file in Directory.EnumerateFiles(agentsRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(RepositoryRoot, file).Replace('\\', '/');
            if (!relativePath.Contains("AgentContext", StringComparison.Ordinal)
                && !relativePath.EndsWith("AgentEvent.cs", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            if (ForbiddenCrossFeaturePattern.IsMatch(text))
            {
                violations.Add(relativePath);
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void ContextIntegration_DoesNotLeakToLegacyBackend()
    {
        var violations = new List<string>();

        var legacyType = typeof(LegacyOpenAiCompatibleAgentBackend);

        var executeBody = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src/Features/Agents/Infrastructure/LegacyOpenAiCompatibleAgentBackend.cs"));

        if (executeBody.Contains("ContextManifest", StringComparison.Ordinal))
        {
            violations.Add("Legacy backend consumes ContextManifest");
        }

        if (executeBody.Contains("AgentContextManifest", StringComparison.Ordinal))
        {
            violations.Add("Legacy backend consumes AgentContextManifest");
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void NativeHarness_ConsumesContextManifestOnlyThroughSystemPromptBuilder()
    {
        var violations = new List<string>();
        var allowedDirectReferences = new HashSet<string>(StringComparer.Ordinal)
        {
            "src/Features/Agents/Application/NativeHarnessSystemPromptBuilder.cs",
            "src/Features/Agents/Application/NativeHarnessLoopRunner.cs",
        };

        foreach (var relativeDirectory in new[] { "src/Features/Agents/Application", "src/Features/Agents/Infrastructure" })
        {
            var root = Path.Combine(RepositoryRoot, relativeDirectory);
            foreach (var file in Directory.EnumerateFiles(root, "NativeHarness*.cs", SearchOption.TopDirectoryOnly))
            {
                var relativePath = Path.GetRelativePath(RepositoryRoot, file).Replace('\\', '/');
                if (allowedDirectReferences.Contains(relativePath))
                {
                    continue;
                }

                var text = File.ReadAllText(file);
                if (text.Contains(nameof(AgentContextManifest), StringComparison.Ordinal)
                    || text.Contains(nameof(AgentContextItem), StringComparison.Ordinal))
                {
                    violations.Add(relativePath);
                }
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void Acp_ConsumesContextManifestOnlyThroughContextManifestEncoder()
    {
        var violations = new List<string>();
        var allowedDirectReferences = new HashSet<string>(StringComparer.Ordinal)
        {
            "src/Features/Agents/Application/Acp/AcpContextManifestEncoder.cs",
            "src/Features/Agents/Application/Acp/AcpAgentSessionAdapter.cs",
        };

        var root = Path.Combine(RepositoryRoot, "src/Features/Agents/Application/Acp");
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.TopDirectoryOnly))
        {
            var relativePath = Path.GetRelativePath(RepositoryRoot, file).Replace('\\', '/');
            if (allowedDirectReferences.Contains(relativePath))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            if (text.Contains(nameof(AgentContextManifest), StringComparison.Ordinal)
                || text.Contains(nameof(AgentContextItem), StringComparison.Ordinal))
            {
                violations.Add(relativePath);
            }
        }

        Assert.Empty(violations);
    }

    private static bool ReferencesType(Type candidate, Type target) =>
        candidate == target
        || candidate.IsByRef && candidate.GetElementType() == target;

    private static IEnumerable<string> EnumeratePhase18ContextProductionFiles()
    {
        var agentsDomain = Path.Combine(RepositoryRoot, "src/Features/Agents/Domain");
        foreach (var file in Directory.EnumerateFiles(agentsDomain, "AgentContext*.cs", SearchOption.TopDirectoryOnly))
        {
            yield return Path.GetRelativePath(RepositoryRoot, file).Replace('\\', '/');
        }
    }

    private static IEnumerable<string> EnumerateAllAgentProductionFiles()
    {
        var agentsRoot = Path.Combine(RepositoryRoot, "src/Features/Agents");
        foreach (var file in Directory.EnumerateFiles(agentsRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(RepositoryRoot, file).Replace('\\', '/');
            yield return relativePath;
        }
    }
}
