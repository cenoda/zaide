namespace Zaide.Services;

/// <summary>
/// Structured debug-session outcome categories for operations and terminal failures.
/// </summary>
public enum DebugSessionOutcomeKind
{
    RejectedConcurrent,
    RejectedContext,
    AdapterUnavailable,
    BuildFailed,
    StartupFailed,
    ProtocolFailed,
    AdapterExited,
    Cancelled,
    StoppedByUser,
}
