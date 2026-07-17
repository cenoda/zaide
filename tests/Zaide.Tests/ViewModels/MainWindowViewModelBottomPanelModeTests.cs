using System.Reactive.Concurrency;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Workspace.Infrastructure;
using Zaide.Features.Workspace.Presentation;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests.ViewModels;

/// <summary>
/// Phase 11 M5 bottom-panel mode wiring for Test Results.
/// </summary>
public sealed class MainWindowViewModelBottomPanelModeTests
{
    static MainWindowViewModelBottomPanelModeTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    [Fact]
    public void BottomPanelMode_TestResults_SetsVisibilityFlags()
    {
        var vm = CreateViewModel();
        vm.Activate();

        vm.BottomPanelMode = BottomPanelMode.TestResults;

        Assert.True(vm.IsTestResultsBottomMode);
        Assert.False(vm.IsTerminalBottomMode);
        Assert.False(vm.IsProblemsBottomMode);
        Assert.False(vm.IsOutputBottomMode);
        Assert.False(vm.IsDebugBottomMode);
    }

    [Fact]
    public void BottomPanelMode_Debug_SetsVisibilityFlags()
    {
        var vm = CreateViewModel();
        vm.Activate();

        vm.BottomPanelMode = BottomPanelMode.Debug;

        Assert.True(vm.IsDebugBottomMode);
        Assert.False(vm.IsTerminalBottomMode);
        Assert.False(vm.IsProblemsBottomMode);
        Assert.False(vm.IsOutputBottomMode);
        Assert.False(vm.IsTestResultsBottomMode);
    }

    private static MainWindowViewModel CreateViewModel()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileService, FileService>();
        services.AddTransient<EditorViewModel>();
        services.AddSingleton<Workspace>();
        var sp = services.BuildServiceProvider();
        var workspace = sp.GetRequiredService<Workspace>();
        var editorTabs = new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), workspace);
        var terminalService = new Mock<ITerminalService>();
        var terminalViewModel = new TerminalViewModel(terminalService.Object, a => a());
        var factory = new Mock<ITerminalSessionFactory>();
        factory.Setup(f => f.CreateSession()).Returns(terminalViewModel);
        var terminalHost = new TerminalHost(factory.Object);
        var coordinator = new Mock<IAgentExecutionCoordinator>().Object;
        var panelHost = new AgentPanelHost();
        var parser = new MentionParser(panelHost);
        var router = new AgentRouter(parser, panelHost, coordinator);
        var projectContext = new Mock<IProjectContextService>(MockBehavior.Loose);
        projectContext.Setup(s => s.WhenChanged).Returns(System.Reactive.Linq.Observable.Never<ProjectContext>());

        return new MainWindowViewModel(
            new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance),
            editorTabs,
            terminalHost,
            panelHost,
            coordinator,
            router,
            new TownhallViewModel(new TownhallState()),
            new SourceControlViewModel(
                new SourceControlSnapshotOrchestrator(new Mock<IGitRepositoryService>().Object),
                workspace,
                new Mock<IGitMutationService>().Object,
                new Mock<IGitRepositoryService>().Object),
            TestProblemsFactory.Create(workspace, editorTabs),
            TestProjectWorkflowFactory.Create(),
            TestTestResultsFactory.Create(editorTabs),
            TestDebugSessionFactory.Create(),
            TestDebugPanelFactory.Create(),
            TestEditorBreakpointFactory.Create(editorTabs),
            workspace,
            projectContext.Object);
    }
}
