using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Services;

/// <summary>
/// Starts one redirected child process at a time and kills the entire process
/// tree on cancel or dispose. Not a PTY terminal host.
/// </summary>
public interface IManagedProcessRunner : IDisposable
{
    /// <summary><c>true</c> while a child process is active.</summary>
    bool IsRunning { get; }

    /// <summary>Active child process id, if any.</summary>
    int? ProcessId { get; }

    /// <summary>
    /// Raised for each stdout/stderr line read from the active process.
    /// </summary>
    event Action<ManagedProcessOutputLine>? OutputReceived;

    /// <summary>
    /// Starts the requested process and waits until it exits or
    /// <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    Task<ManagedProcessRunResult> RunAsync(
        ManagedProcessStartRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kills the entire active process tree. Safe when no process is running.
    /// </summary>
    Task KillAsync();
}
