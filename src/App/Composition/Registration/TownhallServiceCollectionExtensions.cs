using System;
using Microsoft.Extensions.DependencyInjection;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Agents.Presentation.Transparency;
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
        services.AddSingleton<ConversationPersistenceService>(sp =>
        {
            var get = (Func<Type, object?>)sp.GetService;
            return new ConversationPersistenceService(
                (IConversationStore)get(typeof(IConversationStore))!,
                (IConversationWorkspacePersistenceBridge)get(typeof(IConversationWorkspacePersistenceBridge))!);
        });
        services.AddSingleton<TownhallViewModel>(sp =>
        {
            var get = (Func<Type, object?>)sp.GetService;
            return new TownhallViewModel(
                (TownhallState)get(typeof(TownhallState))!,
                (IActorCatalog)get(typeof(IActorCatalog))!,
                (IConversationStore)get(typeof(IConversationStore))!,
                (IAgentPanelHost)get(typeof(IAgentPanelHost))!,
                (IAgentExecutionCoordinator)get(typeof(IAgentExecutionCoordinator))!,
                (IAgentContextSessionPolicyService)get(typeof(IAgentContextSessionPolicyService))!,
                (TownhallConversationUiState)get(typeof(TownhallConversationUiState))!,
                (IConversationWorkspacePersistenceBridge)get(typeof(IConversationWorkspacePersistenceBridge))!,
                (ConversationPersistenceService)get(typeof(ConversationPersistenceService))!,
                (IAgentRouter?)get(typeof(IAgentRouter)),
                (IAgentActorBackendSelectionService?)get(typeof(IAgentActorBackendSelectionService)),
                (AgentBackendBindingPresenter?)get(typeof(AgentBackendBindingPresenter)),
                (IAgentSessionService?)get(typeof(IAgentSessionService)),
                (AgentTransparencyManagementViewModel?)get(typeof(AgentTransparencyManagementViewModel)));
        });

        return services;
    }
}
