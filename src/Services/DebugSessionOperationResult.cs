namespace Zaide.Services;

/// <summary>
/// Result of a debug-session command such as launch, stop, or continue.
/// </summary>
/// <param name="Succeeded">Whether the command reached its intended terminal outcome.</param>
/// <param name="Outcome">Structured outcome when <paramref name="Succeeded"/> is false.</param>
/// <param name="Message">Diagnostic message when <paramref name="Succeeded"/> is false.</param>
public sealed record DebugSessionOperationResult(
    bool Succeeded,
    DebugSessionOutcomeKind? Outcome,
    string? Message);
