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
    public void AddMirroredActivityToConversation_StillTargetsCapturedConversation_WithTypedOwnership()
    {
        var store = ConversationsTestSupport.CreateStore();
        var vm = ConversationsTestSupport.CreateTownhallViewModel(store: store);
        var initialId = vm.ActiveChannelId!;
        var otherId = vm.Channels.First(c => c.Id != initialId).Id;
        Assert.True(store.TryGetChannelConversation(initialId, out var initialConversation));

        vm.AddMirroredActivityToConversation(
            initialConversation!.Id,
            ConversationEntryKind.UserChat,
            "Message on initial channel",
            ActorId.HumanUser,
            senderId: "user-1",
            senderName: "User");
        vm.SelectChannelCommand.Execute(otherId).Subscribe();
        Assert.True(store.TryGetChannelConversation(otherId, out var otherConversation));
        vm.AddMirroredActivityToConversation(
            otherConversation!.Id,
            ConversationEntryKind.UserChat,
            "Message on other channel",
            ActorId.HumanUser,
            senderId: "user-1",
            senderName: "User");

        Assert.True(store.TryGetChannelConversation(initialId, out initialConversation));
        Assert.True(store.TryGetChannelConversation(otherId, out otherConversation));
        Assert.Single(initialConversation!.Entries);
        Assert.Equal(2, otherConversation!.Entries.Count);
        Assert.Equal("Message on other channel", otherConversation.Entries[^1].Content);
    }

    [Fact]
    public void AddMirroredActivityToConversation_TargetsCapturedConversation_NotActiveChannel()
    {
        var store = ConversationsTestSupport.CreateStore();
        var vm = ConversationsTestSupport.CreateTownhallViewModel(store: store);
        var initialId = vm.ActiveChannelId!;
        var otherId = vm.Channels.First(c => c.Id != initialId).Id;
        Assert.True(store.TryGetChannelConversation(initialId, out var initialConversation));

        vm.AddMirroredActivityToConversation(
            initialConversation!.Id,
            ConversationEntryKind.UserChat,
            "Captured target",
            ActorId.HumanUser,
            senderId: "user-1",
            senderName: "User");
        vm.SelectChannelCommand.Execute(otherId).Subscribe();

        Assert.True(store.TryGetChannelConversation(initialId, out initialConversation));
        Assert.True(store.TryGetChannelConversation(otherId, out var otherConversation));
        Assert.Single(initialConversation!.Entries);
        Assert.Equal("Captured target", initialConversation.Entries[0].Content);
        Assert.Single(otherConversation!.Entries);
        Assert.Equal(ConversationEntryKind.ChannelEvent, otherConversation.Entries[0].Kind);
        Assert.Single(GetChannelMessages(vm, initialId));
        Assert.Single(GetChannelMessages(vm, otherId));
    }

    private static System.Collections.ObjectModel.ObservableCollection<TownhallMessage> GetChannelMessages(
        TownhallViewModel townhall,
        string channelId)
    {
        var stateField = typeof(TownhallViewModel)
            .GetField("_state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var state = stateField!.GetValue(townhall);
        var channelMessagesProperty = state!.GetType().GetProperty("ChannelMessages");
        var channelMessages = (System.Collections.Generic.Dictionary<string, System.Collections.ObjectModel.ObservableCollection<TownhallMessage>>)channelMessagesProperty!.GetValue(state)!;
        return channelMessages[channelId];
    }

    [Fact]
    public void FilterModeCompatibility_RemainsUnchangedAfterTypedProjection()
    {
        var store = ConversationsTestSupport.CreateStore();
        var vm = ConversationsTestSupport.CreateTownhallViewModel(store: store);
        Assert.True(store.TryGetChannelConversation(vm.ActiveChannelId!, out var conversation));
        vm.AddMirroredActivityToConversation(
            conversation!.Id,
            ConversationEntryKind.UserChat,
            "chat",
            ActorId.HumanUser,
            senderId: "user-1",
            senderName: "User");
        vm.AddMirroredActivityToConversation(
            conversation.Id,
            ConversationEntryKind.ChannelEvent,
            "event",
            ActorId.HumanUser,
            senderId: "user-1",
            senderName: "User");

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
