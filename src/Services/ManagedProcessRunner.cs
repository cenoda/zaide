using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Services;

/// <summary>
/// Production managed-process runner using <see cref="Process"/> with redirected
/// stdout/stderr and entire-process-tree kill on cancel/dispose.
/// </summary>
public sealed class ManagedProcessRunner : IManagedProcessRunner
{
    private static readonly TimeSpan KillWaitTimeout = TimeSpan.FromSeconds(5);

    private readonly object _sync = new();
    private Process? _process;
    private bool _disposed;

    /// <inheritdoc />
    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _process is { HasExited: false };
            }
        }
    }

    /// <inheritdoc />
    public int? ProcessId
    {
        get
        {
            lock (_sync)
            {
                return _process?.HasExited == false ? _process.Id : _process?.Id;
            }
        }
    }

    /// <inheritdoc />
    public event Action<ManagedProcessOutputLine>? OutputReceived;

    /// <inheritdoc />
    public event Action? ProcessStarted;

    /// <inheritdoc />
    public async Task<ManagedProcessRunResult> RunAsync(
        ManagedProcessStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_sync)
        {
            if (_process is { HasExited: false })
                throw new InvalidOperationException("A managed process is already running.");
        }

        Process? process = null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = request.FileName,
                Arguments = request.Arguments,
                WorkingDirectory = request.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true,
            };

            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _process = process;
            }

            if (!process.Start())
                return new ManagedProcessRunResult(null, false, StartupFailed: true);

            ProcessStarted?.Invoke();

            var stdoutTask = PumpStreamAsync(
                process.StandardOutput,
                ProcessStreamKind.StdOut,
                request.Generation,
                cancellationToken);
            var stderrTask = PumpStreamAsync(
                process.StandardError,
                ProcessStreamKind.StdErr,
                request.Generation,
                cancellationToken);

            using var registration = cancellationToken.Register(() => KillProcessTree(process));

            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                KillProcessTree(process);
                await WaitForExitBestEffortAsync(process).ConfigureAwait(false);
                return new ManagedProcessRunResult(
                    process.HasExited ? process.ExitCode : null,
                    WasCancelled: true,
                    StartupFailed: false);
            }

            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

            return new ManagedProcessRunResult(
                process.ExitCode,
                WasCancelled: false,
                StartupFailed: false);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            return new ManagedProcessRunResult(
                process?.HasExited == true ? process.ExitCode : null,
                WasCancelled: true,
                StartupFailed: false);
        }
        catch (Win32Exception)
        {
            return new ManagedProcessRunResult(null, false, StartupFailed: true);
        }
        catch (InvalidOperationException)
        {
            return new ManagedProcessRunResult(null, false, StartupFailed: true);
        }
        catch (IOException)
        {
            return new ManagedProcessRunResult(null, false, StartupFailed: true);
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_process, process))
                {
                    _process?.Dispose();
                    _process = null;
                }
            }
        }
    }

    /// <inheritdoc />
    public Task KillAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Process? process;
        lock (_sync)
        {
            process = _process;
        }

        KillProcessTree(process);
        return WaitForExitBestEffortAsync(process);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        Process? process;
        lock (_sync)
        {
            process = _process;
            _process = null;
        }

        KillProcessTree(process);

        try
        {
            WaitForExitBestEffortAsync(process).GetAwaiter().GetResult();
        }
        catch
        {
            // Best effort during app shutdown.
        }

        process?.Dispose();
    }

    private async Task PumpStreamAsync(
        StreamReader reader,
        ProcessStreamKind stream,
        long generation,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                break;
            }

            if (line is null)
                break;

            OutputReceived?.Invoke(
                new ManagedProcessOutputLine(generation, stream, line, DateTimeOffset.UtcNow));
        }
    }

    private static void KillProcessTree(Process? process)
    {
        if (process is null || process.HasExited)
            return;

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Process may already be gone.
        }
    }

    private static async Task WaitForExitBestEffortAsync(Process? process)
    {
        if (process is null || process.HasExited)
            return;

        try
        {
            await process.WaitForExitAsync().WaitAsync(KillWaitTimeout).ConfigureAwait(false);
        }
        catch
        {
            // Best effort.
        }
    }
}
