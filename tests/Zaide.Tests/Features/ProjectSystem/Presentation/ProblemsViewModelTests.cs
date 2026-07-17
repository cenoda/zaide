using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.Language.Infrastructure.Lsp;
using Zaide.App.Shell;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Presentation;
using Zaide.Features.Language.Contracts;
using Zaide.Features.Language.Application;

namespace Zaide.Tests.Features.ProjectSystem.Presentation;

/// <summary>
/// Phase 10 M3 tests for Problems projection and navigation.
/// </summary>
public sealed class ProblemsViewModelTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase10-m3-problems-" + Guid.NewGuid().ToString("N"));

    static ProblemsViewModelTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        Directory.CreateDirectory(TempRoot);
    }

    private sealed class FakeDiagnosticsService : ILanguageDiagnosticsService
    {
        private readonly BehaviorSubject<LanguageDiagnosticsSnapshot> _subject =
            new(LanguageDiagnosticsSnapshot.Empty);

        public LanguageDiagnosticsSnapshot Current => _subject.Value;
        public IObservable<LanguageDiagnosticsSnapshot> WhenChanged => _subject.AsObservable();

        public void Publish(LanguageDiagnosticsSnapshot snapshot) => _subject.OnNext(snapshot);

        public void Dispose()
        {
            _subject.OnCompleted();
            _subject.Dispose();
        }
    }

    private sealed class FakeBuildDiagnosticsService : IBuildDiagnosticsService
    {
        private readonly BehaviorSubject<BuildDiagnosticsSnapshot> _subject =
            new(BuildDiagnosticsSnapshot.Empty);

        public BuildDiagnosticsSnapshot Current => _subject.Value;
        public IObservable<BuildDiagnosticsSnapshot> WhenChanged => _subject.AsObservable();

        public void Publish(BuildDiagnosticsSnapshot snapshot) => _subject.OnNext(snapshot);

        public void Dispose()
        {
            _subject.OnCompleted();
            _subject.Dispose();
        }
    }

    private sealed class Harness : IDisposable
    {
        public global::Zaide.Features.Workspace.Domain.Workspace Workspace { get; } = new();
        public FakeDiagnosticsService Diagnostics { get; } = new();
        public FakeBuildDiagnosticsService BuildDiagnostics { get; } = new();
        public EditorTabViewModel EditorTabs { get; }
        public ProblemsViewModel Problems { get; }
        private readonly ServiceProvider _sp;

        public Harness()
        {
            var services = new ServiceCollection();
            services.AddSingleton(Workspace);
            services.AddSingleton<IFileService>(new FileService());
            _sp = services.BuildServiceProvider();
            EditorTabs = new EditorTabViewModel(_sp, _sp.GetRequiredService<IFileService>(), Workspace);
            Problems = new ProblemsViewModel(Diagnostics, BuildDiagnostics, EditorTabs, Workspace)
            {
                Scheduler = ImmediateScheduler.Instance,
            };
            Problems.Activate();
        }

        public string WriteCs(string name, string content)
        {
            var path = Path.Combine(TempRoot, name + ".cs");
            File.WriteAllText(path, content);
            return path;
        }

        public LanguageDiagnostic MakeDiagnostic(
            string path,
            string content,
            string message,
            int startChar = 0,
            int endChar = 1,
            int version = 1,
            long generation = 1)
        {
            var range = new LspRange(0, startChar, 0, endChar);
            Assert.True(LspUtf16PositionMapper.TryMapRange(content, range, out var start, out var end));
            return new LanguageDiagnostic(
                LanguageDocumentUri.FromPath(path),
                path,
                version,
                generation,
                LanguageDiagnosticSeverity.Error,
                message,
                "CS1002",
                "csharp",
                range,
                start,
                end);
        }

        public void Dispose()
        {
            Problems.Dispose();
            Diagnostics.Dispose();
            BuildDiagnostics.Dispose();
            _sp.Dispose();
        }
    }

    [Fact]
    public void InitialProjection_UnavailableEmpty()
    {
        using var harness = new Harness();
        Assert.Equal(LanguageSessionState.Unavailable, harness.Problems.State);
        Assert.Empty(harness.Problems.Problems);
        Assert.Contains("unavailable", harness.Problems.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadyEmpty_ShowsNoProblemsStatus()
    {
        using var harness = new Harness();
        harness.Diagnostics.Publish(new LanguageDiagnosticsSnapshot(
            LanguageSessionState.Ready, 1, null, Array.Empty<LanguageDiagnostic>()));

        Assert.Equal(LanguageSessionState.Ready, harness.Problems.State);
        Assert.Empty(harness.Problems.Problems);
        Assert.Equal("No problems.", harness.Problems.StatusMessage);
    }

    [Fact]
    public void ReadyWithDiagnostics_ProjectsItems()
    {
        using var harness = new Harness();
        var path = harness.WriteCs("proj", "class A { }");
        var diag = harness.MakeDiagnostic(path, "class A { }", "missing semi");

        harness.Diagnostics.Publish(new LanguageDiagnosticsSnapshot(
            LanguageSessionState.Ready, 1, null, new[] { diag }));

        var item = Assert.Single(harness.Problems.Problems);
        Assert.Equal("missing semi", item.Message);
        Assert.Equal(path, item.FilePath);
        Assert.Equal(1, item.Line);
        Assert.Null(harness.Problems.StatusMessage);
        Assert.Equal(1, harness.Problems.ProblemCount);
    }

    [Fact]
    public void LoadingFailedStates_ProjectTruthfullyAndClearItems()
    {
        using var harness = new Harness();
        var path = harness.WriteCs("state", "x");
        var diag = harness.MakeDiagnostic(path, "x", "err");

        harness.Diagnostics.Publish(new LanguageDiagnosticsSnapshot(
            LanguageSessionState.Ready, 1, null, new[] { diag }));
        Assert.Single(harness.Problems.Problems);

        harness.Diagnostics.Publish(new LanguageDiagnosticsSnapshot(
            LanguageSessionState.Loading, 2, null, Array.Empty<LanguageDiagnostic>()));
        Assert.Equal(LanguageSessionState.Loading, harness.Problems.State);
        Assert.Empty(harness.Problems.Problems);
        Assert.Contains("loading", harness.Problems.StatusMessage, StringComparison.OrdinalIgnoreCase);

        harness.Diagnostics.Publish(new LanguageDiagnosticsSnapshot(
            LanguageSessionState.Failed,
            3,
            new LanguageSessionFailure(LanguageSessionFailureKind.MissingServerBinary, "no server"),
            Array.Empty<LanguageDiagnostic>()));
        Assert.Equal(LanguageSessionState.Failed, harness.Problems.State);
        Assert.Equal(
            LanguageSessionStatusPolicy.MapFailureMessage(
                new LanguageSessionFailure(LanguageSessionFailureKind.MissingServerBinary, "no server")),
            harness.Problems.StatusMessage);
        Assert.Empty(harness.Problems.Problems);
    }

    [Fact]
    public async Task NavigateLiveProblem_OpensFileAndRequestsCaret()
    {
        using var harness = new Harness();
        var content = "class A { }";
        var path = harness.WriteCs("nav-live", content);
        var diag = harness.MakeDiagnostic(path, content, "problem", startChar: 6, endChar: 7);

        harness.Diagnostics.Publish(new LanguageDiagnosticsSnapshot(
            LanguageSessionState.Ready, 1, null, new[] { diag }));

        var item = Assert.Single(harness.Problems.Problems);
        var ok = await harness.Problems.NavigateToProblemAsync(item);

        Assert.True(ok);
        Assert.NotNull(harness.EditorTabs.ActiveTab);
        Assert.Equal(path, harness.EditorTabs.ActiveTab!.FilePath);
        Assert.Equal(diag.StartOffset, harness.EditorTabs.ActiveTab.PendingNavigationOffset);
        Assert.Equal(diag.EndOffset - diag.StartOffset, harness.EditorTabs.ActiveTab.PendingNavigationLength);
        Assert.True(harness.EditorTabs.ActiveTab.NavigationRequestId > 0);
    }

    [Fact]
    public async Task NavigateStaleGeneration_NoOps()
    {
        using var harness = new Harness();
        var content = "class A { }";
        var path = harness.WriteCs("nav-stale", content);
        var diag = harness.MakeDiagnostic(path, content, "stale", generation: 1);

        harness.Diagnostics.Publish(new LanguageDiagnosticsSnapshot(
            LanguageSessionState.Ready, 1, null, new[] { diag }));
        var item = Assert.Single(harness.Problems.Problems);

        // Snapshot advances without the diagnostic.
        harness.Diagnostics.Publish(new LanguageDiagnosticsSnapshot(
            LanguageSessionState.Ready, 2, null, Array.Empty<LanguageDiagnostic>()));

        var ok = await harness.Problems.NavigateToProblemAsync(item);
        Assert.False(ok);
        Assert.Null(harness.EditorTabs.ActiveTab);
    }

    [Fact]
    public async Task NavigateWhenUnavailable_NoOps()
    {
        using var harness = new Harness();
        var content = "class A { }";
        var path = harness.WriteCs("nav-unavail", content);
        var diag = harness.MakeDiagnostic(path, content, "x");

        harness.Diagnostics.Publish(new LanguageDiagnosticsSnapshot(
            LanguageSessionState.Ready, 1, null, new[] { diag }));
        var item = Assert.Single(harness.Problems.Problems);

        harness.Diagnostics.Publish(new LanguageDiagnosticsSnapshot(
            LanguageSessionState.Unavailable, 2, null, Array.Empty<LanguageDiagnostic>()));

        Assert.False(await harness.Problems.NavigateToProblemAsync(item));
    }

    [Fact]
    public async Task NavigateNullItem_NoOps()
    {
        using var harness = new Harness();
        Assert.False(await harness.Problems.NavigateToProblemAsync(null));
    }

    [Fact]
    public void SnapshotReplace_DoesNotKeepStaleItems()
    {
        using var harness = new Harness();
        var path = harness.WriteCs("replace", "ab");
        var first = harness.MakeDiagnostic(path, "ab", "one");
        var second = harness.MakeDiagnostic(path, "ab", "two", startChar: 1, endChar: 2);

        harness.Diagnostics.Publish(new LanguageDiagnosticsSnapshot(
            LanguageSessionState.Ready, 1, null, new[] { first }));
        Assert.Equal("one", harness.Problems.Problems[0].Message);

        harness.Diagnostics.Publish(new LanguageDiagnosticsSnapshot(
            LanguageSessionState.Ready, 1, null, new[] { second }));
        Assert.Single(harness.Problems.Problems);
        Assert.Equal("two", harness.Problems.Problems[0].Message);
    }
}
