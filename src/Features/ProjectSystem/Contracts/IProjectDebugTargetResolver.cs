using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Features.ProjectSystem.Contracts;

/// <summary>
/// Resolves one debug launch target from a built C# project via MSBuild
/// <c>TargetPath</c> property query.
/// </summary>
public interface IProjectDebugTargetResolver
{
    /// <summary>
    /// Queries <c>dotnet msbuild &lt;csproj&gt; -getProperty:TargetPath</c> and
    /// accepts exactly one non-empty normalized absolute existing <c>.dll</c> path.
    /// </summary>
    Task<ProjectDebugTargetResolution> ResolveTargetPathAsync(
        string absoluteCsprojPath,
        CancellationToken cancellationToken = default);
}