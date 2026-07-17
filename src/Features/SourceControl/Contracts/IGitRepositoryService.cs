using Zaide.Features.SourceControl.Application;

namespace Zaide.Features.SourceControl.Contracts;

/// <summary>
/// Read-only git repository seam. Discovers the repository root from a path and
/// reads branch/HEAD + working-tree status. No mutation operations (stage,
/// unstage, commit) are exposed; this is read-only for all of Phase 7.1.
/// </summary>
public interface IGitRepositoryService
{
    /// <summary>
    /// Discovers the repository root by walking upward from <paramref name="startingPath"/>.
    /// Returns a <see cref="RepositoryDiscoveryResult"/> with <see cref="RepositoryDiscoveryResult.IsRepository"/>
    /// false (not an exception) when the path is not inside a git repository.
    /// </summary>
    RepositoryDiscoveryResult Discover(string startingPath);

    /// <summary>
    /// Reads branch/HEAD state and working-tree status for an already-discovered
    /// repository root. Throws only on unexpected IO/permission failures, never on
    /// "not a repo" — callers must discover first.
    /// </summary>
    RepositoryStatusSnapshot ReadStatus(string repositoryRoot);
}