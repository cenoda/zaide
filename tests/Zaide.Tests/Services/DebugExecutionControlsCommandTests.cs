using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 12 M4 tests for debug execution-control command registration, gating,
/// dispatch, and gesture uniqueness.
/// </summary>
public sealed class DebugExecutionControlsCommandTests
{
    static DebugExecutionControlsCommandTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static DebugSessionSnapshot MakeSnapshot(
        DebugSessionState state,
        int? threadId = 1) =>
        new(
            state,
            Generation: 1,
            ProgramPath: "/tmp/App.dll",
            WorkingDirectory: "/tmp",
            AdapterProcessId: 42,
            StopInfo: threadId is null ? null : new DapStoppedInfo("entry", threadId),
            Failure: null,
            LastOutcome: null,
            DiagnosticOutput: Array.Empty<string>(),
            BreakpointVerifications: DebugSessionSnapshot.EmptyVerifications);

    private static (DebugSessionViewModel ViewModel, Mock<IDebugSessionService> Debug) CreateHarness(
        DebugSessionState initialState,
        int? threadId = 1,
        ICommandRegistry? registry = null)
    {
        var launch = new Mock<IProjectDebugLaunchService>();
        var debug = new Mock<IDebugSessionService>();
        var snapshot = MakeSnapshot(initialState, threadId);
        debug.SetupGet(s => s.Current).Returns(snapshot);
        debug.SetupGet(s => s.WhenChanged).Returns(Observable.Return(snapshot));
        debug.Setup(s => s.StopAsync(default))
            .ReturnsAsync(new DebugSessionOperationResult(true, null, null));
        debug.Setup(s => s.PauseAsync(default))
            .ReturnsAsync(new DebugSessionOperationResult(true, null, null));
        debug.Setup(s => s.StepOverAsync(default))
            .ReturnsAsync(new DebugSessionOperationResult(true, null, null));
        debug.Setup(s => s.StepIntoAsync(default))
            .ReturnsAsync(new DebugSessionOperationResult(true, null, null));
        debug.Setup(s => s.StepOutAsync(default))
            .ReturnsAsync(new DebugSessionOperationResult(true, null, null));

        var vm = new DebugSessionViewModel(launch.Object, debug.Object, registry);
        vm.Activate();
        return (vm, debug);
    }

    [Fact]
    public void Registry_ContainsExecutionControlCommandsWithExpectedGestures()
    {
        var registry = CommandRegistryFactory.Create();
        var vm = CreateHarness(DebugSessionState.Idle, registry: registry).ViewModel;
        vm.Dispose();

        Assert.Equal(Array.Empty<string>(), registry.GetById("debug.pause")!.DefaultGestures);
        Assert.Equal(new[] { "Shift+F5" }, registry.GetById("debug.stop")!.DefaultGestures);
        Assert.Equal(new[] { "F10" }, registry.GetById("debug.stepOver")!.DefaultGestures);
        Assert.Equal(new[] { "F11" }, registry.GetById("debug.stepInto")!.DefaultGestures);
        Assert.Equal(new[] { "Shift+F11" }, registry.GetById("debug.stepOut")!.DefaultGestures);
    }

    [Fact]
    public void Registry_DebugGesturesResolveExactlyOnce()
    {
        var registry = CommandRegistryFactory.Create();
        CreateMainWindowWithRegistry(registry);

        var settings = new Mock<ISettingsService>();
        settings.SetupGet(s => s.Current).Returns(SettingsModel.Defaults);
        var bindings = registry.ResolveKeyBindings(settings.Object);

        foreach (var gesture in new[] { "F5", "F9", "F10", "F11", "Shift+F5", "Shift+F11" })
        {
            var matches = bindings.Where(binding => binding.Gesture == gesture).ToList();
            Assert.Single(matches);
        }

        Assert.Equal("debug.startOrContinue", bindings.Single(b => b.Gesture == "F5").CommandId);
        Assert.Equal("debug.toggleBreakpoint", bindings.Single(b => b.Gesture == "F9").CommandId);
        Assert.Equal("debug.stepOver", bindings.Single(b => b.Gesture == "F10").CommandId);
        Assert.Equal("debug.stepInto", bindings.Single(b => b.Gesture == "F11").CommandId);
        Assert.Equal("debug.stop", bindings.Single(b => b.Gesture == "Shift+F5").CommandId);
        Assert.Equal("debug.stepOut", bindings.Single(b => b.Gesture == "Shift+F11").CommandId);
    }

    [Fact]
    public void Pause_RunningOnly()
    {
        var (runningVm, _) = CreateHarness(DebugSessionState.Running);
        var (stoppedVm, _) = CreateHarness(DebugSessionState.Stopped);

        Assert.True(runningVm.PauseCommand.CanExecute.FirstAsync().Wait());
        Assert.False(stoppedVm.PauseCommand.CanExecute.FirstAsync().Wait());

        runningVm.Dispose();
        stoppedVm.Dispose();
    }

    [Theory]
    [InlineData(DebugSessionState.Starting)]
    [InlineData(DebugSessionState.Running)]
    [InlineData(DebugSessionState.Stopped)]
    public void Stop_AvailableDuringActiveSessionStates(DebugSessionState state)
    {
        var (vm, _) = CreateHarness(state);
        Assert.True(vm.StopCommand.CanExecute.FirstAsync().Wait());
        vm.Dispose();
    }

    [Theory]
    [InlineData(DebugSessionState.Idle)]
    [InlineData(DebugSessionState.Stopping)]
    [InlineData(DebugSessionState.Failed)]
    [InlineData(DebugSessionState.Unavailable)]
    public void Stop_UnavailableOutsideActiveSessionStates(DebugSessionState state)
    {
        var (vm, _) = CreateHarness(state, threadId: null);
        Assert.False(vm.StopCommand.CanExecute.FirstAsync().Wait());
        vm.Dispose();
    }

    [Fact]
    public void StepCommands_StoppedWithThreadOnly()
    {
        var (stoppedVm, _) = CreateHarness(DebugSessionState.Stopped, threadId: 3);
        var (runningVm, _) = CreateHarness(DebugSessionState.Running, threadId: null);
        var (stoppedNoThreadVm, _) = CreateHarness(DebugSessionState.Stopped, threadId: null);

        Assert.True(stoppedVm.StepOverCommand.CanExecute.FirstAsync().Wait());
        Assert.True(stoppedVm.StepIntoCommand.CanExecute.FirstAsync().Wait());
        Assert.True(stoppedVm.StepOutCommand.CanExecute.FirstAsync().Wait());
        Assert.False(runningVm.StepOverCommand.CanExecute.FirstAsync().Wait());
        Assert.False(stoppedNoThreadVm.StepOverCommand.CanExecute.FirstAsync().Wait());

        stoppedVm.Dispose();
        runningVm.Dispose();
        stoppedNoThreadVm.Dispose();
    }

    [Fact]
    public async Task Stop_DispatchesStopAsync()
    {
        var (vm, debug) = CreateHarness(DebugSessionState.Stopped);
        await vm.StopCommand.Execute();
        debug.Verify(s => s.StopAsync(default), Times.Once);
        vm.Dispose();
    }

    [Fact]
    public async Task Pause_DispatchesPauseAsync()
    {
        var (vm, debug) = CreateHarness(DebugSessionState.Running, threadId: null);
        await vm.PauseCommand.Execute();
        debug.Verify(s => s.PauseAsync(default), Times.Once);
        vm.Dispose();
    }

    [Fact]
    public async Task StepOver_DispatchesStepOverAsync()
    {
        var (vm, debug) = CreateHarness(DebugSessionState.Stopped, threadId: 9);
        await vm.StepOverCommand.Execute();
        debug.Verify(s => s.StepOverAsync(default), Times.Once);
        vm.Dispose();
    }

    [Fact]
    public async Task Continue_Gap_DisablesConflictingCommandsUntilRunningSnapshot()
    {
        var launch = new Mock<IProjectDebugLaunchService>();
        var subject = new Subject<DebugSessionSnapshot>();
        var debug = new Mock<IDebugSessionService>();
        var stopped = MakeSnapshot(DebugSessionState.Stopped, threadId: 3);
        var current = stopped;
        debug.SetupGet(s => s.Current).Returns(() => current);
        debug.SetupGet(s => s.WhenChanged).Returns(subject);
        subject.Subscribe(snapshot => current = snapshot);

        var continueStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        debug.Setup(s => s.ContinueAsync(3, default))
            .Returns(async () =>
            {
                subject.OnNext(MakeSnapshot(DebugSessionState.Running, threadId: null));
                continueStarted.TrySetResult(true);
                await Task.Delay(50).ConfigureAwait(false);
                return new DebugSessionOperationResult(true, null, null);
            });

        var vm = new DebugSessionViewModel(launch.Object, debug.Object);
        vm.Activate();

        Assert.True(vm.StartOrContinueCommand.CanExecute.FirstAsync().Wait());
        Assert.True(vm.StepOverCommand.CanExecute.FirstAsync().Wait());

        var executeTask = vm.StartOrContinueCommand.Execute();
        await continueStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(vm.StartOrContinueCommand.CanExecute.FirstAsync().Wait());
        Assert.False(vm.StepOverCommand.CanExecute.FirstAsync().Wait());

        await executeTask;
        vm.Dispose();
    }

    private static void CreateMainWindowWithRegistry(ICommandRegistry registry)
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

        _ = new MainWindowViewModel(
            new FileTreeViewModel(new FileTreeService(), System.Reactive.Concurrency.CurrentThreadScheduler.Instance),
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
                new Mock<IGitRepositoryService>().Object,
                commandRegistry: registry),
            TestProblemsFactory.Create(workspace, editorTabs),
            TestProjectWorkflowFactory.Create(registry: registry),
            TestTestResultsFactory.Create(editorTabs),
            TestDebugSessionFactory.Create(registry),
            TestDebugPanelFactory.Create(),
            TestEditorBreakpointFactory.Create(editorTabs, registry),
            workspace,
            new Mock<IProjectContextService>(MockBehavior.Loose).Object,
            registry);
    }
}
