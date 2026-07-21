using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
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
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.SourceControl.Presentation;
using Zaide.Features.Terminal.Contracts;
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
/// Focused proofs for <see cref="Zaide.App.Shell.ShellPanelNavigation"/>:
/// all nine command decisions, delegate-only mutation, and no authoritative mode state.
/// </summary>
public sealed class ShellPanelNavigationTests
{
    static ShellPanelNavigationTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    [Fact]
    public void SwitchToExplorer_AndSourceControl_SetLeftViaDelegate()
    {
        LeftPanelMode? left = null;
        var nav = Create(setLeft: m => left = m);

        nav.SwitchToSourceControlCommand.Execute().Subscribe();
        Assert.Equal(LeftPanelMode.SourceControl, left);

        nav.SwitchToExplorerCommand.Execute().Subscribe();
        Assert.Equal(LeftPanelMode.Explorer, left);
    }

    [Theory]
    [InlineData(nameof(ShellPanelNavigation.SwitchToTerminalBottomCommand), BottomPanelMode.Terminal)]
    [InlineData(nameof(ShellPanelNavigation.SwitchToProblemsBottomCommand), BottomPanelMode.Problems)]
    [InlineData(nameof(ShellPanelNavigation.SwitchToOutputBottomCommand), BottomPanelMode.Output)]
    [InlineData(nameof(ShellPanelNavigation.SwitchToTestResultsBottomCommand), BottomPanelMode.TestResults)]
    [InlineData(nameof(ShellPanelNavigation.SwitchToDebugBottomCommand), BottomPanelMode.Debug)]
    public void SwitchToBottom_SetsModeAndShowsPanel(string commandName, BottomPanelMode expectedMode)
    {
        BottomPanelMode? bottom = null;
        bool? visible = null;
        var nav = Create(
            setBottom: m => bottom = m,
            setBottomVisible: v => visible = v);

        var command = commandName switch
        {
            nameof(ShellPanelNavigation.SwitchToTerminalBottomCommand) => nav.SwitchToTerminalBottomCommand,
            nameof(ShellPanelNavigation.SwitchToProblemsBottomCommand) => nav.SwitchToProblemsBottomCommand,
            nameof(ShellPanelNavigation.SwitchToOutputBottomCommand) => nav.SwitchToOutputBottomCommand,
            nameof(ShellPanelNavigation.SwitchToTestResultsBottomCommand) => nav.SwitchToTestResultsBottomCommand,
            nameof(ShellPanelNavigation.SwitchToDebugBottomCommand) => nav.SwitchToDebugBottomCommand,
            _ => throw new Xunit.Sdk.XunitException($"Unknown command {commandName}"),
        };

        command.Execute().Subscribe();

        Assert.Equal(expectedMode, bottom);
        Assert.True(visible);
    }

    [Fact]
    public void ToggleBottomPanel_UsesCurrentVisibilityFromGetter()
    {
        var visible = false;
        var nav = Create(
            setBottomVisible: v => visible = v,
            getBottomVisible: () => visible);

        nav.ToggleBottomPanelCommand.Execute().Subscribe();
        Assert.True(visible);

        nav.ToggleBottomPanelCommand.Execute().Subscribe();
        Assert.False(visible);
    }

    [Fact]
    public void HideBottomPanel_HidesWithoutTouchingBottomMode()
    {
        BottomPanelMode? bottom = BottomPanelMode.Problems;
        var visible = true;
        var nav = Create(
            setBottom: m => bottom = m,
            setBottomVisible: v => visible = v,
            getBottomVisible: () => visible);

        nav.HideBottomPanelCommand.Execute().Subscribe();

        Assert.False(visible);
        Assert.Equal(BottomPanelMode.Problems, bottom);
    }

    [Fact]
    public void Commands_DoNotStoreAuthoritativeModeState_OnlyDelegates()
    {
        // Prove decisions flow exclusively through injected delegates: if delegates
        // are no-ops, no residual state on ShellPanelNavigation can re-surface values.
        var leftHits = new List<LeftPanelMode>();
        var bottomHits = new List<BottomPanelMode>();
        var visibleHits = new List<bool>();

        var nav = Create(
            setLeft: leftHits.Add,
            setBottom: bottomHits.Add,
            setBottomVisible: visibleHits.Add,
            getBottomVisible: () => false);

        nav.SwitchToExplorerCommand.Execute().Subscribe();
        nav.SwitchToSourceControlCommand.Execute().Subscribe();
        nav.SwitchToTerminalBottomCommand.Execute().Subscribe();
        nav.SwitchToProblemsBottomCommand.Execute().Subscribe();
        nav.SwitchToOutputBottomCommand.Execute().Subscribe();
        nav.SwitchToTestResultsBottomCommand.Execute().Subscribe();
        nav.SwitchToDebugBottomCommand.Execute().Subscribe();
        nav.ToggleBottomPanelCommand.Execute().Subscribe();
        nav.HideBottomPanelCommand.Execute().Subscribe();

        Assert.Equal(
            new[] { LeftPanelMode.Explorer, LeftPanelMode.SourceControl },
            leftHits);
        Assert.Equal(
            new[]
            {
                BottomPanelMode.Terminal,
                BottomPanelMode.Problems,
                BottomPanelMode.Output,
                BottomPanelMode.TestResults,
                BottomPanelMode.Debug,
            },
            bottomHits);
        Assert.Equal(new[] { true, true, true, true, true, true, false }, visibleHits);
    }

    [Fact]
    public void MwvmCommands_NotifyLeftPanelMode_ViaWhenAnyValue()
    {
        var vm = CreateMainWindowViewModel();
        LeftPanelMode? observed = null;
        using var sub = vm.WhenAnyValue(x => x.LeftPanelMode).Skip(1).Subscribe(m => observed = m);

        vm.SwitchToSourceControlCommand.Execute().Subscribe();
        Assert.Equal(LeftPanelMode.SourceControl, observed);
        Assert.Equal(LeftPanelMode.SourceControl, vm.LeftPanelMode);
        Assert.True(vm.IsSourceControlMode);
        Assert.False(vm.IsExplorerMode);

        vm.SwitchToExplorerCommand.Execute().Subscribe();
        Assert.Equal(LeftPanelMode.Explorer, observed);
        Assert.True(vm.IsExplorerMode);
    }

    [Fact]
    public void MwvmBottomCommands_NotifyModeVisibilityAndDerivedFlags()
    {
        var vm = CreateMainWindowViewModel();
        BottomPanelMode? observedMode = null;
        bool? observedVisible = null;
        using var modeSub = vm.WhenAnyValue(x => x.BottomPanelMode).Skip(1).Subscribe(m => observedMode = m);
        using var visSub = vm.WhenAnyValue(x => x.IsBottomPanelVisible).Skip(1).Subscribe(v => observedVisible = v);

        vm.SwitchToProblemsBottomCommand.Execute().Subscribe();
        Assert.Equal(BottomPanelMode.Problems, observedMode);
        Assert.True(observedVisible);
        Assert.True(vm.IsProblemsBottomMode);
        Assert.False(vm.IsTerminalBottomMode);

        vm.SwitchToOutputBottomCommand.Execute().Subscribe();
        Assert.Equal(BottomPanelMode.Output, observedMode);
        Assert.True(vm.IsOutputBottomMode);

        vm.SwitchToTestResultsBottomCommand.Execute().Subscribe();
        Assert.Equal(BottomPanelMode.TestResults, observedMode);
        Assert.True(vm.IsTestResultsBottomMode);

        vm.SwitchToDebugBottomCommand.Execute().Subscribe();
        Assert.Equal(BottomPanelMode.Debug, observedMode);
        Assert.True(vm.IsDebugBottomMode);

        vm.SwitchToTerminalBottomCommand.Execute().Subscribe();
        Assert.Equal(BottomPanelMode.Terminal, observedMode);
        Assert.True(vm.IsTerminalBottomMode);

        observedVisible = null;
        vm.HideBottomPanelCommand.Execute().Subscribe();
        Assert.False(observedVisible);
        Assert.False(vm.IsBottomPanelVisible);
        Assert.Equal(BottomPanelMode.Terminal, vm.BottomPanelMode);

        vm.ToggleBottomPanelCommand.Execute().Subscribe();
        Assert.True(vm.IsBottomPanelVisible);
        vm.ToggleBottomPanelCommand.Execute().Subscribe();
        Assert.False(vm.IsBottomPanelVisible);
    }

    private static ShellPanelNavigation Create(
        Action<LeftPanelMode>? setLeft = null,
        Action<BottomPanelMode>? setBottom = null,
        Action<bool>? setBottomVisible = null,
        Func<bool>? getBottomVisible = null) =>
        new(
            setLeft: setLeft ?? (_ => { }),
            setBottom: setBottom ?? (_ => { }),
            setBottomVisible: setBottomVisible ?? (_ => { }),
            getBottomVisible: getBottomVisible ?? (() => false));

    private static MainWindowViewModel CreateMainWindowViewModel()
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
        var terminalService = new Mock<ITerminalService>();
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(terminalService.Object);
        var terminalHost = new TerminalHost(factory.Object);
        var coordinator = new Mock<IAgentExecutionCoordinator>().Object;
        var panelHost = ConversationsTestSupport.CreatePanelHost();
        var parser = new MentionParser();
        var router = new AgentRouter(parser, panelHost, coordinator, ConversationsTestSupport.CreateCatalog(), ConversationsTestSupport.CreateStore());
        var projectContext = new Mock<IProjectContextService>(MockBehavior.Loose);
        projectContext.Setup(s => s.WhenChanged).Returns(Observable.Never<ProjectContext>());

        return new MainWindowViewModel(
            new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance),
            editorTabs,
            terminalHost,
            panelHost,
            router,
            ConversationsTestSupport.CreateTownhallViewModel(),
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
            projectContext.Object, ConversationsTestSupport.CreateCatalogAsInterface());
    }
}
