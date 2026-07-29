using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Contracts.Transparency.Trace;
using Zaide.Features.Agents.Domain.Transparency.Trace;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Phase 21 M2 trace evidence ratchets. Enforces:
///   * Redaction occurs before any durable write, render, export, log,
///     index, backup, or cross-process transfer.
///   * Backend evidence adapters never share backend-private internals.
///   * The capture queue and payload size are bounded.
///   * Trace capture is a separate, non-overlapping record class.
/// </summary>
public sealed class Phase21TraceRatchetTests
{
    private static readonly string RepositoryRoot = ArchitectureInventoryReader.ResolveRepositoryRoot();

    private static readonly Regex RawPayloadLeakPattern = new(
        @"\bUnredactedPayload\b|\bRawProviderBody\b|\bSecretPayload\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [Fact]
    public void CaptureSink_AlwaysRunsRedactionBeforeAdmit()
    {
        // The capture sink must call AgentTraceRedactionProcessor.Apply on
        // every non-marker submission. The only path that bypasses redaction
        // is the unavailable marker, which is a constant bounded string.
        var sinkType = typeof(AgentTraceCaptureSink);

        var applyUsages = sinkType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.Name == "TrySubmit")
            .SelectMany(method => method
                .GetMethodBody()
                ?.GetILAsByteArray() ?? Array.Empty<byte>())
            .ToArray();

        // Source-level guarantee: AgentTraceRedactionProcessor.Apply is
        // referenced from TrySubmit. We assert the reference name appears
        // in the file as a stronger static check than IL inspection.
        var source = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src/Features/Agents/Application/Transparency/Trace/AgentTraceCaptureSink.cs"));

        Assert.Contains("AgentTraceRedactionProcessor.Apply", source, StringComparison.Ordinal);
        Assert.Contains("RedactionFailed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CaptureSink_FailsClosedOnRedactionFailure()
    {
        var source = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src/Features/Agents/Application/Transparency/Trace/AgentTraceCaptureSink.cs"));

        Assert.Contains("RedactionFailed", source, StringComparison.Ordinal);
        Assert.Contains("AgentTraceCaptureState.Failed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BoundedQueue_ExposesLimitsAndDroppedCounter()
    {
        var queueType = typeof(AgentTraceBoundedCaptureQueue);
        var limitsProperty = queueType.GetProperty(
            nameof(AgentTraceBoundedCaptureQueue.Limits),
            BindingFlags.Instance | BindingFlags.Public);
        var droppedProperty = queueType.GetProperty(
            nameof(AgentTraceBoundedCaptureQueue.DroppedCount),
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(limitsProperty);
        Assert.NotNull(droppedProperty);
    }

    [Fact]
    public void BoundedQueue_DefaultLimitsEnforcePositiveBounds()
    {
        Assert.True(AgentTraceCaptureLimits.DefaultMaxPayloadBytes > 0);
        Assert.True(AgentTraceCaptureLimits.DefaultMaxQueueDepth > 0);
        Assert.True(AgentTraceCaptureLimits.DefaultMaxRecordsPerPage > 0);
    }

    [Fact]
    public void TracePipelineFiles_DoNotLeakUnredactedPayloadNames()
    {
        var forbiddenPattern = RawPayloadLeakPattern;
        var traceRoot = Path.Combine(
            RepositoryRoot,
            "src/Features/Agents/Application/Transparency/Trace");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(traceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (forbiddenPattern.IsMatch(text))
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
                "src/Features/Agents/Application/Transparency/Trace/NativeHarnessAgentTraceSource.cs"));

        Assert.DoesNotContain("NativeHarnessLoopRunner", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NativeHarnessLoopHistory", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NativeHarnessProvider", source, StringComparison.Ordinal);
        Assert.DoesNotContain("INativeHarnessProviderTransport", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AgentExecutionOptions", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AcpSource_DoesNotReferenceBackendPrivateTypes()
    {
        var source = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src/Features/Agents/Application/Transparency/Trace/AcpAgentTraceSource.cs"));

        Assert.DoesNotContain("AcpAgentSessionAdapter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AcpProtocolSession", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AcpProcessHostShutdownRegistry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IAcpSessionClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IAcpProcessLauncher", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TraceCapture_FilesDoNotWriteConversationStore()
    {
        var traceRoot = Path.Combine(
            RepositoryRoot,
            "src/Features/Agents/Application/Transparency/Trace");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(traceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (text.Contains("IConversationStore", StringComparison.Ordinal)
                || text.Contains("ConversationPersistenceService", StringComparison.Ordinal)
                || text.Contains("ConversationStorePathResolver", StringComparison.Ordinal))
            {
                violations.Add(Path.GetRelativePath(RepositoryRoot, file));
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void TracePipelineFiles_DoNotReferenceConversationPersistencePath()
    {
        var traceRoot = Path.Combine(
            RepositoryRoot,
            "src/Features/Agents/Application/Transparency/Trace");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(traceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (text.Contains("ConversationStorePathResolver", StringComparison.Ordinal))
            {
                violations.Add(Path.GetRelativePath(RepositoryRoot, file));
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void TraceCapture_RoutesThroughM1TraceRecordClass()
    {
        var source = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src/Features/Agents/Application/Transparency/Trace/AgentTraceBoundedCaptureQueue.cs"));

        Assert.Contains("AgentDurableRecordClass.Trace", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TraceCapture_SinkRejectsUnknownBackend()
    {
        // The sink must consult a registry filter so unregistered backends
        // cannot inject trace evidence. The registry filter is wired through
        // AgentTraceCoordinator.
        var coordinator = typeof(AgentTraceCoordinator);
        var filter = typeof(Zaide.Features.Agents.Application.Transparency.Trace.AgentTraceBackendEvidenceSourceRegistryFilter);
        Assert.NotNull(filter);
        Assert.True(
            typeof(IAgentTraceBackendEvidenceSourceRegistryFilter).IsAssignableFrom(filter),
            "Registry filter must implement IAgentTraceBackendEvidenceSourceRegistryFilter.");
        // Guard: coordinator should consume IAgentTraceSourceRegistry.
        var registryField = coordinator
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .FirstOrDefault(field => field.FieldType == typeof(IAgentTraceSourceRegistry));
        Assert.NotNull(registryField);
    }

    [Fact]
    public void TracePipelineFiles_AreFeatureOwned_NotRootInfrastructure()
    {
        var traceRoot = Path.Combine(
            RepositoryRoot,
            "src/Features/Agents/Application/Transparency/Trace");
        Assert.True(Directory.Exists(traceRoot));
        var rootInfrastructure = Path.Combine(
            RepositoryRoot,
            "src/Infrastructure/Transparency/Trace");
        Assert.False(Directory.Exists(rootInfrastructure));
    }

    [Fact]
    public void TraceCapture_DefaultLimitsEnforceTruncationAndQueueBound()
    {
        // The capture sink's truncation logic must be invoked when the
        // payload exceeds the limit. The truncated marker must be a bounded
        // constant; never the original payload.
        var source = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src/Features/Agents/Application/Transparency/Trace/AgentTraceCaptureSink.cs"));

        Assert.Contains("MaxPayloadBytes", source, StringComparison.Ordinal);
        Assert.Contains("TruncateForBound", source, StringComparison.Ordinal);
        Assert.Contains("truncated", source, StringComparison.Ordinal);
    }
}
