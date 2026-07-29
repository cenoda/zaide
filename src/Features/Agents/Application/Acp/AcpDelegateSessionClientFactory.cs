using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;

namespace Zaide.Features.Agents.Application.Acp;

/// <summary>
/// Adapts legacy test factories to <see cref="IAcpSessionClientFactory"/>.
/// </summary>
internal sealed class AcpDelegateSessionClientFactory : IAcpSessionClientFactory
{
    private readonly Func<CancellationToken, Task<IAcpSessionClient>> _factory;

    public AcpDelegateSessionClientFactory(Func<CancellationToken, Task<IAcpSessionClient>> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public Task<IAcpSessionClient> CreateAsync(
        AgentBackendExecutionContext context,
        CancellationToken cancellationToken) =>
        _factory(cancellationToken);
}
