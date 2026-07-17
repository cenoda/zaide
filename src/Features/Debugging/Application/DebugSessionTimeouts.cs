using System;

namespace Zaide.Features.Debugging.Application;

/// <summary>
/// Locked DAP request and teardown timeouts for Phase 12 M1.
/// </summary>
public static class DebugSessionTimeouts
{
    /// <summary><c>initialize</c> request bound.</summary>
    public static readonly TimeSpan Initialize = TimeSpan.FromSeconds(15);

    /// <summary><c>launch</c>, <c>setBreakpoints</c>, and <c>configurationDone</c> bound.</summary>
    public static readonly TimeSpan LaunchConfiguration = TimeSpan.FromSeconds(15);

    /// <summary>Ordinary stopped-state and execution-control request bound.</summary>
    public static readonly TimeSpan OrdinaryRequest = TimeSpan.FromSeconds(10);

    /// <summary><c>disconnect</c> grace period before adapter process-tree kill.</summary>
    public static readonly TimeSpan Disconnect = TimeSpan.FromSeconds(5);
}
