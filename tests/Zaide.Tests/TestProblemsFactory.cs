using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.DependencyInjection;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests;

/// <summary>
/// Shared factory for empty Problems projections in MainWindow composition tests.
/// </summary>
internal static class TestProblemsFactory
{
    public static ProblemsViewModel Create(Workspace workspace, EditorTabViewModel editorTabs) =>
        new(new EmptyLanguageDiagnosticsService(), new EmptyBuildDiagnosticsService(), editorTabs, workspace);

    public static ProblemsViewModel CreateWithWorkspace(Workspace workspace)
    {
        var services = new ServiceCollection();
        services.AddSingleton(workspace);
        services.AddSingleton<IFileService>(new global::Zaide.Features.Editor.Infrastructure.FileService());
        var sp = services.BuildServiceProvider();
        var tabs = new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), workspace);
        return Create(workspace, tabs);
    }

    private sealed class EmptyLanguageDiagnosticsService : ILanguageDiagnosticsService
    {
        private readonly BehaviorSubject<LanguageDiagnosticsSnapshot> _subject =
            new(LanguageDiagnosticsSnapshot.Empty);

        public LanguageDiagnosticsSnapshot Current => _subject.Value;

        public IObservable<LanguageDiagnosticsSnapshot> WhenChanged => _subject.AsObservable();

        public void Dispose()
        {
            _subject.OnCompleted();
            _subject.Dispose();
        }
    }

    private sealed class EmptyBuildDiagnosticsService : IBuildDiagnosticsService
    {
        private readonly BehaviorSubject<BuildDiagnosticsSnapshot> _subject =
            new(BuildDiagnosticsSnapshot.Empty);

        public BuildDiagnosticsSnapshot Current => _subject.Value;

        public IObservable<BuildDiagnosticsSnapshot> WhenChanged => _subject.AsObservable();

        public void Dispose()
        {
            _subject.OnCompleted();
            _subject.Dispose();
        }
    }
}
