using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Features.ProjectSystem.Contracts;

/// <summary>
/// Framework-neutral seam for project-file discovery at a workspace root.
/// </summary>
public interface IProjectDiscovery
{
    /// <summary>
    /// Scans <paramref name="workspaceRoot"/> for supported and known
    /// unsupported project files, and returns a structured result.
    /// </summary>
    /// <param name="workspaceRoot">The directory to scan.</param>
    /// <param name="cancellationToken">Propagates cancellation notification.</param>
    /// <returns>A <see cref="ProjectDiscoveryResult"/> describing what was found.</returns>
    Task<ProjectDiscoveryResult> DiscoverAsync(
        string workspaceRoot,
        CancellationToken cancellationToken = default);
}
