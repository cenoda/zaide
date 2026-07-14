namespace Zaide.Services;

/// <summary>
/// Structured failure details attached to a <see cref="DebugSessionSnapshot"/>
/// when <see cref="DebugSessionState.Failed"/> is published.
/// </summary>
/// <param name="Kind">The failure category.</param>
/// <param name="Message">A concise, diagnostic message (not presentation-formatted).</param>
public sealed record DebugSessionFailure(
    DebugSessionOutcomeKind Kind,
    string Message);
