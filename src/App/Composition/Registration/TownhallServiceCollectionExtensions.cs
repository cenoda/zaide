using Microsoft.Extensions.DependencyInjection;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Infrastructure;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;

namespace Zaide.App.Composition.Registration;

internal static class TownhallServiceCollectionExtensions
{
    internal static IServiceCollection AddZaideTownhall(
        this IServiceCollection services)
    {
        services.AddSingleton<TownhallState>();
        services.AddSingleton<TownhallConversationUiState>();
        services.AddSingleton<IConversationWorkspacePersistenceBridge, TownhallConversationPersistenceBridge>();
        services.AddSingleton<ConversationPersistenceService>();
        services.AddSingleton<TownhallViewModel>();

        return services;
    }
}
