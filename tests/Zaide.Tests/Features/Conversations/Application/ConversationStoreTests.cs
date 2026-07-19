using System;
using System.Linq;
using Xunit;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Conversations.Application;

public sealed class ConversationStoreTests
{
    [Fact]
    public void CreateChannelConversation_ProvisionsAuthoritativeChannelOwner()
    {
        var store = new ConversationStore();

        var conversation = store.CreateChannelConversation("channel-1");

        Assert.Equal(ConversationKind.Channel, conversation.Kind);
        Assert.Equal(ConversationId.ForChannel("channel-1"), conversation.Id);
        Assert.Empty(conversation.Participants.All);
        Assert.True(store.TryGet(conversation.Id, out var lookedUp));
        Assert.Same(conversation, lookedUp);
        Assert.True(store.TryGetChannelConversation("channel-1", out var byChannel));
        Assert.Same(conversation, byChannel);
    }

    [Fact]
    public void CreateChannelConversation_IsIdempotentForSameChannelId()
    {
        var store = new ConversationStore();

        var first = store.CreateChannelConversation("channel-2");
        var second = store.CreateChannelConversation("channel-2");

        Assert.Same(first, second);
    }

    [Fact]
    public void CreateDirectConversation_ProvisionsAtCreateTime()
    {
        var store = new ConversationStore();

        var conversation = store.CreateDirectConversation(
            ActorId.HumanUser,
            ActorId.PanelSeed("alpha"));

        Assert.Equal(ConversationKind.Direct, conversation.Kind);
        Assert.StartsWith("direct:", conversation.Id.Value, StringComparison.Ordinal);
        Assert.Equal(2, conversation.Participants.All.Count);
        Assert.True(conversation.Participants.Contains(ActorId.HumanUser));
        Assert.True(conversation.Participants.Contains(ActorId.PanelSeed("alpha")));
        Assert.True(store.TryGet(conversation.Id, out _));
    }

    [Fact]
    public void TryGet_ReturnsFalseForUnknownConversation()
    {
        var store = new ConversationStore();

        Assert.False(store.TryGet(ConversationId.ForChannel("missing"), out _));
        Assert.False(store.TryGetChannelConversation("missing", out _));
    }
}

public sealed class ConversationProvisioningIntegrationTests
{
    [Fact]
    public void TownhallViewModel_ProvisionsChannelConversationsForSeededChannels()
    {
        var store = ConversationsTestSupport.CreateStore();
        var vm = ConversationsTestSupport.CreateTownhallViewModel(store: store);

        Assert.Equal(3, vm.Channels.Count);
        foreach (var channel in vm.Channels)
        {
            Assert.True(store.TryGetChannelConversation(channel.Id, out var conversation));
            Assert.Equal(ConversationKind.Channel, conversation.Kind);
            Assert.Equal(ConversationId.ForChannel(channel.Id), conversation.Id);
        }
    }

    [Fact]
    public void TownhallViewModel_PreservesLegacyChannelMessagesCollections()
    {
        var state = new TownhallState();
        var vm = ConversationsTestSupport.CreateTownhallViewModel(state);

        Assert.Equal(3, state.ChannelMessages.Count);
        Assert.All(state.ChannelMessages.Values, messages => Assert.NotNull(messages));
        Assert.NotNull(vm.ActiveChannelId);
        Assert.Same(state.ChannelMessages[vm.ActiveChannelId], vm.Messages);
    }

    [Fact]
    public void AgentPanelHost_ProvisionsDirectConversationAtPanelCreate()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);

        var panel = host.CreatePanel("agent-x", "X Agent", "avatar_x");

        Assert.NotEqual(default, panel.ConversationId);
        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        Assert.Equal(ConversationKind.Direct, conversation.Kind);
        Assert.True(conversation.Participants.Contains(ActorId.HumanUser));
        Assert.True(conversation.Participants.Contains(ActorId.PanelCustom("agent-x")));
    }

    [Fact]
    public void AgentPanelHost_SeededFallbackAndCustomPanels_UseCanonicalParticipants()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);

        var seeded = host.CreatePanel();
        var fallback = host.CreatePanel();
        host.CreatePanel();
        host.CreatePanel();
        fallback = host.CreatePanel();
        var custom = host.CreatePanel("agent-x", "X Agent", "avatar_x");

        Assert.True(store.TryGet(seeded.ConversationId, out var seededConversation));
        Assert.True(seededConversation!.Participants.Contains(ActorId.PanelSeed("alpha")));

        Assert.True(store.TryGet(fallback.ConversationId, out var fallbackConversation));
        Assert.True(fallbackConversation!.Participants.Contains(ActorId.PanelFallback(1)));

        Assert.True(store.TryGet(custom.ConversationId, out var customConversation));
        Assert.True(customConversation!.Participants.Contains(ActorId.PanelCustom("agent-x")));
    }

    [Fact]
    public void AgentPanelHost_IdenticalCustomActorAcrossPanels_GetsDistinctDirectConversations()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);

        var first = host.CreatePanel("agent-x", "X Agent", "avatar_x");
        var second = host.CreatePanel("agent-x", "X Agent", "avatar_x");

        Assert.NotEqual(first.ConversationId, second.ConversationId);
        Assert.NotEqual(first.PanelId, second.PanelId);
        Assert.True(store.TryGet(first.ConversationId, out _));
        Assert.True(store.TryGet(second.ConversationId, out _));
    }

    [Fact]
    public void AgentPanelHost_ClosePanel_RetainsConversationInStore()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        panel.OutputHistory.Add("User: retained");

        var conversationId = panel.ConversationId;
        host.ClosePanel(panel.PanelId);

        Assert.Empty(host.Panels);
        Assert.True(store.TryGet(conversationId, out var conversation));
        Assert.Equal(ConversationKind.Direct, conversation!.Kind);
        Assert.Single(panel.OutputHistory);
    }

    [Fact]
    public void AgentPanelHost_PreservesLegacyOutputHistoryCollection()
    {
        var host = ConversationsTestSupport.CreatePanelHost();
        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");

        panel.OutputHistory.Add("User: hello");

        Assert.Single(panel.OutputHistory);
        Assert.Equal("User: hello", panel.OutputHistory[0]);
    }
}
