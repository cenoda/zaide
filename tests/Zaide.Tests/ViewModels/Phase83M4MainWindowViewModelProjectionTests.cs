using System;
using System.Reactive.Subjects;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;

using Zaide.Tests;

namespace Zaide.Tests.ViewModels;

/// <summary>
/// Phase 8.3 M4 focused tests for <see cref="MainWindowViewModel.CurrentProjectContext"/>
/// initialization, lifecycle, and UI-thread marshalling.
///
/// These tests cover only the M4 projection behaviour; they do not exercise
/// Workspace event routing, discovery, selection, or service lifecycle.
/// Existing <see cref="MainWindowViewModelTests"/> continue covering the
/// pre-M4 constructor and activation contract.
/// </summary>
public sealed class Phase83M4MainWindowViewModelProjectionTests
{
    static Phase83M4MainWindowViewModelProjectionTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    /// <summary>
    /// Creates a <see cref="MainWindowViewModel"/> with a mock
    /// <see cref="IProjectContextService"/> whose <c>Current</c> returns a
    /// known snapshot and whose <c>WhenChanged</c> is backed by a real
    /// <see cref="Subject{ProjectContext}"/> for deterministic emission.
    /// </summary>
    private static (MainWindowViewModel Vm, Subject<ProjectContext> Emitter) CreateWithControlledService()
    {
        var initial = new ProjectContext(
            ProjectContextState.Unloaded,
            WorkspaceRoot: null,
            Candidates: Array.Empty<ProjectCandidate>(),
            SelectedProject: null,
            UnsupportedFiles: Array.Empty<string>(),
            ErrorMessage: null);

        var emitter = new Subject<ProjectContext>();
        var mock = new Mock<IProjectContextService>(MockBehavior.Strict);
        mock.Setup(s => s.Current).Returns(initial);
        mock.Setup(s => s.WhenChanged).Returns(emitter);
        mock.Setup(s => s.Dispose());

        var vm = CreateViewModel(mock.Object);
        return (vm, emitter);
    }

    /// <summary>
    /// Builds a bare-minimum <see cref="MainWindowViewModel"/> with the given
    /// <see cref="IProjectContextService"/>, activates it, and returns it.
    /// All other dependencies are minimal stubs.
    /// </summary>
    private static MainWindowViewModel CreateViewModel(IProjectContextService projectContextService)
    {
        var fileTree = new FileTreeViewModel(new FileTreeService(), System.Reactive.Concurrency.CurrentThreadScheduler.Instance);
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
        var mutation = new Mock<IGitMutationService>();
        var sourceControl = new SourceControlViewModel(
            new SourceControlSnapshotOrchestrator(git.Object),
            new Workspace(), mutation.Object, git.Object);

        var workspace = sp.GetRequiredService<Workspace>();
        var vm = new MainWindowViewModel(
            fileTree, editorTabs, terminalHost, panelHost, coordinator, router,
            townhall, sourceControl, TestProblemsFactory.Create(workspace, editorTabs), TestProjectWorkflowFactory.Create(), TestTestResultsFactory.Create(), TestDebugSessionFactory.Create(), TestDebugPanelFactory.Create(), TestEditorBreakpointFactory.Create(editorTabs), workspace,
            projectContextService);
        // Use ImmediateScheduler so scheduled work executes synchronously
        // in unit test environments where AvaloniaScheduler is unavailable.
        vm.ProjectContextScheduler = System.Reactive.Concurrency.ImmediateScheduler.Instance;
        vm.Activate();
        return vm;
    }

    // ── Tests ────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesCurrentProjectContext_FromServiceCurrent()
    {
        var (vm, _) = CreateWithControlledService();
        Assert.NotNull(vm.CurrentProjectContext);
        Assert.Equal(ProjectContextState.Unloaded, vm.CurrentProjectContext.State);
    }

    [Fact]
    public void WhenChanged_Emission_UpdatesCurrentProjectContext()
    {
        var (vm, emitter) = CreateWithControlledService();

        var singleCtx = new ProjectContext(
            ProjectContextState.SingleProject,
            "/root",
            new[] { new ProjectCandidate("/root/test.csproj", "test", ProjectKind.CSharpProject) },
            new ProjectCandidate("/root/test.csproj", "test", ProjectKind.CSharpProject),
            Array.Empty<string>(),
            ErrorMessage: null);

        emitter.OnNext(singleCtx);

        Assert.Equal(ProjectContextState.SingleProject, vm.CurrentProjectContext.State);
        Assert.Equal("test", vm.CurrentProjectContext.SelectedProject?.DisplayName);
    }

    [Fact]
    public void MultipleEmissions_UpdateCurrentProjectContext()
    {
        var (vm, emitter) = CreateWithControlledService();

        var loading = new ProjectContext(
            ProjectContextState.Loading, "/root",
            Array.Empty<ProjectCandidate>(), null, Array.Empty<string>(), null);
        emitter.OnNext(loading);
        Assert.Equal(ProjectContextState.Loading, vm.CurrentProjectContext.State);

        var noProject = new ProjectContext(
            ProjectContextState.NoProject, "/root",
            Array.Empty<ProjectCandidate>(), null, Array.Empty<string>(), null);
        emitter.OnNext(noProject);
        Assert.Equal(ProjectContextState.NoProject, vm.CurrentProjectContext.State);

        var failed = new ProjectContext(
            ProjectContextState.Failed, "/root",
            Array.Empty<ProjectCandidate>(), null, Array.Empty<string>(), "error");
        emitter.OnNext(failed);
        Assert.Equal(ProjectContextState.Failed, vm.CurrentProjectContext.State);
    }

    [Fact]
    public void WorkspaceProjectName_StillAvailable_ForLegacyConsumers()
    {
        var (vm, _) = CreateWithControlledService();

        // WorkspaceProjectName is driven by workspace.ProjectName, not by
        // CurrentProjectContext. It should still be accessible and not throw.
        Assert.NotNull(vm.WorkspaceProjectName);
    }

    [Fact]
    public void ProjectContextService_NotAvaloniaDependent()
    {
        // Verify no Avalonia dependency is forced into the service interface.
        var serviceType = typeof(IProjectContextService);
        var avaloniaAssembly = typeof(Avalonia.AvaloniaObject).Assembly;

        foreach (var method in serviceType.GetMethods())
        {
            foreach (var param in method.GetParameters())
            {
                var paramAssembly = param.ParameterType.Assembly;
                Assert.NotEqual(avaloniaAssembly, paramAssembly);
            }
        }

        foreach (var prop in serviceType.GetProperties())
        {
            var propAssembly = prop.PropertyType.Assembly;
            Assert.NotEqual(avaloniaAssembly, propAssembly);
        }
    }
}
