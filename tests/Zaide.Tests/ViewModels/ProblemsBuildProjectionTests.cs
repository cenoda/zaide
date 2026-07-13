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
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

/// <summary>
/// Phase 11 M3 tests for Problems merge of LSP and build diagnostics.
/// </summary>
public sealed class ProblemsBuildProjectionTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase11-problems-merge-" + Guid.NewGuid().ToString("N"));

    static ProblemsBuildProjectionTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        Directory.CreateDirectory(TempRoot);
    }

    private sealed class FakeLanguageDiagnosticsService : ILanguageDiagnosticsService
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
        public Workspace Workspace { get; } = new();
        public FakeLanguageDiagnosticsService LanguageDiagnostics { get; } = new();
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
            Problems = new ProblemsViewModel(
                LanguageDiagnostics,
                BuildDiagnostics,
                EditorTabs,
                Workspace)
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

        public LanguageDiagnostic MakeLanguageDiagnostic(
            string path,
            string content,
            string message,
            int startChar = 0,
            int endChar = 1,
            long generation = 1)
        {
            var range = new LspRange(0, startChar, 0, endChar);
            Assert.True(LspUtf16PositionMapper.TryMapRange(content, range, out var start, out var end));
            return new LanguageDiagnostic(
                LanguageDocumentUri.FromPath(path),
                path,
                1,
                generation,
                LanguageDiagnosticSeverity.Error,
                message,
                "CS1002",
                "csharp-ls",
                range,
                start,
                end);
        }

        public void Dispose()
        {
            Problems.Dispose();
            LanguageDiagnostics.Dispose();
            BuildDiagnostics.Dispose();
            _sp.Dispose();
        }
    }

    [Fact]
    public void Merge_ProjectsLanguageAndBuildItemsWithSourceAttribution()
    {
        using var harness = new Harness();
        var lspPath = harness.WriteCs("lsp", "class A { }");
        var buildPath = harness.WriteCs("build", "class B { }");

        harness.LanguageDiagnostics.Publish(new LanguageDiagnosticsSnapshot(
            LanguageSessionState.Ready,
            1,
            null,
            new[]
            {
                harness.MakeLanguageDiagnostic(lspPath, "class A { }", "lsp issue"),
            }));

        harness.BuildDiagnostics.Publish(new BuildDiagnosticsSnapshot(
            2,
            ProjectWorkflowOutcomeKind.Failed,
            false,
            new[]
            {
                new BuildDiagnostic(buildPath, 1, 7, LanguageDiagnosticSeverity.Error, "CS1002", "build issue"),
            }));

        Assert.Equal(2, harness.Problems.Problems.Count);
        Assert.Contains(harness.Problems.Problems, p => p.Kind == ProblemKind.Language && p.Source == "csharp-ls");
        Assert.Contains(harness.Problems.Problems, p => p.Kind == ProblemKind.Build && p.Source == "build");
    }

    [Fact]
    public void BuildStart_ClearsBuildItems_ButRetainsLanguageItems()
    {
        using var harness = new Harness();
        var lspPath = harness.WriteCs("retain-lsp", "class A { }");
        var buildPath = harness.WriteCs("clear-build", "class B { }");

        harness.LanguageDiagnostics.Publish(new LanguageDiagnosticsSnapshot(
            LanguageSessionState.Ready,
            1,
            null,
            new[]
            {
                harness.MakeLanguageDiagnostic(lspPath, "class A { }", "keep me"),
            }));

        harness.BuildDiagnostics.Publish(new BuildDiagnosticsSnapshot(
            2,
            ProjectWorkflowOutcomeKind.Failed,
            false,
            new[]
            {
                new BuildDiagnostic(buildPath, 1, 7, LanguageDiagnosticSeverity.Error, "CS1002", "build one"),
            }));

        Assert.Equal(2, harness.Problems.Problems.Count);

        harness.BuildDiagnostics.Publish(new BuildDiagnosticsSnapshot(
            3,
            null,
            false,
            Array.Empty<BuildDiagnostic>()));

        Assert.Single(harness.Problems.Problems);
        Assert.Equal(ProblemKind.Language, harness.Problems.Problems[0].Kind);
        Assert.Equal("keep me", harness.Problems.Problems[0].Message);
    }

    [Fact]
    public void BuildFinish_ReplacesBuildItems_ButRetainsLanguageItems()
    {
        using var harness = new Harness();
        var lspPath = harness.WriteCs("retain-lsp-finish", "class A { }");
        var buildPath = harness.WriteCs("replace-build", "class B { }");

        harness.LanguageDiagnostics.Publish(new LanguageDiagnosticsSnapshot(
            LanguageSessionState.Ready,
            1,
            null,
            new[]
            {
                harness.MakeLanguageDiagnostic(lspPath, "class A { }", "still here"),
            }));

        harness.BuildDiagnostics.Publish(new BuildDiagnosticsSnapshot(
            4,
            ProjectWorkflowOutcomeKind.Failed,
            false,
            new[]
            {
                new BuildDiagnostic(buildPath, 1, 7, LanguageDiagnosticSeverity.Error, "CS1002", "old build"),
            }));

        harness.BuildDiagnostics.Publish(new BuildDiagnosticsSnapshot(
            4,
            ProjectWorkflowOutcomeKind.Succeeded,
            false,
            Array.Empty<BuildDiagnostic>()));

        Assert.Single(harness.Problems.Problems);
        Assert.Equal(ProblemKind.Language, harness.Problems.Problems[0].Kind);
        Assert.DoesNotContain(harness.Problems.Problems, p => p.Kind == ProblemKind.Build);
    }

    [Fact]
    public async Task NavigateBuildProblem_OpensFileAtLineColumn()
    {
        using var harness = new Harness();
        var content = "class BuildNav { int x }";
        var path = harness.WriteCs("build-nav", content);

        harness.BuildDiagnostics.Publish(new BuildDiagnosticsSnapshot(
            5,
            ProjectWorkflowOutcomeKind.Failed,
            false,
            new[]
            {
                new BuildDiagnostic(path, 1, 23, LanguageDiagnosticSeverity.Error, "CS1002", "missing semi"),
            }));

        var item = Assert.Single(harness.Problems.Problems);
        Assert.Equal(ProblemKind.Build, item.Kind);

        Assert.True(await harness.Problems.NavigateToProblemAsync(item));
        Assert.NotNull(harness.EditorTabs.ActiveTab);
        Assert.Equal(path, harness.EditorTabs.ActiveTab!.FilePath);
        Assert.True(LspUtf16PositionMapper.TryGetOffset(content, 0, 22, out var expectedOffset));
        Assert.Equal(expectedOffset, harness.EditorTabs.ActiveTab.PendingNavigationOffset);
        Assert.Equal(0, harness.EditorTabs.ActiveTab.PendingNavigationLength);
    }
}
