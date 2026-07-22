using System;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Tests.Features.Agents;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Townhall.Presentation;

/// <summary>
/// Phase 14 M7 automated parity suite against the M8 retirement checklist rows
/// that can be proven without interactive GUI.
/// </summary>
public sealed class Phase14M7ParityBridgeTests
{
    private static (
        TownhallViewModel Vm,
        ConversationStore Store,
        AgentPanelHost Host,
        TownhallConversationUiState Drafts,
        IActorCatalog Catalog) CreateSurface(
        Func<string, Task<AgentExecutionResult>>? handler = null)
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var draftState = ConversationsTestSupport.CreateDraftState();
        var drafts = new TownhallConversationUiState(draftState);
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store, draftState);
        var coordinator = AgentExecutionTestSupport.CreateCoordinatorFromHandler(
            host,
            handler ?? (_ => Task.FromResult(AgentExecutionResult.Success("Assistant reply"))),
            store,
            draftState);
        var router = new AgentRouter(new MentionParser(), host, coordinator, catalog, store);
        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            catalog: catalog,
            store: store,
            panelHost: host,
            executionCoordinator: coordinator,
            conversationUiState: drafts,
            agentRouter: router,
            draftState: draftState);
        return (vm, store, host, drafts, catalog);
    }

    [Fact]
    public async Task DirectSend_UserAndAssistantStayOnOwningConversation_PrivateHistory()
    {
        var (vm, store, _, _, _) = CreateSurface();
        var agentId = ActorId.PanelSeed("alpha");
        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();
        var conversationId = vm.ActiveConversationId!.Value;

        vm.DraftText = "Hello agent";
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.True(store.TryGet(conversationId, out var conversation));
        Assert.Equal(2, conversation!.Entries.Count);
        Assert.Equal(ConversationEntryKind.UserChat, conversation.Entries[0].Kind);
        Assert.Equal(ConversationEntryKind.AssistantResponse, conversation.Entries[1].Kind);

        // No public channel pollution (M4 + M7 private history ownership).
        foreach (var channel in store.ListConversations().Where(c => c.Kind == ConversationKind.Channel))
        {
            Assert.DoesNotContain(
                channel.Entries,
                e => e.Content.Contains("Hello agent", StringComparison.Ordinal)
                     || e.Content.Contains("Assistant reply", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task ErrorAndCancel_RemainVisibleOnOwningConversation()
    {
        var (vm, store, _, _, _) = CreateSurface(
            _ => Task.FromResult(AgentExecutionResult.Failure("Request failed.")));
        var agentId = ActorId.PanelSeed("alpha");
        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();
        var conversationId = vm.ActiveConversationId!.Value;

        vm.DraftText = "will fail";
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.True(store.TryGet(conversationId, out var conversation));
        Assert.Contains(
            conversation!.Entries,
            e => e.Kind == ConversationEntryKind.ExecutionFailure
                 && e.Content.Contains("Request failed", StringComparison.Ordinal));
        Assert.Contains(vm.Messages, m => m.Content.Contains("Request failed", StringComparison.Ordinal));

        // Cancelled path
        var (vm2, store2, _, _, _) = CreateSurface(
            _ => throw new OperationCanceledException("The operation was canceled."));
        vm2.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("beta")).Subscribe();
        var cancelConversationId = vm2.ActiveConversationId!.Value;
        vm2.DraftText = "will cancel";
        await vm2.SendMessageCommand.Execute().ToTask();

        Assert.True(store2.TryGet(cancelConversationId, out var cancelled));
        Assert.Contains(
            cancelled!.Entries,
            e => e.Kind == ConversationEntryKind.ExecutionFailure);
    }

    [Fact]
    public async Task ReSend_AfterFailure_IsNotRetryChrome_WorksViaSendAgain()
    {
        var calls = 0;
        var (vm, store, _, _, _) = CreateSurface(msg =>
        {
            calls++;
            if (calls == 1)
            {
                return Task.FromResult(AgentExecutionResult.Failure("first failed"));
            }

            return Task.FromResult(AgentExecutionResult.Success("second ok"));
        });
        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
        var conversationId = vm.ActiveConversationId!.Value;

        vm.DraftText = "try once";
        await vm.SendMessageCommand.Execute().ToTask();
        Assert.True(store.TryGet(conversationId, out var c1));
        Assert.Contains(c1!.Entries, e => e.Kind == ConversationEntryKind.ExecutionFailure);

        // Re-send is ordinary send with draft text — no retry command/API.
        Assert.Null(typeof(IAgentExecutionCoordinator).GetMethod("RetryAsync"));
        Assert.Null(typeof(TownhallViewModel).GetProperty("RetryCommand"));

        vm.DraftText = "try once";
        await vm.SendMessageCommand.Execute().ToTask();
        Assert.True(store.TryGet(conversationId, out var c2));
        Assert.Contains(c2!.Entries, e => e.Kind == ConversationEntryKind.AssistantResponse);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Routing_MentionWithoutOpenTargetPanel_UsesCatalogActorId()
    {
        var (vm, store, host, _, _) = CreateSurface();
        // Open only Alpha direct; mention Beta which has no open panel tab.
        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
        Assert.DoesNotContain(host.Panels, p => p.ActorId == ActorId.PanelSeed("beta"));

        vm.DraftText = "@Beta please review this";
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.Contains(host.Panels, p => p.ActorId == ActorId.PanelSeed("beta"));
        Assert.True(store.TryGetDirectConversation(
            ActorId.HumanUser,
            ActorId.PanelSeed("beta"),
            out var betaConversation));
        Assert.Contains(
            betaConversation!.Entries,
            e => e.Kind == ConversationEntryKind.UserChat
                 && e.Content == "please review this");
    }

    [Fact]
    public async Task NavigationDuringWork_KeepsConversationBusyAndHistory()
    {
        var gate = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var draftState = ConversationsTestSupport.CreateDraftState();
        var drafts = new TownhallConversationUiState(draftState);
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store, draftState);
        var (coordinator, backend, _) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(
            host,
            store,
            draftState);
        backend.SetGatedCompletion(gate, "done after nav");
        var router = new AgentRouter(new MentionParser(), host, coordinator, catalog, store);
        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            catalog: catalog,
            store: store,
            panelHost: host,
            executionCoordinator: coordinator,
            conversationUiState: drafts,
            agentRouter: router,
            draftState: draftState);

        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
        var conversationId = vm.ActiveConversationId!.Value;
        var channelId = vm.Channels[0].Id;

        vm.DraftText = "in flight";
        var sendTask = vm.SendMessageCommand.Execute().ToTask();
        Assert.True(vm.IsDirectSendBusy);
        Assert.True(coordinator.IsConversationBusy(conversationId));

        // Navigate away while work is in flight — channel is never busy.
        vm.SelectChannelCommand.Execute(channelId).Subscribe();
        Assert.False(vm.IsDirectSendBusy);
        // Conversation-keyed in-flight remains true on the coordinator.
        Assert.True(coordinator.IsConversationBusy(conversationId));

        // Re-select the same conversation — busy projects again; history still owned.
        vm.SelectConversationCommand.Execute(conversationId).Subscribe();
        Assert.True(vm.IsDirectSendBusy);

        gate.SetResult("done after nav");
        await sendTask;

        Assert.False(vm.IsDirectSendBusy);
        Assert.False(coordinator.IsConversationBusy(conversationId));
        Assert.True(store.TryGet(conversationId, out var conversation));
        Assert.Equal(2, conversation!.Entries.Count);
        Assert.Contains(vm.Messages, m => m.Content.Contains("done after nav", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PanelCloseDuringWork_DoesNotDropInFlightStatusOnConversation()
    {
        var gate = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var draftState = ConversationsTestSupport.CreateDraftState();
        var drafts = new TownhallConversationUiState(draftState);
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store, draftState);
        var (coordinator, backend, _) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(
            host,
            store,
            draftState);
        backend.SetGatedCompletion(gate, "completed");
        var router = new AgentRouter(new MentionParser(), host, coordinator, catalog, store);
        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            catalog: catalog,
            store: store,
            panelHost: host,
            executionCoordinator: coordinator,
            conversationUiState: drafts,
            agentRouter: router,
            draftState: draftState);

        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
        var conversationId = vm.ActiveConversationId!.Value;

        vm.DraftText = "stay busy";
        var sendTask = vm.SendMessageCommand.Execute().ToTask();
        Assert.True(coordinator.IsConversationBusy(conversationId));
        Assert.True(vm.IsDirectSendBusy);

        var panelId = host.Panels.Single(p => p.ConversationId == conversationId).PanelId;
        host.ClosePanel(panelId);
        Assert.Empty(host.Panels);
        // Conversation-keyed busy survives panel close.
        Assert.True(coordinator.IsConversationBusy(conversationId));
        Assert.True(vm.IsDirectSendBusy);

        gate.SetResult("completed");
        await sendTask;

        Assert.False(coordinator.IsConversationBusy(conversationId));
        Assert.False(vm.IsDirectSendBusy);
        Assert.True(store.TryGet(conversationId, out var conversation));
        Assert.Equal(2, conversation!.Entries.Count);
    }

    [Fact]
    public void Draft_PanelAndTownhallShareConversationOwnedMap()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var draftState = ConversationsTestSupport.CreateDraftState();
        var drafts = new TownhallConversationUiState(draftState);
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store, draftState);
        var (coordinator, backend, _) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(
            host,
            store,
            draftState);
        backend.SetCompletion("x");
        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            catalog: catalog,
            store: store,
            panelHost: host,
            executionCoordinator: coordinator,
            conversationUiState: drafts,
            draftState: draftState);

        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
        var conversationId = vm.ActiveConversationId!.Value;

        vm.DraftText = "shared draft";
        Assert.Equal("shared draft", drafts.GetDraft(conversationId));
        var panel = host.GetOrCreatePanelForActor(ActorId.PanelSeed("alpha"));
        Assert.Equal("shared draft", panel.DraftInput);

        panel.DraftInput = "typed in panel";
        Assert.Equal("typed in panel", drafts.GetDraft(conversationId));
    }

    [Fact]
    public async Task RoutingFailure_VisibleOnSourceConversation_NotLost()
    {
        var (vm, store, _, _, _) = CreateSurface();
        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
        var conversationId = vm.ActiveConversationId!.Value;

        vm.DraftText = "@Ghost does not exist";
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.True(store.TryGet(conversationId, out var conversation));
        Assert.Contains(
            conversation!.Entries,
            e => e.Kind == ConversationEntryKind.RoutingFailure);
        Assert.Contains(vm.Messages, m => m.Content.Contains("Unknown target", StringComparison.Ordinal));
    }

    private sealed class StubExecutionService : IAgentExecutionService
    {
        private readonly Func<string, Task<AgentExecutionResult>> _handler;

        public StubExecutionService(Func<string, Task<AgentExecutionResult>> handler) =>
            _handler = handler;

        public Task<AgentExecutionResult> ExecuteAsync(string userMessage, CancellationToken ct = default) =>
            _handler(userMessage);
    }
}
