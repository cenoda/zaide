using Microsoft.Extensions.DependencyInjection;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Infrastructure;

namespace Zaide.App.Composition.Registration;

internal static class ConversationsServiceCollectionExtensions
{
    internal static IServiceCollection AddZaideConversations(
        this IServiceCollection services)
    {
        services.AddSingleton<IActorCatalog, ActorCatalog>();
        services.AddSingleton<IConversationStore, ConversationStore>();
        services.AddSingleton<IConversationDraftState, ConversationDraftState>();

        return services;
    }
}
