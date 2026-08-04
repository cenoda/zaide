using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Zaide.Features.Agents.Application.Transparency.Usage;
using Zaide.Features.Agents.Contracts.Transparency.Usage;
using Zaide.Features.Agents.Domain.Transparency.Usage;

namespace Zaide.Tests.Architecture;

public sealed class Phase21UsageRatchetTests
{
    private static readonly string RepositoryRoot = ArchitectureInventoryReader.ResolveRepositoryRoot();

    [Fact]
    public void UsageCaptureSink_RejectsZeroCostWhenNotUnavailable()
    {
        var sinkType = typeof(AgentUsageCaptureSink);
        var source = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src/Features/Agents/Application/Transparency/Usage/AgentUsageCaptureSink.cs"));

        Assert.Contains("InvalidRequest", source, StringComparison.Ordinal);
        Assert.Contains("Cost value must not be zero", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UsagePipelineFiles_AreFeatureOwned_NotRootInfrastructure()
    {
        var usageRoot = Path.Combine(
            RepositoryRoot,
            "src/Features/Agents/Application/Transparency/Usage");
        Assert.True(Directory.Exists(usageRoot));

        var rootInfrastructure = Path.Combine(
            RepositoryRoot,
            "src/Infrastructure/Transparency/Usage");
        Assert.False(Directory.Exists(rootInfrastructure));
    }

    [Fact]
    public void UsagePipeline_RoutesThroughM1UsageRecordClass()
    {
        var source = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src/Features/Agents/Application/Transparency/Usage/AgentUsageCaptureSink.cs"));

        Assert.Contains("AgentDurableRecordClass.Usage", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UsagePipelineFiles_DoNotWriteConversationStore()
    {
        var usageRoot = Path.Combine(
            RepositoryRoot,
            "src/Features/Agents/Application/Transparency/Usage");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(usageRoot, "*.cs", SearchOption.AllDirectories))
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
    public void NativeHarnessSource_DoesNotReferenceBackendPrivateTypes()
    {
        var source = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src/Features/Agents/Application/Transparency/Usage/NativeHarnessAgentUsageSource.cs"));

        Assert.DoesNotContain("NativeHarnessLoopRunner", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NativeHarnessLoopHistory", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NativeHarnessProvider", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IAgentExecutionService", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AcpSource_DoesNotReferenceBackendPrivateTypes()
    {
        var source = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src/Features/Agents/Application/Transparency/Usage/AcpAgentUsageSource.cs"));

        Assert.DoesNotContain("AcpAgentSessionAdapter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AcpProtocolSession", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AcpProcessHostShutdownRegistry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IAcpSessionClient", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UsagePipelineFiles_DoNotReferenceTraceNamespace()
    {
        var usageRoot = Path.Combine(
            RepositoryRoot,
            "src/Features/Agents/Application/Transparency/Usage");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(usageRoot, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (text.Contains("AgentTrace", StringComparison.Ordinal)
                && !text.Contains("AgentTraceUsage", StringComparison.Ordinal)
                && !text.Contains("AgentTraceBackend", StringComparison.Ordinal))
            {
                violations.Add(Path.GetRelativePath(RepositoryRoot, file));
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void UsageCaptureLimits_EnforcePositiveBounds()
    {
        Assert.True(AgentUsageCaptureLimits.DefaultMaxRecordsPerPage > 0);
    }

    [Fact]
    public void UsageValueOrigin_IncludesAllRequiredDistinctions()
    {
        var origins = Enum.GetValues<AgentUsageValueOrigin>();
        var originNames = origins.Select(o => o.ToString()).ToArray();

        Assert.Contains("Reported", originNames);
        Assert.Contains("Measured", originNames);
        Assert.Contains("Calculated", originNames);
        Assert.Contains("Estimated", originNames);
        Assert.Contains("Invoiced", originNames);
        Assert.Contains("Unavailable", originNames);
        Assert.Contains("Disputed", originNames);
        Assert.Equal(7, origins.Length);
    }

    [Fact]
    public void UsageAggregationSemantics_IncludesLockedValues()
    {
        var values = Enum.GetValues<AgentUsageAggregationSemantics>();
        var names = values.Select(v => v.ToString()).ToArray();

        Assert.Contains("Unknown", names);
        Assert.Contains("Delta", names);
        Assert.Contains("Cumulative", names);
        Assert.Contains("PointInTime", names);
        Assert.Equal(4, values.Length);
    }
}
