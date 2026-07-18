using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Agents.Presentation;

namespace Zaide.App.Composition.Registration;

internal static class AgentsServiceCollectionExtensions
{
    internal static IServiceCollection AddZaideAgents(
        this IServiceCollection services)
    {
        services.AddSingleton<IAgentPanelHost, AgentPanelHost>();
        services.AddSingleton<IAgentExecutionService, AgentExecutionService>();
        services.AddSingleton<IAgentExecutionCoordinator, AgentExecutionCoordinator>();
        services.AddSingleton<MentionParser>();
        services.AddSingleton<IAgentRouter, AgentRouter>();
        services.AddSingleton(_ =>
        {
            var client = new HttpClient();
            // Default timeout for non-streaming requests
            client.Timeout = TimeSpan.FromSeconds(120);
            return client;
        });

        return services;
    }
}
