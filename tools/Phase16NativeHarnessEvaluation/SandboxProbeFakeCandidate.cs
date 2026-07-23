using System.Text;
using System.Text.RegularExpressions;

namespace Phase16NativeHarnessEvaluation;

public static class SandboxProbeFakeCandidate
{
    private static readonly Regex SpawnedPidPattern = new(
        @"spawned=(\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static FakeCandidateRunResult Run(FakeCandidateRunRequest request)
    {
        if (request.RunnerConfig is null)
        {
            throw new ManifestValidationException("sandbox_probe requires runner configuration.");
        }

        if (!Phase16SandboxAvailability.IsBubblewrapAvailable())
        {
            throw new InvalidOperationException("Bubblewrap is required for sandbox_probe fake candidates.");
        }

        Phase16CleanupGate.EnsureNotBlockedOrThrow();

        return request.Manifest.FakeCandidate.FakeCandidateId switch
        {
            "probe-argv-env" => RunArgvEnvironmentProbe(request),
            "probe-writable-root" => RunWritableRootProbe(request),
            "probe-traversal" => RunTraversalProbe(request),
            "probe-timeout" => RunTimeoutProbe(request),
            "probe-cancel" => RunCancellationProbe(request),
            "probe-descendant" => RunDescendantProbe(request),
            "probe-output-flood" => RunOutputFloodProbe(request),
            "probe-redaction" => RunRedactionProbe(request),
            "probe-workspace-dirty" => RunWorkspaceDirtyProbe(request),
            _ => throw new ManifestValidationException(
                $"Unknown sandbox_probe id '{request.Manifest.FakeCandidate.FakeCandidateId}'."),
        };
    }

    private static FakeCandidateRunResult RunArgvEnvironmentProbe(FakeCandidateRunRequest request)
    {
        using var trial = CreateTrialContext(request);
        var deniedHostEnv = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PHASE16_FORBIDDEN_ENV"] = "must-not-appear",
        };

        ManifestValidationException? deniedException = null;
        try
        {
            Phase16EnvironmentPolicy.FilterAllowlistedOrThrow(
                deniedHostEnv,
                ["PHASE16_ALLOWED_ENV"]);
        }
        catch (ManifestValidationException ex)
        {
            deniedException = ex;
        }

        if (deniedException is null ||
            !deniedException.Message.Contains("PHASE16_FORBIDDEN_ENV", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected forbidden environment variable rejection.");
        }

        var allowed = Phase16EnvironmentPolicy.FilterAllowlistedOrThrow(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PHASE16_ALLOWED_ENV"] = "allowed-value",
            },
            ["PHASE16_ALLOWED_ENV"]);

        var launch = Phase16BubblewrapLauncher.LaunchAsync(new Phase16SandboxLaunchRequest
        {
            ExecutablePath = "/bin/sh",
            Arguments = ["-c", "printf 'argv=%s env=%s\\n' \"$0\" \"$PHASE16_ALLOWED_ENV\""],
            AllowedEnvironment = allowed,
            WorkingDirectory = trial.WorkspaceRoot,
            WritableRootPaths = [trial.WorkspaceRoot],
            TrialMarker = trial.Marker,
        }).GetAwaiter().GetResult();

        AssertExactArgvContains(launch.ExactArgv, "/bin/sh");
        AssertFalse(launch.AppliedEnvironment.ContainsKey("PHASE16_FORBIDDEN_ENV"));
        AssertEqual(launch.AppliedEnvironment["PHASE16_ALLOWED_ENV"], "allowed-value");

        return SuccessResult(
            request,
            $"probe=argv-env exact_argv={string.Join('|', launch.ExactArgv)}",
            launch);
    }

    private static FakeCandidateRunResult RunWritableRootProbe(FakeCandidateRunRequest request)
    {
        using var trial = CreateTrialContext(request);
        var hostWriteScript = Phase16ProbePaths.ResolveProbeScript("attempt_host_write.sh");
        var launch = Phase16BubblewrapLauncher.LaunchAsync(new Phase16SandboxLaunchRequest
        {
            ExecutablePath = "/bin/sh",
            Arguments = [hostWriteScript],
            AllowedEnvironment = new Dictionary<string, string>(StringComparer.Ordinal),
            WorkingDirectory = trial.WorkspaceRoot,
            WritableRootPaths = [trial.WorkspaceRoot],
            TrialMarker = trial.Marker,
        }).GetAwaiter().GetResult();

        AssertContains(launch.Stdout, "host_write_exit=");
        AssertFalse(File.Exists("/phase16-host-escape-probe.txt"));

        var workspaceTarget = Path.Combine(trial.WorkspaceRoot, "writable.txt");
        File.WriteAllText(workspaceTarget, "seed");
        AssertTrue(File.Exists(workspaceTarget));

        return SuccessResult(request, $"probe=writable-root stdout={launch.Stdout.Trim()}", launch);
    }

    private static FakeCandidateRunResult RunTraversalProbe(FakeCandidateRunRequest request)
    {
        using var trial = CreateTrialContext(request);
        var escapeLink = Path.Combine(trial.WorkspaceRoot, "escape-link");
        if (File.Exists(escapeLink))
        {
            File.Delete(escapeLink);
        }

        File.CreateSymbolicLink(escapeLink, "/etc/passwd");
        ManifestValidationException? symlinkException = null;
        try
        {
            Phase16SymlinkTraversalGuard.RejectSymlinkEscapeOrThrow(trial.WorkspaceRoot, escapeLink);
        }
        catch (ManifestValidationException ex)
        {
            symlinkException = ex;
        }

        if (symlinkException is null ||
            !symlinkException.Message.Contains("escapes workspace root", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected symlink escape rejection.");
        }

        File.Delete(escapeLink);

        var traversalScript = Phase16ProbePaths.ResolveProbeScript("attempt_traversal_write.sh");
        var launch = Phase16BubblewrapLauncher.LaunchAsync(new Phase16SandboxLaunchRequest
        {
            ExecutablePath = "/bin/sh",
            Arguments = [traversalScript, "/etc/phase16-traversal-probe.txt"],
            AllowedEnvironment = new Dictionary<string, string>(StringComparer.Ordinal),
            WorkingDirectory = trial.WorkspaceRoot,
            WritableRootPaths = [trial.WorkspaceRoot],
            TrialMarker = trial.Marker,
        }).GetAwaiter().GetResult();

        AssertContains(launch.Stdout, "traversal_write_exit=");
        AssertFalse(File.Exists("/etc/phase16-traversal-probe.txt"));

        return SuccessResult(request, $"probe=traversal stdout={launch.Stdout.Trim()}", launch);
    }

    private static FakeCandidateRunResult RunTimeoutProbe(FakeCandidateRunRequest request)
    {
        using var trial = CreateTrialContext(request);
        var launch = Phase16BubblewrapLauncher.LaunchAsync(new Phase16SandboxLaunchRequest
        {
            ExecutablePath = "/bin/sleep",
            Arguments = ["30"],
            AllowedEnvironment = new Dictionary<string, string>(StringComparer.Ordinal),
            WorkingDirectory = trial.WorkspaceRoot,
            WritableRootPaths = [trial.WorkspaceRoot],
            WallTimeout = TimeSpan.FromMilliseconds(500),
            TrialMarker = trial.Marker,
        }).GetAwaiter().GetResult();

        AssertTrue(launch.TimedOut);
        AssertFalse(launch.OrphanProcessesDetected);

        return SuccessResult(request, "probe=timeout timed_out=true orphan=false", launch);
    }

    private static FakeCandidateRunResult RunCancellationProbe(FakeCandidateRunRequest request)
    {
        using var trial = CreateTrialContext(request);
        using var cts = new CancellationTokenSource();
        var launchTask = Phase16BubblewrapLauncher.LaunchAsync(
            new Phase16SandboxLaunchRequest
            {
                ExecutablePath = "/bin/sleep",
                Arguments = ["30"],
                AllowedEnvironment = new Dictionary<string, string>(StringComparer.Ordinal),
                WorkingDirectory = trial.WorkspaceRoot,
                WritableRootPaths = [trial.WorkspaceRoot],
                TrialMarker = trial.Marker,
                CancellationToken = cts.Token,
            },
            cts.Token);

        Task.Delay(200).ContinueWith(_ => cts.Cancel(), TaskScheduler.Default);
        var launch = launchTask.GetAwaiter().GetResult();

        AssertTrue(launch.Cancelled);
        AssertFalse(launch.OrphanProcessesDetected);

        return SuccessResult(request, "probe=cancel cancelled=true orphan=false", launch);
    }

    private static FakeCandidateRunResult RunDescendantProbe(FakeCandidateRunRequest request)
    {
        using var trial = CreateTrialContext(request);
        var spawnScript = Phase16ProbePaths.ResolveProbeScript("spawn_descendant.sh");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var launch = Phase16BubblewrapLauncher.LaunchAsync(
            new Phase16SandboxLaunchRequest
            {
                ExecutablePath = "/bin/sh",
                Arguments = [spawnScript, trial.Marker],
                AllowedEnvironment = new Dictionary<string, string>(StringComparer.Ordinal),
                WorkingDirectory = trial.WorkspaceRoot,
                WritableRootPaths = [trial.WorkspaceRoot],
                TrialMarker = trial.Marker,
                CancellationToken = cts.Token,
            },
            cts.Token).GetAwaiter().GetResult();

        var match = SpawnedPidPattern.Match(launch.Stdout);
        AssertTrue(match.Success);
        AssertFalse(launch.OrphanProcessesDetected);

        return SuccessResult(request, "probe=descendant orphan=false", launch);
    }

    private static FakeCandidateRunResult RunOutputFloodProbe(FakeCandidateRunRequest request)
    {
        using var trial = CreateTrialContext(request);
        var floodScript = Phase16ProbePaths.ResolveProbeScript("output_flood.sh");
        var launch = Phase16BubblewrapLauncher.LaunchAsync(new Phase16SandboxLaunchRequest
        {
            ExecutablePath = "/bin/sh",
            Arguments = [floodScript],
            AllowedEnvironment = new Dictionary<string, string>(StringComparer.Ordinal),
            WorkingDirectory = trial.WorkspaceRoot,
            WritableRootPaths = [trial.WorkspaceRoot],
            TrialMarker = trial.Marker,
        }).GetAwaiter().GetResult();

        AssertTrue(launch.StdoutTruncated);
        if (Encoding.UTF8.GetByteCount(launch.Stdout) != CaptureLimits.MaxStdoutBytes)
        {
            throw new InvalidOperationException("Expected stdout capture to reach the byte limit.");
        }

        return SuccessResult(request, "probe=output-flood truncated=true", launch);
    }

    private static FakeCandidateRunResult RunRedactionProbe(FakeCandidateRunRequest request)
    {
        using var trial = CreateTrialContext(request);
        var secretScript = Phase16ProbePaths.ResolveProbeScript("emit_secret.sh");
        var launch = Phase16BubblewrapLauncher.LaunchAsync(new Phase16SandboxLaunchRequest
        {
            ExecutablePath = "/bin/sh",
            Arguments = [secretScript],
            AllowedEnvironment = new Dictionary<string, string>(StringComparer.Ordinal),
            WorkingDirectory = trial.WorkspaceRoot,
            WritableRootPaths = [trial.WorkspaceRoot],
            TrialMarker = trial.Marker,
        }).GetAwaiter().GetResult();

        var redactedStdout = Phase16OutputRedactor.RedactOrThrow(launch.Stdout);
        AssertFalse(redactedStdout.Contains("phase16-test-secret-value", StringComparison.Ordinal));
        AssertFalse(redactedStdout.Contains("phase16-bearer-token-value", StringComparison.Ordinal));
        AssertContains(redactedStdout, "[REDACTED]");

        return SuccessResult(request, $"probe=redaction stdout={redactedStdout.Trim()}", launch);
    }

    private static FakeCandidateRunResult RunWorkspaceDirtyProbe(FakeCandidateRunRequest request)
    {
        using var trial = CreateTrialContext(request);
        var writeScript = Phase16ProbePaths.ResolveProbeScript("write_workspace.sh");
        var launch = Phase16BubblewrapLauncher.LaunchAsync(new Phase16SandboxLaunchRequest
        {
            ExecutablePath = "/bin/sh",
            Arguments = [writeScript, "dirty.txt"],
            AllowedEnvironment = new Dictionary<string, string>(StringComparer.Ordinal),
            WorkingDirectory = trial.WorkspaceRoot,
            WritableRootPaths = [trial.WorkspaceRoot],
            TrialMarker = trial.Marker,
        }).GetAwaiter().GetResult();

        var evidence = Phase16WorkspaceManager.CollectEvidenceOrThrow(
            trial.WorkspaceRoot,
            request.Manifest.FixtureHash,
            trial.FixtureTree);
        AssertTrue(evidence.WorkspaceDirty);
        AssertContains(evidence.ChangedRelativePaths, "dirty.txt");

        var resetSucceeded = Phase16WorkspaceManager.ResetWorkspaceOrThrow(
            trial.WorkspaceRoot,
            trial.FixtureTree);
        var postResetEvidence = Phase16WorkspaceManager.CollectEvidenceOrThrow(
            trial.WorkspaceRoot,
            request.Manifest.FixtureHash,
            trial.FixtureTree);
        AssertTrue(resetSucceeded);
        AssertFalse(postResetEvidence.WorkspaceDirty);

        return SuccessResult(
            request,
            $"probe=workspace-dirty dirty={evidence.WorkspaceDirty} reset={resetSucceeded} inventory={evidence.PostTrialInventoryHash}",
            launch);
    }

    private static FakeCandidateRunResult SuccessResult(
        FakeCandidateRunRequest request,
        string summary,
        Phase16SandboxLaunchResult launch)
    {
        var metrics = new TrialMetrics
        {
            PassCount = 1,
            FailCount = 0,
            DurationMilliseconds = 1,
            TurnCount = 1,
            InputTokenEstimate = summary.Length,
            OutputTokenEstimate = summary.Length,
        };
        TrialMetricValidator.ValidateOrThrow(metrics);

        var stderrSummary = string.Join(';', launch.LifecycleEvents);
        return new FakeCandidateRunResult
        {
            Metrics = metrics,
            Stdout = summary,
            Stderr = stderrSummary,
            StdoutTruncated = false,
            StderrTruncated = false,
            EvidenceClass = "observational",
            InvalidationReasons = Array.Empty<string>(),
        };
    }

    private static TrialContext CreateTrialContext(FakeCandidateRunRequest request)
    {
        var runnerConfig = request.RunnerConfig!;
        var trialId = Guid.NewGuid().ToString("N");
        var trialRoot = Path.Combine(
            runnerConfig.ArtifactRoot,
            "phase-16",
            "artifacts",
            "trials",
            trialId);
        var workspaceRoot = Path.GetFullPath(Path.Combine(trialRoot, "workspace"));
        Directory.CreateDirectory(workspaceRoot);

        var fixtureTree = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["README.md"] = $"fixture={request.Manifest.FixtureHash}",
        };
        Phase16WorkspaceManager.MaterializeFixtureOrThrow(workspaceRoot, fixtureTree);

        return new TrialContext(trialRoot, workspaceRoot, fixtureTree, $"phase16-{trialId}");
    }

    private sealed class TrialContext : IDisposable
    {
        public TrialContext(
            string trialRoot,
            string workspaceRoot,
            IReadOnlyDictionary<string, string> fixtureTree,
            string marker)
        {
            TrialRoot = trialRoot;
            WorkspaceRoot = workspaceRoot;
            FixtureTree = fixtureTree;
            Marker = marker;
        }

        public string TrialRoot { get; }
        public string WorkspaceRoot { get; }
        public IReadOnlyDictionary<string, string> FixtureTree { get; }
        public string Marker { get; }

        public void Dispose()
        {
            try
            {
                Phase16WorkspaceManager.CleanupTrialDirectoryOrThrow(TrialRoot);
            }
            catch (IOException ex)
            {
                Phase16CleanupGate.RecordCleanupFailure(ex.Message);
                throw;
            }
        }
    }

    private static void AssertContains(string haystack, string needle)
    {
        if (!haystack.Contains(needle, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected text to contain '{needle}'.");
        }
    }

    private static void AssertFalse(bool condition)
    {
        if (condition)
        {
            throw new InvalidOperationException("Expected condition to be false.");
        }
    }

    private static void AssertTrue(bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Expected condition to be true.");
        }
    }

    private static void AssertEqual(string actual, string expected)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected '{expected}' but got '{actual}'.");
        }
    }

    private static void AssertExactArgvContains(IReadOnlyList<string> argv, string expectedSegment)
    {
        if (!argv.Any(segment => string.Equals(segment, expectedSegment, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Expected argv to contain '{expectedSegment}'.");
        }
    }

    private static void AssertContains(IReadOnlyList<string> values, string expected)
    {
        if (!values.Contains(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Expected collection to contain '{expected}'.");
        }
    }
}
