using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Services;

namespace Zaide.Tests.Services;

[Trait("Category", "Integration")]
public class LinuxTerminalServiceTests
{
    [Fact]
    public async Task StartAsync_RaisesOutput()
    {
        using var service = new LinuxTerminalService();
        var outputArrived = new ManualResetEventSlim(false);

        service.OutputReceived += _ => outputArrived.Set();

        await service.StartAsync();

        try
        {
            bool signaled = outputArrived.Wait(TimeSpan.FromSeconds(5));
            Assert.True(signaled, "Expected OutputReceived to fire with prompt bytes within 5 s");
            Assert.True(service.IsRunning);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task WriteAsync_EchoesInput()
    {
        using var service = new LinuxTerminalService();
        var captured = new StringBuilder();
        var sawHello = new ManualResetEventSlim(false);

        service.OutputReceived += data =>
        {
            string text = Encoding.UTF8.GetString(data);
            lock (captured)
            {
                captured.Append(text);
                if (captured.ToString().Contains("hello", StringComparison.Ordinal))
                    sawHello.Set();
            }
        };

        await service.StartAsync();

        try
        {
            // Let the prompt settle, then send echo command (\r is Enter in a PTY).
            await Task.Delay(400);
            await service.WriteAsync(Encoding.UTF8.GetBytes("echo hello\r"));

            bool signaled = sawHello.Wait(TimeSpan.FromSeconds(5));
            Assert.True(signaled, "Expected 'hello' in output within 5 s");
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task Dispose_KillsProcess()
    {
        var service = new LinuxTerminalService();
        await service.StartAsync();
        Assert.True(service.IsRunning);

        service.Dispose();

        Assert.False(service.IsRunning);
    }

    [Fact]
    public async Task Restart_StartExitRestartExit_RaisesProcessExitedEachTime()
    {
        using var service = new LinuxTerminalService();
        int exitCount = 0;
        var exited = new AutoResetEvent(false);
        service.ProcessExited += () =>
        {
            Interlocked.Increment(ref exitCount);
            exited.Set();
        };

        // --- First session ---
        await service.StartAsync();
        Assert.True(service.IsRunning);

        await Task.Delay(300); // let bash reach its prompt
        await service.WriteAsync(Encoding.UTF8.GetBytes("exit\r"));

        Assert.True(exited.WaitOne(TimeSpan.FromSeconds(5)), "first exit was not signaled");
        Assert.False(service.IsRunning);

        // --- Second session (restart on the same instance) ---
        await service.StartAsync();
        Assert.True(service.IsRunning);

        await Task.Delay(300);
        await service.WriteAsync(Encoding.UTF8.GetBytes("exit\r"));

        Assert.True(exited.WaitOne(TimeSpan.FromSeconds(5)),
            "second exit was not signaled — restart left stale exit/reader state");
        Assert.False(service.IsRunning);

        // Exactly two exits: the latch was reset on restart, and the single
        // subscription was not duplicated across sessions.
        Assert.Equal(2, exitCount);
    }

    [Fact]
    public async Task Restart_DoesNotLeakFileDescriptors()
    {
        using var service = new LinuxTerminalService();
        var exited = new AutoResetEvent(false);
        service.ProcessExited += () => exited.Set();

        async Task RunCycleAsync()
        {
            await service.StartAsync();
            await Task.Delay(300); // let bash reach its prompt
            await service.WriteAsync(Encoding.UTF8.GetBytes("exit\r"));
            Assert.True(exited.WaitOne(TimeSpan.FromSeconds(5)), "shell did not exit");
        }

        // Warm up one full cycle, then take the fd baseline.
        await RunCycleAsync();
        int baseline = CountOpenFds();

        // Several more restart cycles. A per-restart master-fd leak would grow
        // the count by roughly one per cycle.
        for (int i = 0; i < 5; i++)
            await RunCycleAsync();

        int after = CountOpenFds();

        Assert.True(after <= baseline + 2,
            $"Open fd count grew from {baseline} to {after} across 5 restarts — " +
            "likely a leaked PTY master fd.");
    }

    // Counts this process's open file descriptors via /proc (Linux-only).
    // The PTY master is a raw int fd, not a SafeHandle, so the GC never closes
    // it — a leak here is deterministic.
    private static int CountOpenFds()
        => System.IO.Directory.GetFiles("/proc/self/fd").Length;
}
