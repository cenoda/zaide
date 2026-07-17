using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.App.Composition;
using Zaide.Features.Debugging.Application;

namespace Zaide.Features.ProjectSystem.Contracts;

/// <summary>
/// Orchestrates build-before-debug launch under the shared project-operation gate.
/// </summary>
public interface IProjectDebugLaunchService
{
    /// <summary>
    /// Builds the selected C# project, resolves <c>TargetPath</c>, and starts DAP launch.
    /// </summary>
    Task<DebugSessionOperationResult> StartDebuggingAsync(
        CancellationToken cancellationToken = default);
}