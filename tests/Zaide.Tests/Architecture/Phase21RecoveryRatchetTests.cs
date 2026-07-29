using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Zaide.Features.Agents.Application.Continuity;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Tests.Architecture;

public sealed class Phase21RecoveryRatchetTests
{
    private static readonly string RepositoryRoot = ArchitectureInventoryReader.ResolveRepositoryRoot();

    [Fact]
    public void ContinuityPipeline_RoutesThroughM1SessionRecoveryRecordClass()
    {
        var source = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src/Features/Agents/Application/Continuity/AgentSessionContinuityCheckpointWriter.cs"));

        Assert.Contains("AgentDurableRecordClass.SessionRecovery", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ContinuityPipelineFiles_AreFeatureOwned_NotRootInfrastructure()
    {
        var continuityRoot = Path.Combine(
            RepositoryRoot,
            "src/Features/Agents/Application/Continuity");
        Assert.True(Directory.Exists(continuityRoot));

        var rootInfrastructure = Path.Combine(
            RepositoryRoot,
            "src/Infrastructure/Continuity");
        Assert.False(Directory.Exists(rootInfrastructure));
    }

    [Fact]
    public void ContinuityPipelineFiles_DoNotWriteConversationStore()
    {
        var continuityRoot = Path.Combine(
            RepositoryRoot,
            "src/Features/Agents/Application/Continuity");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(continuityRoot, "*.cs", SearchOption.AllDirectories))
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
    public void NativeHarnessContinuityAdapter_DoesNotReferenceAcpPrivateTypes()
    {
        var source = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src/Features/Agents/Application/Continuity/NativeHarnessAgentContinuityAdapter.cs"));

        Assert.DoesNotContain("AcpAgentSessionAdapter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AcpProtocolSession", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AcpContinuityAdapter_DoesNotReferenceNativeHarnessPrivateTypes()
    {
        var source = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src/Features/Agents/Application/Continuity/AcpAgentContinuityAdapter.cs"));

        Assert.DoesNotContain("NativeHarnessLoopRunner", source, StringComparison.Ordinal);
        Assert.DoesNotContain("INativeHarnessProviderTransport", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BackendCapabilityMatrix_DefinesBothSiblingBackends()
    {
        var rows = AgentBackendContinuityCapabilityMatrix.Rows;
        Assert.Equal(2, rows.Count);
        Assert.Equal(
            new[] { "backend:zaide-native-harness", "backend:acp" }.OrderBy(id => id, StringComparer.Ordinal),
            rows.Select(row => row.BackendId).OrderBy(id => id, StringComparer.Ordinal));
    }
}
