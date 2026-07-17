using System.Collections.Generic;

namespace Zaide.Features.ProjectSystem.Domain;

/// <summary>
/// The structured result of a project-discovery scan, returned by
/// <see cref="IProjectDiscovery.DiscoverAsync"/>.
/// </summary>
/// <param name="SupportedCandidates">
/// Discovered supported project/solution files, sorted by ordinal
/// <c>FilePath</c>. Non-null and duplicate-free. Empty when no supported
/// files were found.
/// </param>
/// <param name="UnsupportedFiles">
/// Full paths of files with known unsupported extensions, sorted by ordinal
/// path. Non-null and duplicate-free. Empty when no known unsupported files
/// were found.
/// </param>
/// <param name="Failure">
/// Non-null when discovery failed due to an I/O or permission error.
/// When non-null, both <see cref="SupportedCandidates"/> and
/// <see cref="UnsupportedFiles"/> must be empty.
/// </param>
public sealed record ProjectDiscoveryResult(
    IReadOnlyList<ProjectCandidate> SupportedCandidates,
    IReadOnlyList<string> UnsupportedFiles,
    ProjectDiscoveryFailure? Failure);
