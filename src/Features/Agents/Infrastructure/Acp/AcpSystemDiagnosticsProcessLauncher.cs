using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// Launches ACP child processes through <see cref="System.Diagnostics.Process"/>.
/// </summary>
internal sealed class AcpSystemDiagnosticsProcessLauncher : IAcpProcessLauncher
{
    public Task<IAcpChildProcess> StartAsync(
        AcpProcessLaunchOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = options.FileName,
            WorkingDirectory = options.WorkingDirectory ?? Environment.CurrentDirectory,
        };

        foreach (var argument in options.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        AcpProcessEnvironmentPolicy.Apply(startInfo, options);

        var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            process.Dispose();
            throw new AcpProcessLifecycleException(
                AcpProcessLifecycleFailureKind.ProcessExit,
                "ACP child process failed to start.");
        }

        return Task.FromResult<IAcpChildProcess>(new AcpSystemDiagnosticsChildProcess(process));
    }
}
