using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Services;
using Zaide.Features.Language.Infrastructure.Lsp;
using Zaide.ViewModels;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Presentation;
using Zaide.Features.Language.Contracts;
using Zaide.Features.Language.Application;

namespace Zaide.Tests.Features.ProjectSystem.Presentation;

/// <summary>
/// Focused editor-projection/navigation tests for Phase 10 M3 Problems.
/// </summary>
public sealed class ProblemsNavigationProjectionTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase10-m3-nav-" + Guid.NewGuid().ToString("N"));

    static ProblemsNavigationProjectionTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        Directory.CreateDirectory(TempRoot);
    }

    [Fact]
    public void EditorViewModel_RequestNavigate_SetsPendingFields()
    {
        var doc = new Document(Path.Combine(TempRoot, "a.cs"), "hello");
        var vm = new EditorViewModel(doc, new FileService());

        vm.RequestNavigate(2, 3);

        Assert.Equal(2, vm.PendingNavigationOffset);
        Assert.Equal(3, vm.PendingNavigationLength);
        Assert.Equal(1, vm.NavigationRequestId);

        vm.ClearNavigationRequest();
        Assert.Null(vm.PendingNavigationOffset);
        Assert.Equal(0, vm.PendingNavigationLength);
    }

    [Fact]
    public void EditorViewModel_RequestNavigate_RejectsNegativeOffset()
    {
        var doc = new Document(Path.Combine(TempRoot, "b.cs"), "hello");
        var vm = new EditorViewModel(doc, new FileService());

        vm.RequestNavigate(-1, 1);
        Assert.Null(vm.PendingNavigationOffset);
        Assert.Equal(0, vm.NavigationRequestId);
    }

    [Fact]
    public async Task NavigateToAlreadyOpenTab_ActivatesAndRequestsSelection()
    {
        var services = new ServiceCollection();
        var workspace = new global::Zaide.Features.Workspace.Domain.Workspace();
        services.AddSingleton(workspace);
        services.AddSingleton<IFileService>(new FileService());
        await using var sp = services.BuildServiceProvider();

        var content = "namespace X { class C {} }";
        var path = Path.Combine(TempRoot, "open.cs");
        await File.WriteAllTextAsync(path, content);

        var tabs = new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), workspace);
        Assert.True(await tabs.OpenFileCommand.Execute(path).FirstAsync());
        Assert.NotNull(tabs.ActiveTab);

        var diagnostics = new MutableDiagnosticsService();
        var buildDiagnostics = new EmptyBuildDiagnosticsService();
        var range = new LspRange(0, 10, 0, 11);
        Assert.True(LspUtf16PositionMapper.TryMapRange(content, range, out var start, out var end));
        var diagnostic = new LanguageDiagnostic(
            LanguageDocumentUri.FromPath(path),
            path,
            1,
            1,
            LanguageDiagnosticSeverity.Error,
            "id",
            "CS0103",
            "csharp",
            range,
            start,
            end);
        diagnostics.Publish(new LanguageDiagnosticsSnapshot(
            LanguageSessionState.Ready, 1, null, new[] { diagnostic }));

        var problems = new ProblemsViewModel(diagnostics, buildDiagnostics, tabs, workspace)
        {
            Scheduler = System.Reactive.Concurrency.ImmediateScheduler.Instance,
        };
        problems.Activate();

        var item = Assert.Single(problems.Problems);
        Assert.True(await problems.NavigateToProblemAsync(item));
        Assert.Equal(path, tabs.ActiveTab!.FilePath);
        Assert.Equal(start, tabs.ActiveTab.PendingNavigationOffset);
        Assert.Equal(end - start, tabs.ActiveTab.PendingNavigationLength);
        buildDiagnostics.Dispose();
    }

    [Fact]
    public async Task NavigateAfterDocumentTextInvalidatesRange_NoOps()
    {
        var services = new ServiceCollection();
        var workspace = new global::Zaide.Features.Workspace.Domain.Workspace();
        services.AddSingleton(workspace);
        services.AddSingleton<IFileService>(new FileService());
        await using var sp = services.BuildServiceProvider();

        var path = Path.Combine(TempRoot, "mutate.cs");
        var original = "ABCDEFGH";
        await File.WriteAllTextAsync(path, original);

        var tabs = new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), workspace);
        Assert.True(await tabs.OpenFileCommand.Execute(path).FirstAsync());

        var diagnostics = new MutableDiagnosticsService();
        var buildDiagnostics = new EmptyBuildDiagnosticsService();
        // Range at end of original text.
        var range = new LspRange(0, 6, 0, 8);
        Assert.True(LspUtf16PositionMapper.TryMapRange(original, range, out var start, out var end));
        var diagnostic = new LanguageDiagnostic(
            LanguageDocumentUri.FromPath(path),
            path,
            1,
            1,
            LanguageDiagnosticSeverity.Error,
            "range",
            null,
            null,
            range,
            start,
            end);
        diagnostics.Publish(new LanguageDiagnosticsSnapshot(
            LanguageSessionState.Ready, 1, null, new[] { diagnostic }));

        var problems = new ProblemsViewModel(diagnostics, buildDiagnostics, tabs, workspace)
        {
            Scheduler = System.Reactive.Concurrency.ImmediateScheduler.Instance,
        };
        problems.Activate();
        var item = Assert.Single(problems.Problems);

        // Shrink the live document so the stored LSP range no longer maps.
        tabs.ActiveTab!.TextContent = "AB";

        Assert.False(await problems.NavigateToProblemAsync(item));
        Assert.Null(tabs.ActiveTab.PendingNavigationOffset);
        buildDiagnostics.Dispose();
    }

    private sealed class MutableDiagnosticsService : ILanguageDiagnosticsService
    {
        private LanguageDiagnosticsSnapshot _current = LanguageDiagnosticsSnapshot.Empty;
        private readonly Subject<LanguageDiagnosticsSnapshot> _subject = new();

        public LanguageDiagnosticsSnapshot Current => _current;
        public IObservable<LanguageDiagnosticsSnapshot> WhenChanged => _subject;

        public void Publish(LanguageDiagnosticsSnapshot snapshot)
        {
            _current = snapshot;
            _subject.OnNext(snapshot);
        }

        public void Dispose()
        {
            _subject.OnCompleted();
            _subject.Dispose();
        }
    }

    private sealed class EmptyBuildDiagnosticsService : IBuildDiagnosticsService
    {
        public BuildDiagnosticsSnapshot Current => BuildDiagnosticsSnapshot.Empty;
        public IObservable<BuildDiagnosticsSnapshot> WhenChanged =>
            System.Reactive.Linq.Observable.Empty<BuildDiagnosticsSnapshot>();

        public void Dispose()
        {
        }
    }
}
