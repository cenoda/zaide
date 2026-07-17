using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Infrastructure;

namespace Zaide.Tests.Features.ProjectSystem.Infrastructure;

/// <summary>
/// Phase 11 M1 tests for <see cref="ManagedProcessRunner"/> redirected I/O and
/// process-tree kill behavior.
/// </summary>
public sealed class ManagedProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_DotnetVersion_CapturesStdoutAndExitsZero()
    {
        using var runner = new ManagedProcessRunner();
        var lines = new List<ManagedProcessOutputLine>();

        runner.OutputReceived += lines.Add;

        var result = await runner.RunAsync(
            new ManagedProcessStartRequest(
                "dotnet",
                "--version",
                Environment.CurrentDirectory,
                Generation: 1));

        Assert.False(result.StartupFailed);
        Assert.False(result.WasCancelled);
        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.Equal(ProcessStreamKind.StdOut, line.Stream));
        Assert.False(runner.IsRunning);
    }

    [Fact]
    public async Task RunAsync_MissingExecutable_ReturnsStartupFailed()
    {
        using var runner = new ManagedProcessRunner();

        var result = await runner.RunAsync(
            new ManagedProcessStartRequest(
                "zaide-nonexistent-executable",
                "",
                Environment.CurrentDirectory,
                Generation: 2));

        Assert.True(result.StartupFailed);
        Assert.Null(result.ExitCode);
        Assert.False(result.WasCancelled);
    }

    [Fact]
    public async Task RunAsync_Cancellation_KillsProcessTree()
    {
        using var runner = new ManagedProcessRunner();
        using var cts = new CancellationTokenSource();

        var runTask = runner.RunAsync(
            new ManagedProcessStartRequest(
                "/bin/sleep",
                "120",
                Environment.CurrentDirectory,
                Generation: 3),
            cts.Token);

        await WaitUntilAsync(() => runner.IsRunning, TimeSpan.FromSeconds(5));
        await cts.CancelAsync();

        var result = await runTask;

        Assert.True(result.WasCancelled);
        Assert.False(runner.IsRunning);
    }

    [Fact]
    public async Task KillAsync_TerminatesActiveProcess()
    {
        using var runner = new ManagedProcessRunner();

        var runTask = runner.RunAsync(
            new ManagedProcessStartRequest(
                "/bin/sleep",
                "120",
                Environment.CurrentDirectory,
                Generation: 4));

        await WaitUntilAsync(() => runner.IsRunning, TimeSpan.FromSeconds(5));
        await runner.KillAsync();

        var result = await runTask;

        Assert.False(runner.IsRunning);
        Assert.NotNull(result.ExitCode);
        Assert.False(result.StartupFailed);
    }

    [Fact]
    public async Task Dispose_KillsActiveProcess()
    {
        var runner = new ManagedProcessRunner();

        var runTask = runner.RunAsync(
            new ManagedProcessStartRequest(
                "/bin/sleep",
                "120",
                Environment.CurrentDirectory,
                Generation: 5));

        await WaitUntilAsync(() => runner.IsRunning, TimeSpan.FromSeconds(5));
        runner.Dispose();

        var result = await runTask;

        Assert.NotNull(result.ExitCode);
        Assert.False(result.StartupFailed);
    }

    [Fact]
    public async Task RunAsync_SecondStartWhileRunning_Throws()
    {
        using var runner = new ManagedProcessRunner();

        var first = runner.RunAsync(
            new ManagedProcessStartRequest(
                "/bin/sleep",
                "5",
                Environment.CurrentDirectory,
                Generation: 6));

        await WaitUntilAsync(() => runner.IsRunning, TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await runner.RunAsync(
                new ManagedProcessStartRequest(
                    "/bin/sleep",
                    "1",
                    Environment.CurrentDirectory,
                    Generation: 7)));

        await first;
    }

    [Fact]
    public async Task RunAsync_UnterminatedFinalLineAfterCancel_EmitsResidualLine()
    {
        using var runner = new ManagedProcessRunner();
        var lines = new List<ManagedProcessOutputLine>();
        using var cts = new CancellationTokenSource();

        runner.OutputReceived += lines.Add;

        // Write "unterminated" (no \n) then sleep; cancel while process is alive.
        // The pump task has buffered data without a trailing newline; on cancel,
        // ReadLineAsync drops the buffer.  After the fix, the residual must appear.
        var runTask = runner.RunAsync(
            new ManagedProcessStartRequest(
                "/bin/sh",
                "-c \"printf 'unterminated'; sleep 120\"",
                Environment.CurrentDirectory,
                Generation: 42),
            cts.Token);

        await WaitUntilAsync(() => runner.IsRunning, TimeSpan.FromSeconds(5));
        await cts.CancelAsync();

        var result = await runTask;

        Assert.True(result.WasCancelled);
        Assert.Contains(lines, l => l.Text == "unterminated");
    }

    [Fact]
    public async Task RunAsync_ExitsWithoutTrailingNewline_CapturesFinalLine()
    {
        using var runner = new ManagedProcessRunner();
        var lines = new List<ManagedProcessOutputLine>();

        runner.OutputReceived += lines.Add;

        var result = await runner.RunAsync(
            new ManagedProcessStartRequest(
                "/bin/sh",
                "-c \"printf 'line1\\nline2\\nfinal-without-newline'\"",
                Environment.CurrentDirectory,
                Generation: 43));

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.StartupFailed);
        Assert.False(result.WasCancelled);
        Assert.Contains(lines, l => l.Text == "line1");
        Assert.Contains(lines, l => l.Text == "line2");
        Assert.Contains(lines, l => l.Text == "final-without-newline");
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
                return;

            await Task.Delay(20);
        }

        throw new TimeoutException("Timed out waiting for runner state.");
    }
}
