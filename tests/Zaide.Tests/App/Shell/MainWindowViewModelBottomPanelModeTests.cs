using System.Reactive.Concurrency;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReactiveUI.Builder;
using Xunit;
using Zaide.App.Composition;
using Zaide.App.Shell;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Workspace.Infrastructure;
using Zaide.Features.Workspace.Presentation;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Tests.Features.ProjectSystem;
using Zaide.Tests.Features.Debugging.Application;
using Zaide.Tests.Features.Debugging.Presentation;
using Zaide.Features.SourceControl.Domain;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Presentation;
using Zaide.Features.Terminal.Contracts;
using Zaide.Features.Terminal.Infrastructure;
using Zaide.Features.Terminal.Presentation;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Presentation;

namespace Zaide.Tests.App.Shell;
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
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton<Workspace>();
        var sp = services.BuildServiceProvider();
        var workspace = sp.GetRequiredService<Workspace>();
        var editorTabs = new EditorTabViewModel(sp.GetRequiredService<IEditorSessionFactory>(), sp.GetRequiredService<IFileService>(), workspace);
        var terminalService = new Mock<ITerminalService>();
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(terminalService.Object);
        var terminalHost = new TerminalHost(factory.Object);
        var coordinator = new Mock<IAgentExecutionCoordinator>().Object;
        var panelHost = new AgentPanelHost();
        var parser = new MentionParser();
        var router = new AgentRouter(parser, panelHost, coordinator);
        var projectContext = new Mock<IProjectContextService>(MockBehavior.Loose);
        projectContext.Setup(s => s.WhenChanged).Returns(System.Reactive.Linq.Observable.Never<ProjectContext>());

        return new MainWindowViewModel(
            new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance),
            editorTabs,
            terminalHost,
            panelHost,
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
