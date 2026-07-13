using System;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;

using Zaide.Tests;

namespace Zaide.Tests.ViewModels;

/// <summary>
/// Phase 8.3 M4 focused tests for <see cref="StatusBarViewModel"/> project-context
/// projection. Covers every display state, selected-project updates, and
/// regression of caret, language, branch, settings, and configured-model
/// projections.
///
/// These tests do not inject <see cref="IProjectContextService"/> into
/// <see cref="StatusBarViewModel"/> directly; the status bar observes
/// <see cref="MainWindowViewModel.CurrentProjectContext"/>.
/// </summary>
public sealed class Phase83M4StatusBarViewModelProjectionTests
{
    static Phase83M4StatusBarViewModelProjectionTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private sealed class ControlledLanguageSessionService : ILanguageSessionService
    {
        private readonly Subject<LanguageSessionSnapshot> _subject = new();
        public LanguageSessionSnapshot Current { get; set; } = new(
            LanguageSessionState.Unavailable, 0, null, null, null, null);

        public IObservable<LanguageSessionSnapshot> WhenChanged => _subject;

        public void Emit(LanguageSessionSnapshot snapshot)
        {
            Current = snapshot;
            _subject.OnNext(snapshot);
        }

        public ILanguageServerSession? TryGetReadySession(long generation) => null;
        public Task RestartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() => _subject.Dispose();
    }

    /// <summary>
    /// A controlled <see cref="IProjectContextService"/> mock backed by a real
    /// <see cref="Subject{ProjectContext}"/> for deterministic emissions.
    /// </summary>
    private sealed class ControlledProjectContextService : IProjectContextService
    {
        private readonly Subject<ProjectContext> _subject = new();
        public ProjectContext Current { get; set; } = new(
            ProjectContextState.Unloaded, null,
            Array.Empty<ProjectCandidate>(), null,
            Array.Empty<string>(), null);

        public IObservable<ProjectContext> WhenChanged => _subject;

        public void Emit(ProjectContext ctx)
        {
            Current = ctx;
            _subject.OnNext(ctx);
        }

        public Task LoadAsync(string workspaceRoot, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task ReloadAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task UnloadAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public void SelectProject(ProjectCandidate? candidate) { }
        public void Dispose() { }
    }

    /// <summary>
    /// Builds a <see cref="MainWindowViewModel"/> with a controlled project-context
    /// service, creates a <see cref="StatusBarViewModel"/> with the given scheduler,
    /// and returns both along with the service emitter.
    /// </summary>
    private static (MainWindowViewModel Vm, StatusBarViewModel Status, ControlledProjectContextService Service)
        Create(IScheduler scheduler)
    {
        var svc = new ControlledProjectContextService();
        var fileTree = new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance);
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton<IFileService>(new FileService());
        services.AddTransient<EditorViewModel>();
        services.AddSingleton<Workspace>();
        using var sp = services.BuildServiceProvider();
        var editorTabs = new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), sp.GetRequiredService<Workspace>());
        var terminalService = new Mock<ITerminalService>();
        var terminalVm = new TerminalViewModel(terminalService.Object, a => a());
        var factory = new Mock<ITerminalSessionFactory>();
        factory.Setup(f => f.CreateSession()).Returns(terminalVm);
        var terminalHost = new TerminalHost(factory.Object);
        var panelHost = new AgentPanelHost();
        var coordinator = new Mock<IAgentExecutionCoordinator>().Object;
        var router = new AgentRouter(new MentionParser(panelHost), panelHost, coordinator);
        var townhall = new TownhallViewModel(new TownhallState());
        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>())).Returns(RepositoryDiscoveryResult.NotFound(""));
        git.Setup(g => g.ReadStatus(It.IsAny<string>())).Returns(new RepositoryStatusSnapshot());
        var diff = new Mock<IFileDiffService>();
        var mutation = new Mock<IGitMutationService>();
        var sourceControl = new SourceControlViewModel(
            new SourceControlSnapshotOrchestrator(git.Object),
            new Workspace(), diff.Object, mutation.Object, git.Object);

        var workspace = sp.GetRequiredService<Workspace>();
        var vm = new MainWindowViewModel(
            fileTree, editorTabs, terminalHost, panelHost, coordinator, router,
            townhall, sourceControl, TestProblemsFactory.Create(workspace, editorTabs), workspace,
            svc);
        // Use ImmediateScheduler so scheduled work executes synchronously
        // in unit test environments where AvaloniaScheduler is unavailable.
        vm.ProjectContextScheduler = ImmediateScheduler.Instance;
        vm.Activate();

        var settings = new Mock<ISettingsService>(MockBehavior.Strict);
        settings.Setup(s => s.Current).Returns(SettingsModel.Defaults);
        settings.Setup(s => s.WhenChanged).Returns(new Subject<SettingsModel>());

        var languageSession = new ControlledLanguageSessionService();
        var status = new StatusBarViewModel(vm, settings.Object, languageSession, scheduler);
        return (vm, status, svc);
    }

    /// <summary>
    /// Builds a controlled service, VM, and status bar with an immediate scheduler
    /// that runs all scheduled work synchronously.
    /// </summary>
    private static (MainWindowViewModel Vm, StatusBarViewModel Status, ControlledProjectContextService Service)
        CreateImmediate() => Create(ImmediateScheduler.Instance);

    // ── SingleProject / Selected display ─────────────────────────────────

    [Fact]
    public void SingleProject_DisplaysSelectedProjectDisplayName()
    {
        var (vm, status, svc) = CreateImmediate();

        svc.Emit(new ProjectContext(
            ProjectContextState.SingleProject, "/root",
            new[] { new ProjectCandidate("/root/MyApp.csproj", "MyApp", ProjectKind.CSharpProject) },
            new ProjectCandidate("/root/MyApp.csproj", "MyApp", ProjectKind.CSharpProject),
            Array.Empty<string>(), null));

        Assert.Equal("MyApp", status.ProjectText);
    }

    [Fact]
    public void Selected_DisplaysSelectedProjectDisplayName()
    {
        var (vm, status, svc) = CreateImmediate();

        svc.Emit(new ProjectContext(
            ProjectContextState.Selected, "/root",
            new[] { new ProjectCandidate("/root/A.csproj", "A", ProjectKind.CSharpProject),
                     new ProjectCandidate("/root/B.csproj", "B", ProjectKind.CSharpProject) },
            new ProjectCandidate("/root/A.csproj", "A", ProjectKind.CSharpProject),
            Array.Empty<string>(), null));

        Assert.Equal("A", status.ProjectText);
    }

    // ── Non-selected states ──────────────────────────────────────────────

    [Fact]
    public void Loading_DisplaysLoadingEllipsis()
    {
        var (vm, status, svc) = CreateImmediate();
        svc.Emit(new ProjectContext(
            ProjectContextState.Loading, "/root",
            Array.Empty<ProjectCandidate>(), null,
            Array.Empty<string>(), null));
        Assert.Equal("Loading…", status.ProjectText);
    }

    [Fact]
    public void NoProject_DisplaysNoProject()
    {
        var (vm, status, svc) = CreateImmediate();
        svc.Emit(new ProjectContext(
            ProjectContextState.NoProject, "/root",
            Array.Empty<ProjectCandidate>(), null,
            Array.Empty<string>(), null));
        Assert.Equal("No project", status.ProjectText);
    }

    [Fact]
    public void Unsupported_DisplaysUnsupportedProject()
    {
        var (vm, status, svc) = CreateImmediate();
        svc.Emit(new ProjectContext(
            ProjectContextState.Unsupported, "/root",
            Array.Empty<ProjectCandidate>(), null,
            new[] { "/root/unknown.vbproj" }, null));
        Assert.Equal("Unsupported project", status.ProjectText);
    }

    [Fact]
    public void Failed_DisplaysProjectError()
    {
        var (vm, status, svc) = CreateImmediate();
        svc.Emit(new ProjectContext(
            ProjectContextState.Failed, "/root",
            Array.Empty<ProjectCandidate>(), null,
            Array.Empty<string>(), "disk error"));
        Assert.Equal("Project error", status.ProjectText);
    }

    [Fact]
    public void Unloaded_DisplaysZaide()
    {
        var (vm, status, svc) = CreateImmediate();
        svc.Emit(new ProjectContext(
            ProjectContextState.Unloaded, null,
            Array.Empty<ProjectCandidate>(), null,
            Array.Empty<string>(), null));
        Assert.Equal("Zaide", status.ProjectText);
    }

    [Fact]
    public void SingleProjectWithNullSelectedProject_DisplaysProjectError()
    {
        var (vm, status, svc) = CreateImmediate();
        svc.Emit(new ProjectContext(
            ProjectContextState.SingleProject, "/root",
            new[] { new ProjectCandidate("/root/p.csproj", "p", ProjectKind.CSharpProject) },
            SelectedProject: null,
            Array.Empty<string>(), null));
        Assert.Equal("Project error", status.ProjectText);
    }

    [Fact]
    public void SelectedWithNullSelectedProject_DisplaysProjectError()
    {
        var (vm, status, svc) = CreateImmediate();
        svc.Emit(new ProjectContext(
            ProjectContextState.Selected, "/root",
            new[] { new ProjectCandidate("/root/p.csproj", "p", ProjectKind.CSharpProject) },
            SelectedProject: null,
            Array.Empty<string>(), null));
        Assert.Equal("Project error", status.ProjectText);
    }

    // ── Regression: caret, language, branch, settings, model ─────────────

    [Fact]
    public void CaretText_Default_Ln1Col1()
    {
        var (vm, status, svc) = CreateImmediate();
        Assert.Equal("Ln 1, Col 1", status.CaretText);
    }

    [Fact]
    public void LanguageIntelligenceText_ProjectsReadySession()
    {
        var (vm, status, svc) = CreateImmediate();
        var languageSession = new ControlledLanguageSessionService();
        var settings = new Mock<ISettingsService>(MockBehavior.Strict);
        settings.Setup(s => s.Current).Returns(SettingsModel.Defaults);
        settings.Setup(s => s.WhenChanged).Returns(new Subject<SettingsModel>());
        using var statusWithSession = new StatusBarViewModel(
            vm, settings.Object, languageSession, ImmediateScheduler.Instance);

        languageSession.Emit(new LanguageSessionSnapshot(
            LanguageSessionState.Ready, 1, "/root/App.csproj", "/root", 42, null));

        Assert.Equal("C# · Ready", statusWithSession.LanguageIntelligenceText);
    }

    [Fact]
    public void LanguageText_Default_EmDash()
    {
        var (vm, status, svc) = CreateImmediate();
        Assert.Equal("—", status.LanguageText);
    }

    [Fact]
    public void BranchText_Default_NoRepo()
    {
        var (vm, status, svc) = CreateImmediate();
        // SourceControlViewModel defaults to "no repo" when no folder is open.
        Assert.Equal("no repo", status.BranchText);
    }

    [Fact]
    public void ConfiguredModel_Default_ModelFromSettings()
    {
        var (vm, status, svc) = CreateImmediate();
        // LlmSettings.Default.Model is "gpt-4o-mini", so ConfiguredModel
        // is not null by default.
        Assert.Equal("gpt-4o-mini", status.ConfiguredModel);
    }

    [Fact]
    public void OpenSettingsCommand_Registered()
    {
        var (vm, status, svc) = CreateImmediate();
        Assert.NotNull(status.OpenSettingsCommand);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Phase 9 M6: Caret + Selection display
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void CaretText_NoSelection_ShowsLineColumn()
    {
        var (vm, status, svc) = CreateImmediate();
        var editor = new EditorViewModel(new Document(""), new FileService());
        vm.EditorTabs.OpenTabs.Add(editor);
        vm.EditorTabs.ActiveTab = editor;

        Assert.Equal(0, editor.SelectionLength);
        Assert.Equal("Ln 1, Col 1", status.CaretText);
    }

    [Fact]
    public void CaretText_WithSelection_ShowsSuffix()
    {
        var (vm, status, svc) = CreateImmediate();
        var editor = new EditorViewModel(new Document(""), new FileService());
        vm.EditorTabs.OpenTabs.Add(editor);
        vm.EditorTabs.ActiveTab = editor;

        editor.CaretLine = 3;
        editor.CaretColumn = 15;
        editor.SelectionLength = 10;

        Assert.Equal("Ln 3, Col 15 | Sel 10", status.CaretText);
    }

    [Fact]
    public void CaretText_SelectionZero_NoSuffix()
    {
        var (vm, status, svc) = CreateImmediate();
        var editor = new EditorViewModel(new Document(""), new FileService());
        vm.EditorTabs.OpenTabs.Add(editor);
        vm.EditorTabs.ActiveTab = editor;

        editor.CaretLine = 5;
        editor.CaretColumn = 8;
        editor.SelectionLength = 0;

        Assert.Equal("Ln 5, Col 8", status.CaretText);
        Assert.DoesNotContain("Sel", status.CaretText, StringComparison.Ordinal);
    }

    [Fact]
    public void CaretText_SelectionUpdatesDynamically()
    {
        var (vm, status, svc) = CreateImmediate();
        var editor = new EditorViewModel(new Document(""), new FileService());
        vm.EditorTabs.OpenTabs.Add(editor);
        vm.EditorTabs.ActiveTab = editor;

        editor.CaretLine = 10;
        editor.CaretColumn = 25;
        editor.SelectionLength = 42;

        Assert.Equal("Ln 10, Col 25 | Sel 42", status.CaretText);

        // Clear selection — suffix must disappear.
        editor.SelectionLength = 0;
        Assert.Equal("Ln 10, Col 25", status.CaretText);
    }

    [Fact]
    public void CaretText_SelectionResetsOnTabSwitch()
    {
        var (vm, status, svc) = CreateImmediate();
        var editor1 = new EditorViewModel(new Document(""), new FileService());
        var editor2 = new EditorViewModel(new Document(""), new FileService());
        vm.EditorTabs.OpenTabs.Add(editor1);
        vm.EditorTabs.OpenTabs.Add(editor2);
        vm.EditorTabs.ActiveTab = editor1;

        editor1.CaretLine = 7;
        editor1.CaretColumn = 12;
        editor1.SelectionLength = 5;
        Assert.Equal("Ln 7, Col 12 | Sel 5", status.CaretText);

        // Switch to tab 2 — resets to tab 2's defaults.
        vm.EditorTabs.ActiveTab = editor2;
        Assert.Equal("Ln 1, Col 1", status.CaretText);
    }

    [Fact]
    public void CaretText_NoActiveTab_ShowsDefault()
    {
        var (vm, status, svc) = CreateImmediate();
        Assert.Equal("Ln 1, Col 1", status.CaretText);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Phase 9 M6: DocumentText display
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void DocumentText_NoTab_ShowsDash()
    {
        var (vm, status, svc) = CreateImmediate();
        Assert.Equal("—", status.DocumentText);
    }

    [Fact]
    public void DocumentText_WithFileTab_ShowsFileName()
    {
        var (vm, status, svc) = CreateImmediate();
        var editor = new EditorViewModel(new Document("/project/Program.cs", ""), new FileService());
        vm.EditorTabs.OpenTabs.Add(editor);
        vm.EditorTabs.ActiveTab = editor;

        Assert.Equal("Program.cs", status.DocumentText);
    }

    [Fact]
    public void DocumentText_UntitledTab_ShowsUntitled()
    {
        var (vm, status, svc) = CreateImmediate();
        var editor = new EditorViewModel(new Document("", ""), new FileService());
        vm.EditorTabs.OpenTabs.Add(editor);
        vm.EditorTabs.ActiveTab = editor;

        Assert.Equal("Untitled", status.DocumentText);
    }

    [Fact]
    public void DocumentText_ClearsOnTabClose()
    {
        var (vm, status, svc) = CreateImmediate();
        var editor = new EditorViewModel(new Document("/project/file.cs", ""), new FileService());
        vm.EditorTabs.OpenTabs.Add(editor);
        vm.EditorTabs.ActiveTab = editor;
        Assert.Equal("file.cs", status.DocumentText);

        // Close the tab (remove from collection and clear active).
        vm.EditorTabs.OpenTabs.Remove(editor);
        vm.EditorTabs.ActiveTab = null;
        Assert.Equal("—", status.DocumentText);
    }

    [Fact]
    public void DocumentText_UpdatesOnTabSwitch()
    {
        var (vm, status, svc) = CreateImmediate();
        var editor1 = new EditorViewModel(new Document("/a/first.cs", ""), new FileService());
        var editor2 = new EditorViewModel(new Document("/b/second.cs", ""), new FileService());
        vm.EditorTabs.OpenTabs.Add(editor1);
        vm.EditorTabs.OpenTabs.Add(editor2);
        vm.EditorTabs.ActiveTab = editor1;

        Assert.Equal("first.cs", status.DocumentText);

        vm.EditorTabs.ActiveTab = editor2;
        Assert.Equal("second.cs", status.DocumentText);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Phase 9 M6: StatusMessage (transient outcomes)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void StatusMessage_Default_Null()
    {
        var (vm, status, svc) = CreateImmediate();
        Assert.Null(status.StatusMessage);
    }

    [Fact]
    public void StatusMessage_FromStatusText()
    {
        var (vm, status, svc) = CreateImmediate();
        vm.StatusText = "Saved: file.cs";
        Assert.Equal("Saved: file.cs", status.StatusMessage);
    }

    [Fact]
    public void StatusMessage_ClearsOnTabSwitch()
    {
        var (vm, status, svc) = CreateImmediate();
        var editor1 = new EditorViewModel(new Document("/a/file1.cs", ""), new FileService());
        var editor2 = new EditorViewModel(new Document("/b/file2.cs", ""), new FileService());
        vm.EditorTabs.OpenTabs.Add(editor1);
        vm.EditorTabs.OpenTabs.Add(editor2);
        vm.EditorTabs.ActiveTab = editor1;

        // Set a status message.
        vm.StatusText = "Search: 3 matches";
        Assert.Equal("Search: 3 matches", status.StatusMessage);

        // Switch tabs — MustBe cleared.
        vm.EditorTabs.ActiveTab = editor2;
        Assert.Null(status.StatusMessage);
    }

    [Fact]
    public void StatusMessage_SaveFailureShowsMessage()
    {
        var (vm, status, svc) = CreateImmediate();
        vm.StatusText = "Save failed: disk full";
        Assert.Equal("Save failed: disk full", status.StatusMessage);
    }

    [Fact]
    public void StatusMessage_SearchOutcomeShowsMessage()
    {
        var (vm, status, svc) = CreateImmediate();
        vm.StatusText = "3 matches";
        Assert.Equal("3 matches", status.StatusMessage);
    }

    [Fact]
    public void StatusMessage_FoldOutcomeShowsMessage()
    {
        var (vm, status, svc) = CreateImmediate();
        vm.StatusText = "Folded all regions";
        Assert.Equal("Folded all regions", status.StatusMessage);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Phase 9 M6: Stale-state prevention — events from old tabs
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void StatusText_ClearsOnActiveTabChange()
    {
        var (vm, status, svc) = CreateImmediate();
        var editor1 = new EditorViewModel(new Document("/a/f1.cs", ""), new FileService());
        var editor2 = new EditorViewModel(new Document("/b/f2.cs", ""), new FileService());
        vm.EditorTabs.OpenTabs.Add(editor1);
        vm.EditorTabs.OpenTabs.Add(editor2);
        vm.EditorTabs.ActiveTab = editor1;

        vm.StatusText = "status before switch";

        // Active-tab change triggers StatusText = null in Activate().
        vm.EditorTabs.ActiveTab = editor2;
        Assert.Null(vm.StatusText);
        Assert.Null(status.StatusMessage);
    }

    [Fact]
    public void LanguageText_UpdatesOnTabSwitch()
    {
        var (vm, status, svc) = CreateImmediate();
        var editor1 = new EditorViewModel(new Document("/a/file.cs", ""), new FileService());
        var editor2 = new EditorViewModel(new Document("/b/file.py", ""), new FileService());
        vm.EditorTabs.OpenTabs.Add(editor1);
        vm.EditorTabs.OpenTabs.Add(editor2);
        vm.EditorTabs.ActiveTab = editor1;

        Assert.Equal("C#", status.LanguageText);

        vm.EditorTabs.ActiveTab = editor2;
        Assert.Equal("Python", status.LanguageText);
    }

    [Fact]
    public void LanguageText_NoTab_ShowsDash()
    {
        var (vm, status, svc) = CreateImmediate();
        Assert.Equal("—", status.LanguageText);
    }
}
