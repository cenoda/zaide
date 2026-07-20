using System;
using System.Linq;
using Xunit;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Townhall.Presentation;

public class TownhallNavigationTests
{
    private static (TownhallViewModel Vm, ConversationStore Store) CreateViewModelWithStore()
    {
        var store = ConversationsTestSupport.CreateStore();
        var state = new TownhallState();
        var vm = ConversationsTestSupport.CreateTownhallViewModel(state, store: store);
        return (vm, store);
    }

    [Fact]
    public void SelectChannel_SetsActiveConversationId()
    {
        var (vm, store) = CreateViewModelWithStore();
        var otherChannelId = vm.Channels.First(c => c.Id != vm.ActiveChannelId).Id;

        vm.SelectChannelCommand.Execute(otherChannelId).Subscribe();

        Assert.Equal(otherChannelId, vm.ActiveChannelId);
        Assert.True(store.TryGetChannelConversation(otherChannelId, out var conversation));
        Assert.Equal(conversation!.Id, vm.ActiveConversationId);
    }

    [Fact]
    public void OpenDirectConversation_Twice_SelectsSameConversationId()
    {
        var (vm, store) = CreateViewModelWithStore();
        var agentId = vm.Agents.First(a => a.Role == "agent").ActorId;

        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();
        var firstId = vm.ActiveConversationId;

        vm.SelectChannelCommand.Execute(vm.Channels[0].Id).Subscribe();
        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();

        Assert.NotNull(firstId);
        Assert.Equal(firstId, vm.ActiveConversationId);
        Assert.True(store.TryGetDirectConversation(ActorId.HumanUser, agentId, out var conversation));
        Assert.Equal(firstId, conversation!.Id);
    }

    [Fact]
    public void Navigation_IncludesChannels_AndDirectAfterOpen()
    {
        var (vm, store) = CreateViewModelWithStore();

        Assert.True(vm.Channels.Count >= 3);
        Assert.DoesNotContain(
            store.ListConversations(),
            c => c.Kind == ConversationKind.Direct);

        var agentId = vm.Agents.First(a => a.Role == "agent").ActorId;
        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();

        var directs = store.ListConversations()
            .Where(c => c.Kind == ConversationKind.Direct)
            .ToList();
        Assert.Single(directs);
        Assert.Equal(directs[0].Id, vm.ActiveConversationId);
        Assert.True(store.TryGet(directs[0].Id, out var conversation));
        Assert.Contains(agentId, conversation!.Participants.All);
    }

    [Fact]
    public void SwitchingChannelAndDirect_UpdatesSelectionWithoutThrowing()
    {
        var (vm, store) = CreateViewModelWithStore();
        var agentId = vm.Agents.First(a => a.Role == "agent").ActorId;
        var channelId = vm.Channels[1].Id;

        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();
        Assert.Null(vm.ActiveChannelId);
        Assert.NotNull(vm.ActiveConversationId);

        vm.SelectChannelCommand.Execute(channelId).Subscribe();
        Assert.Equal(channelId, vm.ActiveChannelId);
        Assert.True(store.TryGetChannelConversation(channelId, out var channelConversation));
        Assert.Equal(channelConversation!.Id, vm.ActiveConversationId);

        Assert.True(store.TryGetDirectConversation(ActorId.HumanUser, agentId, out var directConversation));
        vm.SelectConversationCommand.Execute(directConversation!.Id).Subscribe();
        Assert.Null(vm.ActiveChannelId);
        Assert.Equal(directConversation.Id, vm.ActiveConversationId);
    }

    [Fact]
    public void DirectSelection_ProjectsStoreEntriesToMessages()
    {
        var (vm, store) = CreateViewModelWithStore();
        var agentId = vm.Agents.First(a => a.Role == "agent").ActorId;
        var conversation = store.GetOrCreateDirectConversation(ActorId.HumanUser, agentId);
        var entry = ConversationEntry.UserChat(
            ConversationEntryId.New(),
            ActorId.HumanUser,
            DateTimeOffset.UtcNow,
            "Hello agent");
        store.AppendEntry(conversation.Id, entry);

        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();

        Assert.Single(vm.Messages);
        Assert.Equal("Hello agent", vm.Messages[0].Content);
    }
}
