using System;
using System.Linq;
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
/// Focused behavioral coverage for the M9a-extracted
/// <see cref="AgentTownhallMirrorCoordinator"/> (mirroring once, routing,
/// cancellation). Public MWVM send coverage remains in
/// <see cref="MainWindowViewModelTests"/>.
/// </summary>
public sealed class AgentTownhallMirrorCoordinatorTests
{
    private static (AgentTownhallMirrorCoordinator Coordinator, AgentPanelHost Host, TownhallViewModel Townhall, AgentPanelState Panel, Mock<IAgentExecutionCoordinator> Exec)
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
        var townhall = ConversationsTestSupport.CreateTownhallViewModel();
        var channelId = townhall.Channels[0].Id;
        townhall.SelectChannelCommand.Execute(channelId).Subscribe();

        var coordinator = new AgentTownhallMirrorCoordinator(router, host, townhall, ConversationsTestSupport.CreateCatalogAsInterface());
        return (coordinator, host, townhall, panel, exec);
    }

    [Fact]
    public async Task SendAsync_MirrorsUserMessageExactlyOnce()
    {
        var (sut, _, townhall, panel, _) = CreateSut();
        var before = townhall.Messages.Count;

        await sut.SendAsync(panel.PanelId, "Hello once", CancellationToken.None);

        var userEntries = townhall.Messages
            .Skip(before)
            .Where(m => m.SenderId == "user-1")
            .ToList();
        Assert.Single(userEntries);
        Assert.Equal("Hello once", userEntries[0].Content);
        Assert.Equal(TownhallMessageKind.Chat, userEntries[0].Kind);
    }

    [Fact]
    public async Task SendAsync_SuccessfulResponse_MirroredExactlyOnce()
    {
        var (sut, _, townhall, panel, _) = CreateSut();
        var before = townhall.Messages.Count;

        await sut.SendAsync(panel.PanelId, "Hello", CancellationToken.None);

        Assert.Equal(before + 2, townhall.Messages.Count);
        var responseEntries = townhall.Messages
            .Skip(before)
            .Where(m => m.SenderId == "agent-1" && m.Kind == TownhallMessageKind.Chat)
            .ToList();
        Assert.Single(responseEntries);
        Assert.Equal("Assistant: Hello back", responseEntries[0].Content);
        Assert.Equal("User: Hello", panel.OutputHistory[0]);
        Assert.Equal("Assistant: Hello back", panel.OutputHistory[1]);
    }

    [Fact]
    public async Task SendAsync_Error_MirroredExactlyOnce()
    {
        var (sut, _, townhall, panel, _) = CreateSut(statusOnCompletion: "Error");
        var before = townhall.Messages.Count;

        await sut.SendAsync(panel.PanelId, "Hello", CancellationToken.None);

        Assert.Equal(before + 2, townhall.Messages.Count);
        var errorEntries = townhall.Messages
            .Skip(before)
            .Where(m => m.Kind == TownhallMessageKind.AgentError)
            .ToList();
        Assert.Single(errorEntries);
        Assert.Equal("Error: Request failed", errorEntries[0].Content);
        Assert.Equal("agent-1", errorEntries[0].SenderId);
    }

    [Fact]
    public async Task SendAsync_RoutingFailure_MirrorsStructuredRoutingFailureRun()
    {
        var (sut, _, townhall, panel, exec) = CreateSut();
        var before = townhall.Messages.Count;

        await sut.SendAsync(panel.PanelId, "@NonExistentAgent hello", CancellationToken.None);

        Assert.Equal(before + 2, townhall.Messages.Count);
        Assert.Equal(TownhallMessageKind.AgentError, townhall.Messages[before + 1].Kind);
        Assert.Equal("Routing failed: Unknown target", townhall.Messages[before + 1].Content);
        Assert.Equal("agent-1", townhall.Messages[before + 1].SenderId);
        exec.Verify(
            c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_RoutingFailure_MirroredExactlyOnce()
    {
        var (sut, _, townhall, panel, exec) = CreateSut();
        var before = townhall.Messages.Count;

        await sut.SendAsync(panel.PanelId, "@NonExistentAgent hello", CancellationToken.None);

        Assert.Equal(before + 2, townhall.Messages.Count);
        Assert.Equal(TownhallMessageKind.Chat, townhall.Messages[before].Kind);
        Assert.Equal(TownhallMessageKind.AgentError, townhall.Messages[before + 1].Kind);
        Assert.Contains("Routing failed", townhall.Messages[before + 1].Content);
        exec.Verify(
            c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
        var sut = new AgentTownhallMirrorCoordinator(router, host, townhall, ConversationsTestSupport.CreateCatalogAsInterface());

        using var cts = new CancellationTokenSource();
        await sut.SendAsync(panel.PanelId, "token check", cts.Token);

        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task SendAsync_CancelledToken_PropagatesWithoutExtraMirrorBeyondUser()
    {
        var host = ConversationsTestSupport.CreatePanelHost();
        var panel = host.CreatePanel("agent-1", "Test Agent", "avatar_test");
        var exec = new Mock<IAgentExecutionCoordinator>();
        exec.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var router = new AgentRouter(new MentionParser(), host, exec.Object);
        var townhall = ConversationsTestSupport.CreateTownhallViewModel();
        townhall.SelectChannelCommand.Execute(townhall.Channels[0].Id).Subscribe();
        var sut = new AgentTownhallMirrorCoordinator(router, host, townhall, ConversationsTestSupport.CreateCatalogAsInterface());
        var before = townhall.Messages.Count;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.SendAsync(panel.PanelId, "cancel me", CancellationToken.None));

        Assert.Equal(before + 1, townhall.Messages.Count);
        Assert.Equal("cancel me", townhall.Messages[before].Content);
        Assert.Equal(TownhallMessageKind.Chat, townhall.Messages[before].Kind);
    }

    [Fact]
    public async Task SendAsync_FallbackPanelWithCollidingLegacyId_StoresPanelFallbackAuthor()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        for (var i = 0; i < 4; i++)
        {
            host.CreatePanel();
        }

        var fallbackPanel = host.CreatePanel();
        Assert.Equal("agent-1", fallbackPanel.AgentId);
        Assert.Equal(ActorId.PanelFallback(1), fallbackPanel.ActorId);

        var exec = new Mock<IAgentExecutionCoordinator>();
        exec.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((id, msg, _) =>
            {
                var p = host.Panels.First(pp => pp.PanelId == id);
                AgentPanelTestSupport.SimulateDirectSendSuccess(store, p, msg);
                return Task.FromResult<AgentExecutionCoordinatorResult?>(
                    AgentExecutionTestSupport.SuccessResult(p));
            });

        var router = new AgentRouter(new MentionParser(), host, exec.Object);
        var townhall = ConversationsTestSupport.CreateTownhallViewModel(store: store);
        townhall.SelectChannelCommand.Execute(townhall.Channels[0].Id).Subscribe();
        var sut = new AgentTownhallMirrorCoordinator(
            router,
            host,
            townhall,
            ConversationsTestSupport.CreateCatalogAsInterface());

        await sut.SendAsync(fallbackPanel.PanelId, "Hello", CancellationToken.None);

        Assert.True(store.TryGetChannelConversation(townhall.ActiveChannelId!, out var conversation));
        var assistantEntry = conversation!.Entries
            .Single(e => e.Kind == ConversationEntryKind.AssistantResponse);
        Assert.Equal(ActorId.PanelFallback(1), assistantEntry.Author);

        var mirrored = townhall.Messages[^1];
        Assert.Equal("agent-1", mirrored.SenderId);
        Assert.Equal("Agent 1", mirrored.SenderName);
        Assert.Equal(TownhallMessageKind.Chat, mirrored.Kind);
        Assert.Equal("Assistant: Hello back", mirrored.Content);
    }

    [Fact]
    public async Task SendAsync_CustomPanelWithCollidingLegacyId_StoresPanelCustomAuthor()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var customPanel = host.CreatePanel("agent-1", "Custom Agent One", "avatar_custom");

        Assert.Equal("agent-1", customPanel.AgentId);
        Assert.Equal(ActorId.PanelCustom("agent-1"), customPanel.ActorId);

        var exec = new Mock<IAgentExecutionCoordinator>();
        exec.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((id, msg, _) =>
            {
                var p = host.Panels.First(pp => pp.PanelId == id);
                AgentPanelTestSupport.SimulateDirectSendSuccess(store, p, msg);
                return Task.FromResult<AgentExecutionCoordinatorResult?>(
                    AgentExecutionTestSupport.SuccessResult(p));
            });

        var router = new AgentRouter(new MentionParser(), host, exec.Object);
        var townhall = ConversationsTestSupport.CreateTownhallViewModel(store: store);
        townhall.SelectChannelCommand.Execute(townhall.Channels[0].Id).Subscribe();
        var sut = new AgentTownhallMirrorCoordinator(
            router,
            host,
            townhall,
            ConversationsTestSupport.CreateCatalogAsInterface());

        await sut.SendAsync(customPanel.PanelId, "Hello", CancellationToken.None);

        Assert.True(store.TryGetChannelConversation(townhall.ActiveChannelId!, out var conversation));
        var assistantEntry = conversation!.Entries
            .Single(e => e.Kind == ConversationEntryKind.AssistantResponse);
        Assert.Equal(ActorId.PanelCustom("agent-1"), assistantEntry.Author);

        var mirrored = townhall.Messages[^1];
        Assert.Equal("agent-1", mirrored.SenderId);
        Assert.Equal("Custom Agent One", mirrored.SenderName);
        Assert.Equal(TownhallMessageKind.Chat, mirrored.Kind);
        Assert.Equal("Assistant: Hello back", mirrored.Content);
    }

    [Fact]
    public async Task SendAsync_RoutedSuccess_UsesStructuredTargetActor_NotOutputHistoryParsing()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var source = host.CreatePanel("agent-1", "Alpha", "avatar_a");
        var target = host.CreatePanel("agent-2", "Beta", "avatar_b");
        var exec = new Mock<IAgentExecutionCoordinator>();
        exec.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((id, msg, _) =>
            {
                var p = host.Panels.First(pp => pp.PanelId == id);
                AgentPanelTestSupport.SimulateDirectSendSuccess(store, p, msg, "Routed reply");
                return Task.FromResult<AgentExecutionCoordinatorResult?>(
                    AgentExecutionTestSupport.SuccessResult(p, "Routed reply"));
            });

        var router = new AgentRouter(new MentionParser(), host, exec.Object);
        var townhall = ConversationsTestSupport.CreateTownhallViewModel();
        townhall.SelectChannelCommand.Execute(townhall.Channels[0].Id).Subscribe();
        var sut = new AgentTownhallMirrorCoordinator(
            router,
            host,
            townhall,
            ConversationsTestSupport.CreateCatalogAsInterface());
        var before = townhall.Messages.Count;

        await sut.SendAsync(source.PanelId, "@Beta routed hello", CancellationToken.None);

        var mirrored = townhall.Messages[^1];
        Assert.Equal("agent-2", mirrored.SenderId);
        Assert.Equal("Beta", mirrored.SenderName);
        Assert.Equal("Assistant: Routed reply", mirrored.Content);
        Assert.Equal(before + 2, townhall.Messages.Count);
    }

    [Fact]
    public async Task SendAsync_SwitchDuringAwait_Success_RemainsInAdmittedChannel()
    {
        var (sut, host, townhall, panel, exec, store, channelA, channelB) = CreateDelayedSut();
        var admissionGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        exec.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(async (id, msg, _) =>
            {
                await admissionGate.Task;
                var p = host.Panels.First(pp => pp.PanelId == id);
                AgentPanelTestSupport.SimulateDirectSendSuccess(store, p, msg);
                return AgentExecutionTestSupport.SuccessResult(p);
            });

        var sendTask = sut.SendAsync(panel.PanelId, "delayed hello", CancellationToken.None);
        townhall.SelectChannelCommand.Execute(channelB).Subscribe();
        admissionGate.SetResult(true);
        await sendTask;

        AssertSwitchDuringAwaitAttribution(townhall, store, channelA, channelB, expectTerminal: true);
        Assert.Equal("delayed hello", GetChannelMirrorContent(store, channelA, ConversationEntryKind.UserChat));
        Assert.Equal("Hello back", GetChannelMirrorContent(store, channelA, ConversationEntryKind.AssistantResponse));
    }

    [Fact]
    public async Task SendAsync_SwitchDuringAwait_ExecutionFailure_RemainsInAdmittedChannel()
    {
        var (sut, host, townhall, panel, exec, store, channelA, channelB) = CreateDelayedSut();
        var admissionGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        exec.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(async (id, msg, _) =>
            {
                await admissionGate.Task;
                var p = host.Panels.First(pp => pp.PanelId == id);
                AgentPanelTestSupport.SimulateDirectSendError(store, p, msg);
                return AgentExecutionTestSupport.ErrorResult(p);
            });

        var sendTask = sut.SendAsync(panel.PanelId, "delayed fail", CancellationToken.None);
        townhall.SelectChannelCommand.Execute(channelB).Subscribe();
        admissionGate.SetResult(true);
        await sendTask;

        AssertSwitchDuringAwaitAttribution(townhall, store, channelA, channelB, expectTerminal: true);
        var terminal = GetLastChannelMessage(townhall, channelA);
        Assert.Equal("Error: Request failed", terminal.Content);
        Assert.Equal(TownhallMessageKind.AgentError, terminal.Kind);
    }

    [Fact]
    public async Task SendAsync_SwitchDuringAwait_StructuredCancellation_RemainsInAdmittedChannel()
    {
        var (sut, host, townhall, panel, exec, store, channelA, channelB) = CreateDelayedSut();
        var admissionGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        exec.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(async (id, msg, _) =>
            {
                await admissionGate.Task;
                var p = host.Panels.First(pp => pp.PanelId == id);
                AgentPanelTestSupport.AppendUserChat(store, p, msg);
                return AgentExecutionTestSupport.ErrorResult(
                    p,
                    errorMessage: "Cancelled",
                    outcome: ExecutionRunOutcome.Cancelled);
            });

        var sendTask = sut.SendAsync(panel.PanelId, "delayed cancel", CancellationToken.None);
        townhall.SelectChannelCommand.Execute(channelB).Subscribe();
        admissionGate.SetResult(true);
        await sendTask;

        AssertSwitchDuringAwaitAttribution(townhall, store, channelA, channelB, expectTerminal: true);
        Assert.Equal("Error: Cancelled", GetLastChannelMessage(townhall, channelA).Content);
    }

    [Fact]
    public async Task SendAsync_SwitchDuringAwait_RoutingFailure_RemainsInAdmittedChannel()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var panel = host.CreatePanel("agent-1", "Test Agent", "avatar_test");
        var exec = new Mock<IAgentExecutionCoordinator>();
        var router = new AgentRouter(new MentionParser(), host, exec.Object);
        var townhall = ConversationsTestSupport.CreateTownhallViewModel(store: store);
        var channelA = townhall.ActiveChannelId!;
        var channelB = townhall.Channels.First(c => c.Id != channelA).Id;
        var sut = new AgentTownhallMirrorCoordinator(
            router,
            host,
            townhall,
            ConversationsTestSupport.CreateCatalogAsInterface());

        await sut.SendAsync(panel.PanelId, "@Missing routed", CancellationToken.None);
        townhall.SelectChannelCommand.Execute(channelB).Subscribe();

        AssertSwitchDuringAwaitAttribution(townhall, store, channelA, channelB, expectTerminal: true);
        Assert.Equal("Routing failed: Unknown target", GetLastChannelMessage(townhall, channelA).Content);
        exec.Verify(
            c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_SwitchDuringAwait_PropagatedCancellation_KeepsUserOnlyInAdmittedChannel()
    {
        var (sut, _, townhall, panel, exec, store, channelA, channelB) = CreateDelayedSut();
        var admissionGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        exec.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(async (_, _, _) =>
            {
                await admissionGate.Task;
                throw new OperationCanceledException();
            });

        var sendTask = sut.SendAsync(panel.PanelId, "propagated cancel", CancellationToken.None);
        townhall.SelectChannelCommand.Execute(channelB).Subscribe();
        admissionGate.SetResult(true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sendTask);

        AssertSwitchDuringAwaitAttribution(townhall, store, channelA, channelB, expectTerminal: false);
        Assert.Equal("propagated cancel", GetChannelMirrorContent(store, channelA, ConversationEntryKind.UserChat));
    }

    [Fact]
    public async Task SendAsync_NoSwitch_PreservesExistingBehavior()
    {
        var (sut, host, townhall, panel, exec, store, channelA, _) = CreateDelayedSut();
        exec.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((id, msg, _) =>
            {
                var p = host.Panels.First(pp => pp.PanelId == id);
                AgentPanelTestSupport.SimulateDirectSendSuccess(store, p, msg);
                return Task.FromResult<AgentExecutionCoordinatorResult?>(
                    AgentExecutionTestSupport.SuccessResult(p));
            });

        await sut.SendAsync(panel.PanelId, "no switch", CancellationToken.None);

        Assert.True(store.TryGetChannelConversation(channelA, out var conversation));
        Assert.Equal(2, conversation!.Entries.Count);
        Assert.Equal("no switch", conversation.Entries[0].Content);
        Assert.Equal("Hello back", conversation.Entries[1].Content);
        Assert.Equal(2, townhall.Messages.Count);
    }

    [Fact]
    public void AddMirroredActivityToConversation_UnknownOrNonChannel_DoesNotMutateOtherConversations()
    {
        var store = ConversationsTestSupport.CreateStore();
        var vm = ConversationsTestSupport.CreateTownhallViewModel(store: store);
        var channelA = vm.ActiveChannelId!;
        var direct = store.CreateDirectConversation(ActorId.HumanUser, ActorId.PanelSeed("alpha"));
        var unknown = ConversationId.ForChannel("missing-channel");

        vm.AddMirroredActivityToConversation(
            unknown,
            ConversationEntryKind.UserChat,
            "unknown target",
            ActorId.HumanUser,
            "user-1",
            "User");
        vm.AddMirroredActivityToConversation(
            direct.Id,
            ConversationEntryKind.UserChat,
            "direct target",
            ActorId.HumanUser,
            "user-1",
            "User");

        Assert.True(store.TryGetChannelConversation(channelA, out var channelConversation));
        Assert.Empty(channelConversation!.Entries);
        Assert.Empty(direct.Entries);
        Assert.Empty(vm.Messages);
    }

    private static (
        AgentTownhallMirrorCoordinator Coordinator,
        AgentPanelHost Host,
        TownhallViewModel Townhall,
        AgentPanelState Panel,
        Mock<IAgentExecutionCoordinator> Exec,
        IConversationStore Store,
        string ChannelA,
        string ChannelB) CreateDelayedSut()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var panel = host.CreatePanel("agent-1", "Test Agent", "avatar_test");
        var exec = new Mock<IAgentExecutionCoordinator>();
        var router = new AgentRouter(new MentionParser(), host, exec.Object);
        var townhall = ConversationsTestSupport.CreateTownhallViewModel(store: store);
        var channelA = townhall.ActiveChannelId!;
        var channelB = townhall.Channels.First(c => c.Id != channelA).Id;
        var coordinator = new AgentTownhallMirrorCoordinator(
            router,
            host,
            townhall,
            ConversationsTestSupport.CreateCatalogAsInterface());

        return (coordinator, host, townhall, panel, exec, store, channelA, channelB);
    }

    private static void AssertSwitchDuringAwaitAttribution(
        TownhallViewModel townhall,
        IConversationStore store,
        string channelA,
        string channelB,
        bool expectTerminal)
    {
        Assert.True(store.TryGetChannelConversation(channelA, out var conversationA));
        Assert.True(store.TryGetChannelConversation(channelB, out var conversationB));
        Assert.Equal(expectTerminal ? 2 : 1, conversationA!.Entries.Count);
        Assert.DoesNotContain(
            conversationB!.Entries,
            entry => entry.Kind is ConversationEntryKind.UserChat
                or ConversationEntryKind.AssistantResponse
                or ConversationEntryKind.ExecutionFailure
                or ConversationEntryKind.RoutingFailure);
        Assert.Single(
            conversationB.Entries,
            entry => entry.Kind == ConversationEntryKind.ChannelEvent);

        Assert.Same(GetChannelMessages(townhall, channelB), townhall.Messages);
        Assert.Single(GetChannelMessages(townhall, channelB));
        Assert.Equal(TownhallMessageKind.ChannelEvent, GetChannelMessages(townhall, channelB)[0].Kind);
        Assert.Equal(expectTerminal ? 2 : 1, GetChannelMessages(townhall, channelA).Count);
        Assert.DoesNotContain(
            GetChannelMessages(townhall, channelB),
            message => message.Kind is TownhallMessageKind.Chat or TownhallMessageKind.AgentError);
    }

    private static string GetChannelMirrorContent(
        IConversationStore store,
        string channelId,
        ConversationEntryKind kind)
    {
        Assert.True(store.TryGetChannelConversation(channelId, out var conversation));
        return conversation!.Entries.Single(entry => entry.Kind == kind).Content;
    }

    private static TownhallMessage GetLastChannelMessage(TownhallViewModel townhall, string channelId) =>
        GetChannelMessages(townhall, channelId)[^1];

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
