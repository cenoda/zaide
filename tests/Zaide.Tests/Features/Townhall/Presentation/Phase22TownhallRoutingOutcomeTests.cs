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
        IActorCatalog Catalog) CreateRoutedSurface()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store);
        var (coordinator, backend, session) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(host, store);
        backend.SetCompletion("target reply");
        _ = new AgentConversationEventProjection(session.Events, store, catalog);
        var router = new AgentRouter(new MentionParser(), host, coordinator, catalog, store);
        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            catalog: catalog,
            store: store,
            panelHost: host,
            executionCoordinator: coordinator,
            agentRouter: router);
        return (vm, store, host, coordinator, catalog);
    }

    [Fact]
    public async Task DirectValidMention_RoutesOnceToTargetConversation()
    {
        var (vm, store, _, _, _) = CreateRoutedSurface();
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
        var (vm, store, host, _, _) = CreateRoutedSurface();
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
        var (vm, store, host, _, _) = CreateRoutedSurface();
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
        var (vm, store, _, _, _) = CreateRoutedSurface();
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
        var (vm, store, _, _, _) = CreateRoutedSurface();
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
        var (vm, store, _, _, _) = CreateRoutedSurface();
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
        var (vm, store, host, _, catalog) = CreateRoutedSurface();
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
        var (vm, store, _, _, _) = CreateRoutedSurface();
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
}
