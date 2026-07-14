using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Services;

/// <summary>
/// Production/test seam for launching one NetCoreDbg adapter session.
/// </summary>
public interface IDebugAdapterSessionFactory
{
    /// <summary>
    /// Spawns the adapter child and returns a connected <see cref="IDebugAdapterSession"/>.
    /// </summary>
    Task<IDebugAdapterSession> StartAsync(
        DebugAdapterStartOptions options,
        CancellationToken cancellationToken);
}
