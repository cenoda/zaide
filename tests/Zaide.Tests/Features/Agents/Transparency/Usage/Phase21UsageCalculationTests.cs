using System;
using System.Linq;
using Xunit;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Application.Transparency.Usage;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;

namespace Zaide.Tests.Features.Agents.Transparency.Usage;

public sealed class Phase21UsageCalculationTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly AgentDurableWorkspaceStorageKey _workspaceKey;
    private readonly AgentDurableRecordFileStore _store;
    private readonly AgentUsageCaptureSink _sink;
    private readonly AgentUsageInspector _inspector;

    public Phase21UsageCalculationTests()
    {
        (_rootDirectory, _workspaceKey) = Phase21UsageTestSupport.CreateWorkspaceFixture();
        _store = Phase21UsageTestSupport.CreateStore(_rootDirectory);
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
    public void CalculatedCost_PreservesFormulaAndSourceVersion()
    {
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.EstimatedCost,
            AgentUsageValueOrigin.Calculated,
            "cost", "USD", 0.015m,
            pricingSourceId: "openai-2026-07",
            pricingSourceVersion: 2,
            pricingFormula: "(input_tokens * 0.000005) + (output_tokens * 0.000015)",
            currency: "USD",
            idempotencyKey: "calc-cost-1"));

        var records = Phase21UsageTestSupport.ReplayUsageRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.Contains("calculated", single.PayloadJson.ToLowerInvariant());
        Assert.Contains("input_tokens * 0.000005", single.PayloadJson);
        Assert.Contains("\"pricingSourceVersion\":2", single.PayloadJson);
    }

    [Fact]
    public void MeasuredLatency_PreservesUnitAndValue()
    {
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.LatencyMs,
            AgentUsageValueOrigin.Measured,
            "latency", "ms", 3400,
            idempotencyKey: "latency-1"));

        var records = Phase21UsageTestSupport.ReplayUsageRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.Contains("3400", single.PayloadJson);
        Assert.Contains("ms", single.PayloadJson);
        Assert.Contains("measured", single.PayloadJson.ToLowerInvariant());
    }

    [Fact]
    public void ReportedTokens_PreservesModelAttribution()
    {
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.TokensInput,
            AgentUsageValueOrigin.Reported,
            "tokens_input", "count", 850,
            model: "gpt-4-turbo",
            idempotencyKey: "model-attr-1"));

        var records = Phase21UsageTestSupport.ReplayUsageRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.Contains("gpt-4-turbo", single.PayloadJson);
        Assert.Contains("850", single.PayloadJson);
    }

    [Fact]
    public void Inspector_SummaryGroupsByOrigin()
    {
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.TotalTokens,
            AgentUsageValueOrigin.Reported,
            "tokens", "count", 100,
            idempotencyKey: "grp-1"));
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.EstimatedCost,
            AgentUsageValueOrigin.Calculated,
            "cost", "USD", 0.001m,
            currency: "USD",
            idempotencyKey: "grp-2"));
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.EstimatedCost,
            AgentUsageValueOrigin.Estimated,
            "cost", "USD", 0.002m,
            currency: "USD",
            idempotencyKey: "grp-3"));

        var summary = _inspector.GetSummary(_workspaceKey);
        Assert.Equal(3, summary.TotalRecords);
        Assert.Equal(3, summary.CountsByOrigin.Count);
        Assert.Equal(1, summary.CountsByOrigin[AgentUsageValueOrigin.Reported]);
        Assert.Equal(1, summary.CountsByOrigin[AgentUsageValueOrigin.Calculated]);
        Assert.Equal(1, summary.CountsByOrigin[AgentUsageValueOrigin.Estimated]);
    }
}
