using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Services;

/// <summary>
/// Structured operational state of the language session service.
/// Status text for UI must be a projection of this state, not the source of truth.
/// </summary>
public enum LanguageSessionState
{
    /// <summary>
    /// No session is active because project context is ineligible
    /// (<see cref="ProjectContextState.Unloaded"/>, <see cref="ProjectContextState.NoProject"/>,
    /// <see cref="ProjectContextState.Unsupported"/>, <see cref="ProjectContextState.Ambiguous"/>,
    /// or <see cref="ProjectContextState.Failed"/>).
    /// </summary>
    Unavailable,

    /// <summary>
    /// Project context is loading or a language session is starting.
    /// </summary>
    Loading,

    /// <summary>
    /// The language server process is running and <c>initialize</c> completed successfully.
    /// </summary>
    Ready,

    /// <summary>
    /// Session start or runtime failed (missing binary, initialize error, process exit).
    /// </summary>
    Failed,

    /// <summary>
    /// The most recent session operation was cancelled before reaching a terminal state.
    /// </summary>
    Cancelled,
}
