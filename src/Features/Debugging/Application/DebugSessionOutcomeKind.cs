namespace Zaide.Features.Debugging.Application;

/// <summary>
/// Structured debug-session outcome categories for operations and terminal failures.
/// </summary>
public enum DebugSessionOutcomeKind
{
    RejectedConcurrent,
    RejectedContext,
    AdapterUnavailable,
    UnsupportedLaunchTarget,
    BuildFailed,
    StartupFailed,
    ProtocolFailed,
    AdapterExited,
    Cancelled,
    StoppedByUser,
}
