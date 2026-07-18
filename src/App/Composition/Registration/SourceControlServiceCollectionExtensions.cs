using Microsoft.Extensions.DependencyInjection;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.SourceControl.Infrastructure;
using Zaide.Features.SourceControl.Presentation;

namespace Zaide.App.Composition.Registration;

internal static class SourceControlServiceCollectionExtensions
{
    internal static IServiceCollection AddZaideSourceControl(
        this IServiceCollection services)
    {
        services.AddSingleton<SourceControlViewModel>();

        // M1: read-only git repository discovery + status read seam
        services.AddSingleton<IGitRepositoryService, GitRepositoryService>();

        // M3: focused snapshot refresh orchestration seam for Source Control
        services.AddSingleton<ISourceControlSnapshotOrchestrator, SourceControlSnapshotOrchestrator>();

        // M1: file diff service for Source Control diff view
        services.AddSingleton<IFileDiffService, FileDiffService>();
        services.AddSingleton<ISourceControlDiffTabService, SourceControlDiffTabService>();

        // Phase 7.4 M1: git mutation seam for stage/unstage operations
        services.AddSingleton<IGitMutationService, GitMutationService>();

        return services;
    }
}
