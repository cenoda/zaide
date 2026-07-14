using System;
using System.Reactive.Subjects;
using Moq;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests;

/// <summary>
/// Shared factory for idle debug-session projections in composition tests.
/// </summary>
internal static class TestDebugSessionFactory
{
    public static DebugSessionViewModel Create(ICommandRegistry? registry = null)
    {
        var launch = new Mock<IProjectDebugLaunchService>();
        launch.Setup(s => s.StartDebuggingAsync(default))
            .ReturnsAsync(new DebugSessionOperationResult(false, null, null));

        var debugSession = TestOperationGateFactory.CreateIdleDebugSession();
        return new DebugSessionViewModel(launch.Object, debugSession.Object, registry);
    }
}