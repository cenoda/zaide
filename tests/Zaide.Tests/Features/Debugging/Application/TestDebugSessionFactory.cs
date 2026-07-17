using System;
using System.Reactive.Subjects;
using Moq;
using Zaide.App.Composition;
using Zaide.App.Shell;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Tests.Features.ProjectSystem;
using Zaide.Features.Debugging.Application;
using Zaide.Features.Debugging.Presentation;

namespace Zaide.Tests.Features.Debugging.Application;

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