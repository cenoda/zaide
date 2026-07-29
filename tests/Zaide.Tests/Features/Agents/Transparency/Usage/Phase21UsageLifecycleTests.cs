using System.Linq;
using Xunit;
using Zaide.Features.Agents.Application.Transparency.Usage;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;

namespace Zaide.Tests.Features.Agents.Transparency.Usage;

public sealed class Phase21UsageLifecycleTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly AgentDurableWorkspaceStorageKey _workspaceKey;
    private readonly AgentDurableRecordFileStore _store;
    private readonly AgentUsageCaptureSink _sink;
    private readonly AgentUsageInspector _inspector;

    public Phase21UsageLifecycleTests()
    {
        (_rootDirectory, _workspaceKey) = Phase21UsageTestSupport.CreateWorkspaceFixture();
        _store = Phase21UsageTestSupport.CreateStore(_rootDirectory);
        var resolver = Phase21UsageTestSupport.CreateKeyResolver();
        _sink = Phase21UsageTestSupport.CreateSink(_store);
        _inspector = new AgentUsageInspector(_store);
        _sink.EnableCapture();
    }

    public void Dispose()
    {
        _store.Dispose();
        Phase21UsageTestSupport.DeleteDirectory(_rootDirectory);
    }

    [Fact]
    public void Capture_DefaultsToDisabledWhenEnableNotCalled()
    {
        using var localStore = Phase21UsageTestSupport.CreateStore(_rootDirectory);
        var localSink = Phase21UsageTestSupport.CreateSink(localStore);

        var result = localSink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.TotalTokens,
            AgentUsageValueOrigin.Reported,
            "tokens", "count", 1500));

        Assert.Equal(AgentUsageCaptureStatus.Disabled, result.Status);
    }

    [Fact]
    public void Capture_AdmittedTokenUsageIsPersisted()
    {
        var result = _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.TotalTokens,
            AgentUsageValueOrigin.Reported,
            "tokens", "count", 1500,
            idempotencyKey: "usage-tokens-1"));

        Assert.Equal(AgentUsageCaptureStatus.Accepted, result.Status);

        var records = Phase21UsageTestSupport.ReplayUsageRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.Contains("1500", single.PayloadJson);
        Assert.Contains("tokens", single.PayloadJson);
    }

    [Fact]
    public void Capture_NeverDefaultsMissingCostToZero()
    {
        var result = _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.TotalCost,
            AgentUsageValueOrigin.Unavailable,
            "cost", "USD", 0,
            idempotencyKey: "usage-zero-cost"));

        Assert.Equal(AgentUsageCaptureStatus.Accepted, result.Status);

        var records = Phase21UsageTestSupport.ReplayUsageRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.Contains("unavailable", single.PayloadJson.ToLowerInvariant());
    }

    [Fact]
    public void Capture_RejectsCostWithZeroAndReportedOrigin()
    {
        var result = _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.EstimatedCost,
            AgentUsageValueOrigin.Reported,
            "cost", "USD", 0,
            idempotencyKey: "usage-zero-reported"));

        Assert.Equal(AgentUsageCaptureStatus.InvalidRequest, result.Status);
    }

    [Fact]
    public void Capture_DuplicateIdempotencyKeyIsIgnored()
    {
        var result1 = _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.TotalTokens,
            AgentUsageValueOrigin.Reported,
            "tokens", "count", 1500,
            idempotencyKey: "dup-key"));

        Assert.Equal(AgentUsageCaptureStatus.Accepted, result1.Status);

        var result2 = _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.TotalTokens,
            AgentUsageValueOrigin.Reported,
            "tokens", "count", 9999,
            idempotencyKey: "dup-key"));

        Assert.Equal(AgentUsageCaptureStatus.DuplicateIgnored, result2.Status);
    }

    [Fact]
    public void Inspector_ReturnsSummaryAndRecordsForWorkspace()
    {
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.TotalTokens,
            AgentUsageValueOrigin.Reported,
            "tokens", "count", 1500,
            idempotencyKey: "summary-1"));

        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.EstimatedCost,
            AgentUsageValueOrigin.Estimated,
            "cost", "USD", 0.003m,
            model: "gpt-4",
            pricingSourceId: "openai-2026-07",
            pricingVersion: 1,
            formula: "input_tokens * 0.00001 + output_tokens * 0.00003",
            currency: "USD",
            idempotencyKey: "summary-2"));

        var summary = _inspector.GetSummary(_workspaceKey);
        Assert.Equal(2, summary.TotalRecords);
        Assert.Equal(2, summary.CountsByOrigin.Count);
        Assert.Equal(Phase21UsageTestSupport.NativeHarnessBackendId, summary.CountsByBackend.Keys.Single());

        var page = _inspector.GetRecords(_workspaceKey, afterOrderingSequence: 0, maxRecords: 1);
        Assert.Single(page);
        Assert.Equal(1L, page[0].OrderingSequence);

        var page2 = _inspector.GetRecords(_workspaceKey, afterOrderingSequence: 1, maxRecords: 1);
        Assert.Single(page2);
        Assert.Equal(2L, page2[0].OrderingSequence);
    }

    [Fact]
    public void Inspector_SummaryIsEmptyForUnknownWorkspace()
    {
        var unknownKey = AgentDurableWorkspaceStorageKey.FromValue("ws:0123456789abcdef");
        var summary = _inspector.GetSummary(unknownKey);
        Assert.True(summary.IsEmpty);
    }

    [Fact]
    public void Inspector_RejectsZeroMaxRecords()
    {
        var records = _inspector.GetRecords(_workspaceKey, afterOrderingSequence: 0, maxRecords: 0);
        Assert.Empty(records);
    }

    [Fact]
    public void Capture_UsageClassIsSeparateFromTraceAndAuditClasses()
    {
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.RequestCount,
            AgentUsageValueOrigin.Reported,
            "requests", "count", 1,
            idempotencyKey: "class-sep-1"));

        var usageRecords = _store.Replay(new AgentDurableRecordReplayRequest(
            _workspaceKey, AgentDurableRecordClass.Usage));
        var traceRecords = _store.Replay(new AgentDurableRecordReplayRequest(
            _workspaceKey, AgentDurableRecordClass.Trace));
        var auditRecords = _store.Replay(new AgentDurableRecordReplayRequest(
            _workspaceKey, AgentDurableRecordClass.Audit));

        Assert.Single(usageRecords.Records);
        Assert.Empty(traceRecords.Records);
        Assert.Empty(auditRecords.Records);
    }

    [Fact]
    public void Capture_ScopeReferencesArePersisted()
    {
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.LatencyMs,
            AgentUsageValueOrigin.Reported,
            "latency", "ms", 1200,
            idempotencyKey: "scope-1"));

        var records = Phase21UsageTestSupport.ReplayUsageRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.Equal("conversation:usage-test", single.ScopeReferences.ConversationId);
        Assert.Equal("session:usage-test", single.ScopeReferences.SessionId);
        Assert.Equal("run:usage-test", single.ScopeReferences.RunId);
    }
}
