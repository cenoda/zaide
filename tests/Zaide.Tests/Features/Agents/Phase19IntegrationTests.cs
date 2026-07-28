using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Xunit;
using Zaide;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Domain;
using Zaide.App.Composition;
using Zaide.App.Composition.Registration;
using Zaide.Features.Conversations.Contracts;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Agents;

/// <summary>
/// Phase 19 M4 — production DI wiring, broker resolution, and capability truthfulness.
/// </summary>
public sealed class Phase19IntegrationTests : IDisposable
{
    private readonly string _root;
    private readonly FakeWorkspaceActionAuthority _workspaceAuthority;
    private readonly ScriptedNativeHarnessProviderTransport _transport;
    private readonly ServiceProvider _provider;
    private readonly List<AgentEvent> _capturedEvents = new();

    public Phase19IntegrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "Phase19Integration_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "note.txt"), "integration-content");

        _workspaceAuthority = new FakeWorkspaceActionAuthority(
            FakeWorkspaceActionAuthority.CreateScopeFromDirectory(_root));
        _transport = new ScriptedNativeHarnessProviderTransport();

        var services = new ServiceCollection();
        Program.ConfigureServices(services);
        ReplaceSingleton(services, typeof(INativeHarnessProviderTransport), _transport);
        ReplaceSingleton(services, typeof(IAgentPermissionReviewService), new AllowingPermissionReviewService());
        ReplaceSingleton(services, typeof(IWorkspaceActionAuthority), _workspaceAuthority);
        ReplaceSingleton(
            services,
            typeof(INativeHarnessProviderOptionsSource),
            new FixedNativeHarnessProviderOptionsSource());
        services.AddSingleton<IScheduler>(_ => CurrentThreadScheduler.Instance);

        _provider = services.BuildServiceProvider();
        _provider.GetRequiredService<AgentEventStream>().Events.Subscribe(_capturedEvents.Add);
        _ = _provider.GetRequiredService<AgentConversationEventProjection>();
    }

    public void Dispose()
    {
        _provider.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [Fact]
    public void Phase19Integration_ProductionDi_ResolvesNativeHarnessBackendAndDependencies()
    {
        var backend = _provider.GetRequiredService<IAgentBackend>();
        var priorReader = _provider.GetRequiredService<INativeHarnessPriorConversationReader>();

        Assert.IsType<NativeHarnessAgentBackend>(backend);
        Assert.IsAssignableFrom<IAgentActionRequestCapableBackend>(backend);
        Assert.IsType<NativeHarnessPriorConversationReader>(priorReader);
        Assert.Equal(AgentBackendIds.NativeHarness, backend.BackendId);
        Assert.Equal(NativeHarnessAgentBackend.BackendVersionValue, backend.BackendVersion);
    }

    [Fact]
    public void Phase19Integration_CapabilityRows_ReportTruthfulSixFactStates()
    {
        var backend = (NativeHarnessAgentBackend)_provider.GetRequiredService<IAgentBackend>();
        var snapshot = backend.CapabilitySnapshot;

        Assert.Equal(AgentBackendIds.NativeHarness, snapshot.BackendId);
        Assert.Equal(6, snapshot.Rows.Count);

        Assert.True(snapshot.TryGetState(AgentCapabilityId.MessageCompletion, out var messageCompletion));
        Assert.Equal(AgentCapabilityFactValue.Supported, messageCompletion!.Advertised);
        Assert.Equal(AgentCapabilityFactValue.Supported, messageCompletion.Available);
        Assert.Equal(AgentCapabilityFactValue.Supported, messageCompletion.Configured);
        Assert.Equal(AgentCapabilityFactValue.Supported, messageCompletion.CurrentlyUsable);

        Assert.True(snapshot.TryGetState(AgentCapabilityId.Tools, out var tools));
        Assert.Equal(AgentCapabilityFactValue.Supported, tools!.Advertised);
        Assert.Equal(AgentCapabilityFactValue.Supported, tools.Available);
        Assert.Equal(AgentCapabilityFactValue.Supported, tools.Configured);
        Assert.NotEqual(AgentCapabilityFactValue.Supported, tools.CurrentlyUsable);

        Assert.True(snapshot.TryGetState(AgentCapabilityId.Permissions, out var permissions));
        Assert.Equal(AgentCapabilityFactValue.Supported, permissions!.Advertised);
        Assert.Equal(AgentCapabilityFactValue.Supported, permissions.Available);
        Assert.Equal(AgentCapabilityFactValue.Supported, permissions.Configured);
        Assert.NotEqual(AgentCapabilityFactValue.Supported, permissions.CurrentlyUsable);

        Assert.True(snapshot.TryGetState(AgentCapabilityId.IdeContext, out var ideContext));
        Assert.Equal(AgentCapabilityFactValue.Supported, ideContext!.Advertised);
        Assert.Equal(AgentCapabilityFactValue.Supported, ideContext.Available);
        Assert.Equal(AgentCapabilityFactValue.NotSupported, ideContext.Configured);
        Assert.Equal(AgentCapabilityFactValue.NotSupported, ideContext.CurrentlyUsable);

        Assert.True(snapshot.TryGetState(AgentCapabilityId.Streaming, out var streaming));
        Assert.Equal(AgentCapabilityFactValue.Supported, streaming!.Advertised);
        Assert.Equal(AgentCapabilityFactValue.Supported, streaming.Available);
        Assert.Equal(AgentCapabilityFactValue.Supported, streaming.Configured);
        Assert.Equal(AgentCapabilityFactValue.Supported, streaming.CurrentlyUsable);

        Assert.True(snapshot.TryGetState(AgentCapabilityId.Cancellation, out var cancellation));
        Assert.Equal(AgentCapabilityFactValue.Supported, cancellation!.Advertised);
        Assert.Equal(AgentCapabilityFactValue.Supported, cancellation.Available);
        Assert.Equal(AgentCapabilityFactValue.Supported, cancellation.Configured);
        Assert.Equal(AgentCapabilityFactValue.Supported, cancellation.CurrentlyUsable);
    }

    [Fact]
    public async Task Phase19Integration_ProductionRun_ResolvesContractAgentActionBroker()
    {
        _transport.Enqueue(NativeHarnessProviderResponse.Success(
            assistantContent: null,
            toolCalls: new[]
            {
                new NativeHarnessProviderToolCall(
                    NativeHarnessToolCallId.FromValue("call-broker-proof"),
                    NativeHarnessProviderProtocol.ReadFileToolName,
                    """{"path":"note.txt"}"""),
            }));
        _transport.Enqueue(NativeHarnessProviderResponse.Success("broker proof complete"));

        var session = _provider.GetRequiredService<IAgentSessionService>();
        var store = _provider.GetRequiredService<IConversationStore>();
        var conversation = store.CreateDirectConversation(ActorId.HumanUser, ActorId.TownhallAgent);

        var snapshot = await session.SendAsync(
            conversation.Id,
            ActorId.HumanUser,
            ActorId.TownhallAgent,
            AgentBackendIds.NativeHarness,
            ConversationEntryId.New(),
            "prove broker",
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Completed, snapshot.Status);
        Assert.Contains(_capturedEvents, e => e.Kind == AgentEventKind.ActionRequested);
        Assert.DoesNotContain(
            _capturedEvents,
            e => e.Payload is AgentActionFactPayload payload
                 && payload.FailureKind == AgentActionFailureKind.BrokerUnavailable);
    }

    [Fact]
    public async Task Phase19Integration_ToolCall_DispatchesThroughProductionBroker()
    {
        _transport.Enqueue(NativeHarnessProviderResponse.Success(
            assistantContent: null,
            toolCalls: new[]
            {
                new NativeHarnessProviderToolCall(
                    NativeHarnessToolCallId.FromValue("call-integration-1"),
                    NativeHarnessProviderProtocol.ReadFileToolName,
                    """{"path":"note.txt"}"""),
            }));
        _transport.Enqueue(NativeHarnessProviderResponse.Success("read complete"));

        var session = _provider.GetRequiredService<IAgentSessionService>();
        var auditStore = _provider.GetRequiredService<IAgentActionAuditStore>();
        var store = _provider.GetRequiredService<IConversationStore>();
        var conversation = store.CreateDirectConversation(ActorId.HumanUser, ActorId.TownhallAgent);

        var snapshot = await session.SendAsync(
            conversation.Id,
            ActorId.HumanUser,
            ActorId.TownhallAgent,
            AgentBackendIds.NativeHarness,
            ConversationEntryId.New(),
            "read note",
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Completed, snapshot.Status);

        var actionEvents = _capturedEvents
            .Where(e => e.Kind is AgentEventKind.ActionRequested
                or AgentEventKind.ActionPermissionClassified
                or AgentEventKind.ActionExecutionStarted
                or AgentEventKind.ActionResultReported)
            .ToArray();
        Assert.True(actionEvents.Length >= 4);

        var resultEvent = _capturedEvents.Single(e => e.Kind == AgentEventKind.ActionResultReported);
        var resultPayload = Assert.IsType<AgentActionFactPayload>(resultEvent.Payload);
        Assert.Equal(AgentActionResultKind.Succeeded, resultPayload.ResultKind);
        Assert.Equal(AgentActivityEvidenceLevel.ZaideExecuted, resultEvent.EvidenceLevel);

        var audit = auditStore.GetRunSnapshot(snapshot.RunId, maxRecords: 16);
        Assert.NotEmpty(audit);
        Assert.Contains(audit, record => record.ActionKind == AgentActionKind.ReadFile);

        Assert.Equal(2, _transport.Requests.Count);
        var toolMessage = _transport.Requests[1].Messages.Last();
        Assert.Contains("integration-content", toolMessage.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase19Integration_AddZaideAgents_RegistersNativeHarnessProductionBackend()
    {
        var services = new ServiceCollection();
        services.AddZaideAgents();

        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IAgentBackend)
                 && d.ImplementationFactory is not null);
        Assert.DoesNotContain(
            services,
            d => d.ImplementationType == typeof(LegacyOpenAiCompatibleAgentBackend));
    }

    private static void ReplaceSingleton(IServiceCollection services, Type serviceType, object instance)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == serviceType);
        if (descriptor is not null)
        {
            services.Remove(descriptor);
        }

        services.AddSingleton(serviceType, instance);
    }

    private sealed class FixedNativeHarnessProviderOptionsSource : INativeHarnessProviderOptionsSource
    {
        public AgentExecutionOptions? ResolveOptions() =>
            new()
            {
                BaseUrl = "https://api.test.com/v1",
                ApiKey = "integration-test-key",
                Model = "integration-test-model",
            };
    }

    private sealed class AllowingPermissionReviewService : IAgentPermissionReviewService
    {
        public ValueTask<AgentPermissionDecision> RequestDecisionAsync(
            AgentActionRequest request,
            AgentActionDisplaySummary displaySummary,
            WorkspaceActionScope? workspaceScope,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                request.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Published,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                isAllow: true));
    }
}
