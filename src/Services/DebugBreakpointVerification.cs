namespace Zaide.Services;

/// <summary>
/// One adapter breakpoint verification result for the active debug generation.
/// </summary>
/// <param name="SourcePath">Normalized absolute source path requested of the adapter.</param>
/// <param name="RequestedLine">One-based line the client requested.</param>
/// <param name="ActualLine">
/// One-based line reported by the adapter when different or confirmed; otherwise the requested line.
/// </param>
/// <param name="State">Verified, pending, or rejected.</param>
/// <param name="Message">Optional adapter message (especially for rejected breakpoints).</param>
public sealed record DebugBreakpointVerification(
    string SourcePath,
    int RequestedLine,
    int ActualLine,
    DebugBreakpointVerificationState State,
    string? Message);
