using Microsoft.Extensions.DependencyInjection;
using Zaide.Features.SourceControl.Domain;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Infrastructure;
using Zaide.Features.SourceControl.Presentation;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests.Features.SourceControl;

/// <summary>
/// Shared wiring for Source Control ViewModel tests that exercise editor diff tabs.
/// </summary>
internal static class SourceControlTestFactory
{
    public static (SourceControlViewModel ViewModel, EditorTabViewModel EditorTabs) CreateWithDiffTabs(
        ISourceControlSnapshotOrchestrator orchestrator,
        global::Zaide.Features.Workspace.Domain.Workspace workspace,
        IGitMutationService mutation,
        IGitRepositoryService gitRepository,
        IFileDiffService? diffService = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(workspace);
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton(diffService ?? new FileDiffService());
        services.AddSingleton<IGitRepositoryService>(gitRepository);
        var sp = services.BuildServiceProvider();

        var editorTabs = new EditorTabViewModel(
            sp,
            sp.GetRequiredService<IFileService>(),
            workspace);
        var diffTabs = new SourceControlDiffTabService(
            editorTabs,
            sp.GetRequiredService<IFileDiffService>(),
            workspace,
            gitRepository,
            sp);
        var vm = new SourceControlViewModel(
            orchestrator,
            workspace,
            mutation,
            gitRepository,
            diffTabs);

        return (vm, editorTabs);
    }
}
