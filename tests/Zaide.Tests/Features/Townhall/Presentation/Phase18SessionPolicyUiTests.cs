using System;
using System.Linq;
using System.Reactive;
using Xunit;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Tests.Features.Agents;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Townhall.Presentation;

/// <summary>
/// Phase 18 M5 presentation tests for session policy selector projection.
/// </summary>
public sealed class Phase18SessionPolicyUiTests
{
    [Fact]
    public void PolicySelector_DefaultState_UsesApplicationDefaultWithoutOverride()
    {
        var store = ConversationsTestSupport.CreateStore();
        var catalog = ConversationsTestSupport.CreateCatalog();
        var panelHost = ConversationsTestSupport.CreatePanelHost(catalog, store);
        var (coordinator, _, sessionService) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(
            panelHost,
            store);
        var policyService = (IAgentContextSessionPolicyService)sessionService;

        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            store: store,
            catalog: catalog,
            panelHost: panelHost,
            executionCoordinator: coordinator,
            sessionPolicyService: policyService);

        var agentId = vm.Agents.First(a => a.Role == "agent").ActorId;
        Execute(vm.OpenDirectConversationCommand, agentId);

        Assert.True(vm.IsContextPolicySelectorVisible);
        Assert.False(vm.IsContextPolicyOverrideActive);
        Assert.Equal(0, vm.ContextPolicySelectorIndex);
        Assert.Equal(
            AgentContextSessionPolicyState.FormatApplicationDefaultCaption(
                AgentSessionContextPolicyLevel.Standard),
            vm.ContextPolicyStatusCaption);
    }

    [Fact]
    public void PolicySelector_UserSelection_ReachesSessionPolicyBoundary()
    {
        var store = ConversationsTestSupport.CreateStore();
        var catalog = ConversationsTestSupport.CreateCatalog();
        var panelHost = ConversationsTestSupport.CreatePanelHost(catalog, store);
        var (coordinator, _, sessionService) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(
            panelHost,
            store);
        var policyService = (IAgentContextSessionPolicyService)sessionService;

        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            store: store,
            catalog: catalog,
            panelHost: panelHost,
            executionCoordinator: coordinator,
            sessionPolicyService: policyService);

        var agentId = vm.Agents.First(a => a.Role == "agent").ActorId;
        Execute(vm.OpenDirectConversationCommand, agentId);
        panelHost.GetOrCreatePanelForActor(agentId);
        var conversationId = vm.ActiveConversationId!.Value;

        Execute(vm.SetContextPolicyFromSelectorCommand, 2);

        var state = policyService.GetPolicyState(conversationId);
        Assert.True(state.IsOverrideActive);
        Assert.Equal(AgentSessionContextPolicyLevel.Minimal, state.EffectiveLevel);
        Assert.True(vm.IsContextPolicyOverrideActive);
        Assert.Equal(2, vm.ContextPolicySelectorIndex);
        Assert.Equal(
            AgentContextSessionPolicyState.FormatOverrideCaption(AgentSessionContextPolicyLevel.Minimal),
            vm.ContextPolicyStatusCaption);

        var panel = panelHost.Panels.Single();
        Assert.Equal(vm.ContextPolicyStatusCaption, panel.ContextPolicyStatusCaption);
        Assert.True(panel.IsContextPolicyOverrideActive);
        Assert.Equal(2, panel.ContextPolicySelectorIndex);
    }

    [Fact]
    public void PolicySelector_ClearOverride_ReturnsToApplicationDefault()
    {
        var store = ConversationsTestSupport.CreateStore();
        var catalog = ConversationsTestSupport.CreateCatalog();
        var panelHost = ConversationsTestSupport.CreatePanelHost(catalog, store);
        var (coordinator, _, sessionService) = AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend(
            panelHost,
            store);
        var policyService = (IAgentContextSessionPolicyService)sessionService;

        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            store: store,
            catalog: catalog,
            panelHost: panelHost,
            executionCoordinator: coordinator,
            sessionPolicyService: policyService);

        var agentId = vm.Agents.First(a => a.Role == "agent").ActorId;
        Execute(vm.OpenDirectConversationCommand, agentId);
        Execute(vm.SetContextPolicyFromSelectorCommand, 4);
        Execute(vm.ClearContextPolicyOverrideCommand);

        Assert.False(vm.IsContextPolicyOverrideActive);
        Assert.Equal(0, vm.ContextPolicySelectorIndex);
        Assert.Equal(
            AgentContextSessionPolicyState.FormatApplicationDefaultCaption(
                AgentSessionContextPolicyLevel.Standard),
            vm.ContextPolicyStatusCaption);
    }

    [Fact]
    public void PolicySelector_IsHiddenForChannelConversations()
    {
        var vm = ConversationsTestSupport.CreateTownhallViewModel();

        var channelId = vm.Channels[0].Id;
        Execute(vm.SelectChannelCommand, channelId);

        Assert.False(vm.IsContextPolicySelectorVisible);
    }

    [Fact]
    public void ContextPolicySelector_IsConsumedByTownhallView()
    {
        var viewType = typeof(TownhallView);
        var field = viewType.GetField(
            "_contextPolicySelector",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(field);
        Assert.Equal(typeof(TownhallContextPolicySelector), field!.FieldType);
    }

    private static void Execute(ReactiveUI.ReactiveCommand<ActorId, Unit> command, ActorId parameter) =>
        command.Execute(parameter).Subscribe(Observer.Create<Unit>(_ => { }));

    private static void Execute(ReactiveUI.ReactiveCommand<string, Unit> command, string parameter) =>
        command.Execute(parameter).Subscribe(Observer.Create<Unit>(_ => { }));

    private static void Execute(ReactiveUI.ReactiveCommand<int, Unit> command, int parameter) =>
        command.Execute(parameter).Subscribe(Observer.Create<Unit>(_ => { }));

    private static void Execute(ReactiveUI.ReactiveCommand<Unit, Unit> command) =>
        command.Execute().Subscribe(Observer.Create<Unit>(_ => { }));
}
