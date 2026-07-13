namespace Zaide.Services;

/// <summary>
/// Structured failure details attached to a <see cref="LanguageSessionSnapshot"/>
/// when <see cref="LanguageSessionState.Failed"/> is published.
/// </summary>
/// <param name="Kind">The failure category.</param>
/// <param name="Message">A concise, diagnostic message (not presentation-formatted).</param>
public sealed record LanguageSessionFailure(
    LanguageSessionFailureKind Kind,
    string Message);
