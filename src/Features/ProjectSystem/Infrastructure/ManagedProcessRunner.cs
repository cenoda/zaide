using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Features.ProjectSystem.Infrastructure;

/// <summary>
/// Production managed-process runner using <see cref="Process"/> with redirected
/// stdout/stderr and entire-process-tree kill on cancel/dispose.
/// </summary>
internal sealed class ManagedProcessRunner : IManagedProcessRunner
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
        var buffer = new char[4096];
        var remainder = new StringBuilder();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int charsRead;
                try
                {
                    charsRead = await reader.ReadAsync(
                        buffer.AsMemory(0, buffer.Length), cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException)
                {
                    break;
                }

                if (charsRead == 0)
                    break;

                remainder.Append(buffer, 0, charsRead);
                EmitCompleteLines(remainder, stream, generation);
            }
        }
        finally
        {
            // Emit any residual data that was never terminated by a newline.
            // ReadLineAsync throws on cancel/IOException, discarding buffered
            // data; this path ensures it is always flushed even on kill/cancel.
            if (remainder.Length > 0)
            {
                OutputReceived?.Invoke(
                    new ManagedProcessOutputLine(
                        generation, stream, remainder.ToString(), DateTimeOffset.UtcNow));
            }
        }
    }

    private void EmitCompleteLines(
        StringBuilder remainder,
        ProcessStreamKind stream,
        long generation)
    {
        int newlineIndex;
        while ((newlineIndex = IndexOfNewline(remainder)) >= 0)
        {
            // Determine line length: exclude \r when it precedes \n.
            int lineEnd = newlineIndex;
            if (newlineIndex > 0 && remainder[newlineIndex - 1] == '\r')
                lineEnd = newlineIndex - 1;

            string line = remainder.ToString(0, lineEnd);
            remainder.Remove(0, newlineIndex + 1);

            OutputReceived?.Invoke(
                new ManagedProcessOutputLine(generation, stream, line, DateTimeOffset.UtcNow));
        }
    }

    private static int IndexOfNewline(StringBuilder sb)
    {
        for (int i = 0; i < sb.Length; i++)
        {
            if (sb[i] == '\n')
                return i;
        }
        return -1;
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
