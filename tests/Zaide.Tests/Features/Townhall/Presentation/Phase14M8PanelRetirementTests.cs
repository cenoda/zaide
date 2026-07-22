using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.App.Shell;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Tests.Features.Agents;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Townhall.Presentation;

/// <summary>
/// Phase 14 M8: dedicated Agent Panel shell chrome retired; Townhall is the sole
/// user-facing DM workflow. Extends M7 parity with layout/retirement evidence.
/// </summary>
public sealed class Phase14M8PanelRetirementTests
{
    [Fact]
    public void AgentPanelPresentationViews_AreRemovedFromProduction()
    {
        var assembly = typeof(AgentPanelHost).Assembly;

        Assert.Null(assembly.GetType("Zaide.Features.Agents.Presentation.AgentPanelHostView", throwOnError: false));
        Assert.Null(assembly.GetType("Zaide.Features.Agents.Presentation.AgentPanelView", throwOnError: false));
    }

    [Fact]
    public void MainWindowViewModel_DoesNotExposePanelSendOrHostSurface()
    {
        var type = typeof(MainWindowViewModel);

        Assert.DoesNotContain(
            type.GetProperties(BindingFlags.Instance | BindingFlags.Public),
            p => p.Name == "AgentPanelHost");
        Assert.DoesNotContain(
            type.GetMethods(BindingFlags.Instance | BindingFlags.Public),
            m => m.Name == "SendAgentMessageAsync");
    }

    [Fact]
    public void RightColumnHost_SourceIsEditorOnlyWithoutAgentPanelRows()
    {
        var source = ReadRepoFile("src/App/Shell/RightColumnHost.cs");

        Assert.DoesNotContain("AgentPanelHostView", source);
        Assert.DoesNotContain("GridSplitter", source);
        Assert.Contains("Root = editorPanel", source);
    }

    [Fact]
    public async Task DmOnlyWorkflow_TownhallSendWithoutPanelChrome_MatchesM7Parity()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var draftState = ConversationsTestSupport.CreateDraftState();
        var drafts = new TownhallConversationUiState(draftState);
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store, draftState);
        var coordinator = AgentExecutionTestSupport.CreateCoordinatorFromHandler(
            host,
            _ => Task.FromResult(AgentExecutionResult.Success("Assistant reply")),
            store,
            draftState);
        var router = new AgentRouter(new MentionParser(), host, coordinator, catalog, store);
        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            catalog: catalog,
            store: store,
            panelHost: host,
            executionCoordinator: coordinator,
            conversationUiState: drafts,
            agentRouter: router,
            draftState: draftState);

        var agentId = ActorId.PanelSeed("alpha");
        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();
        var conversationId = vm.ActiveConversationId!.Value;

        vm.DraftText = "DM-only hello";
        await vm.SendMessageCommand.Execute().ToTask();

        Assert.True(store.TryGet(conversationId, out var conversation));
        Assert.Equal(2, conversation!.Entries.Count);
        Assert.Equal(ConversationEntryKind.UserChat, conversation.Entries[0].Kind);
        Assert.Equal(ConversationEntryKind.AssistantResponse, conversation.Entries[1].Kind);

        foreach (var channel in store.ListConversations().Where(c => c.Kind == ConversationKind.Channel))
        {
            Assert.DoesNotContain(
                channel.Entries,
                e => e.Content.Contains("DM-only hello", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Draft_TownhallAndThinHostShareConversationOwnedMap_WithoutPanelUiSync()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var draftState = ConversationsTestSupport.CreateDraftState();
        var drafts = new TownhallConversationUiState(draftState);
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store, draftState);
        var coordinator = AgentExecutionTestSupport.CreateCoordinatorFromHandler(
            host,
            _ => Task.FromResult(AgentExecutionResult.Success("x")),
            store);
        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            catalog: catalog,
            store: store,
            panelHost: host,
            executionCoordinator: coordinator,
            conversationUiState: drafts,
            draftState: draftState);

        vm.OpenDirectConversationCommand.Execute(ActorId.PanelSeed("alpha")).Subscribe();
        var conversationId = vm.ActiveConversationId!.Value;

        vm.DraftText = "shared draft";
        Assert.Equal("shared draft", drafts.GetDraft(conversationId));

        var panel = host.GetOrCreatePanelForActor(ActorId.PanelSeed("alpha"));
        Assert.Equal("shared draft", panel.DraftInput);

        panel.DraftInput = "thin-host edit";
        Assert.Equal("thin-host edit", drafts.GetDraft(conversationId));
    }

    private static string ReadRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate repository file: {relativePath}");
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
