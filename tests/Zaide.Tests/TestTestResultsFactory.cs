using System;
using System.Reactive.Linq;
using Microsoft.Extensions.DependencyInjection;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests;

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
        services.AddSingleton(new Zaide.Models.Workspace());
        services.AddSingleton<IFileService, FileService>();
        services.AddTransient<EditorViewModel>();
        var sp = services.BuildServiceProvider();
        var workspace = sp.GetRequiredService<Zaide.Models.Workspace>();
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
