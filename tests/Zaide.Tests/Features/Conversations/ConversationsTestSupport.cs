using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
using ConversationDraftStateImpl = Zaide.Features.Conversations.Application.ConversationDraftState;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;

namespace Zaide.Tests.Features.Conversations;

/// <summary>
/// Shared catalog/store/host helpers for tests that construct agent/townhall surfaces
/// outside the production DI container.
/// </summary>
internal static class ConversationsTestSupport
{
    public static ActorCatalog CreateCatalog() => new();

    public static IActorCatalog CreateCatalogAsInterface() => CreateCatalog();

    public static ConversationStore CreateStore() => new();

    public static IConversationStore CreateStoreAsInterface() => CreateStore();

    public static IConversationDraftState CreateDraftState() => new ConversationDraftStateImpl();

    public static AgentPanelHost CreatePanelHost(
        IActorCatalog? catalog = null,
        IConversationStore? store = null,
        IConversationDraftState? draftState = null) =>
        new(catalog ?? CreateCatalog(), store ?? CreateStore(), draftState);

    public static TownhallViewModel CreateTownhallViewModel(
        TownhallState? state = null,
        IActorCatalog? catalog = null,
        IConversationStore? store = null,
        IAgentPanelHost? panelHost = null,
        IAgentExecutionCoordinator? executionCoordinator = null,
        IAgentContextSessionPolicyService? sessionPolicyService = null,
        TownhallConversationUiState? conversationUiState = null,
        TownhallConversationPersistenceBridge? persistenceBridge = null,
        Zaide.Features.Conversations.Infrastructure.ConversationPersistenceService? persistenceService = null,
        IAgentRouter? agentRouter = null,
        IConversationDraftState? draftState = null,
        IAgentSessionService? sessionService = null)
    {
        var resolvedCatalog = catalog ?? CreateCatalog();
        var resolvedStore = store ?? CreateStore();
        var resolvedDrafts = draftState ?? CreateDraftState();
        var resolvedUiState = conversationUiState ?? new TownhallConversationUiState(resolvedDrafts);
        return new TownhallViewModel(
            state ?? new TownhallState(),
            resolvedCatalog,
            resolvedStore,
            panelHost ?? CreatePanelHost(resolvedCatalog, resolvedStore, resolvedDrafts),
            executionCoordinator ?? new NoOpAgentExecutionCoordinator(),
            sessionPolicyService ?? new NoOpAgentContextSessionPolicyService(),
            resolvedUiState,
            persistenceBridge,
            persistenceService,
            agentRouter,
            sessionService: sessionService);
    }

    private sealed class NoOpAgentContextSessionPolicyService : IAgentContextSessionPolicyService
    {
        public AgentContextSessionPolicyState GetPolicyState(ConversationId conversationId) =>
            AgentContextSessionPolicyState.CreateApplicationDefault(
                conversationId,
                AgentSessionContextPolicyLevel.Standard);

        public bool TrySetSessionOverride(
            ConversationId conversationId,
            AgentSessionContextPolicyLevel level) =>
            false;

        public bool ClearSessionOverride(ConversationId conversationId) =>
            false;
    }

    private sealed class NoOpAgentExecutionCoordinator : IAgentExecutionCoordinator
    {
#pragma warning disable CS0067 // Event required by interface; no-op coordinator never raises it.
        public event Action<Zaide.Features.Conversations.Domain.ConversationId, bool>? ConversationBusyChanged;
#pragma warning restore CS0067

        public bool IsConversationBusy(Zaide.Features.Conversations.Domain.ConversationId conversationId) =>
            false;

        public Task<AgentExecutionCoordinatorResult?> SendAsync(
            string panelId,
            string userMessage,
            CancellationToken ct = default) =>
            Task.FromResult<AgentExecutionCoordinatorResult?>(null);
    }
}
