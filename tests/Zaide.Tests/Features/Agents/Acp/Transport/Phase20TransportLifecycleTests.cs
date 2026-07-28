using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Tests.Features.Agents.Acp.Transport;

public sealed class Phase20TransportLifecycleTests
{
    [Fact]
    public async Task HealthyFixture_CompletesInitializeAndSessionLifecycle()
    {
        await using var host = await AcpFakeAgentFixture.StartHealthyHostAsync();

        var negotiated = await host.InitializeAsync(CancellationToken.None);
        Assert.Equal(1, negotiated.ProtocolVersion);
        Assert.False(negotiated.AdvertisesTerminal);

        var sessionId = await host.CreateSessionAsync("/tmp", CancellationToken.None);
        Assert.Equal("fake-session-1", sessionId);

        var prompt = await host.PromptAsync(
            sessionId,
            new[] { AcpContentBlock.FromText("hello") },
            CancellationToken.None);

        Assert.Equal(AcpStopReason.EndTurn, prompt.StopReason);
        Assert.Equal(AcpProcessLifecycleState.Running, host.State);
    }

    [Fact]
    public async Task ExitImmediateFixture_SurfacesProcessExitFailure()
    {
        var launcher = new AcpSystemDiagnosticsProcessLauncher();
        await using var host = await AcpStdioProcessHost.StartAsync(
            AcpFakeAgentFixture.CreateLaunchOptions("exit-immediate"),
            launcher,
            default);

        var ex = await Assert.ThrowsAsync<AcpProcessLifecycleException>(
            () => host.InitializeAsync(CancellationToken.None));

        Assert.Equal(AcpProcessLifecycleFailureKind.ProcessExit, ex.Kind);
        Assert.Equal(AcpProcessLifecycleState.ProcessExited, host.State);
    }

    [Fact]
    public async Task MalformedStdout_IsIgnoredWithoutUnboundedFailure()
    {
        var launcher = new AcpSystemDiagnosticsProcessLauncher();
        await using var host = await AcpStdioProcessHost.StartAsync(
            AcpFakeAgentFixture.CreateLaunchOptions("malformed-stdout"),
            launcher,
            default);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var ex = await Assert.ThrowsAnyAsync<Exception>(
            () => host.InitializeAsync(cts.Token));

        Assert.True(
            ex is AcpProcessLifecycleException { Kind: AcpProcessLifecycleFailureKind.Timeout }
            or OperationCanceledException
            or AcpProcessLifecycleException { Kind: AcpProcessLifecycleFailureKind.ProcessExit });
    }
}
