using System;
using System.Linq;
using Xunit;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Application.Transparency.Usage;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;

namespace Zaide.Tests.Features.Agents.Transparency.Usage;

public sealed class Phase21CostEvidenceTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly AgentDurableWorkspaceStorageKey _workspaceKey;
    private readonly AgentDurableRecordFileStore _store;
    private readonly AgentUsageCaptureSink _sink;
    private readonly AgentUsageInspector _inspector;

    public Phase21CostEvidenceTests()
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
    public void CapturedCost_PreservesCurrencyAndPricingSource()
    {
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.EstimatedCost,
            AgentUsageValueOrigin.Estimated,
            "cost", "USD", 0.0045m,
            model: "gpt-4",
            pricingSourceId: "openai-2026-07",
            pricingSourceVersion: 1,
            pricingFormula: "input_tokens * 0.00001 + output_tokens * 0.00003",
            currency: "USD",
            idempotencyKey: "cost-pricing-1"));

        var records = Phase21UsageTestSupport.ReplayUsageRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.Contains("USD", single.PayloadJson);
        Assert.Contains("openai-2026-07", single.PayloadJson);
        Assert.Contains("input_tokens", single.PayloadJson);
    }

    [Fact]
    public void CapturedCost_DistinguishesOrigin()
    {
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.EstimatedCost,
            AgentUsageValueOrigin.Estimated,
            "cost", "USD", 0.01m,
            idempotencyKey: "origin-est"));
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.InvoicedCost,
            AgentUsageValueOrigin.Invoiced,
            "cost", "USD", 0.02m,
            idempotencyKey: "origin-inv"));

        var summary = _inspector.GetSummary(_workspaceKey);
        Assert.Equal(2, summary.TotalRecords);
        Assert.Equal(1, summary.CountsByOrigin[AgentUsageValueOrigin.Estimated]);
        Assert.Equal(1, summary.CountsByOrigin[AgentUsageValueOrigin.Invoiced]);
    }

    [Fact]
    public void CapturedCost_WithDisputedOriginIsPreserved()
    {
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.InvoicedCost,
            AgentUsageValueOrigin.Disputed,
            "cost", "USD", 0.05m,
            idempotencyKey: "disputed-1"));

        var records = Phase21UsageTestSupport.ReplayUsageRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.Contains("disputed", single.PayloadJson.ToLowerInvariant());
    }

    [Fact]
    public void CapturedCost_WithUnavailableOriginDoesNotDefaultToZero()
    {
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.TotalCost,
            AgentUsageValueOrigin.Unavailable,
            "cost", "USD", 0,
            idempotencyKey: "cost-unavailable-1"));

        var records = Phase21UsageTestSupport.ReplayUsageRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.Contains("unavailable", single.PayloadJson.ToLowerInvariant());
        Assert.Contains("totalCost", single.PayloadJson);
    }

    [Fact]
    public void Summary_TracksTotalCostValueAndCurrency()
    {
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.EstimatedCost,
            AgentUsageValueOrigin.Estimated,
            "cost", "USD", 0.01m,
            currency: "USD",
            idempotencyKey: "summary-cost-1",
            aggregationSemantics: AgentUsageAggregationSemantics.Delta));
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.EstimatedCost,
            AgentUsageValueOrigin.Estimated,
            "cost", "USD", 0.02m,
            currency: "USD",
            idempotencyKey: "summary-cost-2",
            aggregationSemantics: AgentUsageAggregationSemantics.Delta));

        var summary = _inspector.GetSummary(_workspaceKey);
        Assert.True(summary.HasVerifiedTotalCost);
        Assert.Equal(0.03m, summary.TotalCostValue);
        Assert.Equal("USD", summary.TotalCostCurrency);
    }

    [Fact]
    public void Summary_ExcludesUnknownAggregationFromVerifiedCostTotal()
    {
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.EstimatedCost,
            AgentUsageValueOrigin.Estimated,
            "cost", "USD", 0.01m,
            currency: "USD",
            idempotencyKey: "summary-unknown-1"));

        var summary = _inspector.GetSummary(_workspaceKey);
        Assert.Equal(1, summary.TotalRecords);
        Assert.False(summary.HasVerifiedTotalCost);
        Assert.Equal(0m, summary.TotalCostValue);
        Assert.Null(summary.TotalCostCurrency);
    }

    [Fact]
    public void Summary_UsesLatestCumulativeCostPerSession()
    {
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.AcpBackendId,
            AgentUsageKind.TotalCost,
            AgentUsageValueOrigin.Reported,
            "session_cost", "USD", 0.10m,
            currency: "USD",
            idempotencyKey: "cum-1",
            aggregationSemantics: AgentUsageAggregationSemantics.Cumulative));
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.AcpBackendId,
            AgentUsageKind.TotalCost,
            AgentUsageValueOrigin.Reported,
            "session_cost", "USD", 0.25m,
            currency: "USD",
            idempotencyKey: "cum-2",
            aggregationSemantics: AgentUsageAggregationSemantics.Cumulative));

        var summary = _inspector.GetSummary(_workspaceKey);
        Assert.True(summary.HasVerifiedTotalCost);
        Assert.Equal(0.25m, summary.TotalCostValue);
        Assert.Equal("USD", summary.TotalCostCurrency);
    }

    [Fact]
    public void Coordinator_ResolvesUnboundKeyForNullRoot()
    {
        var resolver = Phase21UsageTestSupport.CreateKeyResolver();
        var coordinator = new AgentUsageCoordinator(_sink, _inspector, resolver);

        var summary = coordinator.GetSummary(workspaceRoot: null);

        Assert.True(summary.IsEmpty);
    }

    [Fact]
    public void Inspector_ReturnsRecordsInOrderingSequence()
    {
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.RequestCount,
            AgentUsageValueOrigin.Reported,
            "requests", "count", 1,
            idempotencyKey: "order-1"));
        _sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.TotalTokens,
            AgentUsageValueOrigin.Reported,
            "tokens", "count", 2000,
            idempotencyKey: "order-2"));

        var records = Phase21UsageTestSupport.ReplayUsageRecords(_store, _workspaceKey);
        Assert.Equal(2, records.Count);
        Assert.Equal(1L, records[0].OrderingSequence);
        Assert.Equal(2L, records[1].OrderingSequence);
    }
}
