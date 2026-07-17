using System;
using System.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Services;
using Zaide.Features.Debugging.Infrastructure.Dap;
using Zaide.ViewModels;
using Zaide.Tests.Features.ProjectSystem;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Application;

namespace Zaide.Tests.ViewModels;

/// <summary>
/// Phase 12 M5 tests for stopped-state thread/stack/scope/variable projection.
/// </summary>
public sealed class DebugStackProjectionTests
{
    static DebugStackProjectionTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static (DebugStackProjectionViewModel Projection, Subject<DebugSessionSnapshot> Subject, Mock<IDebugSessionService> Debug)
        CreateHarness(DebugSessionSnapshot initial)
    {
        var subject = new Subject<DebugSessionSnapshot>();
        var current = initial;
        var debug = new Mock<IDebugSessionService>();
        debug.SetupGet(s => s.Current).Returns(() => current);
        debug.SetupGet(s => s.WhenChanged).Returns(subject);
        subject.Subscribe(snapshot => current = snapshot);

        var projection = new DebugStackProjectionViewModel(debug.Object);
        projection.Activate();
        return (projection, subject, debug);
    }

    private static DebugSessionSnapshot StoppedSnapshot(
        long generation = 1,
        int threadId = 1) =>
        new(
            DebugSessionState.Stopped,
            generation,
            ProgramPath: "/tmp/App.dll",
            WorkingDirectory: "/tmp",
            AdapterProcessId: 42,
            StopInfo: new DapStoppedInfo("breakpoint", threadId),
            Failure: null,
            LastOutcome: null,
            DiagnosticOutput: Array.Empty<string>(),
            BreakpointVerifications: DebugSessionSnapshot.EmptyVerifications);

    [Fact]
    public async Task Stopped_LoadsThreadsThenStackFrames()
    {
        var (projection, subject, debug) = CreateHarness(new DebugSessionSnapshot(
            DebugSessionState.Idle,
            Generation: 1,
            ProgramPath: null,
            WorkingDirectory: null,
            AdapterProcessId: null,
            StopInfo: null,
            Failure: null,
            LastOutcome: null,
            DiagnosticOutput: Array.Empty<string>(),
            BreakpointVerifications: DebugSessionSnapshot.EmptyVerifications));

        debug.Setup(s => s.RequestThreadsAsync(default))
            .ReturnsAsync(JsonDocument.Parse("{\"threads\":[{\"id\":1,\"name\":\"main\"}]}").RootElement);
        debug.Setup(s => s.RequestStackTraceAsync(1, default))
            .ReturnsAsync(JsonDocument.Parse(
                "{\"stackFrames\":[{\"id\":10,\"name\":\"Main\",\"source\":{\"path\":\"/tmp/Program.cs\"},\"line\":2}]}")
                .RootElement);
        debug.Setup(s => s.RequestScopesAsync(10, default))
            .ReturnsAsync(JsonDocument.Parse("{\"scopes\":[{\"name\":\"Locals\",\"variablesReference\":3}]}").RootElement);
        debug.Setup(s => s.RequestVariablesAsync(3, default))
            .ReturnsAsync(JsonDocument.Parse("{\"variables\":[{\"name\":\"x\",\"value\":\"42\"}]}").RootElement);

        subject.OnNext(StoppedSnapshot());
        await WaitForReadyAsync(() => projection.Variables.Count == 1);

        Assert.Equal(DebugProjectionState.Ready, projection.CallStackState);
        Assert.Single(projection.Threads);
        Assert.Single(projection.Frames);
        Assert.Equal("Main", projection.Frames[0].Name);
        Assert.Equal(10, projection.SelectedFrame?.Id);
        Assert.Single(projection.Scopes);
        Assert.Single(projection.Variables);
        Assert.Equal("x", projection.Variables[0].Name);
        debug.Verify(s => s.RequestThreadsAsync(default), Times.Once);
        debug.Verify(s => s.RequestStackTraceAsync(1, default), Times.Once);
        debug.Verify(s => s.RequestScopesAsync(10, default), Times.Once);
        debug.Verify(s => s.RequestVariablesAsync(3, default), Times.Once);
        projection.Dispose();
    }

    [Fact]
    public async Task FrameSelection_LoadsScopesAndVariables()
    {
        var initial = StoppedSnapshot();
        var (projection, subject, debug) = CreateHarness(initial);

        debug.Setup(s => s.RequestThreadsAsync(default))
            .ReturnsAsync(JsonDocument.Parse("{\"threads\":[{\"id\":1,\"name\":\"main\"}]}").RootElement);
        debug.Setup(s => s.RequestStackTraceAsync(1, default))
            .ReturnsAsync(JsonDocument.Parse(
                "{\"stackFrames\":[{\"id\":10,\"name\":\"A\"},{\"id\":11,\"name\":\"B\"}]}")
                .RootElement);
        debug.Setup(s => s.RequestScopesAsync(11, default))
            .ReturnsAsync(JsonDocument.Parse("{\"scopes\":[{\"name\":\"Args\",\"variablesReference\":9}]}").RootElement);
        debug.Setup(s => s.RequestVariablesAsync(9, default))
            .ReturnsAsync(JsonDocument.Parse("{\"variables\":[{\"name\":\"argv\",\"value\":\"[]\"}]}").RootElement);

        subject.OnNext(initial);
        await WaitForReadyAsync(() => projection.Frames.Count == 2);

        projection.SelectFrameCommand.Execute(projection.Frames[1]).Subscribe();
        await WaitForReadyAsync(() => projection.Variables.Count == 1);

        Assert.Equal("B", projection.SelectedFrame?.Name);
        Assert.Equal("Args", projection.SelectedScope?.Name);
        Assert.Equal("argv", projection.Variables[0].Name);
        debug.Verify(s => s.RequestScopesAsync(11, default), Times.Once);
        projection.Dispose();
    }

    [Fact]
    public void Running_ClearsProjection()
    {
        var (projection, subject, _) = CreateHarness(StoppedSnapshot());
        subject.OnNext(StoppedSnapshot());
        subject.OnNext(new DebugSessionSnapshot(
            DebugSessionState.Running,
            Generation: 1,
            ProgramPath: "/tmp/App.dll",
            WorkingDirectory: "/tmp",
            AdapterProcessId: 42,
            StopInfo: null,
            Failure: null,
            LastOutcome: null,
            DiagnosticOutput: Array.Empty<string>(),
            BreakpointVerifications: DebugSessionSnapshot.EmptyVerifications));

        Assert.Empty(projection.Frames);
        Assert.Empty(projection.Variables);
        Assert.Contains("running", projection.CallStackStatusText, StringComparison.OrdinalIgnoreCase);
        projection.Dispose();
    }

    [Fact]
    public async Task EmptyThreads_ShowsEmptyState()
    {
        var initial = StoppedSnapshot();
        var (projection, subject, debug) = CreateHarness(initial);
        debug.Setup(s => s.RequestThreadsAsync(default))
            .ReturnsAsync(JsonDocument.Parse("{\"threads\":[]}").RootElement);

        subject.OnNext(initial);
        await WaitForReadyAsync(() => projection.CallStackState == DebugProjectionState.Empty);

        Assert.Equal(DebugProjectionState.Empty, projection.CallStackState);
        Assert.Contains("no threads", projection.CallStackStatusText, StringComparison.OrdinalIgnoreCase);
        projection.Dispose();
    }

    [Fact]
    public async Task StackRequestFailure_ShowsErrorState()
    {
        var initial = StoppedSnapshot();
        var (projection, subject, debug) = CreateHarness(initial);
        debug.Setup(s => s.RequestThreadsAsync(default))
            .ReturnsAsync(JsonDocument.Parse("{\"threads\":[{\"id\":1,\"name\":\"main\"}]}").RootElement);
        debug.Setup(s => s.RequestStackTraceAsync(1, default))
            .ThrowsAsync(new InvalidOperationException("stopped required"));

        subject.OnNext(initial);
        await WaitForReadyAsync(() => projection.CallStackState == DebugProjectionState.Error);

        Assert.Equal(DebugProjectionState.Error, projection.CallStackState);
        Assert.Contains("failed", projection.CallStackStatusText, StringComparison.OrdinalIgnoreCase);
        projection.Dispose();
    }

    [Fact]
    public async Task StaleGenerationReply_IsIgnored()
    {
        var (projection, subject, debug) = CreateHarness(new DebugSessionSnapshot(
            DebugSessionState.Idle,
            Generation: 0,
            ProgramPath: null,
            WorkingDirectory: null,
            AdapterProcessId: null,
            StopInfo: null,
            Failure: null,
            LastOutcome: null,
            DiagnosticOutput: Array.Empty<string>(),
            BreakpointVerifications: DebugSessionSnapshot.EmptyVerifications));

        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;
        debug.Setup(s => s.RequestThreadsAsync(default))
            .Returns(async () =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                {
                    await release.Task.ConfigureAwait(false);
                    return JsonDocument.Parse("{\"threads\":[{\"id\":1,\"name\":\"late\"}]}").RootElement;
                }

                return JsonDocument.Parse("{\"threads\":[]}").RootElement;
            });

        subject.OnNext(StoppedSnapshot(generation: 1));
        subject.OnNext(StoppedSnapshot(generation: 2));
        release.TrySetResult(true);
        await WaitForReadyAsync(() => projection.CallStackState == DebugProjectionState.Empty);

        Assert.Empty(projection.Threads);
        Assert.Equal(DebugProjectionState.Empty, projection.CallStackState);
        projection.Dispose();
    }

    [Fact]
    public async Task StaleFrameSelectionReply_IsIgnored()
    {
        var (projection, subject, debug) = CreateHarness(new DebugSessionSnapshot(
            DebugSessionState.Idle,
            Generation: 1,
            ProgramPath: null,
            WorkingDirectory: null,
            AdapterProcessId: null,
            StopInfo: null,
            Failure: null,
            LastOutcome: null,
            DiagnosticOutput: Array.Empty<string>(),
            BreakpointVerifications: DebugSessionSnapshot.EmptyVerifications));

        debug.Setup(s => s.RequestThreadsAsync(default))
            .ReturnsAsync(JsonDocument.Parse("{\"threads\":[{\"id\":1,\"name\":\"main\"}]}").RootElement);
        debug.Setup(s => s.RequestStackTraceAsync(1, default))
            .ReturnsAsync(JsonDocument.Parse(
                "{\"stackFrames\":[{\"id\":10,\"name\":\"A\"},{\"id\":11,\"name\":\"B\"}]}")
                .RootElement);

        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        debug.Setup(s => s.RequestScopesAsync(10, default))
            .ReturnsAsync(JsonDocument.Parse("{\"scopes\":[{\"name\":\"Current\",\"variablesReference\":1}]}").RootElement);
        debug.Setup(s => s.RequestScopesAsync(11, default))
            .Returns(async () =>
            {
                await release.Task.ConfigureAwait(false);
                return JsonDocument.Parse("{\"scopes\":[{\"name\":\"Late\",\"variablesReference\":2}]}").RootElement;
            });
        debug.Setup(s => s.RequestVariablesAsync(It.IsAny<int>(), default))
            .ReturnsAsync(JsonDocument.Parse("{\"variables\":[{\"name\":\"ok\",\"value\":\"1\"}]}").RootElement);

        subject.OnNext(StoppedSnapshot());
        await WaitForReadyAsync(() => projection.Frames.Count == 2);

        projection.SelectFrameCommand.Execute(projection.Frames[1]).Subscribe();
        await Task.Delay(20);
        projection.SelectFrameCommand.Execute(projection.Frames[0]).Subscribe();
        await WaitForReadyAsync(() => projection.Scopes.Any(scope => scope.Name == "Current"));
        release.TrySetResult(true);
        await Task.Delay(100);

        Assert.DoesNotContain(projection.Scopes, scope => scope.Name == "Late");
        Assert.Contains(projection.Scopes, scope => scope.Name == "Current");
        projection.Dispose();
    }

    [Fact]
    public void DebugPanel_ComposesVariablesInDebugMode()
    {
        var debug = TestOperationGateFactory.CreateIdleDebugSession();
        var stack = new DebugStackProjectionViewModel(debug.Object);
        var panel = new DebugPanelViewModel(debug.Object, stack);

        Assert.Same(stack, panel.StackProjection);
        Assert.NotNull(panel.StackProjection.Variables);
        panel.Dispose();
    }

    private static async Task WaitForReadyAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (condition())
                return;

            await Task.Delay(20).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out waiting for debug stack projection.");
    }
}