using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Features.Terminal.Contracts;

/// <summary>
/// Abstracts a terminal emulator backed by a real PTY and child process.
/// Implementations are OS-specific; consumers (ViewModels) never touch native
/// interop directly.
///
/// <para><b>Threading contract:</b> Events are raised on an internal reader
/// thread, not the UI thread. Subscribers that touch Avalonia controls must
/// marshal to <c>Dispatcher.UIThread</c> themselves.</para>
/// </summary>
public interface ITerminalService : IDisposable
{
    /// <summary>
    /// Fired when the child process writes output. The byte array is a fresh
    /// copy and safe to hold onto. Raised on the reader thread.
    /// </summary>
    event Action<byte[]>? OutputReceived;

    /// <summary>
    /// Fired once when the child process exits. Raised on the reader thread.
    /// </summary>
    event Action? ProcessExited;

    /// <summary>
    /// Allocates a PTY and spawns the requested shell. No-op if already
    /// running. Throws on native syscall failure so callers can surface
    /// startup errors.
    /// </summary>
    Task StartAsync(string shell = "/bin/bash", CancellationToken ct = default);

    /// <summary>
    /// Writes raw bytes to the PTY master. Safe no-op if the terminal is not
    /// running. Serializes concurrent writes internally.
    /// </summary>
    Task WriteAsync(byte[] data, CancellationToken ct = default);

    /// <summary>
    /// Terminates the current terminal session. Safe no-op if the terminal is
    /// not running.
    /// </summary>
    Task StopAsync(CancellationToken ct = default);

    /// <summary>
    /// Notifies the PTY that the viewport size has changed. Safe no-op if the
    /// terminal is not running.
    /// </summary>
    void Resize(int columns, int rows);

    /// <summary>
    /// Whether the child process is still alive.
    /// </summary>
    bool IsRunning { get; }
}
