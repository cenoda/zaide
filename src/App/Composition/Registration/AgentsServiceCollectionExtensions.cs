using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Application.Acp;
using Zaide.Features.Agents.Application.Continuity;
using Zaide.Features.Agents.Application.Memory;
using Zaide.Features.Agents.Application.Transparency;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Application.Transparency.Usage;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Contracts.Continuity;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Contracts.Transparency.Memory;
using Zaide.Features.Agents.Contracts.Transparency.Trace;
using Zaide.Features.Agents.Contracts.Transparency.Usage;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency.Memory;
using Zaide.Features.Agents.Domain.Transparency.Trace;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Agents.Infrastructure.Acp;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Agents.Presentation.Memory;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.Features.Settings.Contracts;
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
        // Phase 22.2 M1: durable schema-v1 binding store under the Zaide config
        // directory. Active-run busy gate is resolved lazily from the session
        // service (registered later) to avoid constructor cycles.
        services.AddSingleton<IAgentActorBackendBindingStore>(sp =>
        {
            var get = (Func<Type, object?>)sp.GetService;
            // Lazy projection: session service implements IAgentActorActiveRunQuery.
            // Binding store construction must not force session creation first.
            var activeRunQuery = new LazyAgentActorActiveRunQuery(
                () => (IAgentActorActiveRunQuery?)get(typeof(IAgentSessionService)));
            return new AgentActorBackendBindingStore(
                AgentActorBackendBindingPathResolver.GetPrimaryPath(),
                AgentActorBackendBindingPathResolver.GetTempPath(),
                AgentActorBackendBindingPathResolver.GetLastKnownGoodPath(),
                activeRunQuery);
        });
        // Phase 22.2 M3: selection resolves onboarding lazily so authenticate
        // bridges to the real ACP protocol path without a constructor cycle.
        services.AddSingleton<IAgentActorBackendSelectionService>(sp =>
        {
            var get = (Func<Type, object?>)sp.GetService;
            return new AgentActorBackendSelectionService(
                (IAgentActorBackendBindingStore)get(typeof(IAgentActorBackendBindingStore))!,
                () => (IAcpOnboardingConnectionService?)get(typeof(IAcpOnboardingConnectionService)));
        });
        // Phase 22.2 M2: production-owned Townhall binding workflow presenter.
        services.AddSingleton<AgentBackendBindingPresenter>(sp =>
        {
            var get = (Func<Type, object?>)sp.GetService;
            return new AgentBackendBindingPresenter(
                (IAgentActorBackendSelectionService)get(typeof(IAgentActorBackendSelectionService))!,
                (IAgentActorBackendBindingStore)get(typeof(IAgentActorBackendBindingStore))!,
                (INativeHarnessProviderOptionsSource?)get(typeof(INativeHarnessProviderOptionsSource)),
                (IWorkspaceActionAuthority?)get(typeof(IWorkspaceActionAuthority)),
                (IAcpOnboardingConnectionService?)get(typeof(IAcpOnboardingConnectionService)));
        });
        services.AddSingleton<IAcpProcessLauncher, AcpSystemDiagnosticsProcessLauncher>();
        // Phase 22.2 M3: ACP cwd from workspace authority (fail closed), not CurrentDirectory.
        services.AddSingleton<IAcpSessionClientFactory>(sp =>
        {
            var get = (Func<Type, object?>)sp.GetService;
            var workspaceAuthority = (IWorkspaceActionAuthority?)get(typeof(IWorkspaceActionAuthority));
            return new AcpProductionSessionClientFactory(
                (IAgentActorBackendBindingStore)get(typeof(IAgentActorBackendBindingStore))!,
                (IAcpProcessLauncher)get(typeof(IAcpProcessLauncher))!,
                AcpWorkspaceWorkingDirectory.CreateProvider(workspaceAuthority));
        });
        services.AddSingleton<IAcpOnboardingConnectionService>(sp =>
        {
            var get = (Func<Type, object?>)sp.GetService;
            return new AcpOnboardingConnectionService(
                (IAgentActorBackendBindingStore)get(typeof(IAgentActorBackendBindingStore))!,
                (IAgentActorBackendSelectionService)get(typeof(IAgentActorBackendSelectionService))!,
                (IAcpProcessLauncher)get(typeof(IAcpProcessLauncher))!,
                (IWorkspaceActionAuthority?)get(typeof(IWorkspaceActionAuthority)),
                new LazyAgentActorActiveRunQuery(
                    () => (IAgentActorActiveRunQuery?)get(typeof(IAgentSessionService))));
        });
        services.AddSingleton<NativeHarnessAgentBackend>(sp =>
        {
            var get = (Func<Type, object?>)sp.GetService;
            var traceSource = ((IEnumerable<IAgentTraceBackendEvidenceSource>)get(
                    typeof(IEnumerable<IAgentTraceBackendEvidenceSource>))!)
                .Single(source => source.BackendId == AgentBackendIds.NativeHarnessValue);
            var usageSource = ((IEnumerable<IAgentUsageBackendEvidenceSource>)get(
                    typeof(IEnumerable<IAgentUsageBackendEvidenceSource>))!)
                .Single(source => source.BackendId == AgentBackendIds.NativeHarnessValue);
            return new NativeHarnessAgentBackend(
                (INativeHarnessProviderOptionsSource)get(typeof(INativeHarnessProviderOptionsSource))!,
                (INativeHarnessProviderTransport)get(typeof(INativeHarnessProviderTransport))!,
                (INativeHarnessPriorConversationReader)get(typeof(INativeHarnessPriorConversationReader))!,
                (IWorkspaceActionAuthority?)get(typeof(IWorkspaceActionAuthority)),
                traceSource,
                (AgentDurableWorkspaceStorageKeyResolver)get(
                    typeof(AgentDurableWorkspaceStorageKeyResolver))!,
                usageSource);
        });
        services.AddSingleton<AcpActionCapableAgentBackend>(sp =>
        {
            var get = (Func<Type, object?>)sp.GetService;
            var workspaceAuthority = (IWorkspaceActionAuthority?)get(typeof(IWorkspaceActionAuthority));
            var traceSource = ((IEnumerable<IAgentTraceBackendEvidenceSource>)get(
                    typeof(IEnumerable<IAgentTraceBackendEvidenceSource>))!)
                .Single(source => source.BackendId == AgentBackendIds.AcpValue);
            var usageSource = ((IEnumerable<IAgentUsageBackendEvidenceSource>)get(
                    typeof(IEnumerable<IAgentUsageBackendEvidenceSource>))!)
                .Single(source => source.BackendId == AgentBackendIds.AcpValue);
            return new AcpActionCapableAgentBackend(
                (IAcpSessionClientFactory)get(typeof(IAcpSessionClientFactory))!,
                AcpWorkspaceWorkingDirectory.CreateProvider(workspaceAuthority),
                (IAgentActorBackendBindingStore)get(typeof(IAgentActorBackendBindingStore))!,
                workspaceAuthority,
                traceSource,
                (AgentDurableWorkspaceStorageKeyResolver)get(
                    typeof(AgentDurableWorkspaceStorageKeyResolver))!,
                usageSource);
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
        services.AddSingleton<IAgentTraceBackendEvidenceSource>(_ => new NativeHarnessAgentTraceSource());
        services.AddSingleton<IAgentTraceBackendEvidenceSource>(_ => new AcpAgentTraceSource());
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
        services.AddSingleton<AgentTransparencySettingsSync>();

        // Phase 21 M4: session continuity, explicit recovery, and termination.
        services.AddSingleton<AgentSessionContinuityCheckpointWriter>();
        services.AddSingleton<AgentSessionContinuityInspector>();
        services.AddSingleton<IAgentSessionContinuityInspector, AgentSessionContinuityInspector>();
        services.AddSingleton<AgentSessionContinuityLegacyCwdReader>();
        services.AddSingleton<AgentSessionContinuityRevalidator>();
        services.AddSingleton<IAgentBackendContinuityAdapter, NativeHarnessAgentContinuityAdapter>();
        services.AddSingleton<IAgentBackendContinuityAdapter, AcpAgentContinuityAdapter>();
        services.AddSingleton<IAgentSessionContinuityCoordinator, AgentSessionContinuityCoordinator>();
        services.AddSingleton<AgentSessionContinuityConversationProjector>();
        services.AddSingleton<AgentSessionContinuityStartupReconciler>();
        services.AddSingleton<AgentSessionContinuityWorkspaceOpenReconciler>();
        services.AddSingleton<AgentSessionContinuityEventSubscriber>();
        services.AddSingleton<AgentSessionContinuityAvailabilityProjection>();
        services.AddSingleton<AgentSessionContinuityInspectionViewModel>();
        services.AddSingleton<IAgentSessionService>(sp =>
        {
            var get = (Func<Type, object?>)sp.GetService;
            var workspaceAuthority = (IWorkspaceActionAuthority?)get(typeof(IWorkspaceActionAuthority));
            return new AgentSessionService(
                (IEnumerable<IAgentBackend>)get(typeof(IEnumerable<IAgentBackend>))!,
                (AgentEventStream)get(typeof(AgentEventStream))!,
                (IAgentActionBrokerFactory?)get(typeof(IAgentActionBrokerFactory)),
                (IAgentActionAuditStore?)get(typeof(IAgentActionAuditStore)),
                workspaceAuthority,
                (AgentContextManifestBuilder?)get(typeof(AgentContextManifestBuilder)),
                (IAgentContextSnapshotSources?)get(typeof(IAgentContextSnapshotSources)),
                (IAgentSessionContinuityCoordinator?)get(typeof(IAgentSessionContinuityCoordinator)),
                (AgentDurableWorkspaceStorageKeyResolver?)get(typeof(AgentDurableWorkspaceStorageKeyResolver)),
                (IAgentMemoryRetrievalService?)get(typeof(IAgentMemoryRetrievalService)),
                (IAgentMemoryInfluenceRecorder?)get(typeof(IAgentMemoryInfluenceRecorder)),
                AgentContinuityWorkspaceRootProvider.CreateOpenedWorkspaceProvider(workspaceAuthority),
                (ISettingsService?)get(typeof(ISettingsService)));
        });

        // Phase 21 M5: durable scoped memory records (store only; no retrieval/injection).
        // Phase 22.4 M2: opened-workspace projection and lifecycle surface reachability.
        services.AddSingleton<AgentMemoryStoreWriter>();
        services.AddSingleton<AgentMemoryInspector>();
        services.AddSingleton<IAgentMemoryInspector, AgentMemoryInspector>();
        services.AddSingleton<IAgentMemoryPolicyEvaluator, AgentMemoryPolicyEvaluator>();
        services.AddSingleton<IAgentMemoryLifecycleService, AgentMemoryLifecycleService>();
        services.AddSingleton<AgentMemoryCoordinator>();
        services.AddSingleton<IAgentMemoryCoordinator, AgentMemoryCoordinator>();
        services.AddSingleton<AgentMemoryAvailabilityProjection>();
        services.AddSingleton<AgentMemoryInspectionViewModel>();
        // Phase 21 M6: budgeted memory retrieval, influence attribution, integrated lifecycle.
        services.AddSingleton<AgentMemoryRetriever>();
        services.AddSingleton<IAgentMemoryRetrievalService, AgentMemoryRetriever>();
        services.AddSingleton<AgentMemoryInfluenceRecorder>();
        services.AddSingleton<IAgentMemoryInfluenceRecorder, AgentMemoryInfluenceRecorder>();
        services.AddSingleton<AgentTransparencyLifecycleCoordinator>();
        services.AddSingleton<IAgentTransparencyLifecycleCoordinator, AgentTransparencyLifecycleCoordinator>();
        services.AddSingleton<AgentTransparencyManagementViewModel>();

        return services;
    }
}
