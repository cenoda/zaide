using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Application.Acp;
using Zaide.Features.Agents.Application.Transparency.Usage;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Agents.Infrastructure.Acp;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Contracts;
using Zaide.Tests.Features.Agents;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Agents.Transparency.Usage;

public sealed class Phase22UsageProducerTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly AgentDurableRecordFileStore _store;
    private readonly AgentUsageBackendEvidenceSourceWriter _writer;

    public Phase22UsageProducerTests()
    {
        (_rootDirectory, _) = Phase21UsageTestSupport.CreateWorkspaceFixture();
        _store = Phase21UsageTestSupport.CreateStore(_rootDirectory);
        var sink = Phase21UsageTestSupport.CreateSink(_store);
        var coordinator = Phase21UsageTestSupport.CreateCoordinator(_store, sink);
        _writer = new AgentUsageBackendEvidenceSourceWriter(coordinator);
        sink.EnableCapture();
    }

    [Fact]
    public void NativeAndAcpSources_KeepTheirBackendIdsIndependent()
    {
        var native = new NativeHarnessAgentUsageSource(_writer);
        var acp = new AcpAgentUsageSource(_writer);

        Assert.Equal(AgentBackendIds.NativeHarnessValue, native.BackendId);
        Assert.Equal(AgentBackendIds.AcpValue, acp.BackendId);
        Assert.True(native.CanExpose(AgentUsageKind.RequestCount));
        Assert.True(acp.CanExpose(AgentUsageKind.TotalCost));
        Assert.NotEqual(native.BackendId, acp.BackendId);
    }

    [Fact]
    public async Task NativeHarnessRun_EmitsMeasuredUsageAndUnavailableCostMarkers()
    {
        var workspace = Path.Combine(_rootDirectory, "run-workspace");
        Directory.CreateDirectory(workspace);
        var transport = new ScriptedNativeHarnessProviderTransport();
        transport.Enqueue(NativeHarnessProviderResponse.Success("usage response"));

        var services = new ServiceCollection();
        Program.ConfigureServices(services);
        services.RemoveAll<IWorkspaceActionAuthority>();
        services.AddSingleton<IWorkspaceActionAuthority>(new FakeWorkspaceActionAuthority(
            FakeWorkspaceActionAuthority.CreateScopeFromDirectory(workspace)));
        services.RemoveAll<INativeHarnessProviderTransport>();
        services.AddSingleton<INativeHarnessProviderTransport>(transport);
        services.RemoveAll<INativeHarnessProviderOptionsSource>();
        services.AddSingleton<INativeHarnessProviderOptionsSource>(new FixedNativeHarnessProviderOptionsSource());

        using var provider = services.BuildServiceProvider();
        var management = provider.GetRequiredService<AgentTransparencyManagementViewModel>();
        management.ToggleUsageCaptureCommand.Execute().Subscribe(
            System.Reactive.Observer.Create<System.Reactive.Unit>(_ => { }));

        var session = provider.GetRequiredService<IAgentSessionService>();
        var store = provider.GetRequiredService<IConversationStore>();
        var catalog = ConversationsTestSupport.CreateCatalog();
        var conversation = store.GetOrCreateDirectConversation(
            catalog.CanonicalHuman.Id,
            ActorId.TownhallAgent);

        var result = await session.SendAsync(
            conversation.Id,
            ActorId.HumanUser,
            ActorId.TownhallAgent,
            AgentBackendIds.NativeHarness,
            ConversationEntryId.New(),
            "hello usage",
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Completed, result.Status);

        await management.RefreshUsageSurfaceAsync();
        var records = management.UsageInspection.Records;

        Assert.Contains(records, r =>
            r.Kind == AgentUsageKind.RequestCount
            && r.Origin == AgentUsageValueOrigin.Measured
            && r.AggregationSemantics == AgentUsageAggregationSemantics.Delta);
        Assert.Contains(records, r =>
            r.Kind == AgentUsageKind.LatencyMs
            && r.Origin == AgentUsageValueOrigin.Measured
            && r.AggregationSemantics == AgentUsageAggregationSemantics.PointInTime);
        Assert.Contains(records, r =>
            r.Kind == AgentUsageKind.TotalTokens
            && r.Origin == AgentUsageValueOrigin.Unavailable);
        Assert.Contains(records, r =>
            r.Kind == AgentUsageKind.TotalCost
            && r.Origin == AgentUsageValueOrigin.Unavailable);
        Assert.All(records, r => Assert.Equal(AgentBackendIds.NativeHarnessValue, r.BackendId));
        Assert.NotNull(management.UsageInspection.Summary);
        Assert.False(management.UsageInspection.Summary!.HasVerifiedTotalCost);
    }

    [Fact]
    public void AcpUsageUpdate_MapsPointInTimeTokensAndCumulativeCost()
    {
        var workspace = Path.Combine(_rootDirectory, "acp-workspace");
        Directory.CreateDirectory(workspace);
        var store = Phase21UsageTestSupport.CreateStore(_rootDirectory);
        var sink = Phase21UsageTestSupport.CreateSink(store);
        sink.EnableCapture();
        var coordinator = Phase21UsageTestSupport.CreateCoordinator(store, sink);
        var writer = new AgentUsageBackendEvidenceSourceWriter(coordinator);
        var acpSource = new AcpAgentUsageSource(writer);
        var workspaceKey = AgentDurableWorkspaceStorageKey.FromWorkspaceRoot(workspace);

        var used = acpSource.Submit(new AgentUsageCaptureRequest(
            workspaceKey,
            AgentBackendIds.AcpValue,
            AgentUsageKind.TotalTokens,
            AgentUsageValueOrigin.Reported,
            "context_tokens_used",
            "count",
            128,
            new AgentUsageRecordScope(
                conversationId: "conversation:acp",
                sessionId: "session:acp",
                runId: "run:acp",
                backendId: AgentBackendIds.AcpValue),
            evidenceSourceDescription: "ACP usage_update.used",
            idempotencyKey: "acp-used-1",
            aggregationSemantics: AgentUsageAggregationSemantics.PointInTime));
        Assert.Equal(AgentUsageCaptureStatus.Accepted, used.Status);

        var size = acpSource.Submit(new AgentUsageCaptureRequest(
            workspaceKey,
            AgentBackendIds.AcpValue,
            AgentUsageKind.Other,
            AgentUsageValueOrigin.Reported,
            "context_window_size",
            "count",
            200000,
            new AgentUsageRecordScope(
                conversationId: "conversation:acp",
                sessionId: "session:acp",
                runId: "run:acp",
                backendId: AgentBackendIds.AcpValue),
            evidenceSourceDescription: "ACP usage_update.size",
            idempotencyKey: "acp-size-1",
            aggregationSemantics: AgentUsageAggregationSemantics.PointInTime));
        Assert.Equal(AgentUsageCaptureStatus.Accepted, size.Status);

        var cost1 = acpSource.Submit(new AgentUsageCaptureRequest(
            workspaceKey,
            AgentBackendIds.AcpValue,
            AgentUsageKind.TotalCost,
            AgentUsageValueOrigin.Reported,
            "session_cost",
            "USD",
            0.10m,
            new AgentUsageRecordScope(
                conversationId: "conversation:acp",
                sessionId: "session:acp",
                runId: "run:acp",
                backendId: AgentBackendIds.AcpValue),
            currency: "USD",
            evidenceSourceDescription: "ACP usage_update.cost cumulative",
            idempotencyKey: "acp-cost-1",
            aggregationSemantics: AgentUsageAggregationSemantics.Cumulative));
        Assert.Equal(AgentUsageCaptureStatus.Accepted, cost1.Status);

        var cost2 = acpSource.Submit(new AgentUsageCaptureRequest(
            workspaceKey,
            AgentBackendIds.AcpValue,
            AgentUsageKind.TotalCost,
            AgentUsageValueOrigin.Reported,
            "session_cost",
            "USD",
            0.35m,
            new AgentUsageRecordScope(
                conversationId: "conversation:acp",
                sessionId: "session:acp",
                runId: "run:acp-2",
                backendId: AgentBackendIds.AcpValue),
            currency: "USD",
            evidenceSourceDescription: "ACP usage_update.cost cumulative later",
            idempotencyKey: "acp-cost-2",
            aggregationSemantics: AgentUsageAggregationSemantics.Cumulative));
        Assert.Equal(AgentUsageCaptureStatus.Accepted, cost2.Status);

        var inspector = new AgentUsageInspector(store);
        var summary = inspector.GetSummary(workspaceKey);
        Assert.True(summary.HasVerifiedTotalCost);
        // Latest cumulative snapshot wins; never sum cumulatives as deltas.
        Assert.Equal(0.35m, summary.TotalCostValue);
        Assert.Equal("USD", summary.TotalCostCurrency);

        var records = inspector.GetRecords(workspaceKey, afterOrderingSequence: 0, maxRecords: 16);
        Assert.Contains(records, r =>
            r.MetricName == "context_tokens_used"
            && r.AggregationSemantics == AgentUsageAggregationSemantics.PointInTime);
        Assert.Contains(records, r =>
            r.MetricName == "context_window_size"
            && r.AggregationSemantics == AgentUsageAggregationSemantics.PointInTime);
        Assert.Equal(2, records.Count(r => r.Kind == AgentUsageKind.TotalCost));
    }

    [Fact]
    public void AcpSessionUpdateNormalizer_PreservesUsageUpdateJson()
    {
        var update = System.Text.Json.JsonSerializer.Deserialize<AcpSessionUpdate>(
            """{"sessionUpdate":"usage_update","used":42,"size":128000,"cost":{"amount":0.12,"currency":"USD"}}""")!;

        Assert.True(AcpSessionUpdateNormalizer.TryNormalizeActivity(update, out var payload));
        Assert.NotNull(payload);
        Assert.Equal(AcpBackendActivityKind.UsageUpdate, payload!.ActivityKind);
        Assert.False(string.IsNullOrWhiteSpace(payload.UsageUpdateJson));
        Assert.Contains("\"used\":42", payload.UsageUpdateJson, StringComparison.Ordinal);
        Assert.Contains("\"size\":128000", payload.UsageUpdateJson, StringComparison.Ordinal);
        Assert.Contains("USD", payload.UsageUpdateJson, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _store.Dispose();
        Phase21UsageTestSupport.DeleteDirectory(_rootDirectory);
    }

    private sealed class FixedNativeHarnessProviderOptionsSource : INativeHarnessProviderOptionsSource
    {
        public AgentExecutionOptions? ResolveOptions() => new()
        {
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "usage-test-key",
            Model = "usage-test-model",
        };
    }
}
