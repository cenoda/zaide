using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// Terminates the exact owned process tree with bounded wait.
/// </summary>
internal static class AcpProcessTreeTerminator
{
    public static void Terminate(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (NotSupportedException)
        {
        }

        try
        {
            process.WaitForExit((int)AcpProcessLifecycleLimits.ProcessTreeCleanupTimeout.TotalMilliseconds);
        }
        catch (InvalidOperationException)
        {
        }

        process.Dispose();
    }

    public static async Task TerminateAsync(Process? process, CancellationToken cancellationToken)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (NotSupportedException)
        {
        }

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
        }

        process.Dispose();
    }
}
