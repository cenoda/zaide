using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Bounded child-process surface used by the ACP stdio host for testing and production launch.
/// </summary>
internal interface IAcpChildProcess : IAsyncDisposable
{
    int? ProcessId { get; }

    bool HasExited { get; }

    int? ExitCode { get; }

    Stream StandardInput { get; }

    Stream StandardOutput { get; }

    Stream StandardError { get; }

    event EventHandler? Exited;

    Task WaitForExitAsync(CancellationToken cancellationToken);
}
