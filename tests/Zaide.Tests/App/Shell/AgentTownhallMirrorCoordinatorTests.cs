using System;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Zaide.Tests.Features.Conversations;
using Zaide.App.Shell;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Tests.Features.Agents;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;

namespace Zaide.Tests.App.Shell;

/// <summary>
/// Focused behavioral coverage for the agent-panel send coordinator (routing,
/// privacy, cancellation). Public MWVM send coverage remains in
/// <see cref="MainWindowViewModelTests"/>.
/// </summary>
public sealed class AgentTownhallMirrorCoordinatorTests
{
    private static (AgentTownhallMirrorCoordinator Coordinator, AgentPanelHost Host, TownhallViewModel Townhall, AgentPanelState Panel, Mock<IAgentExecutionCoordinator> Exec, IConversationStore Store)
        CreateSut(string statusOnCompletion = "Idle", bool appendAssistantOutput = true)
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var panel = host.CreatePanel("agent-1", "Test Agent", "avatar_test");
        var exec = new Mock<IAgentExecutionCoordinator>();
        exec.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((id, msg, _) =>
            {
                var p = host.Panels.FirstOrDefault(pp => pp.PanelId == id);
                if (p is null)
                    return Task.FromResult<AgentExecutionCoordinatorResult?>(null);

                if (appendAssistantOutput && statusOnCompletion != "Error")
                {
                    AgentPanelTestSupport.SimulateDirectSendSuccess(store, p, msg);
                    p.Status = statusOnCompletion;
                    return Task.FromResult<AgentExecutionCoordinatorResult?>(
                        AgentExecutionTestSupport.SuccessResult(p));
                }

                if (appendAssistantOutput && statusOnCompletion == "Error")
                {
                    AgentPanelTestSupport.SimulateDirectSendError(store, p, msg);
                    p.Status = statusOnCompletion;
                    return Task.FromResult<AgentExecutionCoordinatorResult?>(
                        AgentExecutionTestSupport.ErrorResult(p));
                }

                AgentPanelTestSupport.AppendUserChat(store, p, msg);
                p.Status = statusOnCompletion;
                p.IsBusy = false;
                return Task.FromResult<AgentExecutionCoordinatorResult?>(null);
            });

        var router = new AgentRouter(new MentionParser(), host, exec.Object);
        var townhall = ConversationsTestSupport.CreateTownhallViewModel(store: store);
        townhall.SelectChannelCommand.Execute(townhall.Channels[0].Id).Subscribe();

        var coordinator = new AgentTownhallMirrorCoordinator(router);
        return (coordinator, host, townhall, panel, exec, store);
    }

    [Fact]
    public async Task SendAsync_DoesNotMirrorUserIntoActiveChannel()
    {
        var (sut, _, townhall, panel, _, store) = CreateSut();
        var channelId = townhall.ActiveChannelId!;
        var before = townhall.Messages.Count;

        await sut.SendAsync(panel.PanelId, "Private user hello", CancellationToken.None);

        Assert.Equal(before, townhall.Messages.Count);
        Assert.True(store.TryGetChannelConversation(channelId, out var channelConversation));
        Assert.Empty(channelConversation!.Entries);
    }

    [Fact]
    public async Task SendAsync_DoesNotMirrorAssistantIntoActiveChannel()
    {
        var (sut, _, townhall, panel, _, store) = CreateSut();
        var channelId = townhall.ActiveChannelId!;
        var before = townhall.Messages.Count;

        await sut.SendAsync(panel.PanelId, "Hello", CancellationToken.None);

        Assert.Equal(before, townhall.Messages.Count);
        Assert.True(store.TryGetChannelConversation(channelId, out var channelConversation));
        Assert.Empty(channelConversation!.Entries);
        Assert.Equal(2, panel.OutputHistory.Count);
        Assert.Equal("User: Hello", panel.OutputHistory[0]);
        Assert.Equal("Assistant: Hello back", panel.OutputHistory[1]);
    }

    [Fact]
    public async Task SendAsync_DoesNotMirrorErrorIntoActiveChannel()
    {
        var (sut, _, townhall, panel, _, store) = CreateSut(statusOnCompletion: "Error");
        var channelId = townhall.ActiveChannelId!;
        var before = townhall.Messages.Count;

        await sut.SendAsync(panel.PanelId, "Hello", CancellationToken.None);

        Assert.Equal(before, townhall.Messages.Count);
        Assert.True(store.TryGetChannelConversation(channelId, out var channelConversation));
        Assert.Empty(channelConversation!.Entries);
        Assert.Equal("Error: Request failed", panel.OutputHistory[^1]);
    }

    [Fact]
    public async Task SendAsync_DirectConversationStillReceivesExecutionEntries()
    {
        var (sut, _, _, panel, _, store) = CreateSut();

        await sut.SendAsync(panel.PanelId, "Direct only", CancellationToken.None);

        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        Assert.Equal(2, conversation!.Entries.Count);
        Assert.Equal(ConversationEntryKind.UserChat, conversation.Entries[0].Kind);
        Assert.Equal("Direct only", conversation.Entries[0].Content);
        Assert.Equal(ConversationEntryKind.AssistantResponse, conversation.Entries[1].Kind);
        Assert.Equal("Hello back", conversation.Entries[1].Content);
    }

    [Fact]
    public async Task SendAsync_RoutingFailure_DoesNotMirrorIntoActiveChannel()
    {
        var (sut, _, townhall, panel, exec, store) = CreateSut();
        var channelId = townhall.ActiveChannelId!;
        var before = townhall.Messages.Count;

        await sut.SendAsync(panel.PanelId, "@NonExistentAgent hello", CancellationToken.None);

        Assert.Equal(before, townhall.Messages.Count);
        Assert.True(store.TryGetChannelConversation(channelId, out var channelConversation));
        Assert.Empty(channelConversation!.Entries);
        exec.Verify(
            c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_SwitchDuringAwait_DoesNotCreateChannelEntries()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var panel = host.CreatePanel("agent-1", "Test Agent", "avatar_test");
        var exec = new Mock<IAgentExecutionCoordinator>();
        var admissionGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        exec.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(async (id, msg, _) =>
            {
                await admissionGate.Task;
                var p = host.Panels.First(pp => pp.PanelId == id);
                AgentPanelTestSupport.SimulateDirectSendSuccess(store, p, msg);
                return AgentExecutionTestSupport.SuccessResult(p);
            });

        var router = new AgentRouter(new MentionParser(), host, exec.Object);
        var townhall = ConversationsTestSupport.CreateTownhallViewModel(store: store);
        var channelA = townhall.ActiveChannelId!;
        var channelB = townhall.Channels.First(c => c.Id != channelA).Id;
        var sut = new AgentTownhallMirrorCoordinator(router);
        var beforeA = GetChannelMessages(townhall, channelA).Count;
        var beforeB = GetChannelMessages(townhall, channelB).Count;

        var sendTask = sut.SendAsync(panel.PanelId, "delayed hello", CancellationToken.None);
        townhall.SelectChannelCommand.Execute(channelB).Subscribe();
        admissionGate.SetResult(true);
        await sendTask;

        Assert.Equal(beforeA, GetChannelMessages(townhall, channelA).Count);
        Assert.Equal(beforeB + 1, GetChannelMessages(townhall, channelB).Count);
        Assert.Equal(TownhallMessageKind.ChannelEvent, GetChannelMessages(townhall, channelB)[^1].Kind);
        Assert.True(store.TryGetChannelConversation(channelA, out var conversationA));
        Assert.True(store.TryGetChannelConversation(channelB, out var conversationB));
        Assert.Empty(conversationA!.Entries);
        Assert.Single(
            conversationB!.Entries,
            entry => entry.Kind == ConversationEntryKind.ChannelEvent);
    }

    [Fact]
    public async Task SendAsync_PassesCancellationTokenToRouterExecution()
    {
        var host = ConversationsTestSupport.CreatePanelHost();
        var panel = host.CreatePanel("agent-1", "Test Agent", "avatar_test");
        var exec = new Mock<IAgentExecutionCoordinator>();
        CancellationToken observed = default;
        exec.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, _, ct) => observed = ct)
            .Returns<string, string, CancellationToken>((id, _, _) =>
            {
                var p = host.Panels.First(pp => pp.PanelId == id);
                return Task.FromResult<AgentExecutionCoordinatorResult?>(
                    AgentExecutionTestSupport.SuccessResult(p));
            });

        var router = new AgentRouter(new MentionParser(), host, exec.Object);
        var townhall = ConversationsTestSupport.CreateTownhallViewModel();
        townhall.SelectChannelCommand.Execute(townhall.Channels[0].Id).Subscribe();
        var sut = new AgentTownhallMirrorCoordinator(router);

        using var cts = new CancellationTokenSource();
        await sut.SendAsync(panel.PanelId, "token check", cts.Token);

        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task SendAsync_CancelledToken_DoesNotMirrorUserIntoChannel()
    {
        var host = ConversationsTestSupport.CreatePanelHost();
        var panel = host.CreatePanel("agent-1", "Test Agent", "avatar_test");
        var exec = new Mock<IAgentExecutionCoordinator>();
        exec.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var router = new AgentRouter(new MentionParser(), host, exec.Object);
        var townhall = ConversationsTestSupport.CreateTownhallViewModel();
        townhall.SelectChannelCommand.Execute(townhall.Channels[0].Id).Subscribe();
        var sut = new AgentTownhallMirrorCoordinator(router);
        var before = townhall.Messages.Count;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.SendAsync(panel.PanelId, "cancel me", CancellationToken.None));

        Assert.Equal(before, townhall.Messages.Count);
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
}
