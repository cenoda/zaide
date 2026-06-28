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
}
