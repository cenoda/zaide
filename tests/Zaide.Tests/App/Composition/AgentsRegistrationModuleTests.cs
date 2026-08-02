using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Xunit;
using Zaide;
using Zaide.App.Composition;
using Zaide.App.Composition.Registration;
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
using Zaide.Features.Agents.Domain.Transparency.Trace;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Agents.Infrastructure.Acp;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Agents.Presentation.Memory;
using Zaide.Features.Agents.Presentation.Transparency;

namespace Zaide.Tests.App.Composition;

/// <summary>
/// Refactor 6.3 M6f: proves Agents DI membership moved into
/// <see cref="AgentsServiceCollectionExtensions.AddZaideAgents"/> without
/// changing service types, lifetimes, mappings, factory behavior, or total
/// registration membership. Registration performs no external network activity.
/// </summary>
public sealed class AgentsRegistrationModuleTests
{
    private static readonly string[] AgentsServiceTypeNames =
    {
        typeof(AcpActionCapableAgentBackend).FullName!,
        typeof(AgentEventStream).FullName!,
        typeof(AgentBackendBindingPresenter).FullName!,
        typeof(AgentContextManifestBuilder).FullName!,
        typeof(IAgentContextSnapshotSources).FullName!,
        typeof(IAgentSessionService).FullName!,
        typeof(IAgentContextSessionPolicyService).FullName!,
        typeof(AgentConversationEventProjection).FullName!,
        typeof(IAgentPanelHost).FullName!,
        typeof(AgentExecutionService).FullName!,
        typeof(IAgentExecutionService).FullName!,
        typeof(INativeHarnessProviderTransport).FullName!,
        typeof(INativeHarnessProviderOptionsSource).FullName!,
        typeof(INativeHarnessPriorConversationReader).FullName!,
        typeof(IAgentActorBackendBindingStore).FullName!,
        typeof(IAgentActorBackendSelectionService).FullName!,
        typeof(IAcpProcessLauncher).FullName!,
        typeof(IAcpSessionClientFactory).FullName!,
        typeof(IAcpOnboardingConnectionService).FullName!,
        typeof(NativeHarnessAgentBackend).FullName!,
        typeof(IAgentBackend).FullName!,
        typeof(IAgentBackend).FullName!,
        typeof(IAgentExecutionCoordinator).FullName!,
        typeof(MentionParser).FullName!,
        typeof(IAgentRouter).FullName!,
        typeof(HttpClient).FullName!,
        // Phase 17 M3: permission review surface.
        typeof(IAgentPermissionDialogPresenter).FullName!,
        typeof(IAgentPermissionReviewService).FullName!,
        typeof(IAgentActionAuditStore).FullName!,
        typeof(IAgentFileReader).FullName!,
        typeof(IAgentFileMutator).FullName!,
        typeof(IAgentCommandResolver).FullName!,
        typeof(IAgentCommandExecutor).FullName!,
        typeof(IAgentActionBrokerFactory).FullName!,
        // Phase 21 M1: backend-neutral durable record storage foundation.
        typeof(IAgentDurableRecordStore).FullName!,
        typeof(AgentDurableRecordCoordinator).FullName!,
        // Phase 21 M2: backend-neutral redacted trace evidence capture and inspection.
        typeof(AgentTraceCaptureLimits).FullName!,
        typeof(AgentUsageCaptureLimits).FullName!,
        typeof(AgentDurableWorkspaceStorageKeyResolver).FullName!,
        typeof(AgentTraceBoundedCaptureQueue).FullName!,
        typeof(AgentTraceCaptureSink).FullName!,
        typeof(IAgentTraceSourceRegistry).FullName!,
        typeof(IAgentTraceInspector).FullName!,
        typeof(AgentTraceCoordinator).FullName!,
        typeof(AgentTraceBackendEvidenceSourceWriter).FullName!,
        typeof(IAgentTraceBackendEvidenceSource).FullName!,
        typeof(IAgentTraceBackendEvidenceSource).FullName!,
        typeof(AgentTraceAvailabilityProjection).FullName!,
        typeof(AgentTraceInspectionViewModel).FullName!,
        // Phase 21 M3: usage and cost evidence ledger.
        typeof(AgentUsageCaptureSink).FullName!,
        typeof(IAgentUsageInspector).FullName!,
        typeof(AgentUsageCoordinator).FullName!,
        typeof(AgentUsageBackendEvidenceSourceWriter).FullName!,
        typeof(IAgentUsageBackendEvidenceSource).FullName!,
        typeof(IAgentUsageBackendEvidenceSource).FullName!,
        typeof(AgentUsageAvailabilityProjection).FullName!,
        typeof(AgentUsageInspectionViewModel).FullName!,
        // Phase 21 M4: session continuity, explicit recovery, and termination.
        typeof(AgentSessionContinuityCheckpointWriter).FullName!,
        typeof(AgentSessionContinuityInspector).FullName!,
        typeof(IAgentSessionContinuityInspector).FullName!,
        typeof(AgentSessionContinuityRevalidator).FullName!,
        typeof(IAgentBackendContinuityAdapter).FullName!,
        typeof(IAgentBackendContinuityAdapter).FullName!,
        typeof(IAgentSessionContinuityCoordinator).FullName!,
        typeof(AgentSessionContinuityStartupReconciler).FullName!,
        typeof(AgentSessionContinuityEventSubscriber).FullName!,
        typeof(AgentSessionContinuityAvailabilityProjection).FullName!,
        typeof(AgentSessionContinuityInspectionViewModel).FullName!,
        // Phase 21 M5: durable scoped memory records (store only; no retrieval/injection).
        typeof(AgentMemoryStoreWriter).FullName!,
        typeof(AgentMemoryInspector).FullName!,
        typeof(IAgentMemoryInspector).FullName!,
        typeof(IAgentMemoryPolicyEvaluator).FullName!,
        typeof(IAgentMemoryLifecycleService).FullName!,
        typeof(AgentMemoryCoordinator).FullName!,
        typeof(IAgentMemoryCoordinator).FullName!,
        typeof(AgentMemoryAvailabilityProjection).FullName!,
        typeof(AgentMemoryInspectionViewModel).FullName!,
        // Phase 21 M6: budgeted memory retrieval, influence attribution, integrated lifecycle.
        typeof(AgentMemoryRetriever).FullName!,
        typeof(IAgentMemoryRetrievalService).FullName!,
        typeof(AgentMemoryInfluenceRecorder).FullName!,
        typeof(IAgentMemoryInfluenceRecorder).FullName!,
        typeof(AgentTransparencyLifecycleCoordinator).FullName!,
        typeof(IAgentTransparencyLifecycleCoordinator).FullName!,
        typeof(AgentTransparencyManagementViewModel).FullName!,
    };



    internal static ServiceProvider BuildProductionProvider()
    {
        var services = new ServiceCollection();
        Program.ConfigureServices(services);
        services.AddSingleton<IScheduler>(_ => CurrentThreadScheduler.Instance);
        return services.BuildServiceProvider();
    }

    private static string ReadRepoFile(string relativePath)
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(
            Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Zaide.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root (Zaide.slnx).");
    }

    [Fact]
    public void AddZaideAgents_RegistersExactlyThePlannedServices()
    {
        var services = new ServiceCollection();
        var returned = services.AddZaideAgents();

        Assert.Same(services, returned);
        // Phase 21 M1–M6 expanded the Agents DI membership to admit durable
        // record storage, trace/usage capture pipelines, session continuity,
        // memory records, retrieval/influence, and the integrated lifecycle
        // coordinator. The total reflects every AddSingleton admitted by
        // M1–M6 in registration order.
        Assert.Equal(84, services.Count);
        Assert.All(services, d => Assert.Equal(ServiceLifetime.Singleton, d.Lifetime));

        var serviceTypes = services
            .Select(d => d.ServiceType.FullName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        var expected = AgentsServiceTypeNames
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, serviceTypes);

        Assert.Contains(
            services,
            d => d.ServiceType == typeof(AgentEventStream)
                && d.ImplementationType == typeof(AgentEventStream));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(AgentContextManifestBuilder)
                && d.ImplementationType == typeof(AgentContextManifestBuilder));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IAgentContextSnapshotSources)
                && d.ImplementationType == typeof(LiveAgentContextSnapshotSources));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IAgentSessionService)
                && d.ImplementationType == typeof(AgentSessionService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(AgentConversationEventProjection)
                && d.ImplementationType == typeof(AgentConversationEventProjection));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IAgentPanelHost)
                && d.ImplementationType == typeof(AgentPanelHost));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(AgentExecutionService)
                && d.ImplementationType == typeof(AgentExecutionService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IAgentExecutionService)
                && d.ImplementationFactory is not null);
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IAgentBackend)
                && d.ImplementationFactory is not null);
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IAgentExecutionCoordinator)
                && d.ImplementationFactory is not null);
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(MentionParser)
                && d.ImplementationType == typeof(MentionParser));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IAgentRouter)
                && d.ImplementationType == typeof(AgentRouter));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(HttpClient)
                && d.ImplementationFactory is not null
                && d.ImplementationType is null);

        // Phase 17 M3: permission review surface. The shell attaches the
        // owner window to the presenter singleton after the main window is
        // created (see AppSource_AttachesPermissionPresenterOwnerToMainWindow).
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IAgentPermissionDialogPresenter)
                && d.ImplementationType == typeof(PermissionReviewDialogPresenter));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IAgentPermissionReviewService)
                && d.ImplementationType == typeof(InteractiveAgentPermissionReviewService));
    }

    [Fact]
    public void ProgramConfigureServices_ResolvesAgentsServicesAsSingletons()
    {
        using var provider = BuildProductionProvider();

        var panelHost1 = provider.GetRequiredService<IAgentPanelHost>();
        var panelHost2 = provider.GetRequiredService<IAgentPanelHost>();
        Assert.Same(panelHost1, panelHost2);
        Assert.IsType<AgentPanelHost>(panelHost1);

        var executionService1 = provider.GetRequiredService<IAgentExecutionService>();
        var executionService2 = provider.GetRequiredService<IAgentExecutionService>();
        Assert.Same(executionService1, executionService2);
        Assert.IsType<AgentExecutionService>(executionService1);

        var backend1 = provider.GetRequiredService<IAgentBackend>();
        var backend2 = provider.GetRequiredService<IAgentBackend>();
        Assert.Same(backend1, backend2);
        Assert.IsType<NativeHarnessAgentBackend>(backend1);

        var coordinator1 = provider.GetRequiredService<IAgentExecutionCoordinator>();
        var coordinator2 = provider.GetRequiredService<IAgentExecutionCoordinator>();
        Assert.Same(coordinator1, coordinator2);
        Assert.IsType<AgentExecutionCoordinator>(coordinator1);

        var parser1 = provider.GetRequiredService<MentionParser>();
        var parser2 = provider.GetRequiredService<MentionParser>();
        Assert.Same(parser1, parser2);

        var router1 = provider.GetRequiredService<IAgentRouter>();
        var router2 = provider.GetRequiredService<IAgentRouter>();
        Assert.Same(router1, router2);
        Assert.IsType<AgentRouter>(router1);

        var sessionService1 = provider.GetRequiredService<IAgentSessionService>();
        var sessionService2 = provider.GetRequiredService<IAgentSessionService>();
        Assert.Same(sessionService1, sessionService2);
        Assert.IsType<AgentSessionService>(sessionService1);

        var policyService1 = provider.GetRequiredService<IAgentContextSessionPolicyService>();
        var policyService2 = provider.GetRequiredService<IAgentContextSessionPolicyService>();
        Assert.Same(policyService1, policyService2);
        Assert.Same(sessionService1, policyService1);

        var manifestBuilder1 = provider.GetRequiredService<AgentContextManifestBuilder>();
        var manifestBuilder2 = provider.GetRequiredService<AgentContextManifestBuilder>();
        Assert.Same(manifestBuilder1, manifestBuilder2);

        var snapshotSources1 = provider.GetRequiredService<IAgentContextSnapshotSources>();
        var snapshotSources2 = provider.GetRequiredService<IAgentContextSnapshotSources>();
        Assert.Same(snapshotSources1, snapshotSources2);
        Assert.IsType<LiveAgentContextSnapshotSources>(snapshotSources1);

        var eventStream1 = provider.GetRequiredService<AgentEventStream>();
        var eventStream2 = provider.GetRequiredService<AgentEventStream>();
        Assert.Same(eventStream1, eventStream2);

        // Resolving HttpClient constructs a client only; no network request is issued.
        var httpClient1 = provider.GetRequiredService<HttpClient>();
        var httpClient2 = provider.GetRequiredService<HttpClient>();
        Assert.Same(httpClient1, httpClient2);
        Assert.Equal(TimeSpan.FromSeconds(120), httpClient1.Timeout);

        // Phase 17 M3: production DI produces the interactive review service
        // connected to the single owned dialog presenter instance.
        var reviewService1 = provider.GetRequiredService<IAgentPermissionReviewService>();
        var reviewService2 = provider.GetRequiredService<IAgentPermissionReviewService>();
        Assert.Same(reviewService1, reviewService2);
        Assert.IsType<InteractiveAgentPermissionReviewService>(reviewService1);

        var presenter1 = provider.GetRequiredService<IAgentPermissionDialogPresenter>();
        var presenter2 = provider.GetRequiredService<IAgentPermissionDialogPresenter>();
        Assert.Same(presenter1, presenter2);
        Assert.IsType<PermissionReviewDialogPresenter>(presenter1);
    }

    [Fact]
    public void ProgramConfigureServices_ResolvesAgentSessionServiceWithContextDependencies()
    {
        using var provider = BuildProductionProvider();

        var sessionService = provider.GetRequiredService<IAgentSessionService>();
        var manifestBuilder = provider.GetRequiredService<AgentContextManifestBuilder>();
        var snapshotSources = provider.GetRequiredService<IAgentContextSnapshotSources>();

        Assert.IsType<AgentSessionService>(sessionService);
        Assert.IsType<AgentContextManifestBuilder>(manifestBuilder);
        Assert.IsType<LiveAgentContextSnapshotSources>(snapshotSources);

        var constructor = typeof(AgentSessionService).GetConstructors(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            .Single();
        var parameters = constructor.GetParameters().Select(p => p.ParameterType).ToArray();
        Assert.Contains(typeof(AgentContextManifestBuilder), parameters);
        Assert.Contains(typeof(IAgentContextSnapshotSources), parameters);
    }

    [Fact]
    public void AppSource_AttachesPermissionPresenterOwnerToMainWindow()
    {
        // Phase 17 M3: the shell must attach the owned main window to the
        // permission review presenter so the Allow path is reachable in
        // production; absent an owner the presenter fails closed.
        var appSource = ReadRepoFile("src/App/Composition/App.axaml.cs");

        Assert.Contains(
            "GetRequiredService<IAgentPermissionDialogPresenter>()",
            appSource);
        Assert.Contains(".SetOwner(desktop.MainWindow)", appSource);
    }

    [Fact]
    public void ProgramSource_CallsAddZaideAgentsOnce_AndDoesNotDeclareAgentsRegistrations()
    {
        var programSource = ReadRepoFile("src/App/Composition/Program.cs");

        Assert.Single(Regex.Matches(programSource, @"AddZaideAppCore\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideConversations\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideSettings\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideWorkspace\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideEditor\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideTerminal\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideAgents\s*\(\s*\)"));

        var appCoreIndex = programSource.IndexOf("AddZaideAppCore()", StringComparison.Ordinal);
        var conversationsIndex = programSource.IndexOf("AddZaideConversations()", StringComparison.Ordinal);
        var settingsIndex = programSource.IndexOf("AddZaideSettings()", StringComparison.Ordinal);
        var workspaceIndex = programSource.IndexOf("AddZaideWorkspace()", StringComparison.Ordinal);
        var editorIndex = programSource.IndexOf("AddZaideEditor()", StringComparison.Ordinal);
        var terminalIndex = programSource.IndexOf("AddZaideTerminal()", StringComparison.Ordinal);
        var agentsIndex = programSource.IndexOf("AddZaideAgents()", StringComparison.Ordinal);
        Assert.True(appCoreIndex >= 0);
        Assert.True(conversationsIndex > appCoreIndex);
        Assert.True(settingsIndex > conversationsIndex);
        Assert.True(workspaceIndex > settingsIndex);
        Assert.True(editorIndex > workspaceIndex);
        Assert.True(terminalIndex > editorIndex);
        Assert.True(agentsIndex > terminalIndex);

        Assert.DoesNotContain(
            "AddSingleton<IAgentPanelHost, AgentPanelHost>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<IAgentExecutionService, AgentExecutionService>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<IAgentExecutionCoordinator, AgentExecutionCoordinator>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<IAgentRouter, AgentRouter>()",
            programSource);
        Assert.DoesNotContain("new HttpClient()", programSource);
        Assert.DoesNotContain("TimeSpan.FromSeconds(120)", programSource);

        // AddLogging remains in Program (not an M6f registration).
        Assert.Contains("AddLogging(", programSource);
    }

    [Fact]
    public void AgentsModuleSource_ContainsExactlyThePlannedRegistrations()
    {
        var moduleSource = ReadRepoFile(
            "src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs");

        Assert.Contains(
            "internal static class AgentsServiceCollectionExtensions",
            moduleSource);
        Assert.Contains("internal static IServiceCollection AddZaideAgents", moduleSource);

        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<AgentEventStream>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<AgentContextManifestBuilder>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IAgentContextSnapshotSources,\s*LiveAgentContextSnapshotSources>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IAgentSessionService,\s*AgentSessionService>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<AgentConversationEventProjection>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IAgentPanelHost,\s*AgentPanelHost>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<AgentExecutionService>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IAgentExecutionService>\s*\([\s\S]*?\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<INativeHarnessProviderTransport,\s*NativeHarnessProviderClient>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<INativeHarnessProviderOptionsSource,\s*NativeHarnessProviderOptionsSource>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<INativeHarnessPriorConversationReader,\s*NativeHarnessPriorConversationReader>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IAgentActorBackendBindingStore>\s*\([\s\S]*?\)"));
        Assert.Contains("AgentActorBackendBindingPathResolver.GetPrimaryPath()", moduleSource);
        Assert.Contains("AgentActorBackendBindingPathResolver.GetTempPath()", moduleSource);
        Assert.Contains("AgentActorBackendBindingPathResolver.GetLastKnownGoodPath()", moduleSource);
        Assert.Contains("LazyAgentActorActiveRunQuery", moduleSource);
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IAgentActorBackendSelectionService>\s*\("));
        Assert.Contains("AgentActorBackendSelectionService", moduleSource, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<AgentBackendBindingPresenter>\s*\("));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IAcpProcessLauncher,\s*AcpSystemDiagnosticsProcessLauncher>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IAcpSessionClientFactory>\s*\([\s\S]*?\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IAcpOnboardingConnectionService>\s*\("));
        Assert.Contains("AcpWorkspaceWorkingDirectory.CreateProvider", moduleSource, StringComparison.Ordinal);
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<NativeHarnessAgentBackend>\s*\([\s\S]*?\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<AcpActionCapableAgentBackend>\s*\([\s\S]*?\)"));
        Assert.Equal(2, Regex.Matches(moduleSource, @"AddSingleton<IAgentBackend>\s*\([\s\S]*?\)").Count);
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IAgentExecutionCoordinator>\(Program\.CreateAgentExecutionCoordinator\)"));
        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<MentionParser>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IAgentRouter,\s*AgentRouter>\(\)"));
        Assert.Single(Regex.Matches(moduleSource, @"new HttpClient\(\)"));
        Assert.Contains("TimeSpan.FromSeconds(120)", moduleSource);

        // Phase 17 M3: permission review surface registrations.
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IAgentPermissionDialogPresenter,\s*PermissionReviewDialogPresenter>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IAgentPermissionReviewService,\s*InteractiveAgentPermissionReviewService>\(\)"));

        // Phase 21 M1–M6 expanded the Agents DI membership; the count now
        // reflects the durable record, trace/usage, continuity, memory, and
        // integrated-lifecycle registrations admitted through M6.
        Assert.Equal(84, Regex.Matches(moduleSource, @"AddSingleton").Count);
    }

    [Fact]
    public void Program_ConfigureServices_ResolvesExecutionCoordinatorAndNativeHarnessDependenciesWithoutTestReplacementsOrNetwork()
    {
        using var provider = BuildProductionProvider();
        var coordinator = provider.GetRequiredService<IAgentExecutionCoordinator>();
        var backend = provider.GetRequiredService<IAgentBackend>();
        var optionsSource = provider.GetRequiredService<INativeHarnessProviderOptionsSource>();
        var executionService = provider.GetRequiredService<IAgentExecutionService>();
        var concreteExecutionService = provider.GetRequiredService<AgentExecutionService>();
        var priorReader = provider.GetRequiredService<INativeHarnessPriorConversationReader>();
        var transport = provider.GetRequiredService<INativeHarnessProviderTransport>();

        Assert.NotNull(coordinator);
        Assert.IsType<NativeHarnessAgentBackend>(backend);
        Assert.NotNull(optionsSource);
        Assert.NotNull(executionService);
        Assert.NotNull(concreteExecutionService);
        Assert.Same(concreteExecutionService, executionService);
        Assert.NotNull(priorReader);
        Assert.NotNull(transport);
    }


    [Fact]
    public void ProgramSource_CallsAllTwelveModules_AndHasNoDirectProductionAddSingleton()
    {
        var programSource = ReadRepoFile("src/App/Composition/Program.cs");

        Assert.Single(Regex.Matches(programSource, @"AddZaideAppCore\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideConversations\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideSettings\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideWorkspace\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideEditor\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideTerminal\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideAgents\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideTownhall\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideSourceControl\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideProjectSystem\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideLanguage\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideDebugging\s*\(\s*\)"));

        var appCoreIndex = programSource.IndexOf("AddZaideAppCore()", StringComparison.Ordinal);
        var conversationsIndex = programSource.IndexOf("AddZaideConversations()", StringComparison.Ordinal);
        var settingsIndex = programSource.IndexOf("AddZaideSettings()", StringComparison.Ordinal);
        var workspaceIndex = programSource.IndexOf("AddZaideWorkspace()", StringComparison.Ordinal);
        var editorIndex = programSource.IndexOf("AddZaideEditor()", StringComparison.Ordinal);
        var terminalIndex = programSource.IndexOf("AddZaideTerminal()", StringComparison.Ordinal);
        var agentsIndex = programSource.IndexOf("AddZaideAgents()", StringComparison.Ordinal);
        var townhallIndex = programSource.IndexOf("AddZaideTownhall()", StringComparison.Ordinal);
        var sourceControlIndex = programSource.IndexOf("AddZaideSourceControl()", StringComparison.Ordinal);
        var projectSystemIndex = programSource.IndexOf("AddZaideProjectSystem()", StringComparison.Ordinal);
        var languageIndex = programSource.IndexOf("AddZaideLanguage()", StringComparison.Ordinal);
        var debuggingIndex = programSource.IndexOf("AddZaideDebugging()", StringComparison.Ordinal);
        Assert.True(appCoreIndex >= 0);
        Assert.True(conversationsIndex > appCoreIndex);
        Assert.True(settingsIndex > conversationsIndex);
        Assert.True(workspaceIndex > settingsIndex);
        Assert.True(editorIndex > workspaceIndex);
        Assert.True(terminalIndex > editorIndex);
        Assert.True(agentsIndex > terminalIndex);
        Assert.True(townhallIndex > agentsIndex);
        Assert.True(sourceControlIndex > townhallIndex);
        Assert.True(projectSystemIndex > sourceControlIndex);
        Assert.True(languageIndex > projectSystemIndex);
        Assert.True(debuggingIndex > languageIndex);

        // M6k moved all Debugging registrations; no direct production AddSingleton remains.
        Assert.DoesNotContain("AddSingleton<", programSource);
        Assert.DoesNotContain("AddSingleton(", programSource);

        // AddLogging remains in Program.
        Assert.Contains("AddLogging(", programSource);

        // M7: CompositionRoot store assigned in Program; no fictitious registration module.
        Assert.Contains("CompositionRoot.Services = sp!", programSource);
        Assert.DoesNotContain("App.Services", programSource);
        Assert.DoesNotContain("AddZaideCompositionRoot", programSource);
    }
}
