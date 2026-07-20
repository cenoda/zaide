using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Tests.Features.Agents;
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
    public void CreateDirectConversation_DoesNotRegisterPairIndex()
    {
        var store = new ConversationStore();
        var human = ActorId.HumanUser;
        var agent = ActorId.PanelSeed("alpha");

        var first = store.CreateDirectConversation(human, agent);
        var second = store.CreateDirectConversation(human, agent);

        Assert.NotEqual(first.Id, second.Id);
        Assert.False(store.TryGetDirectConversation(human, agent, out _));
    }

    [Fact]
    public void GetOrCreateDirectConversation_ReturnsSameConversationForSamePair()
    {
        var store = new ConversationStore();
        var human = ActorId.HumanUser;
        var agent = ActorId.PanelSeed("alpha");

        var first = store.GetOrCreateDirectConversation(human, agent);
        var second = store.GetOrCreateDirectConversation(agent, human);

        Assert.Same(first, second);
        Assert.Equal(first.Id, second.Id);
        Assert.True(store.TryGetDirectConversation(human, agent, out var lookedUp));
        Assert.Same(first, lookedUp);
    }

    [Fact]
    public void GetOrCreateDirectConversation_DifferentPairsAreDistinct()
    {
        var store = new ConversationStore();
        var human = ActorId.HumanUser;
        var alpha = ActorId.PanelSeed("alpha");
        var beta = ActorId.PanelSeed("beta");

        var alphaConversation = store.GetOrCreateDirectConversation(human, alpha);
        var betaConversation = store.GetOrCreateDirectConversation(human, beta);

        Assert.NotEqual(alphaConversation.Id, betaConversation.Id);
        Assert.NotSame(alphaConversation, betaConversation);
    }

    [Fact]
    public void GetOrCreateDirectConversation_RejectsSameParticipant()
    {
        var store = new ConversationStore();

        Assert.Throws<ArgumentException>(() =>
            store.GetOrCreateDirectConversation(ActorId.HumanUser, ActorId.HumanUser));
    }

    [Fact]
    public void ListConversations_IncludesChannelsAndDirectsAfterCreate()
    {
        var store = new ConversationStore();
        var channel = store.CreateChannelConversation("channel-1");
        var direct = store.GetOrCreateDirectConversation(
            ActorId.HumanUser,
            ActorId.PanelSeed("alpha"));

        var conversations = store.ListConversations();

        Assert.Equal(2, conversations.Count);
        Assert.Contains(conversations, conversation => conversation.Id == channel.Id);
        Assert.Contains(conversations, conversation => conversation.Id == direct.Id);
    }

    [Fact]
    public async Task GetOrCreateDirectConversation_ConcurrentSamePair_DoesNotCreateDuplicates()
    {
        var store = new ConversationStore();
        var human = ActorId.HumanUser;
        var agent = ActorId.PanelSeed("alpha");
        var results = new List<Conversation>();
        var tasks = new List<Task>();

        for (var i = 0; i < 32; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var conversation = store.GetOrCreateDirectConversation(human, agent);
                lock (results)
                {
                    results.Add(conversation);
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(32, results.Count);
        Assert.Single(results.Select(conversation => conversation.Id).Distinct());
        Assert.Equal(1, store.ListConversations().Count(conversation =>
            conversation.Kind == ConversationKind.Direct));
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
    public void AgentPanelHost_IdenticalCustomActorAcrossPanels_SharesDirectConversation()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);

        var first = host.CreatePanel("agent-x", "X Agent", "avatar_x");
        var second = host.CreatePanel("agent-x", "X Agent", "avatar_x");

        Assert.Equal(first.ConversationId, second.ConversationId);
        Assert.NotEqual(first.PanelId, second.PanelId);
        Assert.True(store.TryGet(first.ConversationId, out _));
    }

    [Fact]
    public void AgentPanelHost_CloseThenRecreate_ReusesConversationIdForSameActor()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);

        var first = host.CreatePanel("agent-x", "X Agent", "avatar_x");
        AgentPanelTestSupport.AppendUserChat(store, first, "retained");
        var conversationId = first.ConversationId;

        host.ClosePanel(first.PanelId);
        Assert.Empty(host.Panels);

        var second = host.CreatePanel("agent-x", "X Agent", "avatar_x");

        Assert.Equal(conversationId, second.ConversationId);
        Assert.NotEqual(first.PanelId, second.PanelId);
        Assert.True(store.TryGet(conversationId, out var conversation));
        Assert.Single(conversation!.Entries);
        Assert.Equal("retained", conversation.Entries[0].Content);
    }

    [Fact]
    public void AgentPanelHost_ClosePanel_RetainsConversationInStore()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        AgentPanelTestSupport.AppendUserChat(store, panel, "retained");

        var conversationId = panel.ConversationId;
        host.ClosePanel(panel.PanelId);

        Assert.Empty(host.Panels);
        Assert.True(store.TryGet(conversationId, out var conversation));
        Assert.Equal(ConversationKind.Direct, conversation!.Kind);
        Assert.Single(conversation.Entries);
        Assert.Equal("retained", conversation.Entries[0].Content);
        Assert.Equal("User: retained", panel.OutputHistory[0]);
    }

    [Fact]
    public void AgentPanelHost_PanelIdAndConversationId_AreDistinctImmutableAssociations()
    {
        var host = ConversationsTestSupport.CreatePanelHost();

        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");

        Assert.False(string.IsNullOrEmpty(panel.PanelId));
        Assert.NotEqual(default, panel.ConversationId);
        Assert.NotEqual(panel.PanelId, panel.ConversationId.Value);

        panel.PanelId = "replacement-panel-id";

        Assert.Equal("replacement-panel-id", panel.PanelId);
        Assert.StartsWith("direct:", panel.ConversationId.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentPanelHost_ProjectsOutputHistoryFromAuthoritativeEntries()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");

        AgentPanelTestSupport.AppendUserChat(store, panel, "hello");

        Assert.Single(panel.OutputHistory);
        Assert.Equal("User: hello", panel.OutputHistory[0]);
    }
}
