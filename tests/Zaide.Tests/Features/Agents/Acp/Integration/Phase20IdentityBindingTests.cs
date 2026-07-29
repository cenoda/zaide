using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Application.Acp;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure.Acp;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Domain;
using Zaide.Tests.Features.Agents.Acp.Backend;
using Zaide.Tests.Features.Agents.Acp.Transport;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Agents.Acp.Integration;

public sealed class Phase20IdentityBindingTests
{
    [Fact]
    public async Task Coordinator_RejectsUnboundActor_WithoutNativeHarnessFallback()
    {
        var store = ConversationsTestSupport.CreateStore();
        var catalog = ConversationsTestSupport.CreateCatalog();
        var bindingStore = new AgentActorBackendBindingStore();
        var panelHost = ConversationsTestSupport.CreatePanelHost(catalog, store);
        var panel = panelHost.CreatePanelForActor(ActorId.TownhallAgent);

        var sessionService = new AgentSessionService(
            new IAgentBackend[]
            {
                new AcpAgentBackend(
                    new DelegatingAcpSessionClientFactory(
                        _ => Task.FromResult<IAcpSessionClient>(new AcpFakeSessionClient(new AcpFakeSessionScript()))),
                    () => Environment.CurrentDirectory,
                    bindingStore),
            },
            new AgentEventStream());

        var coordinator = new AgentExecutionCoordinator(
            panelHost,
            sessionService,
            store,
            bindingStore);

        var result = await coordinator.SendAsync(panel.PanelId, "hello", CancellationToken.None);

        var rejected = Assert.IsType<AgentExecutionCoordinatorResult>(result);
        Assert.Equal(ExecutionRunOutcome.Rejected, rejected.Run.Outcome);
        Assert.Contains("no explicit backend binding", rejected.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AcpBinding_FailsClosedOnAgentInfoMismatch()
    {
        var bindingStore = new AgentActorBackendBindingStore();
        var actorId = ActorId.FromValue("actor:identity");
        bindingStore.SetBinding(
            new AgentActorBackendBinding(
                actorId,
                AgentBackendIds.Acp,
                CreateRuntimeIdentity("healthy"),
                "acp-fake-agent",
                "phase-20-m2"));

        var callCount = 0;
        var backend = new AcpAgentBackend(
            new DelegatingAcpSessionClientFactory(_ =>
            {
                callCount++;
                var script = callCount == 1
                    ? new AcpFakeSessionScript { AgentName = "acp-fake-agent", AgentVersion = "phase-20-m2", AgentMessageText = "first" }
                    : new AcpFakeSessionScript { AgentName = "acp-fake-agent-wrong", AgentVersion = "phase-20-m2", AgentMessageText = "second" };
                return Task.FromResult<IAcpSessionClient>(new AcpFakeSessionClient(script));
            }),
            () => Environment.CurrentDirectory,
            bindingStore);

        var sessionId = AgentSessionId.New();
        var first = await CollectAsync(backend, actorId, sessionId);
        Assert.Contains(first, e => e.Kind == AgentBackendEventKind.MessageCompleted);

        var second = await CollectAsync(backend, actorId, sessionId);
        var failure = Assert.Single(second, e => e.Kind == AgentBackendEventKind.FailureObserved);
        var payload = Assert.IsType<AgentBackendFailurePayload>(failure.Payload);
        Assert.Contains("identity mismatch", payload.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AcpBinding_FailsClosedOnExecutableMismatch()
    {
        var bindingStore = new AgentActorBackendBindingStore();
        var actorId = ActorId.FromValue("actor:executable");
        bindingStore.SetBinding(
            new AgentActorBackendBinding(
                actorId,
                AgentBackendIds.Acp,
                new AcpRuntimeIdentity("/tmp/does-not-exist-acp-agent", Array.Empty<string>()),
                "acp-fake-agent",
                "phase-20-m2"));

        var backend = new AcpAgentBackend(
            new AcpProductionSessionClientFactory(
                bindingStore,
                new AcpSystemDiagnosticsProcessLauncher(),
                () => Environment.CurrentDirectory),
            () => Environment.CurrentDirectory,
            bindingStore);

        var events = await CollectAsync(backend, actorId);
        var failure = Assert.Single(events, e => e.Kind == AgentBackendEventKind.FailureObserved);
        var payload = Assert.IsType<AgentBackendFailurePayload>(failure.Payload);
        Assert.Contains("not found", payload.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateDisplayNames_RouteByActorId_NotByName()
    {
        var bindingStore = new AgentActorBackendBindingStore();
        var actorA = ActorId.FromValue("actor:dup-a");
        var actorB = ActorId.FromValue("actor:dup-b");
        var runtime = CreateRuntimeIdentity("healthy");

        bindingStore.SetBinding(
            new AgentActorBackendBinding(actorA, AgentBackendIds.Acp, runtime, "acp-fake-agent", "phase-20-m2"));
        bindingStore.SetBinding(
            new AgentActorBackendBinding(actorB, AgentBackendIds.NativeHarness));

        var selection = new AgentActorBackendSelectionService(bindingStore);

        var snapshotA = selection.GetSnapshot(actorA);
        var snapshotB = selection.GetSnapshot(actorB);

        Assert.Equal("ACP", snapshotA.BackendLabel);
        Assert.Equal("Native Harness", snapshotB.BackendLabel);
    }

    private static AcpRuntimeIdentity CreateRuntimeIdentity(string mode)
    {
        var options = AcpFakeAgentFixture.CreateLaunchOptions(mode);
        return new AcpRuntimeIdentity(options.FileName, options.Arguments);
    }

    private static async Task<IReadOnlyList<AgentBackendEvent>> CollectAsync(
        AcpAgentBackend backend,
        ActorId targetActorId,
        AgentSessionId? sessionId = null)
    {
        var events = new List<AgentBackendEvent>();
        var context = new AgentBackendExecutionContext(
            new AgentBackendRequest(
                sessionId ?? AgentSessionId.New(),
                ExecutionRunId.New(),
                ConversationId.NewDirect(),
                ActorId.HumanUser,
                targetActorId,
                ConversationEntryId.New(),
                "identity binding test"),
            new UnavailableAgentActionBroker());

        await foreach (var backendEvent in backend.ExecuteAsync(context, CancellationToken.None))
        {
            events.Add(backendEvent);
        }

        return events;
    }
}
