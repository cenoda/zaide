using Zaide.Services;
using Zaide.Features.Editor.Presentation;
using Zaide.Tests.Features.ProjectSystem;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Presentation;

namespace Zaide.Tests.Features.Debugging.Presentation;

/// <summary>
/// Shared factory for idle debug-panel projections in composition tests.
/// </summary>
internal static class TestDebugPanelFactory
{
    public static DebugPanelViewModel Create(IDebugSessionService? debugSession = null)
    {
        debugSession ??= TestOperationGateFactory.CreateIdleDebugSession().Object;
        var stackProjection = new DebugStackProjectionViewModel(debugSession);
        return new DebugPanelViewModel(debugSession, stackProjection);
    }

    public static DebugStackProjectionViewModel CreateStackProjection(IDebugSessionService? debugSession = null)
    {
        debugSession ??= TestOperationGateFactory.CreateIdleDebugSession().Object;
        return new DebugStackProjectionViewModel(debugSession);
    }

    public static DebugCurrentLocationViewModel CreateCurrentLocation(
        EditorTabViewModel editorTabs,
        IDebugSessionService? debugSession = null,
        DebugStackProjectionViewModel? stackProjection = null)
    {
        debugSession ??= TestOperationGateFactory.CreateIdleDebugSession().Object;
        stackProjection ??= new DebugStackProjectionViewModel(debugSession);
        return new DebugCurrentLocationViewModel(debugSession, stackProjection, editorTabs);
    }
}