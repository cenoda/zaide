using System.Diagnostics;
using System.Text;

namespace Phase16NativeHarnessEvaluation;

public static class Phase16ProcessLifecycleManager
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromMilliseconds(250);

    public static async Task<Phase16SandboxLaunchResult> RunAsync(
        ProcessStartInfo startInfo,
        Phase16SandboxLaunchRequest request,
        CancellationToken cancellationToken)
    {
        if (!Phase16SandboxAvailability.IsBubblewrapAvailable())
        {
            throw new InvalidOperationException("Bubblewrap is required for sandbox lifecycle proof.");
        }

        var lifecycleEvents = new List<string>();
        var stdoutBuffer = new StreamCaptureBuffer(CaptureLimits.MaxStdoutBytes);
        var stderrBuffer = new StreamCaptureBuffer(CaptureLimits.MaxStderrBytes);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (request.WallTimeout is { } wallTimeout)
        {
            linkedCts.CancelAfter(wallTimeout);
        }

        using var process = new Process { StartInfo = startInfo };
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        lifecycleEvents.Add("process_start_requested");
        process.Start();
        lifecycleEvents.Add($"process_started pid={process.Id}");

        var stdoutTask = PumpStreamAsync(process.StandardOutput, stdoutBuffer, CancellationToken.None);
        var stderrTask = PumpStreamAsync(process.StandardError, stderrBuffer, CancellationToken.None);

        var timedOut = false;
        var cancelled = false;
        try
        {
            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
            lifecycleEvents.Add($"process_exited exit_code={process.ExitCode}");
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
            cancelled = cancellationToken.IsCancellationRequested;
            timedOut = !cancelled && request.WallTimeout is not null;
            lifecycleEvents.Add(cancelled ? "cancellation_requested" : "wall_timeout_reached");
            await TerminateProcessTreeAsync(process, lifecycleEvents).ConfigureAwait(false);
        }

        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested)
        {
            cancelled = true;
        }

        var orphanDetected = DetectOrphanProcesses(request.TrialMarker, process.Id, lifecycleEvents);
        if (orphanDetected)
        {
            lifecycleEvents.Add("orphan_process_detected");
        }
        else
        {
            lifecycleEvents.Add("orphan_absence_verified");
        }

        return new Phase16SandboxLaunchResult
        {
            ExitCode = process.HasExited ? process.ExitCode : -1,
            TimedOut = timedOut,
            Cancelled = cancelled,
            Stdout = stdoutBuffer.GetCapturedText(),
            Stderr = stderrBuffer.GetCapturedText(),
            StdoutTruncated = stdoutBuffer.Truncated,
            StderrTruncated = stderrBuffer.Truncated,
            ExactArgv = BuildExactArgv(startInfo),
            AppliedEnvironment = ReadAppliedEnvironment(startInfo),
            OrphanProcessesDetected = orphanDetected,
            LifecycleEvents = lifecycleEvents,
        };
    }

    private static async Task TerminateProcessTreeAsync(Process process, List<string> lifecycleEvents)
    {
        if (process.HasExited)
        {
            lifecycleEvents.Add("terminate_skipped_already_exited");
            return;
        }

        lifecycleEvents.Add("terminate_signal_sent");
        try
        {
            process.Kill(entireProcessTree: false);
        }
        catch (InvalidOperationException)
        {
            lifecycleEvents.Add("terminate_signal_failed");
        }

        try
        {
            using var graceCts = new CancellationTokenSource(GracePeriod);
            await process.WaitForExitAsync(graceCts.Token).ConfigureAwait(false);
            lifecycleEvents.Add("graceful_termination_observed");
            return;
        }
        catch (OperationCanceledException)
        {
            lifecycleEvents.Add("grace_period_elapsed");
        }

        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            lifecycleEvents.Add("forced_tree_kill");
            await process.WaitForExitAsync().ConfigureAwait(false);
        }
    }

    private static bool DetectOrphanProcesses(string? trialMarker, int rootPid, List<string> lifecycleEvents)
    {
        if (string.IsNullOrWhiteSpace(trialMarker) || !OperatingSystem.IsLinux())
        {
            lifecycleEvents.Add("orphan_scan_skipped");
            return false;
        }

        foreach (var procDir in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(procDir), out var pid))
            {
                continue;
            }

            if (pid == rootPid)
            {
                continue;
            }

            var cmdlinePath = Path.Combine(procDir, "cmdline");
            if (!File.Exists(cmdlinePath))
            {
                continue;
            }

            var cmdline = File.ReadAllText(cmdlinePath);
            if (cmdline.Contains(trialMarker, StringComparison.Ordinal))
            {
                lifecycleEvents.Add($"orphan_pid={pid}");
                return true;
            }
        }

        return false;
    }

    private static async Task PumpStreamAsync(
        StreamReader reader,
        StreamCaptureBuffer buffer,
        CancellationToken cancellationToken)
    {
        var chunk = new char[4096];
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var read = await reader.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                buffer.Append(new string(chunk, 0, read));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private static IReadOnlyList<string> BuildExactArgv(ProcessStartInfo startInfo)
    {
        var argv = new List<string> { startInfo.FileName };
        argv.AddRange(startInfo.ArgumentList);
        return argv;
    }

    private static IReadOnlyDictionary<string, string> ReadAppliedEnvironment(ProcessStartInfo startInfo)
    {
        var environment = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in startInfo.EnvironmentVariables)
        {
            var key = (string)entry.Key;
            environment[key] = entry.Value?.ToString() ?? string.Empty;
        }

        return environment;
    }
}
