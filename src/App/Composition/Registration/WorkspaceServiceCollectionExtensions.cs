using Microsoft.Extensions.DependencyInjection;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Infrastructure;
using Zaide.Features.Workspace.Presentation;

namespace Zaide.App.Composition.Registration;

internal static class WorkspaceServiceCollectionExtensions
{
    internal static IServiceCollection AddZaideWorkspace(
        this IServiceCollection services)
    {
        services.AddSingleton<IFileTreeService, FileTreeService>();
        services.AddSingleton<FileTreeViewModel>();

        return services;
    }
}
