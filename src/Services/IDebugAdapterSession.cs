using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Services;

/// <summary>
/// A live NetCoreDbg child process with an initialized Content-Length DAP transport.
/// </summary>
public interface IDebugAdapterSession : IAsyncDisposable
{
    /// <summary>Generation captured when this session was created.</summary>
    long Generation { get; }

    /// <summary>Child adapter process id, or <c>null</c> when not launched.</summary>
    int? ProcessId { get; }

    /// <summary>Whether the child adapter process has exited.</summary>
    bool HasExited { get; }

    /// <summary>Captured adapter stderr lines in arrival order.</summary>
    IReadOnlyList<string> StderrLines { get; }

    /// <summary>
    /// Raised once when the child adapter process exits. The argument is <see cref="Generation"/>.
    /// </summary>
    event Action<long>? ProcessExited;

    /// <summary>Raised when the adapter sends a <c>stopped</c> DAP event.</summary>
    event Action<DapStoppedEvent>? Stopped;

    /// <summary>Raised when the adapter sends a <c>continued</c> DAP event.</summary>
    event Action<DapContinuedEvent>? Continued;

    /// <summary>Raised when the adapter sends an <c>output</c> DAP event.</summary>
    event Action<DapOutputEvent>? Output;

    /// <summary>Raised when the adapter sends a <c>terminated</c> DAP event.</summary>
    event Action<long>? Terminated;

    /// <summary>Raised when the adapter sends an <c>exited</c> DAP event.</summary>
    event Action<DapExitedEvent>? Exited;

    /// <summary>Spawns the adapter and starts the Content-Length transport.</summary>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Issues the DAP <c>initialize</c> request.</summary>
    Task<JsonElement?> InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Issues the DAP <c>launch</c> request.</summary>
    Task LaunchAsync(
        string programPath,
        string workingDirectory,
        bool stopAtEntry,
        CancellationToken cancellationToken);

    /// <summary>Issues the DAP <c>setBreakpoints</c> request for one source file.</summary>
    Task<JsonElement?> SetBreakpointsAsync(
        string sourcePath,
        IReadOnlyList<int> lines,
        CancellationToken cancellationToken);

    /// <summary>Issues the DAP <c>configurationDone</c> request.</summary>
    Task<JsonElement?> ConfigurationDoneAsync(CancellationToken cancellationToken);

    /// <summary>Issues the DAP <c>threads</c> request.</summary>
    Task<JsonElement?> RequestThreadsAsync(CancellationToken cancellationToken);

    /// <summary>Issues the DAP <c>stackTrace</c> request.</summary>
    Task<JsonElement?> RequestStackTraceAsync(int threadId, CancellationToken cancellationToken);

    /// <summary>Issues the DAP <c>scopes</c> request.</summary>
    Task<JsonElement?> RequestScopesAsync(int frameId, CancellationToken cancellationToken);

    /// <summary>Issues the DAP <c>continue</c> request.</summary>
    Task ContinueAsync(int threadId, CancellationToken cancellationToken);

    /// <summary>Graceful DAP <c>disconnect</c> with terminating debuggee.</summary>
    Task DisconnectAsync(CancellationToken cancellationToken);

    /// <summary>Force-kill the adapter process tree without protocol shutdown.</summary>
    Task ForceKillAsync();
}
