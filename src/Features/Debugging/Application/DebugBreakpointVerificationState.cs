namespace Zaide.Features.Debugging.Application;

/// <summary>
/// Session-only adapter verification outcome for one requested breakpoint.
/// Does not alter persisted user breakpoint intent.
/// </summary>
public enum DebugBreakpointVerificationState
{
    /// <summary>Adapter has not yet answered, or no verification is available.</summary>
    Pending,

    /// <summary>Adapter accepted and verified the breakpoint.</summary>
    Verified,

    /// <summary>Adapter rejected the breakpoint or reported it unverified with a failure message.</summary>
    Rejected,
}
