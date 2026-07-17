using System;
using System.Reactive.Linq;
using Microsoft.Extensions.DependencyInjection;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.ProjectSystem.Presentation;
using Zaide.Tests.Features.ProjectSystem;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Contracts;

namespace Zaide.Tests.Features.ProjectSystem;

/// <summary>
/// Shared factory for idle test-results projections in composition tests.
/// </summary>
internal static class TestTestResultsFactory
{
    public static TestResultsViewModel Create(
        EditorTabViewModel? editorTabs = null,
        ProjectWorkflowViewModel? workflow = null)
    {
        editorTabs ??= CreateMinimalEditorTabs();
        workflow ??= TestProjectWorkflowFactory.Create();
        var service = new EmptyTestResultsService();
        return new TestResultsViewModel(service, editorTabs, workflow);
    }

    private static EditorTabViewModel CreateMinimalEditorTabs()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton(new Zaide.Features.Workspace.Domain.Workspace());
        services.AddSingleton<IFileService, FileService>();
        services.AddTransient<EditorViewModel>();
        var sp = services.BuildServiceProvider();
        var workspace = sp.GetRequiredService<Zaide.Features.Workspace.Domain.Workspace>();
        return new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), workspace);
    }

    private sealed class EmptyTestResultsService : ITestResultsService
    {
        public TestResultsSnapshot Current => TestResultsSnapshot.Empty;

        public IObservable<TestResultsSnapshot> WhenChanged =>
            System.Reactive.Linq.Observable.Never<TestResultsSnapshot>();

        public void Dispose()
        {
        }
    }
}
