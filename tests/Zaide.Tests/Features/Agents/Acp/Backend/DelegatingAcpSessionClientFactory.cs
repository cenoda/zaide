using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;

namespace Zaide.Tests.Features.Agents.Acp.Backend;

/// <summary>
/// Test adapter from legacy client factories to <see cref="IAcpSessionClientFactory"/>.
/// </summary>
internal sealed class DelegatingAcpSessionClientFactory : IAcpSessionClientFactory
{
    private readonly Func<CancellationToken, Task<IAcpSessionClient>> _factory;

    public DelegatingAcpSessionClientFactory(Func<CancellationToken, Task<IAcpSessionClient>> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public Task<IAcpSessionClient> CreateAsync(
        AgentBackendExecutionContext context,
        CancellationToken cancellationToken) =>
        _factory(cancellationToken);
}
