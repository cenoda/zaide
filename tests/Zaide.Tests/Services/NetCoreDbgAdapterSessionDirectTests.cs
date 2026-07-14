using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Direct adapter-session diagnostics for production transport debugging.
/// </summary>
public sealed class NetCoreDbgAdapterSessionDirectTests
{
    private static readonly string FixtureRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "workflow-console"));

    private static readonly string AdapterPath =
        Environment.GetEnvironmentVariable("ZAIDE_NETCOREDBG_PATH")
        ?? "/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg";

    [Fact]
    public async Task DirectSession_InitializeLaunchAndConfigurationDone_Succeeds()
    {
        if (!File.Exists(AdapterPath))
            throw new InvalidOperationException($"Adapter missing: {AdapterPath}");

        var dllPath = Path.Combine(FixtureRoot, "bin", "Debug", "net10.0", "WorkflowConsole.dll");
        var sourcePath = Path.Combine(FixtureRoot, "Program.cs");
        Assert.True(File.Exists(dllPath), dllPath);

        await using var session = new NetCoreDbgAdapterSession(new DebugAdapterStartOptions(1, AdapterPath));
        var stopped = new TaskCompletionSource<DapStoppedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Stopped += e => stopped.TrySetResult(e);

        await session.ConnectAsync(CancellationToken.None);
        await session.InitializeAsync(CancellationToken.None);
        await session.LaunchAsync(dllPath, FixtureRoot, true, CancellationToken.None);
        await session.SetBreakpointsAsync(sourcePath, new[] { 1 }, CancellationToken.None);
        await session.ConfigurationDoneAsync(CancellationToken.None);
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(15));

        Assert.False(session.HasExited);
        await session.DisconnectAsync(CancellationToken.None);
    }
}
