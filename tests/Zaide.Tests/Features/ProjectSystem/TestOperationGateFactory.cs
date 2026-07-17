using System;
using System.Reactive.Subjects;
using Moq;
using Zaide.Services;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Infrastructure;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Application;

namespace Zaide.Tests.Features.ProjectSystem;

/// <summary>
/// Shared idle gate/debug-session mocks for composition tests.
/// </summary>
internal static class TestOperationGateFactory
{
    public static IProjectOperationGate CreateIdleGate()
    {
        var debugSession = CreateIdleDebugSession();
        return new ProjectOperationGate(debugSession.Object);
    }

    public static Mock<IDebugSessionService> CreateIdleDebugSession()
    {
        var debugSession = new Mock<IDebugSessionService>();
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
        debugSession.SetupGet(s => s.Current).Returns(idle);
        debugSession.SetupGet(s => s.WhenChanged).Returns(new Subject<DebugSessionSnapshot>());
        return debugSession;
    }
}