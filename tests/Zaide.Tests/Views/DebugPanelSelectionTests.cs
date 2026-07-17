using System;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Moq;
using ReactiveUI;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;
using Splat;
using Xunit;
using Zaide.Services;
using Zaide.Features.Debugging.Infrastructure.Dap;
using Zaide.ViewModels;
using Zaide.Views;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Application;

namespace Zaide.Tests.Views;

/// <summary>
/// Phase 12 post-closeout F2 regression: DebugPanel VM→UI selection sync must not
/// re-fire stack/scope/variable DAP loads.
/// </summary>
public sealed class DebugPanelSelectionTests
{
    static DebugPanelSelectionTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        Locator.CurrentMutable.Register(() => new AvaloniaActivationForViewFetcher(), typeof(IActivationForViewFetcher));
        EnsureApplication();
    }

    [Fact]
    public async Task Stopped_WithDebugPanel_IssuesSingleInspectionRequestPerKind()
    {
        var subject = new Subject<DebugSessionSnapshot>();
        var current = IdleSnapshot();
        var debug = new Mock<IDebugSessionService>();
        debug.SetupGet(s => s.Current).Returns(() => current);
        debug.SetupGet(s => s.WhenChanged).Returns(subject);
        subject.Subscribe(snapshot => current = snapshot);

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

        var stack = new DebugStackProjectionViewModel(debug.Object);
        using var panelVm = new DebugPanelViewModel(debug.Object, stack);
        panelVm.Activate();

        var root = new Panel();
        var panel = new DebugPanel { ViewModel = panelVm };
        root.Children.Add(panel);

        subject.OnNext(StoppedSnapshot());
        await WaitForReadyAsync(() => panelVm.StackProjection.Variables.Count == 1);

        debug.Verify(s => s.RequestThreadsAsync(default), Times.Once);
        debug.Verify(s => s.RequestStackTraceAsync(1, default), Times.Once);
        debug.Verify(s => s.RequestScopesAsync(10, default), Times.Once);
        debug.Verify(s => s.RequestVariablesAsync(3, default), Times.Once);

        root.Children.Remove(panel);
    }

    private static DebugSessionSnapshot IdleSnapshot() =>
        new(
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

    private static DebugSessionSnapshot StoppedSnapshot() =>
        new(
            DebugSessionState.Stopped,
            Generation: 1,
            ProgramPath: "/tmp/App.dll",
            WorkingDirectory: "/tmp",
            AdapterProcessId: 42,
            StopInfo: new DapStoppedInfo("breakpoint", 1),
            Failure: null,
            LastOutcome: null,
            DiagnosticOutput: Array.Empty<string>(),
            BreakpointVerifications: DebugSessionSnapshot.EmptyVerifications);

    private static async Task WaitForReadyAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (condition())
                return;

            await Task.Delay(20).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out waiting for debug panel projection.");
    }

    private static void EnsureApplication()
    {
        if (Application.Current is App app)
        {
            if (!app.Resources.ContainsKey("PrimaryAccentBrush"))
                app.Initialize();
            return;
        }

        new App().Initialize();
    }
}