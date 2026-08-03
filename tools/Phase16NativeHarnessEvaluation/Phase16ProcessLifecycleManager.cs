using System.Diagnostics;
using System.Text;

namespace Phase16NativeHarnessEvaluation;

public static class Phase16ProcessLifecycleManager
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Upper bound after a forced tree kill. Without this, a sandbox child that
    /// ignores SIGKILL delivery races (or a hung bwrap) can pin the testhost for
    /// the full remaining sleep duration (historically 30s in cancellation proofs).
    /// </summary>
    private static readonly TimeSpan ForcedExitWaitTimeout = TimeSpan.FromSeconds(2);

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
        var lifecycleEventsGate = new object();
        void AddLifecycleEvent(string lifecycleEvent)
        {
            lock (lifecycleEventsGate)
            {
                lifecycleEvents.Add(lifecycleEvent);
            }
        }

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

        AddLifecycleEvent("process_start_requested");
        process.Start();
        AddLifecycleEvent($"process_started pid={process.Id}");

        using var cancellationRegistration = cancellationToken.Register(
            static state => ((Action)state!).Invoke(),
            (Action)(() => AddLifecycleEvent("cancellation_requested")));

        var stdoutTask = PumpStreamAsync(process.StandardOutput, stdoutBuffer, CancellationToken.None);
        var stderrTask = PumpStreamAsync(process.StandardError, stderrBuffer, CancellationToken.None);

        var timedOut = false;
        var cancelled = false;
        try
        {
            // Prefer WhenAny over WaitForExitAsync(token) alone: under load, some
            // hosts have delayed token delivery to Process.WaitForExitAsync, which
            // previously let cancel/wall proofs run out the full sleep duration.
            var exitTask = process.WaitForExitAsync();
            var cancelTask = Task.Delay(Timeout.InfiniteTimeSpan, linkedCts.Token);
            var completed = await Task.WhenAny(exitTask, cancelTask).ConfigureAwait(false);
            if (completed == exitTask)
            {
                await exitTask.ConfigureAwait(false);
                AddLifecycleEvent($"process_exited exit_code={process.ExitCode}");
            }
            else
            {
                // Observe cancel/timeout path (cancelTask faulted with OCE).
                try
                {
                    await cancelTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
                {
                    cancelled = cancellationToken.IsCancellationRequested;
                    timedOut = !cancelled && request.WallTimeout is not null;
                    if (!cancelled)
                    {
                        AddLifecycleEvent("wall_timeout_reached");
                    }

                    await TerminateProcessTreeAsync(process, AddLifecycleEvent).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                await TerminateProcessTreeAsync(process, AddLifecycleEvent).ConfigureAwait(false);
            }

            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            cancelled = true;
            lock (lifecycleEventsGate)
            {
                if (!lifecycleEvents.Contains("cancellation_requested", StringComparer.Ordinal))
                {
                    lifecycleEvents.Add("cancellation_requested");
                }
            }
        }

        var orphanDetected = DetectOrphanProcesses(
            request.TrialMarker,
            process.Id,
            AddLifecycleEvent);
        if (orphanDetected)
        {
            AddLifecycleEvent("orphan_process_detected");
        }
        else
        {
            AddLifecycleEvent("orphan_absence_verified");
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
            LifecycleEvents = lifecycleEvents.ToArray(),
        };
    }

    private static async Task TerminateProcessTreeAsync(Process process, Action<string> addLifecycleEvent)
    {
        if (process.HasExited)
        {
            addLifecycleEvent("terminate_skipped_already_exited");
            return;
        }

        addLifecycleEvent("terminate_signal_sent");
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            addLifecycleEvent("terminate_signal_failed");
        }

        try
        {
            using var graceCts = new CancellationTokenSource(GracePeriod);
            await process.WaitForExitAsync(graceCts.Token).ConfigureAwait(false);
            addLifecycleEvent("graceful_termination_observed");
            return;
        }
        catch (OperationCanceledException)
        {
            addLifecycleEvent("grace_period_elapsed");
        }

        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            addLifecycleEvent("forced_tree_kill");

            try
            {
                using var forcedWaitCts = new CancellationTokenSource(ForcedExitWaitTimeout);
                await process.WaitForExitAsync(forcedWaitCts.Token).ConfigureAwait(false);
                addLifecycleEvent("forced_termination_observed");
            }
            catch (OperationCanceledException)
            {
                addLifecycleEvent("forced_wait_timeout");
            }
        }
    }

    private static bool DetectOrphanProcesses(
        string? trialMarker,
        int rootPid,
        Action<string> addLifecycleEvent)
    {
        if (string.IsNullOrWhiteSpace(trialMarker) || !OperatingSystem.IsLinux())
        {
            addLifecycleEvent("orphan_scan_skipped");
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
                addLifecycleEvent($"orphan_pid={pid}");
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
