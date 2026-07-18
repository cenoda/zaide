using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Features.Debugging.Infrastructure.Dap;

/// <summary>
/// Production factory that launches NetCoreDbg and starts the DAP transport.
/// </summary>
internal sealed class DebugAdapterSessionFactory : IDebugAdapterSessionFactory
{
    /// <inheritdoc />
    public async Task<IDebugAdapterSession> StartAsync(
        DebugAdapterStartOptions options,
        CancellationToken cancellationToken)
    {
        var session = new NetCoreDbgAdapterSession(options);
        await session.ConnectAsync(cancellationToken).ConfigureAwait(false);
        return session;
    }
}
