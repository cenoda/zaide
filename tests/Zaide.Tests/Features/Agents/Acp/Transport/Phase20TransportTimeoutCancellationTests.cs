using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Tests.Features.Agents.Acp.Transport;

public sealed class Phase20TransportTimeoutCancellationTests
{
    [Fact]
    public async Task SlowInitialize_SurfacesTimeoutFailure()
    {
        var previousTimeout = AcpProcessLifecycleLimits.InitializeTimeout;
        AcpProcessLifecycleLimits.InitializeTimeout = TimeSpan.FromMilliseconds(200);
        try
        {
            var launcher = new AcpSystemDiagnosticsProcessLauncher();
            await using var host = await AcpStdioProcessHost.StartAsync(
                AcpFakeAgentFixture.CreateLaunchOptions("slow-init"),
                launcher,
                default);

            var ex = await Assert.ThrowsAsync<AcpProcessLifecycleException>(
                () => host.InitializeAsync(CancellationToken.None));

            Assert.Equal(AcpProcessLifecycleFailureKind.Timeout, ex.Kind);
        }
        finally
        {
            AcpProcessLifecycleLimits.InitializeTimeout = previousTimeout;
        }
    }

    [Fact]
    public async Task CancelledInitialize_SurfacesCancellationFailure()
    {
        await using var host = await AcpFakeAgentFixture.StartHealthyHostAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ex = await Assert.ThrowsAsync<AcpProcessLifecycleException>(
            () => host.InitializeAsync(cts.Token));

        Assert.Equal(AcpProcessLifecycleFailureKind.Cancellation, ex.Kind);
    }

    [Fact]
    public async Task DuplicateResponse_IsCountedAsLateCompletion()
    {
        var launcher = new AcpSystemDiagnosticsProcessLauncher();
        await using var host = await AcpStdioProcessHost.StartAsync(
            AcpFakeAgentFixture.CreateLaunchOptions("duplicate-response"),
            launcher,
            default);

        await host.InitializeAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        Assert.True(host.LateResponseCount >= 1);
    }
}
