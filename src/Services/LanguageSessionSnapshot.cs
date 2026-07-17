using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Services;

/// <summary>
/// An immutable snapshot of the current language session state.
/// </summary>
/// <param name="State">The current operational state.</param>
/// <param name="Generation">
/// Monotonically increasing session generation. Consumers discard async results
/// when their captured generation does not match the current snapshot.
/// </param>
/// <param name="ProjectFilePath">
/// Absolute path of the winning <see cref="ProjectCandidate.FilePath"/> when
/// eligible, otherwise <c>null</c>.
/// </param>
/// <param name="WorkspaceFolderPath">
/// Parent directory used as the LSP workspace folder when eligible, otherwise <c>null</c>.
/// </param>
/// <param name="ServerProcessId">
/// Child process id when a session handle is live, otherwise <c>null</c>.
/// </param>
/// <param name="Failure">
/// Non-null only when <see cref="State"/> is <see cref="LanguageSessionState.Failed"/>.
/// </param>
public sealed record LanguageSessionSnapshot(
    LanguageSessionState State,
    long Generation,
    string? ProjectFilePath,
    string? WorkspaceFolderPath,
    int? ServerProcessId,
    LanguageSessionFailure? Failure);
