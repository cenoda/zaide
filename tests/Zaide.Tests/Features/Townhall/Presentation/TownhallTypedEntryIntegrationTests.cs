using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Townhall.Presentation;

public sealed class TownhallTypedEntryIntegrationTests
{
    [Fact]
    public void SendMessage_WritesAuthoritativeTypedEntryAndProjectsCompatibilityMessage()
    {
        var store = ConversationsTestSupport.CreateStore();
        var vm = ConversationsTestSupport.CreateTownhallViewModel(store: store);
        var channelId = vm.ActiveChannelId!;

        vm.DraftText = "Townhall hello";
        vm.SendMessageCommand.Execute().Subscribe();

        Assert.True(store.TryGetChannelConversation(channelId, out var conversation));
        var entry = Assert.Single(conversation!.Entries);
        Assert.Equal(ConversationEntryKind.UserChat, entry.Kind);
        Assert.Equal(ActorId.HumanUser, entry.Author);
        Assert.Equal("Townhall hello", entry.Content);

        var message = Assert.Single(vm.Messages);
        Assert.Equal(TownhallMessageKind.Chat, message.Kind);
        Assert.Equal("Townhall hello", message.Content);
        Assert.Equal("user-1", message.SenderId);
        Assert.Equal("avatar-user", message.SenderAvatar);
    }

    [Fact]
    public void SelectChannel_StillLogsChannelEventToActiveChannel()
    {
        var store = ConversationsTestSupport.CreateStore();
        var vm = ConversationsTestSupport.CreateTownhallViewModel(store: store);
        var initialId = vm.ActiveChannelId!;
        var otherId = vm.Channels.First(c => c.Id != initialId).Id;

        vm.SelectChannelCommand.Execute(otherId).Subscribe();

        Assert.True(store.TryGetChannelConversation(otherId, out var conversation));
        var entry = Assert.Single(conversation!.Entries);
        Assert.Equal(ConversationEntryKind.ChannelEvent, entry.Kind);
        Assert.Equal(ActorId.HumanUser, entry.Author);
        Assert.Contains("Switched to #", entry.Content, StringComparison.Ordinal);

        var message = Assert.Single(vm.Messages);
        Assert.Equal(TownhallMessageKind.ChannelEvent, message.Kind);
    }

    [Fact]
    public void FilterModeCompatibility_RemainsUnchangedAfterTypedProjection()
    {
        var store = ConversationsTestSupport.CreateStore();
        var vm = ConversationsTestSupport.CreateTownhallViewModel(store: store);
        var initialId = vm.ActiveChannelId!;
        var otherId = vm.Channels.First(c => c.Id != initialId).Id;

        vm.DraftText = "chat";
        vm.SendMessageCommand.Execute().Subscribe();
        vm.SelectChannelCommand.Execute(otherId).Subscribe();
        vm.SelectChannelCommand.Execute(initialId).Subscribe();

        vm.FilterMode = FilterMode.ChatOnly;
        IReadOnlyList<TownhallMessage>? filtered = null;
        using var sub = vm.FilteredMessages.Subscribe(list => filtered = list);
        Assert.NotNull(filtered);
        Assert.All(filtered!, message => Assert.Equal(TownhallMessageKind.Chat, message.Kind));

        vm.FilterMode = FilterMode.ActivityOnly;
        using var sub2 = vm.FilteredMessages.Subscribe(list => filtered = list);
        Assert.NotNull(filtered);
        Assert.All(filtered!, message => Assert.NotEqual(TownhallMessageKind.Chat, message.Kind));
    }
}
