using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Phase 17 M8 bypass-prevention ratchets for the action control plane.
/// </summary>
public sealed class Phase17BypassRatchetTests
{
    private static readonly string RepositoryRoot = ArchitectureInventoryReader.ResolveRepositoryRoot();

    [Fact]
    public void AgentBackends_DoNotReferenceEditorFileIoOrWorkflowRunners()
    {
        var violations = ScanFiles(
            "src/Features/Agents",
            file => Path.GetFileName(file).EndsWith("AgentBackend.cs", StringComparison.Ordinal)
                    || file.Contains("Infrastructure/LegacyOpenAiCompatibleAgentBackend.cs", StringComparison.Ordinal),
            @"\bIFileService\b|\bIManagedProcessRunner\b|\bManagedProcessRunner\b|\bFileService\b");

        Assert.Empty(violations);
    }

    [Fact]
    public void AgentApplication_DoesNotUseForbiddenBclOrServiceLocation()
    {
        var violations = ScanFiles(
            "src/Features/Agents/Application",
            _ => true,
            @"\bSystem\.IO\.|\bSystem\.Diagnostics\.Process\b|\bIServiceProvider\b",
            excludeFileNames: new[] { "NullAgentDocumentReconciler.cs" });

        Assert.Empty(violations);
    }

    [Fact]
    public void AgentApplication_DoesNotReferenceConcretePresentationOrCrossFeatureInfrastructure()
    {
        var violations = ScanFiles(
            "src/Features/Agents/Application",
            file => !file.EndsWith("AgentConversationEventProjection.cs", StringComparison.Ordinal),
            @"\bZaide\.Features\.Editor\.Presentation\.|\bZaide\.Features\.Editor\.Infrastructure\.|\bZaide\.Features\.ProjectSystem\.Infrastructure\.|\bZaide\.Features\.ProjectSystem\.Presentation\.");

        Assert.Empty(violations);
    }

    [Fact]
    public void ControlPlane_DoesNotWriteConversationStoreOutsideProjection()
    {
        var controlPlaneFiles = new[]
        {
            "src/Features/Agents/Application/ContractAgentActionBroker.cs",
            "src/Features/Agents/Application/UnavailableAgentActionBroker.cs",
            "src/Features/Agents/Application/AgentActionBrokerFactory.cs",
            "src/Features/Agents/Application/InteractiveAgentPermissionReviewService.cs",
            "src/Features/Agents/Infrastructure/WorkspaceFileReader.cs",
            "src/Features/Agents/Infrastructure/WorkspaceFileMutator.cs",
            "src/Features/Agents/Infrastructure/WorkspaceCommandExecutor.cs",
            "src/Features/Editor/Application/WorkspaceEditorDocumentReconciler.cs",
            "tests/Zaide.Tests/Features/Agents/FakeActionRequesterBackend.cs",
        };

        var violations = new List<string>();
        foreach (var relativePath in controlPlaneFiles)
        {
            var file = Path.Combine(RepositoryRoot, relativePath);
            var text = File.ReadAllText(file);
            if (text.Contains("AppendEntry(", StringComparison.Ordinal))
            {
                violations.Add(relativePath);
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void FakeActionRequester_IsTestOnlyAndNotProductionRegistered()
    {
        var productionText = File.ReadAllText(
            Path.Combine(RepositoryRoot, "src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs"));

        Assert.DoesNotContain("FakeActionRequesterBackend", productionText, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            RepositoryRoot,
            "tests/Zaide.Tests/Features/Agents/FakeActionRequesterBackend.cs")));
    }

    private static IReadOnlyList<string> ScanFiles(
        string relativeDirectory,
        Func<string, bool> include,
        string forbiddenPattern,
        IEnumerable<string>? excludeFileNames = null)
    {
        var excludes = new HashSet<string>(excludeFileNames ?? Array.Empty<string>(), StringComparer.Ordinal);
        var regex = new Regex(forbiddenPattern, RegexOptions.CultureInvariant);
        var violations = new List<string>();
        var root = Path.Combine(RepositoryRoot, relativeDirectory);

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (!include(file) || excludes.Contains(Path.GetFileName(file)))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            if (regex.IsMatch(text))
            {
                violations.Add(Path.GetRelativePath(RepositoryRoot, file));
            }
        }

        return violations;
    }
}
