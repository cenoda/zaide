using System;
using Xunit;
using Zaide.Features.Agents.Application.Transparency.Usage;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Contracts.Transparency.Usage;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;

namespace Zaide.Tests.Features.Agents.Transparency.Usage;

public sealed class Phase21UsageBackendAdapterTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly AgentDurableWorkspaceStorageKey _workspaceKey;
    private readonly AgentDurableRecordFileStore _store;
    private readonly AgentUsageCaptureSink _sink;
    private readonly AgentUsageCoordinator _coordinator;
    private readonly AgentUsageBackendEvidenceSourceWriter _writer;
    private readonly NativeHarnessAgentUsageSource _nativeHarnessSource;
    private readonly AcpAgentUsageSource _acpSource;

    public Phase21UsageBackendAdapterTests()
    {
        (_rootDirectory, _workspaceKey) = Phase21UsageTestSupport.CreateWorkspaceFixture();
        _store = Phase21UsageTestSupport.CreateStore(_rootDirectory);
        _sink = Phase21UsageTestSupport.CreateSink(_store);
        var resolver = Phase21UsageTestSupport.CreateKeyResolver();
        _coordinator = new AgentUsageCoordinator(
            _sink,
            new AgentUsageInspector(_store),
            resolver);
        _writer = new AgentUsageBackendEvidenceSourceWriter(_coordinator);
        _nativeHarnessSource = new NativeHarnessAgentUsageSource(_writer);
        _acpSource = new AcpAgentUsageSource(_writer);
        _sink.EnableCapture();
    }

    public void Dispose()
    {
        _store.Dispose();
        Phase21UsageTestSupport.DeleteDirectory(_rootDirectory);
    }

    [Fact]
    public void NativeHarnessSource_ExposesExpectedBackendId()
    {
        Assert.Equal(AgentBackendIds.NativeHarnessValue, _nativeHarnessSource.BackendId);
    }

    [Fact]
    public void AcpSource_ExposesExpectedBackendId()
    {
        Assert.Equal(AgentBackendIds.AcpValue, _acpSource.BackendId);
    }

    [Fact]
    public void NativeHarnessSource_CanExposeTokenAndCostKinds()
    {
        Assert.True(_nativeHarnessSource.CanExpose(AgentUsageKind.TokensInput));
        Assert.True(_nativeHarnessSource.CanExpose(AgentUsageKind.TokensOutput));
        Assert.True(_nativeHarnessSource.CanExpose(AgentUsageKind.TotalTokens));
        Assert.True(_nativeHarnessSource.CanExpose(AgentUsageKind.EstimatedCost));
        Assert.True(_nativeHarnessSource.CanExpose(AgentUsageKind.RequestCount));
    }

    [Fact]
    public void AcpSource_CanExposeTokenAndCostKinds()
    {
        Assert.True(_acpSource.CanExpose(AgentUsageKind.TokensInput));
        Assert.True(_acpSource.CanExpose(AgentUsageKind.TotalTokens));
        Assert.True(_acpSource.CanExpose(AgentUsageKind.EstimatedCost));
        Assert.True(_acpSource.CanExpose(AgentUsageKind.RequestCount));
    }

    [Fact]
    public void NativeHarnessSource_SubmitsTokenUsage()
    {
        var request = Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            AgentBackendIds.NativeHarnessValue,
            AgentUsageKind.TotalTokens,
            AgentUsageValueOrigin.Reported,
            "tokens", "count", 1500,
            idempotencyKey: "nh-tokens-1");

        var result = _nativeHarnessSource.Submit(request);

        Assert.Equal(AgentUsageCaptureStatus.Accepted, result.Status);
        var records = Phase21UsageTestSupport.ReplayUsageRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.Contains("1500", single.PayloadJson);
    }

    [Fact]
    public void AcpSource_SubmitsTokenUsage()
    {
        var request = Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            AgentBackendIds.AcpValue,
            AgentUsageKind.TotalTokens,
            AgentUsageValueOrigin.Reported,
            "tokens", "count", 2500,
            idempotencyKey: "acp-tokens-1");

        var result = _acpSource.Submit(request);

        Assert.Equal(AgentUsageCaptureStatus.Accepted, result.Status);
        var records = Phase21UsageTestSupport.ReplayUsageRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.Contains("2500", single.PayloadJson);
    }

    [Fact]
    public void NativeHarnessSource_RejectsUnsupportedKind()
    {
        // InvoicedCost is admitted by the ledger but not claimed by the
        // native-harness evidence surface (no invoice path).
        var request = Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            AgentBackendIds.NativeHarnessValue,
            AgentUsageKind.InvoicedCost,
            AgentUsageValueOrigin.Invoiced,
            "invoice", "USD", 1.0m,
            currency: "USD",
            idempotencyKey: "nh-reject");

        var result = _nativeHarnessSource.Submit(request);

        Assert.Equal(AgentUsageCaptureStatus.InvalidRequest, result.Status);
    }

    [Fact]
    public void AcpSource_SubmitsCostWithPricingInfo()
    {
        var request = Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            AgentBackendIds.AcpValue,
            AgentUsageKind.EstimatedCost,
            AgentUsageValueOrigin.Estimated,
            "cost", "USD", 0.008m,
            model: "claude-3",
            pricingSourceId: "anthropic-2026-07",
            pricingSourceVersion: 1,
            pricingFormula: "input_tokens * 0.000015 + output_tokens * 0.000075",
            currency: "USD",
            idempotencyKey: "acp-cost-1");

        var result = _acpSource.Submit(request);

        Assert.Equal(AgentUsageCaptureStatus.Accepted, result.Status);
        var records = Phase21UsageTestSupport.ReplayUsageRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.Contains("anthropic-2026-07", single.PayloadJson);
        Assert.Contains("USD", single.PayloadJson);
        Assert.Contains("claude-3", single.PayloadJson);
    }

    [Fact]
    public void BackendSources_AreIndependentSiblings()
    {
        var nhRequest = Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            AgentBackendIds.NativeHarnessValue,
            AgentUsageKind.RequestCount,
            AgentUsageValueOrigin.Reported,
            "requests", "count", 5,
            idempotencyKey: "sibling-nh-1");
        var acpRequest = Phase21UsageTestSupport.CreateRequest(
            _workspaceKey,
            AgentBackendIds.AcpValue,
            AgentUsageKind.RequestCount,
            AgentUsageValueOrigin.Reported,
            "requests", "count", 3,
            idempotencyKey: "sibling-acp-1");

        _nativeHarnessSource.Submit(nhRequest);
        _acpSource.Submit(acpRequest);

        var records = Phase21UsageTestSupport.ReplayUsageRecords(_store, _workspaceKey);
        Assert.Equal(2, records.Count);
    }
}
