using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Phase16NativeHarnessEvaluation;
using Xunit;

namespace Zaide.Tests.Phase16Evaluation;

[Collection("Phase16Isolation")]
public sealed class Phase16SandboxExecutorTests
{
    [Fact]
    public async Task LaunchAsync_UsesExactArgvWithoutShellInterpolation()
    {
        if (!Phase16SandboxAvailability.IsBubblewrapAvailable())
        {
            return;
        }

        var workspace = CreateWorkspace();
        try
        {
            var request = new Phase16SandboxLaunchRequest
            {
                ExecutablePath = "/bin/sh",
                Arguments = ["-c", "printf 'arg=%s\\n' \"$1\"", "--", "literal-arg"],
                AllowedEnvironment = new Dictionary<string, string>(StringComparer.Ordinal),
                WorkingDirectory = workspace,
                WritableRootPaths = [workspace],
            };

            var result = await Phase16BubblewrapLauncher.LaunchAsync(request);
            Assert.Contains("/usr/bin/bwrap", result.ExactArgv[0], StringComparison.Ordinal);
            Assert.Contains("--", result.ExactArgv);
            Assert.Contains("/bin/sh", result.ExactArgv);
            Assert.DoesNotContain("|", string.Join(string.Empty, result.ExactArgv), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task LaunchAsync_EnforcesWallTimeoutAndOrphanAbsence()
    {
        if (!Phase16SandboxAvailability.IsBubblewrapAvailable())
        {
            return;
        }

        var workspace = CreateWorkspace();
        try
        {
            var marker = $"phase16-timeout-{Guid.NewGuid():N}";
            var result = await Phase16BubblewrapLauncher.LaunchAsync(new Phase16SandboxLaunchRequest
            {
                ExecutablePath = "/bin/sleep",
                Arguments = ["30"],
                AllowedEnvironment = new Dictionary<string, string>(StringComparer.Ordinal),
                WorkingDirectory = workspace,
                WritableRootPaths = [workspace],
                WallTimeout = TimeSpan.FromMilliseconds(500),
                TrialMarker = marker,
            });

            Assert.True(result.TimedOut);
            Assert.False(result.OrphanProcessesDetected);
            Assert.Contains("orphan_absence_verified", result.LifecycleEvents);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task LaunchAsync_CancellationTerminatesProcessTree()
    {
        if (!Phase16SandboxAvailability.IsBubblewrapAvailable())
        {
            return;
        }

        var workspace = CreateWorkspace();
        using var cts = new CancellationTokenSource();
        try
        {
            var marker = $"phase16-cancel-{Guid.NewGuid():N}";
            var launchTask = Phase16BubblewrapLauncher.LaunchAsync(
                new Phase16SandboxLaunchRequest
                {
                    ExecutablePath = "/bin/sleep",
                    Arguments = ["30"],
                    AllowedEnvironment = new Dictionary<string, string>(StringComparer.Ordinal),
                    WorkingDirectory = workspace,
                    WritableRootPaths = [workspace],
                    TrialMarker = marker,
                    CancellationToken = cts.Token,
                },
                cts.Token);

            await Task.Delay(500);
            await cts.CancelAsync();
            var result = await launchTask;

            Assert.True(result.Cancelled);
            Assert.Contains("cancellation_requested", result.LifecycleEvents);
            Assert.False(result.OrphanProcessesDetected);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task LaunchAsync_CapsStdoutCapture()
    {
        if (!Phase16SandboxAvailability.IsBubblewrapAvailable())
        {
            return;
        }

        var workspace = CreateWorkspace();
        var floodScript = Phase16ProbePaths.ResolveProbeScript("output_flood.sh");
        try
        {
            var result = await Phase16BubblewrapLauncher.LaunchAsync(new Phase16SandboxLaunchRequest
            {
                ExecutablePath = "/bin/sh",
                Arguments = [floodScript],
                AllowedEnvironment = new Dictionary<string, string>(StringComparer.Ordinal),
                WorkingDirectory = workspace,
                WritableRootPaths = [workspace],
            });

            Assert.True(result.StdoutTruncated);
            Assert.Equal(CaptureLimits.MaxStdoutBytes, System.Text.Encoding.UTF8.GetByteCount(result.Stdout));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public void FileCaptureLimit_IsBounded()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"phase16-files-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            for (var i = 0; i < Phase16FileCaptureLimits.MaxCapturedFilesPerTrial + 1; i++)
            {
                File.WriteAllText(Path.Combine(directory, $"file-{i:D4}.txt"), "x");
            }

            var count = Phase16WorkspaceManager.CountCapturedFiles(directory);
            Assert.Equal(Phase16FileCaptureLimits.MaxCapturedFilesPerTrial + 1, count);
            Assert.True(count > Phase16FileCaptureLimits.MaxCapturedFilesPerTrial);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateWorkspace()
    {
        var workspace = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"phase16-exec-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(workspace);
        return workspace;
    }
}
