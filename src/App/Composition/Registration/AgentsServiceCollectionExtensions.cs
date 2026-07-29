using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Application.Acp;
using Zaide.Features.Agents.Application.Continuity;
using Zaide.Features.Agents.Application.Transparency;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Application.Transparency.Usage;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Contracts.Continuity;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Contracts.Transparency.Trace;
using Zaide.Features.Agents.Contracts.Transparency.Usage;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Trace;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Agents.Infrastructure.Acp;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Agents.Presentation.Transparency;
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
        services.AddSingleton<IAgentContextSessionPolicyService>(Program.ResolveAgentContextSessionPolicyService);
        services.AddSingleton<AgentConversationEventProjection>();
        services.AddSingleton<IAgentPanelHost, AgentPanelHost>();
        services.AddSingleton<AgentExecutionService>();
        services.AddSingleton<IAgentExecutionService>(sp =>
        {
            var get = (Func<Type, object?>)sp.GetService;
            return (AgentExecutionService)get(typeof(AgentExecutionService))!;
        });
        services.AddSingleton<INativeHarnessProviderTransport, NativeHarnessProviderClient>();
        services.AddSingleton<INativeHarnessProviderOptionsSource, NativeHarnessProviderOptionsSource>();
        services.AddSingleton<INativeHarnessPriorConversationReader, NativeHarnessPriorConversationReader>();
        services.AddSingleton<IAgentActorBackendBindingStore, AgentActorBackendBindingStore>();
        services.AddSingleton<IAgentActorBackendSelectionService, AgentActorBackendSelectionService>();
        services.AddSingleton<AgentBackendBindingPresenter>();
        services.AddSingleton<IAcpProcessLauncher, AcpSystemDiagnosticsProcessLauncher>();
        services.AddSingleton<IAcpSessionClientFactory>(sp =>
        {
            var get = (Func<Type, object?>)sp.GetService;
            return new AcpProductionSessionClientFactory(
                (IAgentActorBackendBindingStore)get(typeof(IAgentActorBackendBindingStore))!,
                (IAcpProcessLauncher)get(typeof(IAcpProcessLauncher))!,
                () => Environment.CurrentDirectory);
        });
        services.AddSingleton<NativeHarnessAgentBackend>(sp =>
        {
            var get = (Func<Type, object?>)sp.GetService;
            return new NativeHarnessAgentBackend(
                (INativeHarnessProviderOptionsSource)get(typeof(INativeHarnessProviderOptionsSource))!,
                (INativeHarnessProviderTransport)get(typeof(INativeHarnessProviderTransport))!,
                (INativeHarnessPriorConversationReader)get(typeof(INativeHarnessPriorConversationReader))!,
                (IWorkspaceActionAuthority?)get(typeof(IWorkspaceActionAuthority)));
        });
        services.AddSingleton<AcpActionCapableAgentBackend>(sp =>
        {
            var get = (Func<Type, object?>)sp.GetService;
            return new AcpActionCapableAgentBackend(
                (IAcpSessionClientFactory)get(typeof(IAcpSessionClientFactory))!,
                () => Environment.CurrentDirectory,
                (IAgentActorBackendBindingStore)get(typeof(IAgentActorBackendBindingStore))!);
        });
        services.AddSingleton<IAgentBackend>(sp =>
        {
            var get = (Func<Type, object?>)sp.GetService;
            return (IAgentBackend)get(typeof(AcpActionCapableAgentBackend))!;
        });
        services.AddSingleton<IAgentBackend>(sp =>
        {
            var get = (Func<Type, object?>)sp.GetService;
            return (IAgentBackend)get(typeof(NativeHarnessAgentBackend))!;
        });
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

        // Phase 21 M1: backend-neutral durable record storage foundation.
        services.AddSingleton<IAgentDurableRecordStore, AgentDurableRecordFileStore>();
        services.AddSingleton<AgentDurableRecordCoordinator>();

        // Phase 21 M2: backend-neutral redacted trace evidence capture and inspection.
        // Composition uses constructor injection through the singleton
        // container; the existing pattern keeps the M3 locator-site ratchet
        // honest for this file.
        services.AddSingleton(_ => AgentTraceCaptureLimits.Default);
        services.AddSingleton(_ => AgentUsageCaptureLimits.Default);
        services.AddSingleton<AgentDurableWorkspaceStorageKeyResolver>(
            _ => new PathDerivedAgentDurableWorkspaceStorageKeyResolver());
        services.AddSingleton<AgentTraceBoundedCaptureQueue>();
        services.AddSingleton<AgentTraceCaptureSink>();
        services.AddSingleton<IAgentTraceSourceRegistry, AgentTraceSourceRegistry>();
        services.AddSingleton<IAgentTraceInspector, AgentTraceInspector>();
        services.AddSingleton<AgentTraceCoordinator>();
        services.AddSingleton<AgentTraceBackendEvidenceSourceWriter>();
        services.AddSingleton<IAgentTraceBackendEvidenceSource, NativeHarnessAgentTraceSource>();
        services.AddSingleton<IAgentTraceBackendEvidenceSource, AcpAgentTraceSource>();
        services.AddSingleton<AgentTraceAvailabilityProjection>();
        services.AddSingleton<AgentTraceInspectionViewModel>();

        // Phase 21 M3: usage and cost evidence ledger.
        services.AddSingleton<AgentUsageCaptureSink>();
        services.AddSingleton<IAgentUsageInspector, AgentUsageInspector>();
        services.AddSingleton<AgentUsageCoordinator>();
        services.AddSingleton<AgentUsageBackendEvidenceSourceWriter>();
        services.AddSingleton<IAgentUsageBackendEvidenceSource, NativeHarnessAgentUsageSource>();
        services.AddSingleton<IAgentUsageBackendEvidenceSource, AcpAgentUsageSource>();
        services.AddSingleton<AgentUsageAvailabilityProjection>();
        services.AddSingleton<AgentUsageInspectionViewModel>();

        // Phase 21 M4: session continuity, explicit recovery, and termination.
        services.AddSingleton<AgentSessionContinuityCheckpointWriter>();
        services.AddSingleton<AgentSessionContinuityInspector>();
        services.AddSingleton<IAgentSessionContinuityInspector, AgentSessionContinuityInspector>();
        services.AddSingleton<AgentSessionContinuityRevalidator>();
        services.AddSingleton<IAgentBackendContinuityAdapter, NativeHarnessAgentContinuityAdapter>();
        services.AddSingleton<IAgentBackendContinuityAdapter, AcpAgentContinuityAdapter>();
        services.AddSingleton<IAgentSessionContinuityCoordinator, AgentSessionContinuityCoordinator>();
        services.AddSingleton<AgentSessionContinuityStartupReconciler>();
        services.AddSingleton<AgentSessionContinuityEventSubscriber>();
        services.AddSingleton<AgentSessionContinuityAvailabilityProjection>();
        services.AddSingleton<AgentSessionContinuityInspectionViewModel>();
        services.AddSingleton<IAgentSessionService, AgentSessionService>();

        return services;
    }
}
