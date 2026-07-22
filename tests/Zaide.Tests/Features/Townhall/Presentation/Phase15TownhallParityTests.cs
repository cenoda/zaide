using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Tests.Features.Agents;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Townhall.Presentation;

/// <summary>
/// Phase 15 M3b-2 Townhall parity tests for event-to-conversation projection.
/// </summary>
public sealed class Phase15TownhallParityTests
{
    private static (
        TownhallViewModel Vm,
        ConversationStore Store,
        AgentPanelHost Host,
        AgentEventStream Stream,
        AgentConversationEventProjection Projection,
        IAgentSessionService Session) CreateSurface(
        FakeAgentBackend? customBackend = null)
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var draftState = ConversationsTestSupport.CreateDraftState();
        var drafts = new TownhallConversationUiState(draftState);
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store, draftState);
        var stream = new AgentEventStream();
        var backend = customBackend ?? new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        var session = new AgentSessionService(new[] { backend }, stream);
        var projection = new AgentConversationEventProjection(stream.Events, store, catalog);

        var coordinator = new AgentExecutionCoordinator(host, session, store, draftState);
        var router = new AgentRouter(new MentionParser(), host, coordinator, catalog, store);

        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            catalog: catalog,
            store: store,
            panelHost: host,
            executionCoordinator: coordinator,
            conversationUiState: drafts,
            agentRouter: router,
            draftState: draftState);

        return (vm, store, host, stream, projection, session);
    }

    [Fact]
    public async Task DirectSend_Success_ProjectsExactUserAndAssistantMessagesInTownhall()
    {
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("Hello back from agent");
        var (vm, store, _, _, _, _) = CreateSurface(backend);

        var agentId = ActorId.PanelSeed("alpha");
        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();
        var conversationId = vm.ActiveConversationId!.Value;

        vm.DraftText = "Hello agent";
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.True(store.TryGet(conversationId, out var conversation));
        Assert.Equal(2, conversation!.Entries.Count);
        Assert.Equal(2, vm.Messages.Count);

        Assert.Equal("Hello agent", vm.Messages[0].Content);
        Assert.Equal(TownhallMessageKind.Chat, vm.Messages[0].Kind);

        Assert.Contains("Hello back from agent", vm.Messages[1].Content);
        Assert.Equal(TownhallMessageKind.Chat, vm.Messages[1].Kind);
    }

    [Fact]
    public async Task DirectSend_Failure_ProjectsExactErrorTextInTownhall()
    {
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetFailure(AgentFailureKind.Execution, "Request failed.");
        var (vm, store, _, _, _, _) = CreateSurface(backend);

        var agentId = ActorId.PanelSeed("alpha");
        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();
        var conversationId = vm.ActiveConversationId!.Value;

        vm.DraftText = "will fail";
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.True(store.TryGet(conversationId, out var conversation));
        Assert.Equal(2, conversation!.Entries.Count);
        Assert.Equal(ConversationEntryKind.ExecutionFailure, conversation.Entries[1].Kind);
        Assert.Equal("Request failed.", conversation.Entries[1].Content);

        Assert.Equal(2, vm.Messages.Count);
        Assert.Contains("Request failed.", vm.Messages[1].Content);
    }

    [Fact]
    public async Task RoutingFailure_ProjectsExactErrorTextAndTaxonomyInTownhall()
    {
        var (vm, store, _, _, _, _) = CreateSurface();

        var agentId = ActorId.PanelSeed("alpha");
        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();
        var conversationId = vm.ActiveConversationId!.Value;

        vm.DraftText = "@Ghost non-existent target";
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.True(store.TryGet(conversationId, out var conversation));
        Assert.Contains(conversation!.Entries, e => e.Kind == ConversationEntryKind.RoutingFailure);
        Assert.Contains(vm.Messages, m => m.Content.Contains("Unknown target"));
    }

    [Fact]
    public async Task NavigationDuringRun_PreservesPrivateHistoryUnreadAndDraftState()
    {
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetGatedCompletion(gate, "done after nav");
        var (vm, store, _, _, _, coordinator) = CreateSurface(backend);

        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
        var conversationId = vm.ActiveConversationId!.Value;
        var channelId = vm.Channels[0].Id;

        vm.DraftText = "in flight draft";
        var sendTask = vm.SendMessageCommand.Execute().ToTask();

        // Navigate away to a channel
        vm.SelectChannelCommand.Execute(channelId).Subscribe();
        Assert.False(vm.IsDirectSendBusy);

        // Return to direct conversation
        vm.SelectConversationCommand.Execute(conversationId).Subscribe();

        gate.SetResult("done after nav");
        await sendTask;

        Assert.True(store.TryGet(conversationId, out var conversation));
        Assert.Equal(2, conversation!.Entries.Count);
        Assert.Equal("done after nav", conversation.Entries[1].Content);
    }

    [Fact]
    public async Task PrivacyAndNoPublicMirror_DirectSendDoesNotAppearInChannelMessages()
    {
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("private reply");
        var (vm, store, _, _, _, _) = CreateSurface(backend);

        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
        var conversationId = vm.ActiveConversationId!.Value;

        vm.DraftText = "private request";
        await vm.SendMessageCommand.Execute().ToTask();

        // Check channel conversations in store and state
        foreach (var channelConv in store.ListConversations().Where(c => c.Kind == ConversationKind.Channel))
        {
            Assert.DoesNotContain(channelConv.Entries, e => e.Content.Contains("private request") || e.Content.Contains("private reply"));
        }
    }

    [Fact]
    public void PersistenceAndNoAutoResume_RestoredConversationHistoryDoesNotAutoResumeRun()
    {
        var store = ConversationsTestSupport.CreateStore();
        var catalog = ConversationsTestSupport.CreateCatalog();
        var agentActor = ActorId.PanelSeed("alpha");
        var conversation = store.GetOrCreateDirectConversation(ActorId.HumanUser, agentActor);

        // Pre-populate historical entries in store
        store.AppendEntry(
            conversation.Id,
            ConversationEntry.UserChat(
                ConversationEntryId.New(),
                ActorId.HumanUser,
                DateTimeOffset.UtcNow,
                "old query"));

        store.AppendEntry(
            conversation.Id,
            ConversationEntry.AssistantResponse(
                ConversationEntryId.New(),
                agentActor,
                DateTimeOffset.UtcNow,
                "old answer"));

        var stream = new AgentEventStream();
        using var projection = new AgentConversationEventProjection(stream.Events, store, catalog);
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        var session = new AgentSessionService(new[] { backend }, stream);
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store);
        var coordinator = new AgentExecutionCoordinator(host, session, store);
        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            catalog: catalog,
            store: store,
            panelHost: host,
            executionCoordinator: coordinator);

        vm.SelectConversationCommand.Execute(conversation.Id).Subscribe();

        Assert.Equal(2, conversation.Entries.Count);
        Assert.Equal(2, vm.Messages.Count);
        Assert.Equal("old query", vm.Messages[0].Content);
        Assert.Contains("old answer", vm.Messages[1].Content);
        Assert.Equal(0, backend.ExecuteCallCount);
        Assert.False(vm.IsDirectSendBusy);
    }
}
