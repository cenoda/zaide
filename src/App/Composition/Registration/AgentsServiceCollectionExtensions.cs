using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Workspace.Contracts;

namespace Zaide.App.Composition.Registration;

internal static class AgentsServiceCollectionExtensions
{
    internal static IServiceCollection AddZaideAgents(
        this IServiceCollection services)
    {
        services.AddSingleton<AgentEventStream>();
        services.AddSingleton<AgentContextManifestBuilder>();
        services.AddSingleton<IAgentContextSnapshotSources, LiveAgentContextSnapshotSources>();
        services.AddSingleton<IAgentSessionService, AgentSessionService>();
        services.AddSingleton<IAgentContextSessionPolicyService>(Program.ResolveAgentContextSessionPolicyService);
        services.AddSingleton<AgentConversationEventProjection>();
        services.AddSingleton<IAgentPanelHost, AgentPanelHost>();
        services.AddSingleton<IAgentExecutionService, AgentExecutionService>();
        services.AddSingleton<INativeHarnessProviderTransport, NativeHarnessProviderClient>();
        services.AddSingleton<INativeHarnessProviderOptionsSource, NativeHarnessProviderOptionsSource>();
        services.AddSingleton<INativeHarnessPriorConversationReader, NativeHarnessPriorConversationReader>();
        services.AddSingleton<IAgentBackend, NativeHarnessAgentBackend>();
        services.AddSingleton<IAgentExecutionCoordinator>(Program.CreateAgentExecutionCoordinator);
        services.AddSingleton<MentionParser>();
        services.AddSingleton<IAgentRouter, AgentRouter>();
        services.AddSingleton(_ =>
        {
            var client = new HttpClient();
            // Default timeout for non-streaming requests
            client.Timeout = TimeSpan.FromSeconds(120);
            return client;
        });

        // Phase 17 M3: permission review surface.
        // App.OnFrameworkInitializationCompleted attaches the owner main
        // window to the presenter singleton after the window is created;
        // until an owner is attached the presenter fails closed
        // (PermissionUnavailable).
        services.AddSingleton<IAgentPermissionDialogPresenter, PermissionReviewDialogPresenter>();
        services.AddSingleton<IAgentPermissionReviewService, InteractiveAgentPermissionReviewService>();

        // Phase 17 M8: action control plane wiring.
        services.AddSingleton<IAgentActionAuditStore, AgentActionAuditStore>();
        services.AddSingleton<IAgentFileReader, WorkspaceFileReader>();
        services.AddSingleton<IAgentFileMutator, WorkspaceFileMutator>();
        services.AddSingleton<IAgentCommandResolver, DefaultAgentCommandResolver>();
        services.AddSingleton<IAgentCommandExecutor, WorkspaceCommandExecutor>();
        services.AddSingleton<IAgentActionBrokerFactory, AgentActionBrokerFactory>();

        return services;
    }
}
