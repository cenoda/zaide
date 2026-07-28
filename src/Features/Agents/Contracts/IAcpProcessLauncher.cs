using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Launches an ACP child process without shell interpolation.
/// </summary>
internal interface IAcpProcessLauncher
{
    Task<IAcpChildProcess> StartAsync(
        AcpProcessLaunchOptions options,
        CancellationToken cancellationToken);
}
