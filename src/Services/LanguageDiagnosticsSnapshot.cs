using System;
using System.Collections.Generic;

namespace Zaide.Services;

/// <summary>
/// Immutable snapshot of structured diagnostics plus session projection state.
/// </summary>
/// <param name="State">
/// Operational projection of the language session that owns these diagnostics.
/// </param>
/// <param name="SessionGeneration">Generation of the session this snapshot was built from.</param>
/// <param name="Failure">
/// Non-null only when <see cref="State"/> is <see cref="LanguageSessionState.Failed"/>.
/// </param>
/// <param name="Diagnostics">
/// All accepted diagnostics for currently open documents in the current generation.
/// </param>
public sealed record LanguageDiagnosticsSnapshot(
    LanguageSessionState State,
    long SessionGeneration,
    LanguageSessionFailure? Failure,
    IReadOnlyList<LanguageDiagnostic> Diagnostics)
{
    /// <summary>Empty unavailable snapshot for service construction.</summary>
    public static LanguageDiagnosticsSnapshot Empty { get; } = new(
        LanguageSessionState.Unavailable,
        SessionGeneration: 0,
        Failure: null,
        Diagnostics: Array.Empty<LanguageDiagnostic>());
}
