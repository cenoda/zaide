using Zaide.Features.Agents.Presentation;
using Zaide.Tests.Features.Conversations;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;

namespace Zaide.Tests.Features.Conversations;

/// <summary>
/// Shared catalog/host helpers for tests that construct agent/townhall surfaces
/// outside the production DI container.
/// </summary>
internal static class ConversationsTestSupport
{
    public static ActorCatalog CreateCatalog() => new();

    public static IActorCatalog CreateCatalogAsInterface() => CreateCatalog();

    public static AgentPanelHost CreatePanelHost(IActorCatalog? catalog = null) =>
        new(catalog ?? CreateCatalog());

    public static TownhallViewModel CreateTownhallViewModel(
        TownhallState? state = null,
        IActorCatalog? catalog = null) =>
        new(state ?? new TownhallState(), catalog ?? CreateCatalog());
}
