using System;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Moq;
using ReactiveUI.Builder;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.Debugging.Infrastructure.Dap;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Presentation;
using Zaide.Tests.Features.ProjectSystem;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Application;
using Zaide.Features.Debugging.Presentation;

namespace Zaide.Tests.Features.Debugging.Presentation;

/// <summary>
/// Phase 12 M4/M5 tests for Debug Console history, isolation, error projection,
/// and debug-panel composition.
/// </summary>
public sealed class DebugPanelViewModelTests
{
    static DebugPanelViewModelTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static (DebugPanelViewModel Panel, Subject<DebugSessionSnapshot> Subject, Mock<IDebugSessionService> Debug)
        CreateHarness(DebugSessionSnapshot initial)
    {
        var subject = new Subject<DebugSessionSnapshot>();
        var debug = new Mock<IDebugSessionService>();
        debug.SetupGet(s => s.Current).Returns(initial);
        debug.SetupGet(s => s.WhenChanged).Returns(subject);

        var stack = new DebugStackProjectionViewModel(debug.Object);
        var panel = new DebugPanelViewModel(debug.Object, stack);
        panel.Activate();
        return (panel, subject, debug);
    }

    private static DebugSessionSnapshot Snapshot(
        DebugSessionState state,
        string? failureMessage = null,
        params string[] diagnostics) =>
        new(
            state,
            Generation: 1,
            ProgramPath: "/tmp/App.dll",
            WorkingDirectory: "/tmp",
            AdapterProcessId: 42,
            StopInfo: state == DebugSessionState.Stopped
                ? new DapStoppedInfo("entry", 1)
                : null,
            Failure: failureMessage is null
                ? null
                : new DebugSessionFailure(DebugSessionOutcomeKind.StartupFailed, failureMessage),
            LastOutcome: null,
            DiagnosticOutput: diagnostics,
            BreakpointVerifications: DebugSessionSnapshot.EmptyVerifications);

    [Fact]
    public void Activate_Starting_RaisesShowDebugRequested()
    {
        var showCount = 0;
        var (panel, subject, _) = CreateHarness(Snapshot(DebugSessionState.Idle));
        panel.WhenShowDebugRequested.Subscribe(_ => showCount++);
        subject.OnNext(Snapshot(DebugSessionState.Starting));

        Assert.Equal(1, showCount);
        panel.Dispose();
    }

    [Fact]
    public void Console_PreservesHistoryAfterSessionEnd()
    {
        var (panel, subject, _) = CreateHarness(Snapshot(DebugSessionState.Starting));
        subject.OnNext(Snapshot(DebugSessionState.Stopped));
        subject.OnNext(Snapshot(DebugSessionState.Idle));
        var lineCountAfterEnd = panel.Lines.Count;

        Assert.True(lineCountAfterEnd >= 2);
        Assert.Contains(panel.Lines, line => line.DisplayText.Contains("stopped", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(panel.Lines, line => line.DisplayText.Contains("ended", StringComparison.OrdinalIgnoreCase));
        panel.Dispose();
    }

    [Fact]
    public void Console_ProjectsAdapterDiagnosticsAndErrors()
    {
        var (panel, subject, _) = CreateHarness(Snapshot(DebugSessionState.Starting));
        subject.OnNext(Snapshot(
            DebugSessionState.Failed,
            failureMessage: "Adapter process exited unexpectedly.",
            diagnostics: new[] { "stderr: boom", "[error] Debug pause failed." }));

        Assert.Contains(panel.Lines, line => line.DisplayText == "stderr: boom");
        Assert.Contains(panel.Lines, line =>
            line.Kind == DebugConsoleLineKind.Error &&
            line.DisplayText.Contains("pause failed", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Adapter process exited unexpectedly.", panel.StatusMessage);
        panel.Dispose();
    }

    [Fact]
    public void DebugMode_IncludesStackAndVariablesProjection()
    {
        var (panel, subject, _) = CreateHarness(Snapshot(DebugSessionState.Idle));
        Assert.NotNull(panel.StackProjection);
        Assert.NotNull(panel.StackProjection.Variables);

        subject.OnNext(Snapshot(DebugSessionState.Running));
        Assert.Contains("running", panel.StackProjection.CallStackStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("running", panel.StackProjection.VariablesStatusText, StringComparison.OrdinalIgnoreCase);

        subject.OnNext(Snapshot(DebugSessionState.Idle));
        Assert.Contains("without an active debug session", panel.StackProjection.CallStackStatusText, StringComparison.OrdinalIgnoreCase);
        panel.Dispose();
    }

    [Fact]
    public async Task Console_IsolatedFromWorkflowOutput()
    {
        var workflow = TestProjectWorkflowFactory.Create();
        workflow.Activate();
        workflow.Lines.Add(new OutputLineViewModel(
            new ManagedProcessOutputLine(1, ProcessStreamKind.StdOut, "build line", DateTimeOffset.UtcNow)));

        var (panel, _, _) = CreateHarness(Snapshot(DebugSessionState.Starting));
        await Task.Delay(20);

        Assert.DoesNotContain(panel.Lines, line => line.DisplayText.Contains("build line", StringComparison.Ordinal));
        Assert.Contains(workflow.Lines, line => line.DisplayText.Contains("build line", StringComparison.Ordinal));
        panel.Dispose();
        workflow.Dispose();
    }
}