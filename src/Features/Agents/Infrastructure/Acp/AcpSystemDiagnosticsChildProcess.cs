using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// Production child process wrapper owned by the ACP stdio host.
/// </summary>
internal sealed class AcpSystemDiagnosticsChildProcess : IAcpChildProcess
{
    private readonly Process _process;
    private bool _disposed;

    public AcpSystemDiagnosticsChildProcess(Process process)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _process.EnableRaisingEvents = true;
    }

    public int? ProcessId => _process.HasExited ? _process.Id : _process.Id;

    public bool HasExited => _process.HasExited;

    public int? ExitCode => _process.HasExited ? _process.ExitCode : null;

    public Stream StandardInput => _process.StandardInput.BaseStream;

    public Stream StandardOutput => _process.StandardOutput.BaseStream;

    public Stream StandardError => _process.StandardError.BaseStream;

    public event EventHandler? Exited
    {
        add => _process.Exited += value;
        remove => _process.Exited -= value;
    }

    public Task WaitForExitAsync(CancellationToken cancellationToken) =>
        _process.WaitForExitAsync(cancellationToken);

    public Process GetUnderlyingProcess() => _process;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        AcpProcessTreeTerminator.Terminate(_process);
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
