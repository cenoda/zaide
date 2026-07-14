using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Services;

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