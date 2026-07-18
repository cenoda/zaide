using Microsoft.Extensions.DependencyInjection;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Infrastructure;
using Zaide.Features.ProjectSystem.Presentation;

namespace Zaide.App.Composition.Registration;

internal static class ProjectSystemServiceCollectionExtensions
{
    internal static IServiceCollection AddZaideProjectSystem(
        this IServiceCollection services)
    {
        // Phase 8.3 M3: authoritative project-context discovery + service.
        services.AddSingleton<IProjectFileSystem, FileSystemProjectFileSystem>();
        services.AddSingleton<IProjectDiscovery, ProjectDiscovery>();
        services.AddSingleton<IProjectContextService, ProjectContextService>();

        // Phase 12 M3a: shared project-operation gate and build-to-debug handoff.
        services.AddSingleton<IProjectOperationGate, ProjectOperationGate>();
        services.AddSingleton<IProjectDebugTargetResolver, ProjectDebugTargetResolver>();
        services.AddSingleton<IProjectDebugLaunchService, ProjectDebugLaunchService>();

        // Phase 11 M1: UI-independent build/run/test process orchestration core.
        services.AddSingleton<IManagedProcessRunner, ManagedProcessRunner>();
        services.AddSingleton<IProjectWorkflowService, ProjectWorkflowService>();

        // Phase 11 M2: structured output projection and workflow commands.
        services.AddSingleton<IProjectOutputService, ProjectOutputService>();
        services.AddSingleton<ProjectWorkflowViewModel>();

        // Phase 11 M3: parsed build diagnostics (Problems merge projection).
        services.AddSingleton<IBuildDiagnosticsService, BuildDiagnosticsService>();

        // Phase 11 M5: structured test results projection.
        services.AddSingleton<ITestResultsService, TestResultsService>();
        services.AddSingleton<TestResultsViewModel>();

        // Phase 10 M3: structured diagnostics + Problems projection.
        services.AddSingleton<ProblemsViewModel>();

        return services;
    }
}
