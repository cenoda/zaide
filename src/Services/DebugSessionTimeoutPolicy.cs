using System;

namespace Zaide.Services;

/// <summary>
/// Injectable DAP request and teardown timeout policy for <see cref="DebugSessionService"/>.
/// Create with the parameterless constructor for production defaults (15s/10s/5s),
/// or supply custom values for unit tests.
/// </summary>
public sealed class DebugSessionTimeoutPolicy
{
    /// <summary><c>initialize</c> request bound.</summary>
    public TimeSpan Initialize { get; }

    /// <summary>
    /// Bound for <c>launch</c>, <c>setBreakpoints</c>, <c>configurationDone</c>,
    /// and the initial <c>stopped</c> wait.
    /// </summary>
    public TimeSpan LaunchConfiguration { get; }

    /// <summary>Ordinary stopped-state and execution-control request bound.</summary>
    public TimeSpan OrdinaryRequest { get; }

    /// <summary><c>disconnect</c> grace period before adapter process-tree kill.</summary>
    public TimeSpan Disconnect { get; }

    /// <summary>Creates the production timeout policy (15s/15s/10s/5s).</summary>
    public DebugSessionTimeoutPolicy()
    {
        Initialize = DebugSessionTimeouts.Initialize;
        LaunchConfiguration = DebugSessionTimeouts.LaunchConfiguration;
        OrdinaryRequest = DebugSessionTimeouts.OrdinaryRequest;
        Disconnect = DebugSessionTimeouts.Disconnect;
    }

    /// <summary>Creates a policy with caller-supplied timeouts (for tests).</summary>
    public DebugSessionTimeoutPolicy(
        TimeSpan initialize,
        TimeSpan launchConfiguration,
        TimeSpan ordinaryRequest,
        TimeSpan disconnect)
    {
        Initialize = initialize;
        LaunchConfiguration = launchConfiguration;
        OrdinaryRequest = ordinaryRequest;
        Disconnect = disconnect;
    }
}
