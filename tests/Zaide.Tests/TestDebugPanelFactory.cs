using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests;

/// <summary>
/// Shared factory for idle debug-panel projections in composition tests.
/// </summary>
internal static class TestDebugPanelFactory
{
    public static DebugPanelViewModel Create(IDebugSessionService? debugSession = null)
    {
        debugSession ??= TestOperationGateFactory.CreateIdleDebugSession().Object;
        return new DebugPanelViewModel(debugSession);
    }
}