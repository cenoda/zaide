using System.Collections.Generic;

namespace Zaide.Features.ProjectSystem.Domain;

/// <summary>
/// An immutable snapshot of the current project context.
/// </summary>
/// <param name="State">The current operational state.</param>
/// <param name="WorkspaceRoot">
/// The canonical full path being discovered, or <c>null</c> when
/// <see cref="State"/> is <see cref="ProjectContextState.Unloaded"/>.
/// </param>
/// <param name="Candidates">
/// Supported project/solution files discovered at the root, sorted by
/// ordinal full path. Empty when no supported files exist. Never null.
/// </param>
/// <param name="SelectedProject">
/// The auto-selected or user-selected candidate, or <c>null</c> when no
/// selection exists. When non-null, it is always one of <see cref="Candidates"/>.
/// </param>
/// <param name="UnsupportedFiles">
/// Full paths of files with known unsupported extensions, sorted by ordinal
/// path. Empty when no unsupported files exist. Never null.
/// </param>
/// <param name="ErrorMessage">
/// Non-null only when <see cref="State"/> is <see cref="ProjectContextState.Failed"/>.
/// Cancellation never writes into this field.
/// </param>
public sealed record ProjectContext(
    ProjectContextState State,
    string? WorkspaceRoot,
    IReadOnlyList<ProjectCandidate> Candidates,
    ProjectCandidate? SelectedProject,
    IReadOnlyList<string> UnsupportedFiles,
    string? ErrorMessage);
