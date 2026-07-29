using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Zaide.Features.Agents.Application.Memory;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;

namespace Zaide.Tests.Architecture;

public sealed class Phase21MemoryRatchetTests
{
    private static readonly string RepositoryRoot = ArchitectureInventoryReader.ResolveRepositoryRoot();

    [Fact]
    public void MemoryPipeline_RoutesThroughM1MemoryRecordClass()
    {
        var source = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src/Features/Agents/Application/Memory/AgentMemoryStoreWriter.cs"));

        Assert.Contains("AgentDurableRecordClass.Memory", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MemoryPipelineFiles_AreFeatureOwned_NotRootInfrastructure()
    {
        var memoryRoot = Path.Combine(
            RepositoryRoot,
            "src/Features/Agents/Application/Memory");
        Assert.True(Directory.Exists(memoryRoot));

        var rootInfrastructure = Path.Combine(
            RepositoryRoot,
            "src/Infrastructure/Memory");
        Assert.False(Directory.Exists(rootInfrastructure));
    }

    [Fact]
    public void MemoryPipelineFiles_DoNotWriteConversationStore()
    {
        var memoryRoot = Path.Combine(
            RepositoryRoot,
            "src/Features/Agents/Application/Memory");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(memoryRoot, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (text.Contains("IConversationStore", StringComparison.Ordinal)
                || text.Contains("ConversationPersistenceService", StringComparison.Ordinal))
            {
                violations.Add(Path.GetRelativePath(RepositoryRoot, file));
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void ContextSourcePolicyMatrix_DefinesDurableMemorySource()
    {
        Assert.True(AgentContextSourcePolicyMatrix.DefinesSource(AgentContextSourceId.DurableMemory));
        Assert.True(AgentContextSourcePolicyMatrix.IsSourceIncluded(
            AgentContextSourceId.DurableMemory,
            AgentContextPolicyLevel.Standard));
        Assert.False(AgentContextSourcePolicyMatrix.IsSourceIncluded(
            AgentContextSourceId.DurableMemory,
            AgentContextPolicyLevel.Minimal));
    }

    [Fact]
    public void MemoryRetrieval_IntegratesOnlyThroughContextManifestBuilder()
    {
        var memoryRoot = Path.Combine(
            RepositoryRoot,
            "src/Features/Agents/Application/Memory");
        var manifestBuilderPath = Path.Combine(
            RepositoryRoot,
            "src/Features/Agents/Application/AgentContextManifestBuilder.cs");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(memoryRoot, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (text.Contains("AgentContextManifest", StringComparison.Ordinal)
                || text.Contains("IAgentContext", StringComparison.Ordinal)
                || text.Contains("embedding", StringComparison.OrdinalIgnoreCase)
                || text.Contains("vector", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(Path.GetRelativePath(RepositoryRoot, file));
            }
        }

        Assert.Empty(violations);
        Assert.Contains("AppendMemoryCandidates", File.ReadAllText(manifestBuilderPath), StringComparison.Ordinal);
        Assert.Contains("DurableMemory", File.ReadAllText(manifestBuilderPath), StringComparison.Ordinal);
    }

    [Fact]
    public void MemoryScopes_IncludeAllRequiredAdmittedScopes()
    {
        var scopes = Enum.GetValues<AgentMemoryScope>();
        var names = scopes.Select(s => s.ToString()).ToArray();

        Assert.Contains("Session", names);
        Assert.Contains("Agent", names);
        Assert.Contains("Conversation", names);
        Assert.Contains("ProjectShared", names);
        Assert.Equal(4, scopes.Length);
    }

    [Fact]
    public void MemoryOperations_IncludeInspectCorrectDisableSupersedeDelete()
    {
        var operations = Enum.GetValues<AgentMemoryOperationKind>();
        var names = operations.Select(o => o.ToString()).ToArray();

        Assert.Contains("Create", names);
        Assert.Contains("Correct", names);
        Assert.Contains("Disable", names);
        Assert.Contains("Supersede", names);
        Assert.Contains("Delete", names);
        Assert.Equal(5, operations.Length);
    }

    [Fact]
    public void MemoryCoordinator_EnforcesCrossWorkspaceDenial()
    {
        var source = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src/Features/Agents/Application/Memory/AgentMemoryCoordinator.cs"));

        Assert.Contains("WorkspaceDenied", source, StringComparison.Ordinal);
        Assert.Contains("Cross-workspace memory access is denied", source, StringComparison.Ordinal);
    }
}
