using System;
using System.Collections.Generic;
using Zaide.Features.Debugging.Infrastructure.Dap;

namespace Zaide.Features.Debugging.Application;

/// <summary>
/// An immutable snapshot of the current debug session state.
/// </summary>
/// <param name="State">The current operational state.</param>
/// <param name="Generation">
/// Monotonically increasing session generation. Consumers discard async results
/// when their captured generation does not match the current snapshot.
/// </param>
/// <param name="ProgramPath">Launched program path when known, otherwise <c>null</c>.</param>
/// <param name="WorkingDirectory">Launch working directory when known, otherwise <c>null</c>.</param>
/// <param name="AdapterProcessId">
/// Child adapter process id when a session handle is live, otherwise <c>null</c>.
/// </param>
/// <param name="StopInfo">
/// Most recent stopped-thread details when <see cref="State"/> is
/// <see cref="DebugSessionState.Stopped"/>; otherwise <c>null</c>.
/// </param>
/// <param name="Failure">
/// Non-null only when <see cref="State"/> is <see cref="DebugSessionState.Failed"/>.
/// </param>
/// <param name="LastOutcome">
/// Terminal outcome for the most recently ended session when idle; not a failure.
/// </param>
/// <param name="DiagnosticOutput">
/// Captured adapter stderr and DAP <c>output</c> event text in arrival order.
/// </param>
/// <param name="BreakpointVerifications">
/// Session-only adapter breakpoint verification outcomes for the active generation.
/// Empty when no session has published verification data.
/// </param>
public sealed record DebugSessionSnapshot(
    DebugSessionState State,
    long Generation,
    string? ProgramPath,
    string? WorkingDirectory,
    int? AdapterProcessId,
    DapStoppedInfo? StopInfo,
    DebugSessionFailure? Failure,
    DebugSessionOutcomeKind? LastOutcome,
    IReadOnlyList<string> DiagnosticOutput,
    IReadOnlyList<DebugBreakpointVerification> BreakpointVerifications)
{
    /// <summary>Empty verification list shared by idle/failed/unavailable snapshots.</summary>
    public static readonly IReadOnlyList<DebugBreakpointVerification> EmptyVerifications =
        Array.Empty<DebugBreakpointVerification>();
}
