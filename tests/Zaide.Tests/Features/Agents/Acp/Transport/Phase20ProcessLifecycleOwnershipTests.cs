using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Tests.Features.Agents.Acp.Transport;

public sealed class Phase20ProcessLifecycleOwnershipTests
{
    [Fact]
    public async Task Host_DisposeAsync_TerminatesOwnedProcessTree()
    {
        var options = AcpFakeAgentFixture.CreateLaunchOptions("spawn-child");
        var launcher = new AcpSystemDiagnosticsProcessLauncher();
        var host = await AcpStdioProcessHost.StartAsync(options, launcher, default);
        var rootPid = host.ProcessId;
        Assert.NotNull(rootPid);

        await host.DisposeAsync();

        Assert.True(IsProcessGone(rootPid.Value));
        Assert.DoesNotContain(
            Process.GetProcesses(),
            process => !process.HasExited && IsDescendantOf(process, rootPid.Value));
    }

    [Fact]
    public async Task ShutdownRegistry_DisposesRegisteredHosts()
    {
        var host = await AcpFakeAgentFixture.StartHealthyHostAsync();
        var pid = host.ProcessId;
        Assert.NotNull(pid);

        AcpProcessHostShutdownRegistry.ShutdownAll();

        Assert.True(IsProcessGone(pid.Value));
    }

    private static bool IsProcessGone(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private static bool IsDescendantOf(Process process, int ancestorPid)
    {
        try
        {
            var current = process;
            while (true)
            {
                if (current.Id == ancestorPid)
                {
                    return true;
                }

                var parentId = GetParentProcessId(current);
                if (parentId is null)
                {
                    return false;
                }

                current = Process.GetProcessById(parentId.Value);
            }
        }
        catch
        {
            return false;
        }
    }

    private static int? GetParentProcessId(Process process)
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        var statPath = $"/proc/{process.Id}/stat";
        if (!System.IO.File.Exists(statPath))
        {
            return null;
        }

        var stat = System.IO.File.ReadAllText(statPath);
        var closeParen = stat.LastIndexOf(')');
        if (closeParen < 0 || closeParen + 2 >= stat.Length)
        {
            return null;
        }

        var remainder = stat[(closeParen + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return remainder.Length > 0 && int.TryParse(remainder[0], out var parentId) ? parentId : null;
    }
}
