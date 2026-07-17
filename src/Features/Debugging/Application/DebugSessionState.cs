namespace Zaide.Features.Debugging.Application;

/// <summary>
/// Structured operational state of the debug session service.
/// </summary>
public enum DebugSessionState
{
    /// <summary>No debug session is active.</summary>
    Idle,

    /// <summary>Adapter startup and DAP initialize/launch/configuration are in flight.</summary>
    Starting,

    /// <summary>The debuggee is running (not stopped at a breakpoint or entry).</summary>
    Running,

    /// <summary>The debuggee is stopped and stopped-state requests are valid.</summary>
    Stopped,

    /// <summary>Disconnect and adapter teardown are in flight.</summary>
    Stopping,

    /// <summary>Startup, protocol, or adapter-exit failure with a terminal snapshot.</summary>
    Failed,

    /// <summary>
    /// Project context is ineligible for debugging or the adapter cannot be resolved.
    /// </summary>
    Unavailable,
}
