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
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Tests.Features.Agents;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Townhall.Presentation;

/// <summary>
/// Phase 14 M5: per-conversation draft preservation and last-read / unread cursor.
/// </summary>
public sealed class TownhallDraftAndUnreadTests
{
    private static (TownhallViewModel Vm, ConversationStore Store) CreateViewModelWithStore()
    {
        var store = ConversationsTestSupport.CreateStore();
        var vm = ConversationsTestSupport.CreateTownhallViewModel(store: store);
        return (vm, store);
    }

    [Fact]
    public void Draft_SurvivesChannelThenDirectThenBackToChannel()
    {
        var (vm, store) = CreateViewModelWithStore();
        var channelId = vm.ActiveChannelId!;
        var agentId = vm.Agents.First(a => a.Role == "agent").ActorId;

        vm.DraftText = "channel draft";
        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();
        Assert.Equal(string.Empty, vm.DraftText);

        vm.DraftText = "direct draft";
        vm.SelectChannelCommand.Execute(channelId).Subscribe();

        Assert.Equal("channel draft", vm.DraftText);

        Assert.True(store.TryGetDirectConversation(ActorId.HumanUser, agentId, out var direct));
        vm.SelectConversationCommand.Execute(direct!.Id).Subscribe();
        Assert.Equal("direct draft", vm.DraftText);
    }

    [Fact]
    public void Draft_SurvivesDm1ThenDm2ThenDm1()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store);
        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            catalog: catalog,
            store: store,
            panelHost: host);

        var agentA = ActorId.PanelSeed("alpha");
        var agentB = ActorId.PanelSeed("beta");
        Assert.True(catalog.TryGet(agentA, out _));
        Assert.True(catalog.TryGet(agentB, out _));

        // OpenDirect uses ActorId get-or-create; roster membership is not required.
        vm.OpenDirectConversationCommand.Execute(agentA).Subscribe();
        vm.DraftText = "dm1 draft";

        vm.OpenDirectConversationCommand.Execute(agentB).Subscribe();
        Assert.Equal(string.Empty, vm.DraftText);
        vm.DraftText = "dm2 draft";

        Assert.True(store.TryGetDirectConversation(ActorId.HumanUser, agentA, out var dm1));
        vm.SelectConversationCommand.Execute(dm1!.Id).Subscribe();
        Assert.Equal("dm1 draft", vm.DraftText);

        Assert.True(store.TryGetDirectConversation(ActorId.HumanUser, agentB, out var dm2));
        vm.SelectConversationCommand.Execute(dm2!.Id).Subscribe();
        Assert.Equal("dm2 draft", vm.DraftText);
    }

    [Fact]
    public async Task Send_ClearsOnlyActiveConversationDraft()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var coordinator = AgentExecutionTestSupport.CreateCoordinatorFromHandler(
            host,
            _ => Task.FromResult(AgentExecutionResult.Success("ok")),
            store);
        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            store: store,
            panelHost: host,
            executionCoordinator: coordinator);

        var channelId = vm.ActiveChannelId!;
        var agentId = vm.Agents.First(a => a.Role == "agent").ActorId;

        vm.DraftText = "keep channel draft";
        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();
        vm.DraftText = "send this dm";
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.Equal(string.Empty, vm.DraftText);

        vm.SelectChannelCommand.Execute(channelId).Subscribe();
        Assert.Equal("keep channel draft", vm.DraftText);

        Assert.True(store.TryGetDirectConversation(ActorId.HumanUser, agentId, out var direct));
        vm.SelectConversationCommand.Execute(direct!.Id).Subscribe();
        Assert.Equal(string.Empty, vm.DraftText);
    }

    [Fact]
    public void EntryAppended_ToInactiveConversation_MarksUnread_SelectClears()
    {
        var (vm, store) = CreateViewModelWithStore();
        var agentId = vm.Agents.First(a => a.Role == "agent").ActorId;
        var channelId = vm.ActiveChannelId!;

        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();
        Assert.True(store.TryGetDirectConversation(ActorId.HumanUser, agentId, out var direct));
        var directId = direct!.Id;

        // Select channel so DM is inactive.
        vm.SelectChannelCommand.Execute(channelId).Subscribe();

        store.AppendEntry(
            directId,
            ConversationEntry.UserChat(
                ConversationEntryId.New(),
                ActorId.HumanUser,
                DateTimeOffset.UtcNow,
                "new activity on dm"));

        var navItem = Assert.Single(vm.DirectNavItems, i => i.ConversationId == directId);
        Assert.True(navItem.HasUnread);

        vm.SelectConversationCommand.Execute(directId).Subscribe();
        Assert.False(navItem.HasUnread);
        // Refresh may recreate items; re-resolve.
        navItem = Assert.Single(vm.DirectNavItems, i => i.ConversationId == directId);
        Assert.False(navItem.HasUnread);
    }

    [Fact]
    public void EntryAppended_ToInactiveChannel_MarksUnread_SelectClears()
    {
        var (vm, store) = CreateViewModelWithStore();
        var activeChannelId = vm.ActiveChannelId!;
        var otherChannel = vm.Channels.First(c => c.Id != activeChannelId);
        Assert.True(store.TryGetChannelConversation(otherChannel.Id, out var otherConversation));

        store.AppendEntry(
            otherConversation!.Id,
            ConversationEntry.UserChat(
                ConversationEntryId.New(),
                ActorId.HumanUser,
                DateTimeOffset.UtcNow,
                "side channel activity"));

        Assert.True(otherChannel.HasUnread);

        vm.SelectChannelCommand.Execute(otherChannel.Id).Subscribe();
        Assert.False(otherChannel.HasUnread);
    }

    [Fact]
    public void EntryAppended_ToActiveConversation_DoesNotLeaveStickyUnread()
    {
        var (vm, store) = CreateViewModelWithStore();
        var agentId = vm.Agents.First(a => a.Role == "agent").ActorId;
        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();
        var directId = vm.ActiveConversationId!.Value;

        store.AppendEntry(
            directId,
            ConversationEntry.UserChat(
                ConversationEntryId.New(),
                ActorId.HumanUser,
                DateTimeOffset.UtcNow,
                "active dm message"));

        var navItem = Assert.Single(vm.DirectNavItems, i => i.ConversationId == directId);
        Assert.False(navItem.HasUnread);
        Assert.False(ConversationHasUnread(vm, store, directId));
    }

    [Fact]
    public async Task ChannelSend_DoesNotMarkActiveChannelUnread()
    {
        var (vm, store) = CreateViewModelWithStore();
        var channelId = vm.ActiveChannelId!;
        var channel = vm.Channels.First(c => c.Id == channelId);

        vm.DraftText = "hello channel";
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.False(channel.HasUnread);
        Assert.True(store.TryGetChannelConversation(channelId, out var conversation));
        Assert.NotEmpty(conversation!.Entries);
    }

    [Fact]
    public void OpenNewDirect_StartsWithEmptyDraftUnlessPresent()
    {
        var (vm, _) = CreateViewModelWithStore();
        var agentId = vm.Agents.First(a => a.Role == "agent").ActorId;

        vm.DraftText = "channel stuff";
        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();
        Assert.Equal(string.Empty, vm.DraftText);

        // Re-open same DM after typing — draft present.
        vm.DraftText = "saved";
        var channelId = vm.Channels[0].Id;
        vm.SelectChannelCommand.Execute(channelId).Subscribe();
        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();
        Assert.Equal("saved", vm.DraftText);
    }

    private static bool ConversationHasUnread(
        TownhallViewModel vm,
        ConversationStore store,
        ConversationId conversationId)
    {
        // Presentation truth: nav item or channel flag after append handling.
        if (store.TryGet(conversationId, out var conversation)
            && conversation!.Kind == ConversationKind.Direct)
        {
            var item = vm.DirectNavItems.FirstOrDefault(i => i.ConversationId == conversationId);
            return item?.HasUnread ?? false;
        }

        if (conversationId.TryGetChannelId(out var channelId))
        {
            return vm.Channels.First(c => c.Id == channelId).HasUnread;
        }

        return false;
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
