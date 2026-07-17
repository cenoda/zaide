using Zaide.Services;
using Zaide.Features.Debugging.Application;

namespace Zaide.ViewModels;

/// <summary>
/// One persisted breakpoint marker projected into the active editor margin,
/// optionally annotated with session-only adapter verification state.
/// </summary>
/// <param name="Line">One-based source line (persisted intent).</param>
/// <param name="Enabled">Whether the breakpoint is enabled (persisted intent).</param>
/// <param name="Verification">
/// Adapter verification for the active session, or <c>null</c> when no session
/// verification applies (idle / no matching reply).
/// </param>
/// <param name="AdapterMessage">Optional adapter message for rejected/pending breakpoints.</param>
public sealed record EditorBreakpointMarker(
    int Line,
    bool Enabled,
    DebugBreakpointVerificationState? Verification = null,
    string? AdapterMessage = null);
