using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Contracts;
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

    public static AgentPanelHost CreatePanelHost(
        IActorCatalog? catalog = null,
        IConversationStore? store = null) =>
        new(catalog ?? CreateCatalog(), store ?? CreateStore());

    public static TownhallViewModel CreateTownhallViewModel(
        TownhallState? state = null,
        IActorCatalog? catalog = null,
        IConversationStore? store = null,
        IAgentPanelHost? panelHost = null,
        IAgentExecutionCoordinator? executionCoordinator = null)
    {
        var resolvedCatalog = catalog ?? CreateCatalog();
        var resolvedStore = store ?? CreateStore();
        return new TownhallViewModel(
            state ?? new TownhallState(),
            resolvedCatalog,
            resolvedStore,
            panelHost ?? CreatePanelHost(resolvedCatalog, resolvedStore),
            executionCoordinator ?? new NoOpAgentExecutionCoordinator());
    }

    private sealed class NoOpAgentExecutionCoordinator : IAgentExecutionCoordinator
    {
        public Task<AgentExecutionCoordinatorResult?> SendAsync(
            string panelId,
            string userMessage,
            CancellationToken ct = default) =>
            Task.FromResult<AgentExecutionCoordinatorResult?>(null);
    }
}
