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

public sealed class TownhallDirectSendTests
{
    [Fact]
    public async Task DirectSend_AppendsUserAndAssistantEntries()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var executionService = new StubExecutionService(
            _ => Task.FromResult(AgentExecutionResult.Success("Assistant reply")));
        var coordinator = AgentExecutionTestSupport.CreateCoordinator(host, executionService, store);
        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            store: store,
            panelHost: host,
            executionCoordinator: coordinator);

        var agentId = vm.Agents.First(a => a.Role == "agent").ActorId;
        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();

        vm.DraftText = "Hello agent";
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.Equal(2, vm.Messages.Count);
        Assert.Equal("Hello agent", vm.Messages[0].Content);
        Assert.Contains("Assistant reply", vm.Messages[1].Content);
        Assert.Empty(vm.DraftText);
    }

    [Fact]
    public async Task DirectSend_SecondWhileBusy_IsRejected()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var gate = new TaskCompletionSource<AgentExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var executionService = new StubExecutionService(_ => gate.Task);
        var coordinator = AgentExecutionTestSupport.CreateCoordinator(host, executionService, store);
        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            store: store,
            panelHost: host,
            executionCoordinator: coordinator);

        var agentId = vm.Agents.First(a => a.Role == "agent").ActorId;
        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();

        vm.DraftText = "first";
        var first = vm.SendMessageCommand.Execute().ToTask();
        Assert.True(vm.IsDirectSendBusy);

        vm.DraftText = "second";
        await vm.SendMessageCommand.Execute().ToTask();
        Assert.Equal("second", vm.DraftText);

        gate.SetResult(AgentExecutionResult.Success("done"));
        await first;

        Assert.False(vm.IsDirectSendBusy);
        Assert.Equal(2, vm.Messages.Count);
    }

    [Fact]
    public async Task ChannelSend_StillWorksAfterDirectSupport()
    {
        var vm = ConversationsTestSupport.CreateTownhallViewModel();
        var initialCount = vm.Messages.Count;

        vm.DraftText = "Channel hello";
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.Equal(initialCount + 1, vm.Messages.Count);
        Assert.Equal("Channel hello", vm.Messages[^1].Content);
        Assert.Empty(vm.DraftText);
    }

    [Fact]
    public void SwitchingChannelAndDirect_UpdatesProjectedHistory()
    {
        var store = ConversationsTestSupport.CreateStore();
        var vm = ConversationsTestSupport.CreateTownhallViewModel(store: store);
        var agentId = vm.Agents.First(a => a.Role == "agent").ActorId;
        var conversation = store.GetOrCreateDirectConversation(ActorId.HumanUser, agentId);
        store.AppendEntry(
            conversation.Id,
            ConversationEntry.UserChat(
                ConversationEntryId.New(),
                ActorId.HumanUser,
                DateTimeOffset.UtcNow,
                "Direct only"));

        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();
        Assert.Single(vm.Messages);
        Assert.Equal("Direct only", vm.Messages[0].Content);

        var channelId = vm.Channels[1].Id;
        vm.SelectChannelCommand.Execute(channelId).Subscribe();
        Assert.DoesNotContain(vm.Messages, message => message.Content == "Direct only");

        vm.SelectConversationCommand.Execute(conversation.Id).Subscribe();
        Assert.Single(vm.Messages);
        Assert.Equal("Direct only", vm.Messages[0].Content);
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
