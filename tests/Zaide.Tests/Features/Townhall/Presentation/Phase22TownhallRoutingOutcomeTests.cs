using System;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Tests.Features.Agents;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Townhall.Presentation;

/// <summary>
/// Phase 22.3 M1 Townhall routing and outcome visibility tests.
/// </summary>
public sealed class Phase22TownhallRoutingOutcomeTests
{
    private static (
        TownhallViewModel ViewModel,
        IConversationStore Store,
        AgentPanelHost Host,
        IAgentExecutionCoordinator Coordinator,
        IActorCatalog Catalog,
        FakeAgentBackend Backend,
        TownhallState State,
        IConversationDraftState DraftState) CreateRoutedSurface(
        Action<FakeAgentBackend>? configureBackend = null)
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var draftState = ConversationsTestSupport.CreateDraftState();
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store, draftState);
        var (coordinator, backend, session) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(
            host,
            store,
            draftState,
            catalog: catalog);
        if (configureBackend is null)
        {
            backend.SetCompletion("target reply");
        }
        else
        {
            configureBackend(backend);
        }

        // CreateCoordinatorWithFakeBackend already attaches AgentConversationEventProjection.
        _ = session;
        var router = new AgentRouter(new MentionParser(), host, coordinator, catalog, store);
        var state = new TownhallState();
        var uiState = new TownhallConversationUiState(draftState);
        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            state: state,
            catalog: catalog,
            store: store,
            panelHost: host,
            executionCoordinator: coordinator,
            conversationUiState: uiState,
            draftState: draftState,
            agentRouter: router);
        return (vm, store, host, coordinator, catalog, backend, state, draftState);
    }

    private static bool IsRouteStatus(ConversationEntry entry) =>
        entry.Kind == ConversationEntryKind.SystemNotification
        && entry.Content.StartsWith("zaide-route|v1|", StringComparison.Ordinal);

    private static bool IsRouteStatusMessage(TownhallMessage message) =>
        message.Content.Contains("Routed to", StringComparison.Ordinal);

    private static async Task WaitForExecutionStartedAsync(FakeAgentBackend backend)
    {
        await backend.ExecutionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DirectValidMention_RoutesOnceToTargetConversation()
    {
        var (vm, store, _, _, _, _, _, _) = CreateRoutedSurface();
        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
        var sourceConversationId = vm.ActiveConversationId!.Value;

        vm.DraftText = "@Beta routed once";
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.True(store.TryGetDirectConversation(ActorId.HumanUser, ActorId.PanelSeed("beta"), out var targetConversation));
        Assert.Contains(targetConversation!.Entries, e => e.Kind == ConversationEntryKind.UserChat && e.Content == "routed once");
        Assert.True(store.TryGet(sourceConversationId, out var sourceConversation));
        Assert.DoesNotContain(
            sourceConversation!.Entries,
            e => e.Kind == ConversationEntryKind.AssistantResponse);
    }

    [Theory]
    [InlineData("@Ghost missing", "Unknown target")]
    [InlineData("@Twin hello", "Ambiguous target")]
    [InlineData("@Alpha @Beta hello", "Multiple mentions")]
    [InlineData("@Beta", "Empty content after stripping")]
    public async Task DirectInvalidMentions_RecordRoutingFailureOnSource(string draft, string expectedReason)
    {
        var (vm, store, host, _, _, _, _, _) = CreateRoutedSurface();
        host.CreatePanel("agent-twin-a", "Twin", "avatar_a");
        host.CreatePanel("agent-twin-b", "Twin", "avatar_b");
        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
        var sourceConversationId = vm.ActiveConversationId!.Value;

        vm.DraftText = draft;
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.True(store.TryGet(sourceConversationId, out var conversation));
        Assert.Contains(
            conversation!.Entries,
            e => e.Kind == ConversationEntryKind.RoutingFailure && e.Content == expectedReason);
        Assert.Contains(vm.Messages, m => m.Content.Contains(expectedReason, StringComparison.Ordinal));
    }

    [Fact]
    public async Task DirectValidMention_DoesNotRequireOpenTargetPanel()
    {
        var (vm, store, host, _, _, _, _, _) = CreateRoutedSurface();
        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
        Assert.DoesNotContain(host.Panels, p => p.ActorId == ActorId.PanelSeed("beta"));

        vm.DraftText = "@Beta hello without tab";
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.True(store.TryGetDirectConversation(ActorId.HumanUser, ActorId.PanelSeed("beta"), out var betaConversation));
        Assert.Contains(betaConversation!.Entries, e => e.Kind == ConversationEntryKind.UserChat);
    }

    [Fact]
    public async Task ChannelPlainChat_RemainsOrdinaryChannelChat()
    {
        var (vm, store, _, _, _, _, _, _) = CreateRoutedSurface();
        var initialCount = vm.Messages.Count;

        vm.DraftText = "plain channel hello";
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.Equal(initialCount + 1, vm.Messages.Count);
        Assert.Equal("plain channel hello", vm.Messages[^1].Content);
        Assert.True(store.TryGetChannelConversation(vm.ActiveChannelId!, out var channelConversation));
        Assert.DoesNotContain(
            channelConversation!.Entries,
            e => e.Content.StartsWith("zaide-route|v1|", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ChannelValidMention_RoutesOnceThroughTypedConversationContext()
    {
        var (vm, store, _, _, _, _, _, _) = CreateRoutedSurface();
        var channelId = vm.ActiveChannelId!;
        var channelConversation = store.TryGetChannelConversation(channelId, out var conversation)
            ? conversation!
            : throw new InvalidOperationException("Channel conversation missing.");

        vm.DraftText = "@Beta from channel";
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.True(store.TryGetDirectConversation(ActorId.HumanUser, ActorId.PanelSeed("beta"), out var targetConversation));
        Assert.Contains(targetConversation!.Entries, e => e.Kind == ConversationEntryKind.UserChat && e.Content == "from channel");
        Assert.DoesNotContain(channelConversation.Entries, e => e.Kind == ConversationEntryKind.UserChat && e.Content == "@Beta from channel");
        Assert.Contains(
            channelConversation.Entries,
            e => e.Kind == ConversationEntryKind.SystemNotification
                 && e.Content.StartsWith("zaide-route|v1|", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RoutedExecution_ProvidesBoundedSourceRouteStatusWithoutPrivateTargetContent()
    {
        var (vm, store, _, _, _, _, _, _) = CreateRoutedSurface();
        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
        var sourceConversationId = vm.ActiveConversationId!.Value;

        vm.DraftText = "@Beta private route";
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.True(store.TryGet(sourceConversationId, out var sourceConversation));
        var routeStatus = Assert.Single(
            sourceConversation!.Entries,
            e => e.Kind == ConversationEntryKind.SystemNotification
                 && e.Content.StartsWith("zaide-route|v1|", StringComparison.Ordinal));
        Assert.Contains("Beta", routeStatus.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(sourceConversation.Entries, e => e.Kind == ConversationEntryKind.AssistantResponse);
        Assert.Contains(vm.Messages, m => m.Content.Contains("Routed to Beta", StringComparison.Ordinal));
        Assert.DoesNotContain(vm.Messages, m => m.Content.Contains("target reply", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TargetConversation_RemainsAuthoritativeForAdmittedHistoryAndUnread()
    {
        var (vm, store, _, _, _, _, _, _) = CreateRoutedSurface();
        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
        var sourceConversationId = vm.ActiveConversationId!.Value;

        vm.DraftText = "@Beta unread target";
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.True(store.TryGetDirectConversation(ActorId.HumanUser, ActorId.PanelSeed("beta"), out var targetConversation));
        Assert.Equal(2, targetConversation!.Entries.Count);
        var betaNav = vm.DirectNavItems.Single(i => i.PeerActorId == ActorId.PanelSeed("beta"));
        Assert.True(betaNav.HasUnread);

        vm.SelectConversationCommand.Execute(sourceConversationId).Subscribe();
        Assert.False(vm.DirectNavItems.Single(i => i.ConversationId == sourceConversationId).HasUnread);
    }

    [Fact]
    public async Task RoutedFlow_DoesNotCreateDuplicateRouteStatusOrConversationEntries()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store);
        var (coordinator, backend, session) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(host, store);
        backend.SetCompletion("once");
        _ = new AgentConversationEventProjection(session.Events, store, catalog);
        var router = new AgentRouter(new MentionParser(), host, coordinator, catalog, store);
        var source = host.GetOrCreatePanelForActor(ActorId.PanelSeed("alpha"));

        var first = await router.RouteAndExecuteAsync(source.PanelId, "@Beta once");
        var second = await router.RouteAndExecuteAsync(source.PanelId, "@Beta twice");

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.True(store.TryGet(source.ConversationId, out var sourceConversation));
        var routeStatuses = sourceConversation!.Entries
            .Where(e => e.Kind == ConversationEntryKind.SystemNotification
                        && e.Content.StartsWith("zaide-route|v1|", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(2, routeStatuses.Count);
        Assert.Equal(2, routeStatuses.Select(e => e.CorrelationId!.Value.Value).Distinct().Count());
    }

    [Fact]
    public async Task EmptyDirectDraft_IsRetainedWithoutConversationEntry()
    {
        var (vm, store, _, _, _, _, _, _) = CreateRoutedSurface();
        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
        var sourceConversationId = vm.ActiveConversationId!.Value;
        var beforeCount = store.TryGet(sourceConversationId, out var before) ? before!.Entries.Count : 0;

        vm.DraftText = "   ";
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.Equal("   ", vm.DraftText);
        Assert.True(store.TryGet(sourceConversationId, out var after));
        Assert.Equal(beforeCount, after!.Entries.Count);
    }

    [Fact]
    public async Task VisibleRoutingFailure_ClearsDraftWhileMissingSourceDoesNot()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store);
        var (coordinator, _, session) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(host, store);
        _ = new AgentConversationEventProjection(session.Events, store, catalog);
        var router = new AgentRouter(new MentionParser(), host, coordinator, catalog, store);
        var draftState = ConversationsTestSupport.CreateDraftState();
        var uiState = new TownhallConversationUiState(draftState);
        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            catalog: catalog,
            store: store,
            panelHost: host,
            executionCoordinator: coordinator,
            conversationUiState: uiState,
            draftState: draftState,
            agentRouter: router);

        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
        vm.DraftText = "@Ghost still visible";
        await vm.SendMessageCommand.Execute().ToTask();
        Assert.Empty(vm.DraftText);

        var missingSource = await router.RouteAndExecuteAsync("missing-panel", "@Beta hello");
        Assert.False(missingSource.Success);
        Assert.Null(missingSource.ExecutionResult);
    }

    [Fact]
    public async Task ChannelRoute_InFlightNavigation_PreservesOtherChannelDraft_AndProjectsRouteStatusExactlyOnce()
    {
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var (vm, store, _, _, _, backend, state, draftState) = CreateRoutedSurface(
            b => b.SetGatedCompletion(gate, "gated reply"));

        var channelAId = vm.ActiveChannelId!;
        var channelB = Assert.Single(vm.Channels.Where(c => c.Id != channelAId).Take(1));
        Assert.True(store.TryGetChannelConversation(channelAId, out var channelAConversation));
        var channelAConversationId = channelAConversation!.Id;

        vm.DraftText = "@Beta gated channel route";
        var sendTask = vm.SendMessageCommand.Execute().ToTask();
        await WaitForExecutionStartedAsync(backend);

        vm.SelectChannelCommand.Execute(channelB.Id).Subscribe();
        Assert.Equal(channelB.Id, vm.ActiveChannelId);
        vm.DraftText = "unrelated draft on B";

        gate.SetResult("gated reply");
        await sendTask;

        Assert.Equal("unrelated draft on B", vm.DraftText);
        Assert.Equal("unrelated draft on B", draftState.GetDraft(vm.ActiveConversationId!.Value));
        Assert.Equal(string.Empty, draftState.GetDraft(channelAConversationId));

        Assert.True(store.TryGet(channelAConversationId, out var sourceAfter));
        Assert.Single(sourceAfter!.Entries, IsRouteStatus);
        Assert.DoesNotContain(sourceAfter.Entries, e => e.Kind == ConversationEntryKind.UserChat && e.Content.Contains("@Beta gated", StringComparison.Ordinal));
        Assert.DoesNotContain(sourceAfter.Entries, e => e.Kind == ConversationEntryKind.AssistantResponse);
        Assert.DoesNotContain(
            sourceAfter.Entries,
            e => e.Content.Contains("gated reply", StringComparison.Ordinal)
                 && e.Kind != ConversationEntryKind.SystemNotification);

        Assert.True(state.ChannelMessages.TryGetValue(channelAId, out var cachedA));
        Assert.Single(cachedA!, IsRouteStatusMessage);
        Assert.DoesNotContain(cachedA, m => m.Content.Contains("gated reply", StringComparison.Ordinal));

        var channelA = Assert.Single(vm.Channels, c => c.Id == channelAId);
        Assert.True(channelA.HasUnread);

        vm.SelectChannelCommand.Execute(channelAId).Subscribe();
        Assert.Equal(channelAId, vm.ActiveChannelId);
        Assert.False(channelA.HasUnread);
        Assert.Single(vm.Messages, IsRouteStatusMessage);
        Assert.Equal(string.Empty, vm.DraftText);
        Assert.DoesNotContain(vm.Messages, m => m.Content.Contains("gated reply", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DirectRoute_InFlightNavigation_PreservesOtherConversationDraft()
    {
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var (vm, store, _, _, _, backend, _, draftState) = CreateRoutedSurface(
            b => b.SetGatedCompletion(gate, "direct gated reply"));

        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
        var sourceConversationId = vm.ActiveConversationId!.Value;

        vm.DraftText = "@Beta gated direct route";
        var sendTask = vm.SendMessageCommand.Execute().ToTask();
        await WaitForExecutionStartedAsync(backend);

        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("gamma")).Subscribe();
        Assert.NotEqual(sourceConversationId, vm.ActiveConversationId!.Value);
        vm.DraftText = "gamma draft while alpha routes";

        gate.SetResult("direct gated reply");
        await sendTask;

        Assert.Equal("gamma draft while alpha routes", vm.DraftText);
        Assert.Equal("gamma draft while alpha routes", draftState.GetDraft(vm.ActiveConversationId!.Value));
        Assert.Equal(string.Empty, draftState.GetDraft(sourceConversationId));

        Assert.True(store.TryGet(sourceConversationId, out var sourceAfter));
        Assert.Single(sourceAfter!.Entries, IsRouteStatus);
        Assert.DoesNotContain(sourceAfter.Entries, e => e.Kind == ConversationEntryKind.AssistantResponse);
        Assert.DoesNotContain(
            sourceAfter.Entries,
            e => e.Content.Contains("direct gated reply", StringComparison.Ordinal)
                 && e.Kind != ConversationEntryKind.SystemNotification);
    }

    [Fact]
    public async Task ChannelRoute_ReturnAndEditSourceDraftDuringFlight_PreservesNewerDraft()
    {
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var (vm, store, _, _, _, backend, _, draftState) = CreateRoutedSurface(
            b => b.SetGatedCompletion(gate, "edit-preserve reply"));

        var channelAId = vm.ActiveChannelId!;
        var channelB = Assert.Single(vm.Channels.Where(c => c.Id != channelAId).Take(1));
        Assert.True(store.TryGetChannelConversation(channelAId, out var channelAConversation));
        var channelAConversationId = channelAConversation!.Id;

        vm.DraftText = "@Beta original draft";
        var sendTask = vm.SendMessageCommand.Execute().ToTask();
        await WaitForExecutionStartedAsync(backend);

        vm.SelectChannelCommand.Execute(channelB.Id).Subscribe();
        vm.SelectChannelCommand.Execute(channelAId).Subscribe();
        vm.DraftText = "newer replacement draft on A";

        gate.SetResult("edit-preserve reply");
        await sendTask;

        Assert.Equal("newer replacement draft on A", vm.DraftText);
        Assert.Equal("newer replacement draft on A", draftState.GetDraft(channelAConversationId));
        Assert.True(store.TryGet(channelAConversationId, out var sourceAfter));
        Assert.Single(sourceAfter!.Entries, IsRouteStatus);
    }

    [Fact]
    public async Task MissingCorrelatedVisibleOutcome_DoesNotClearSourceDraft()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var draftState = ConversationsTestSupport.CreateDraftState();
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store, draftState);
        var (coordinator, _, _) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(
            host,
            store,
            draftState,
            catalog: catalog);
        var router = new AgentRouter(new MentionParser(), host, coordinator, catalog, store);
        var uiState = new TownhallConversationUiState(draftState);
        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            catalog: catalog,
            store: store,
            panelHost: host,
            executionCoordinator: coordinator,
            conversationUiState: uiState,
            draftState: draftState,
            agentRouter: router);

        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
        var sourceConversationId = vm.ActiveConversationId!.Value;
        vm.DraftText = "@Beta hello";
        var retained = vm.DraftText;

        var missingSource = await router.RouteAndExecuteAsync("missing-panel", "@Beta hello");
        Assert.False(missingSource.Success);
        Assert.Null(missingSource.ExecutionResult);

        // ViewModel clear path is only invoked for townhall-owned sends. Missing-source
        // router results have no execution result, so no draft clear decision applies.
        Assert.Equal(retained, vm.DraftText);
        Assert.Equal(retained, draftState.GetDraft(sourceConversationId));
        Assert.True(store.TryGet(sourceConversationId, out var sourceConversation));
        Assert.DoesNotContain(sourceConversation!.Entries, IsRouteStatus);
    }

    [Fact]
    public async Task ChannelSwitchAndPlainChat_ProjectCachedEntriesExactlyOnce()
    {
        var (vm, store, _, _, _, _, state, _) = CreateRoutedSurface();
        var channelAId = vm.ActiveChannelId!;
        var channelB = Assert.Single(vm.Channels.Where(c => c.Id != channelAId).Take(1));

        vm.DraftText = "plain once";
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.True(store.TryGetChannelConversation(channelAId, out var channelAConversation));
        Assert.Single(
            channelAConversation!.Entries,
            e => e.Kind == ConversationEntryKind.UserChat && e.Content == "plain once");
        Assert.True(state.ChannelMessages.TryGetValue(channelAId, out var cachedA));
        Assert.Single(cachedA!, m => m.Content == "plain once");

        vm.SelectChannelCommand.Execute(channelB.Id).Subscribe();
        Assert.True(store.TryGetChannelConversation(channelB.Id, out var channelBConversation));
        Assert.Single(
            channelBConversation!.Entries,
            e => e.Kind == ConversationEntryKind.ChannelEvent);
        Assert.True(state.ChannelMessages.TryGetValue(channelB.Id, out var cachedB));
        Assert.Single(cachedB!, m => m.Kind == TownhallMessageKind.ChannelEvent);

        // Re-select A and confirm plain chat remains once (no reconstruction double-add).
        vm.SelectChannelCommand.Execute(channelAId).Subscribe();
        Assert.Single(vm.Messages, m => m.Content == "plain once");
    }
}
