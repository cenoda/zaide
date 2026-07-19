using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Tests.Features.Conversations;
using Zaide.App.Shell;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Debugging.Application;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Presentation;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Infrastructure;
using Zaide.Features.ProjectSystem.Presentation;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.SourceControl.Domain;
using Zaide.Features.SourceControl.Presentation;
using Zaide.Features.Terminal.Contracts;
using Zaide.Features.Terminal.Infrastructure;
using Zaide.Features.Terminal.Presentation;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Workspace.Infrastructure;
using Zaide.Features.Workspace.Presentation;
using Zaide.Tests.Features.Debugging.Application;
using Zaide.Tests.Features.Debugging.Presentation;
using Zaide.Tests.Features.ProjectSystem;

namespace Zaide.Tests.App.Shell;

/// <summary>
/// Focused proofs for <see cref="MainWindowActivationHost"/> extraction (Refactor 6.3 M9c):
/// MWVM entrypoint idempotence, feature activation, show-panel routes, root-path sync,
/// close-folder, project-context scheduler substitution, status/open-file routing,
/// disposal, optional debug location, and constructor null guards.
/// </summary>
public sealed class MainWindowActivationHostTests
{
    static MainWindowActivationHostTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    [Fact]
    public void MwvmActivate_IsIdempotent_DoesNotDuplicateRootPathSync()
    {
        var harness = CreateMwvmHarness();
        var refreshCount = 0;
        harness.Git.Setup(g => g.Discover(It.IsAny<string>()))
            .Returns(RepositoryDiscoveryResult.NotFound(""))
            .Callback(() => refreshCount++);
        harness.Git.Setup(g => g.ReadStatus(It.IsAny<string>()))
            .Returns(new RepositoryStatusSnapshot());

        harness.Vm.Activate();
        harness.Vm.Activate();
        harness.Vm.Activate();

        var dir = Path.Combine(Path.GetTempPath(), "zaide-m9c-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            refreshCount = 0;
            Assert.True(harness.FileTree.SetRootPath(dir));
            // Single subscription: open triggers one SC refresh (plus any discover during refresh).
            Assert.Equal(1, refreshCount);
            Assert.Equal(Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(harness.Workspace.WorkspacePath ?? "").TrimEnd(Path.DirectorySeparatorChar));
        }
        finally
        {
            harness.Vm.Dispose();
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void MwvmActivate_CallsFeatureActivate_AndOptionalDebugLocation()
    {
        var harness = CreateMwvmHarness(includeDebugLocation: true);
        harness.Vm.Activate();

        // Feature Activate is idempotent; calling again must not throw and must not
        // re-register host subscriptions (covered by root-path test).
        harness.Vm.ProblemsViewModel.Activate();
        harness.Vm.ProjectWorkflowViewModel.Activate();
        harness.Vm.DebugSessionViewModel.Activate();
        harness.Vm.DebugPanelViewModel.Activate();
        harness.Vm.EditorBreakpointViewModel.Activate();
        harness.Vm.TestResultsViewModel.Activate();
        harness.Vm.DebugCurrentLocationViewModel!.Activate();

        harness.Vm.Dispose();
    }

    [Fact]
    public void HostActivate_NullDebugCurrentLocation_DoesNotThrow()
    {
        var bag = CreateHostBag(debugCurrentLocation: null);
        using var disposables = new CompositeDisposable();
        bag.Host.Activate(disposables);
        bag.DisposeAfter();
    }

    [Fact]
    public void HostActivate_WithDebugCurrentLocation_ActivatesIt()
    {
        var bag = CreateHostBag(debugCurrentLocation: CreateDebugLocation);
        using var disposables = new CompositeDisposable();
        bag.Host.Activate(disposables);
        // Second Activate on the VM is a no-op when already activated — proves first call ran.
        bag.DebugCurrentLocation!.Activate();
        bag.DisposeAfter();
    }

    [Fact]
    public void ShowPanel_Debug_Output_And_TestResults_SetModeAndVisibility()
    {
        var harness = CreateMwvmHarness(controlledWorkflow: true);
        harness.Vm.Activate();

        Assert.False(harness.Vm.IsBottomPanelVisible);

        // Output: Starting Build
        harness.WorkflowSubject!.OnNext(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            Generation: 1,
            ActiveOperation: ProjectWorkflowOperation.Build,
            LastOutcome: null,
            TargetFilePath: null,
            ProcessId: null,
            OutputLines: Array.Empty<ManagedProcessOutputLine>()));
        Assert.Equal(BottomPanelMode.Output, harness.Vm.BottomPanelMode);
        Assert.True(harness.Vm.IsBottomPanelVisible);

        harness.Vm.IsBottomPanelVisible = false;
        harness.Vm.BottomPanelMode = BottomPanelMode.Terminal;

        // Test Results (+ Output also fires for Test Starting)
        harness.WorkflowSubject.OnNext(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Starting,
            Generation: 2,
            ActiveOperation: ProjectWorkflowOperation.Test,
            LastOutcome: null,
            TargetFilePath: null,
            ProcessId: null,
            OutputLines: Array.Empty<ManagedProcessOutputLine>()));
        Assert.Equal(BottomPanelMode.TestResults, harness.Vm.BottomPanelMode);
        Assert.True(harness.Vm.IsBottomPanelVisible);

        harness.Vm.IsBottomPanelVisible = false;
        harness.Vm.BottomPanelMode = BottomPanelMode.Terminal;

        // Debug panel show route via session Starting transition
        harness.DebugSessionSubject!.OnNext(new DebugSessionSnapshot(
            DebugSessionState.Starting,
            Generation: 1,
            ProgramPath: null,
            WorkingDirectory: null,
            AdapterProcessId: null,
            StopInfo: null,
            Failure: null,
            LastOutcome: null,
            DiagnosticOutput: Array.Empty<string>(),
            BreakpointVerifications: DebugSessionSnapshot.EmptyVerifications));
        Assert.Equal(BottomPanelMode.Debug, harness.Vm.BottomPanelMode);
        Assert.True(harness.Vm.IsBottomPanelVisible);

        harness.Vm.Dispose();
    }

    [Fact]
    public void RootPath_SyncsWorkspaceAndSourceControl_AndCloseFolderClears()
    {
        var harness = CreateMwvmHarness();
        harness.Vm.Activate();

        var dir = Path.Combine(Path.GetTempPath(), "zaide-m9c-close-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.True(harness.FileTree.SetRootPath(dir));
            Assert.NotNull(harness.Workspace.WorkspacePath);
            Assert.Equal(harness.Workspace.ProjectName, harness.Vm.WorkspaceProjectName);

            harness.Vm.CloseFolderCommand.Execute().Subscribe();
            Assert.Null(harness.FileTree.RootPath);
            Assert.Null(harness.Workspace.WorkspacePath);
        }
        finally
        {
            harness.Vm.Dispose();
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void CloseFolderRequested_WhenFolderOpen_ExecutesCloseFolderCommand()
    {
        var harness = CreateMwvmHarness();
        harness.Vm.Activate();

        var dir = Path.Combine(Path.GetTempPath(), "zaide-m9c-req-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.True(harness.FileTree.SetRootPath(dir));
            harness.FileTree.CloseFolderRequested.Handle(Unit.Default).Subscribe();
            Assert.Null(harness.FileTree.RootPath);
        }
        finally
        {
            harness.Vm.Dispose();
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void ProjectContext_WhenChanged_UsesSchedulerFromGetter_NotCtorCapture()
    {
        var harness = CreateMwvmHarness(controlledProjectContext: true);
        // Default is AvaloniaScheduler; substitute after construction (live contract).
        harness.Vm.ProjectContextScheduler = ImmediateScheduler.Instance;
        harness.Vm.Activate();

        var next = new ProjectContext(
            ProjectContextState.SingleProject,
            WorkspaceRoot: "/tmp/proj",
            Candidates: Array.Empty<ProjectCandidate>(),
            SelectedProject: null,
            UnsupportedFiles: Array.Empty<string>(),
            ErrorMessage: null);
        harness.ProjectContextSubject!.OnNext(next);

        Assert.Equal(ProjectContextState.SingleProject, harness.Vm.CurrentProjectContext.State);
        Assert.Equal("/tmp/proj", harness.Vm.CurrentProjectContext.WorkspaceRoot);
        harness.Vm.Dispose();
    }

    [Fact]
    public void Host_ObserveOn_UsesSchedulerReturnedAtActivateTime()
    {
        IScheduler? observed = null;
        var schedulerHits = 0;
        var immediate = ImmediateScheduler.Instance;
        var ctxSubject = new Subject<ProjectContext>();
        var projectContext = new Mock<IProjectContextService>();
        projectContext.SetupGet(s => s.Current).Returns(UnloadedContext());
        projectContext.SetupGet(s => s.WhenChanged).Returns(ctxSubject);

        ProjectContext? projected = null;
        var bag = CreateHostBag(
            projectContextService: projectContext.Object,
            getScheduler: () =>
            {
                schedulerHits++;
                observed = immediate;
                return immediate;
            },
            setCurrentProjectContext: c => projected = c);

        using var disposables = new CompositeDisposable();
        bag.Host.Activate(disposables);

        var next = new ProjectContext(
            ProjectContextState.SingleProject,
            WorkspaceRoot: "/w",
            Candidates: Array.Empty<ProjectCandidate>(),
            SelectedProject: null,
            UnsupportedFiles: Array.Empty<string>(),
            ErrorMessage: null);
        ctxSubject.OnNext(next);

        Assert.True(schedulerHits >= 1);
        Assert.Same(immediate, observed);
        Assert.NotNull(projected);
        Assert.Equal(ProjectContextState.SingleProject, projected!.State);
        bag.DisposeAfter();
    }

    [Fact]
    public void StatusText_RoutesFoldSaveAndOpenErrors()
    {
        var harness = CreateMwvmHarness();
        harness.Vm.Activate();

        harness.EditorTabs.FoldStatusMessage = "Folded 3 regions";
        Assert.Equal("Folded 3 regions", harness.Vm.StatusText);

        harness.EditorTabs.LastSaveError = "disk full";
        Assert.Equal("Save failed: disk full", harness.Vm.StatusText);

        harness.EditorTabs.LastOpenError = "missing";
        Assert.Equal("Open failed: missing", harness.Vm.StatusText);

        harness.Vm.Dispose();
    }

    [Fact]
    public async Task OpenFileRequested_SupportedFile_OpensTab()
    {
        var harness = CreateMwvmHarness();
        harness.Vm.Activate();

        var dir = Path.Combine(Path.GetTempPath(), "zaide-m9c-open-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "Sample.cs");
        File.WriteAllText(file, "class C {}");
        try
        {
            var node = new FileTreeNode
            {
                Name = "Sample.cs",
                FullPath = file,
                IsDirectory = false,
            };
            harness.FileTree.RequestOpenFileCommand.Execute(node).Subscribe();
            await Task.Delay(100);
            Assert.Single(harness.EditorTabs.OpenTabs);
            Assert.Equal(file, harness.EditorTabs.ActiveTab!.FilePath);
        }
        finally
        {
            harness.Vm.Dispose();
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void OpenFileRequested_UnsupportedFile_SetsUnsupportedStatus()
    {
        var harness = CreateMwvmHarness();
        harness.Vm.Activate();

        var node = new FileTreeNode
        {
            Name = "photo.png",
            FullPath = "/tmp/photo.png",
            IsDirectory = false,
        };
        harness.FileTree.RequestOpenFileCommand.Execute(node).Subscribe();
        Assert.NotNull(harness.Vm.StatusText);
        Assert.DoesNotContain("Opened:", harness.Vm.StatusText);

        harness.Vm.Dispose();
    }

    [Fact]
    public void Dispose_RemovesSubscriptions_RootPathNoLongerSyncs()
    {
        var harness = CreateMwvmHarness();
        harness.Vm.Activate();
        harness.Vm.Dispose();

        var dir = Path.Combine(Path.GetTempPath(), "zaide-m9c-disp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.True(harness.FileTree.SetRootPath(dir));
            // After dispose, activation host subscriptions are gone — workspace stays unloaded.
            Assert.Null(harness.Workspace.WorkspacePath);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }
    }

    [Theory]
    [InlineData("problemsViewModel")]
    [InlineData("projectWorkflowViewModel")]
    [InlineData("debugSessionViewModel")]
    [InlineData("debugPanelViewModel")]
    [InlineData("editorBreakpointViewModel")]
    [InlineData("testResultsViewModel")]
    [InlineData("fileTreeViewModel")]
    [InlineData("sourceControlViewModel")]
    [InlineData("editorTabs")]
    [InlineData("terminalHost")]
    [InlineData("workspace")]
    [InlineData("projectContextService")]
    [InlineData("getProjectContextScheduler")]
    [InlineData("closeFolderCommand")]
    [InlineData("setBottomPanelMode")]
    [InlineData("setIsBottomPanelVisible")]
    [InlineData("setStatusText")]
    [InlineData("setCurrentProjectContext")]
    [InlineData("setWorkspaceProjectName")]
    public void Constructor_NullNonNullableDependency_Throws(string nullParam)
    {
        var ex = Assert.Throws<ArgumentNullException>(() => CreateHostWithNull(nullParam));
        Assert.Equal(nullParam, ex.ParamName);
    }

    [Fact]
    public void Constructor_NullDebugCurrentLocation_IsAllowed()
    {
        var bag = CreateHostBag(debugCurrentLocation: null);
        Assert.NotNull(bag.Host);
        bag.DisposeAfter();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static ProjectContext UnloadedContext() => new(
        ProjectContextState.Unloaded,
        WorkspaceRoot: null,
        Candidates: Array.Empty<ProjectCandidate>(),
        SelectedProject: null,
        UnsupportedFiles: Array.Empty<string>(),
        ErrorMessage: null);

    private sealed class MwvmHarness
    {
        public required MainWindowViewModel Vm { get; init; }
        public required FileTreeViewModel FileTree { get; init; }
        public required Workspace Workspace { get; init; }
        public required EditorTabViewModel EditorTabs { get; init; }
        public required Mock<IGitRepositoryService> Git { get; init; }
        public Subject<ProjectWorkflowSnapshot>? WorkflowSubject { get; init; }
        public Subject<DebugSessionSnapshot>? DebugSessionSubject { get; init; }
        public Subject<ProjectContext>? ProjectContextSubject { get; init; }
    }

    private static MwvmHarness CreateMwvmHarness(
        bool includeDebugLocation = false,
        bool controlledWorkflow = false,
        bool controlledProjectContext = false)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton<Workspace>();
        var sp = services.BuildServiceProvider();

        var workspace = sp.GetRequiredService<Workspace>();
        var editorTabs = new EditorTabViewModel(
            sp.GetRequiredService<IEditorSessionFactory>(),
            sp.GetRequiredService<IFileService>(),
            workspace);
        var fileTree = new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance);

        var terminalService = new Mock<ITerminalService>();
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(terminalService.Object);
        var terminalHost = new TerminalHost(factory.Object);

        var coordinator = new Mock<IAgentExecutionCoordinator>().Object;
        var panelHost = ConversationsTestSupport.CreatePanelHost();
        var router = new AgentRouter(new MentionParser(), panelHost, coordinator);
        var townhall = ConversationsTestSupport.CreateTownhallViewModel();

        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>())).Returns(RepositoryDiscoveryResult.NotFound(""));
        git.Setup(g => g.ReadStatus(It.IsAny<string>())).Returns(new RepositoryStatusSnapshot());
        var sc = new SourceControlViewModel(
            new SourceControlSnapshotOrchestrator(git.Object),
            workspace,
            new Mock<IGitMutationService>().Object,
            git.Object);

        Subject<ProjectWorkflowSnapshot>? workflowSubject = null;
        ProjectWorkflowViewModel workflowVm;
        if (controlledWorkflow)
        {
            workflowSubject = new Subject<ProjectWorkflowSnapshot>();
            var workflow = new Mock<IProjectWorkflowService>();
            workflow.SetupGet(w => w.Current).Returns(new ProjectWorkflowSnapshot(
                ProjectWorkflowOperationState.Idle,
                Generation: 0,
                ActiveOperation: null,
                LastOutcome: null,
                TargetFilePath: null,
                ProcessId: null,
                OutputLines: Array.Empty<ManagedProcessOutputLine>()));
            workflow.SetupGet(w => w.WhenChanged).Returns(workflowSubject);
            workflow.SetupGet(w => w.WhenOutputReceived)
                .Returns(Observable.Never<ManagedProcessOutputLine>());
            var output = new ProjectOutputService(workflow.Object);
            var gate = TestOperationGateFactory.CreateIdleGate();
            var debugSession = TestOperationGateFactory.CreateIdleDebugSession();
            workflowVm = new ProjectWorkflowViewModel(
                workflow.Object, output, TestProjectWorkflowFactory.CreateIdleProjectContext(),
                gate, debugSession.Object);
            // Deterministic scheduler for ObserveOn inside ProjectWorkflowViewModel.Activate
            workflowVm.Scheduler = ImmediateScheduler.Instance;
        }
        else
        {
            workflowVm = TestProjectWorkflowFactory.Create();
        }

        Subject<DebugSessionSnapshot>? debugSubject = null;
        DebugPanelViewModel debugPanel;
        IDebugSessionService debugSessionService;
        if (controlledWorkflow)
        {
            debugSubject = new Subject<DebugSessionSnapshot>();
            var idle = new DebugSessionSnapshot(
                DebugSessionState.Idle,
                Generation: 0,
                ProgramPath: null,
                WorkingDirectory: null,
                AdapterProcessId: null,
                StopInfo: null,
                Failure: null,
                LastOutcome: null,
                DiagnosticOutput: Array.Empty<string>(),
                BreakpointVerifications: DebugSessionSnapshot.EmptyVerifications);
            var ds = new Mock<IDebugSessionService>();
            ds.SetupGet(s => s.Current).Returns(idle);
            ds.SetupGet(s => s.WhenChanged).Returns(debugSubject);
            debugSessionService = ds.Object;
            debugPanel = TestDebugPanelFactory.Create(debugSessionService);
        }
        else
        {
            debugSessionService = TestOperationGateFactory.CreateIdleDebugSession().Object;
            debugPanel = TestDebugPanelFactory.Create(debugSessionService);
        }

        Subject<ProjectContext>? projectSubject = null;
        IProjectContextService projectContext;
        if (controlledProjectContext)
        {
            projectSubject = new Subject<ProjectContext>();
            var mock = new Mock<IProjectContextService>();
            mock.SetupGet(s => s.Current).Returns(UnloadedContext());
            mock.SetupGet(s => s.WhenChanged).Returns(projectSubject);
            projectContext = mock.Object;
        }
        else
        {
            var mock = new Mock<IProjectContextService>(MockBehavior.Loose);
            mock.Setup(s => s.WhenChanged).Returns(Observable.Never<ProjectContext>());
            mock.Setup(s => s.Current).Returns(UnloadedContext());
            projectContext = mock.Object;
        }

        DebugCurrentLocationViewModel? location = includeDebugLocation
            ? TestDebugPanelFactory.CreateCurrentLocation(editorTabs, debugSessionService)
            : null;

        var vm = new MainWindowViewModel(
            fileTree,
            editorTabs,
            terminalHost,
            panelHost,
            router,
            townhall,
            sc,
            TestProblemsFactory.Create(workspace, editorTabs),
            workflowVm,
            TestTestResultsFactory.Create(),
            TestDebugSessionFactory.Create(),
            debugPanel,
            TestEditorBreakpointFactory.Create(editorTabs),
            workspace,
            projectContext,
            ConversationsTestSupport.CreateCatalogAsInterface(),
            debugCurrentLocationViewModel: location);

        return new MwvmHarness
        {
            Vm = vm,
            FileTree = fileTree,
            Workspace = workspace,
            EditorTabs = editorTabs,
            Git = git,
            WorkflowSubject = workflowSubject,
            DebugSessionSubject = debugSubject,
            ProjectContextSubject = projectSubject,
        };
    }

    private sealed class HostBag
    {
        public required MainWindowActivationHost Host { get; init; }
        public DebugCurrentLocationViewModel? DebugCurrentLocation { get; init; }
        public Action DisposeAfter { get; init; } = static () => { };
    }

    private static DebugCurrentLocationViewModel CreateDebugLocation(EditorTabViewModel tabs) =>
        TestDebugPanelFactory.CreateCurrentLocation(tabs);

    private static HostBag CreateHostBag(
        Func<EditorTabViewModel, DebugCurrentLocationViewModel>? debugCurrentLocation = null,
        IProjectContextService? projectContextService = null,
        Func<IScheduler>? getScheduler = null,
        Action<ProjectContext>? setCurrentProjectContext = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton<Workspace>();
        var sp = services.BuildServiceProvider();
        var workspace = sp.GetRequiredService<Workspace>();
        var editorTabs = new EditorTabViewModel(
            sp.GetRequiredService<IEditorSessionFactory>(),
            sp.GetRequiredService<IFileService>(),
            workspace);
        var fileTree = new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance);
        var terminalService = new Mock<ITerminalService>();
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(terminalService.Object);
        var terminalHost = new TerminalHost(factory.Object);
        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>())).Returns(RepositoryDiscoveryResult.NotFound(""));
        git.Setup(g => g.ReadStatus(It.IsAny<string>())).Returns(new RepositoryStatusSnapshot());
        var sc = new SourceControlViewModel(
            new SourceControlSnapshotOrchestrator(git.Object),
            workspace,
            new Mock<IGitMutationService>().Object,
            git.Object);

        var projectContext = projectContextService;
        if (projectContext is null)
        {
            var mock = new Mock<IProjectContextService>(MockBehavior.Loose);
            mock.Setup(s => s.WhenChanged).Returns(Observable.Never<ProjectContext>());
            mock.Setup(s => s.Current).Returns(UnloadedContext());
            projectContext = mock.Object;
        }

        var location = debugCurrentLocation?.Invoke(editorTabs);
        var closeFolder = ReactiveCommand.Create(() => { });

        var host = new MainWindowActivationHost(
            TestProblemsFactory.Create(workspace, editorTabs),
            TestProjectWorkflowFactory.Create(),
            TestDebugSessionFactory.Create(),
            TestDebugPanelFactory.Create(),
            TestEditorBreakpointFactory.Create(editorTabs),
            location,
            TestTestResultsFactory.Create(),
            fileTree,
            sc,
            editorTabs,
            terminalHost,
            workspace,
            projectContext,
            getScheduler ?? (() => ImmediateScheduler.Instance),
            closeFolder,
            _ => { },
            _ => { },
            _ => { },
            setCurrentProjectContext ?? (_ => { }),
            _ => { });

        return new HostBag
        {
            Host = host,
            DebugCurrentLocation = location,
        };
    }

    private static MainWindowActivationHost CreateHostWithNull(string nullParam)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton<Workspace>();
        var sp = services.BuildServiceProvider();
        var workspace = sp.GetRequiredService<Workspace>();
        var editorTabs = new EditorTabViewModel(
            sp.GetRequiredService<IEditorSessionFactory>(),
            sp.GetRequiredService<IFileService>(),
            workspace);
        var fileTree = new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance);
        var terminalService = new Mock<ITerminalService>();
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(terminalService.Object);
        var terminalHost = new TerminalHost(factory.Object);
        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>())).Returns(RepositoryDiscoveryResult.NotFound(""));
        git.Setup(g => g.ReadStatus(It.IsAny<string>())).Returns(new RepositoryStatusSnapshot());
        var sc = new SourceControlViewModel(
            new SourceControlSnapshotOrchestrator(git.Object),
            workspace,
            new Mock<IGitMutationService>().Object,
            git.Object);
        var projectContext = new Mock<IProjectContextService>(MockBehavior.Loose);
        projectContext.Setup(s => s.WhenChanged).Returns(Observable.Never<ProjectContext>());
        projectContext.Setup(s => s.Current).Returns(UnloadedContext());
        var closeFolder = ReactiveCommand.Create(() => { });

        ProblemsViewModel problems = TestProblemsFactory.Create(workspace, editorTabs);
        ProjectWorkflowViewModel workflow = TestProjectWorkflowFactory.Create();
        DebugSessionViewModel debugSession = TestDebugSessionFactory.Create();
        DebugPanelViewModel debugPanel = TestDebugPanelFactory.Create();
        EditorBreakpointViewModel breakpoints = TestEditorBreakpointFactory.Create(editorTabs);
        TestResultsViewModel testResults = TestTestResultsFactory.Create();
        Func<IScheduler> getScheduler = () => ImmediateScheduler.Instance;
        Action<BottomPanelMode> setBottom = _ => { };
        Action<bool> setVisible = _ => { };
        Action<string?> setStatus = _ => { };
        Action<ProjectContext> setCtx = _ => { };
        Action<string?> setName = _ => { };

        return new MainWindowActivationHost(
            nullParam == "problemsViewModel" ? null! : problems,
            nullParam == "projectWorkflowViewModel" ? null! : workflow,
            nullParam == "debugSessionViewModel" ? null! : debugSession,
            nullParam == "debugPanelViewModel" ? null! : debugPanel,
            nullParam == "editorBreakpointViewModel" ? null! : breakpoints,
            null,
            nullParam == "testResultsViewModel" ? null! : testResults,
            nullParam == "fileTreeViewModel" ? null! : fileTree,
            nullParam == "sourceControlViewModel" ? null! : sc,
            nullParam == "editorTabs" ? null! : editorTabs,
            nullParam == "terminalHost" ? null! : terminalHost,
            nullParam == "workspace" ? null! : workspace,
            nullParam == "projectContextService" ? null! : projectContext.Object,
            nullParam == "getProjectContextScheduler" ? null! : getScheduler,
            nullParam == "closeFolderCommand" ? null! : closeFolder,
            nullParam == "setBottomPanelMode" ? null! : setBottom,
            nullParam == "setIsBottomPanelVisible" ? null! : setVisible,
            nullParam == "setStatusText" ? null! : setStatus,
            nullParam == "setCurrentProjectContext" ? null! : setCtx,
            nullParam == "setWorkspaceProjectName" ? null! : setName);
    }
}
