using Microsoft.Extensions.DependencyInjection;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Editor.Application;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;

namespace Zaide.App.Composition.Registration;

internal static class EditorServiceCollectionExtensions
{
    internal static IServiceCollection AddZaideEditor(
        this IServiceCollection services)
    {
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IEditorUiDispatcher, AvaloniaEditorUiDispatcher>();
        services.AddSingleton<IAgentDocumentReconciler, WorkspaceEditorDocumentReconciler>();
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton<IEditorReadOnlyTabService, EditorReadOnlyTabService>();
        services.AddSingleton<IEditorStateSnapshotService, EditorStateSnapshotService>();
        services.AddSingleton<EditorSearchViewModel>();
        services.AddSingleton<EditorTabViewModel>();
        services.AddSingleton<EditorLanguageInputViewModel>();

        return services;
    }
}
