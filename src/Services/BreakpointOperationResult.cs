namespace Zaide.Services;

/// <summary>
/// Result of a breakpoint mutation such as add, remove, or toggle.
/// </summary>
/// <param name="Succeeded">Whether the operation persisted its intended change.</param>
/// <param name="Outcome">Structured outcome when <paramref name="Succeeded"/> is false.</param>
/// <param name="Message">Diagnostic message when <paramref name="Succeeded"/> is false.</param>
public sealed record BreakpointOperationResult(
    bool Succeeded,
    BreakpointOutcomeKind? Outcome,
    string? Message);