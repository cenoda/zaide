using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Creates one ACP session client for an admitted run using the actor binding.
/// </summary>
internal interface IAcpSessionClientFactory
{
    Task<IAcpSessionClient> CreateAsync(
        AgentBackendExecutionContext context,
        CancellationToken cancellationToken);
}
