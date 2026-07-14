using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 12 M3a tests for <c>debug.startOrContinue</c> command registration and dispatch.
/// </summary>
public sealed class DebugStartOrContinueCommandTests
{
    static DebugStartOrContinueCommandTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static DebugSessionSnapshot MakeSnapshot(DebugSessionState state, int? threadId = null) =>
        new(
            state,
            Generation: 1,
            ProgramPath: null,
            WorkingDirectory: null,
            AdapterProcessId: null,
            StopInfo: threadId is null ? null : new DapStoppedInfo("breakpoint", threadId),
            Failure: null,
            LastOutcome: null,
            DiagnosticOutput: Array.Empty<string>(),
            BreakpointVerifications: DebugSessionSnapshot.EmptyVerifications);

    private static (DebugSessionViewModel ViewModel, Mock<IProjectDebugLaunchService> Launch, Mock<IDebugSessionService> Debug)
        CreateHarness(DebugSessionState initialState, ICommandRegistry? registry = null)
    {
        var launch = new Mock<IProjectDebugLaunchService>();
        launch.Setup(s => s.StartDebuggingAsync(default))
            .ReturnsAsync(new DebugSessionOperationResult(true, null, null));

        var debug = new Mock<IDebugSessionService>();
        var snapshot = MakeSnapshot(initialState, threadId: 1);
        debug.SetupGet(s => s.Current).Returns(snapshot);
        debug.SetupGet(s => s.WhenChanged).Returns(Observable.Return(snapshot));
        debug.Setup(s => s.ContinueAsync(It.IsAny<int>(), default))
            .ReturnsAsync(new DebugSessionOperationResult(true, null, null));

        var vm = new DebugSessionViewModel(launch.Object, debug.Object, registry);
        vm.Activate();
        return (vm, launch, debug);
    }

    [Fact]
    public void Registry_ContainsSingleF5Command()
    {
        var registry = CommandRegistryFactory.Create();
        var vm = CreateHarness(DebugSessionState.Idle, registry).ViewModel;
        vm.Dispose();

        var descriptor = registry.GetById("debug.startOrContinue");
        Assert.NotNull(descriptor);
        Assert.Equal("Start Debugging / Continue", descriptor!.DisplayName);
        Assert.Equal(new[] { "F5" }, descriptor.DefaultGestures);

        var settings = new Mock<ISettingsService>();
        settings.SetupGet(s => s.Current).Returns(SettingsModel.Defaults);
        var f5Bindings = registry.ResolveKeyBindings(settings.Object)
            .Where(binding => binding.Gesture == "F5")
            .ToList();

        Assert.Single(f5Bindings);
        Assert.Equal("debug.startOrContinue", f5Bindings[0].CommandId);
    }

    [Theory]
    [InlineData(DebugSessionState.Idle)]
    [InlineData(DebugSessionState.Failed)]
    [InlineData(DebugSessionState.Unavailable)]
    public async Task StartOrContinue_IdleLikeStates_StartsDebugging(DebugSessionState state)
    {
        var (vm, launch, _) = CreateHarness(state);

        Assert.True(vm.StartOrContinueCommand.CanExecute.FirstAsync().Wait());

        await vm.StartOrContinueCommand.Execute();

        launch.Verify(s => s.StartDebuggingAsync(default), Times.Once);
        vm.Dispose();
    }

    [Fact]
    public async Task StartOrContinue_Stopped_Continues()
    {
        var launch = new Mock<IProjectDebugLaunchService>();
        var debug = new Mock<IDebugSessionService>();
        debug.SetupGet(s => s.Current).Returns(MakeSnapshot(DebugSessionState.Stopped, threadId: 7));
        debug.SetupGet(s => s.WhenChanged).Returns(Observable.Return(MakeSnapshot(DebugSessionState.Stopped, threadId: 7)));
        debug.Setup(s => s.ContinueAsync(7, default))
            .ReturnsAsync(new DebugSessionOperationResult(true, null, null));

        var vm = new DebugSessionViewModel(launch.Object, debug.Object);
        vm.Activate();

        await vm.StartOrContinueCommand.Execute();

        debug.Verify(s => s.ContinueAsync(7, default), Times.Once);
        launch.Verify(s => s.StartDebuggingAsync(default), Times.Never);
        vm.Dispose();
    }

    [Theory]
    [InlineData(DebugSessionState.Starting)]
    [InlineData(DebugSessionState.Running)]
    [InlineData(DebugSessionState.Stopping)]
    public void StartOrContinue_ActiveStates_Unavailable(DebugSessionState state)
    {
        var (vm, _, _) = CreateHarness(state);

        Assert.False(vm.StartOrContinueCommand.CanExecute.FirstAsync().Wait());
        vm.Dispose();
    }
}