using System;
using System.Linq;
using Avalonia;
using Xunit;
using Zaide.Features.Townhall.Presentation;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Townhall.Presentation;

public class Phase14F1ConversationContextTests
{
    static Phase14F1ConversationContextTests()
    {
        EnsureApplication();
    }

    [Fact]
    public void ChannelSelected_HeaderAndInputContextMatchChannelName()
    {
        var vm = ConversationsTestSupport.CreateTownhallViewModel();
        var channel = vm.Channels.First(c => c.Name == "ai-status");

        vm.SelectChannelCommand.Execute(channel.Id).Subscribe();

        Assert.Equal("#ai-status", vm.ActiveConversationHeaderLabel);
        Assert.Equal("Message #ai-status", vm.ActiveConversationInputPlaceholder);
        Assert.Equal(channel.Id, vm.ActiveChannelId);
    }

    [Fact]
    public void SwitchChannelToDirect_UpdatesHeaderAndInputContextToAgentIdentity()
    {
        var vm = ConversationsTestSupport.CreateTownhallViewModel();
        var agent = vm.Agents.First(a => a.Role == "agent");

        vm.OpenDirectConversationCommand.Execute(agent.ActorId).Subscribe();

        Assert.Null(vm.ActiveChannelId);
        Assert.Equal(agent.Name, vm.ActiveConversationHeaderLabel);
        Assert.Equal($"Direct message with {agent.Name}", vm.ActiveConversationInputPlaceholder);
        Assert.DoesNotContain(vm.Channels, c => c.IsActive);
    }

    [Fact]
    public void SwitchDirectToChannel_UpdatesHeaderAndInputContextToChannelName()
    {
        var vm = ConversationsTestSupport.CreateTownhallViewModel();
        var agent = vm.Agents.First(a => a.Role == "agent");
        var channel = vm.Channels.First(c => c.Name == "codebase-refactor");

        vm.OpenDirectConversationCommand.Execute(agent.ActorId).Subscribe();
        vm.SelectChannelCommand.Execute(channel.Id).Subscribe();

        Assert.Equal("#codebase-refactor", vm.ActiveConversationHeaderLabel);
        Assert.Equal("Message #codebase-refactor", vm.ActiveConversationInputPlaceholder);
        Assert.Equal(channel.Id, vm.ActiveChannelId);
    }

    [Fact]
    public void ChannelToDirectSwitch_ProjectsMatchingHeaderAndInputContext()
    {
        EnsureApplication();
        var vm = ConversationsTestSupport.CreateTownhallViewModel();
        var chatPanel = new TownhallChatPanel();
        var inputArea = new TownhallInputArea();
        var agent = vm.Agents.First(a => a.Role == "agent");

        chatPanel.SetConversationHeader(vm.ActiveConversationHeaderLabel);
        inputArea.PlaceholderText = vm.ActiveConversationInputPlaceholder;

        vm.OpenDirectConversationCommand.Execute(agent.ActorId).Subscribe();

        chatPanel.SetConversationHeader(vm.ActiveConversationHeaderLabel);
        inputArea.PlaceholderText = vm.ActiveConversationInputPlaceholder;

        Assert.Equal(agent.Name, chatPanel.ConversationHeaderLabel);
        Assert.Equal($"Direct message with {agent.Name}", inputArea.PlaceholderText);
    }

    [Fact]
    public void DirectToChannelSwitch_ProjectsMatchingHeaderAndInputContext()
    {
        EnsureApplication();
        var vm = ConversationsTestSupport.CreateTownhallViewModel();
        var chatPanel = new TownhallChatPanel();
        var inputArea = new TownhallInputArea();
        var agent = vm.Agents.First(a => a.Role == "agent");
        var channel = vm.Channels.First(c => c.Name == "ai-status");

        vm.OpenDirectConversationCommand.Execute(agent.ActorId).Subscribe();
        vm.SelectChannelCommand.Execute(channel.Id).Subscribe();

        chatPanel.SetConversationHeader(vm.ActiveConversationHeaderLabel);
        inputArea.PlaceholderText = vm.ActiveConversationInputPlaceholder;

        Assert.Equal("#ai-status", chatPanel.ConversationHeaderLabel);
        Assert.Equal("Message #ai-status", inputArea.PlaceholderText);
    }

    private static void EnsureApplication()
    {
        if (Application.Current is global::Zaide.App.Composition.App app)
        {
            if (!app.Resources.ContainsKey("PrimaryAccentBrush"))
            {
                app.Initialize();
            }

            return;
        }

        var createdApp = new global::Zaide.App.Composition.App();
        createdApp.Initialize();
    }
}
