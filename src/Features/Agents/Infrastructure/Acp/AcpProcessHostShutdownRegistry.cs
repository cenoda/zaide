using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// Tracks active ACP stdio hosts for application shutdown teardown.
/// </summary>
internal static class AcpProcessHostShutdownRegistry
{
    private static readonly HashSet<AcpStdioProcessHost> ActiveHosts = new();
    private static readonly object Gate = new();

    public static void Register(AcpStdioProcessHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        lock (Gate)
        {
            ActiveHosts.Add(host);
        }
    }

    public static void Unregister(AcpStdioProcessHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        lock (Gate)
        {
            ActiveHosts.Remove(host);
        }
    }

    public static void ShutdownAll()
    {
        AcpStdioProcessHost[] snapshot;
        lock (Gate)
        {
            snapshot = new AcpStdioProcessHost[ActiveHosts.Count];
            ActiveHosts.CopyTo(snapshot);
            ActiveHosts.Clear();
        }

        foreach (var host in snapshot)
        {
            try
            {
                host.DisposeAsync().AsTask().Wait(AcpProcessLifecycleLimits.ProcessTreeCleanupTimeout);
            }
            catch
            {
                // Shutdown must continue for remaining hosts.
            }
        }
    }
}
