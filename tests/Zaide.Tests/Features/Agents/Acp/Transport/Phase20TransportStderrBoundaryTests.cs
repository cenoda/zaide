using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Tests.Features.Agents.Acp.Transport;

[Collection("AcpProcessIsolation")]
public sealed class Phase20TransportStderrBoundaryTests
{
    [Fact]
    public async Task StderrSecrets_AreRedactedAndBounded()
    {
        var launcher = new AcpSystemDiagnosticsProcessLauncher();
        await using var host = await AcpStdioProcessHost.StartAsync(
            AcpFakeAgentFixture.CreateLaunchOptions("stderr-secret"),
            launcher,
            default);

        await host.InitializeAsync(CancellationToken.None);

        var lines = host.CapturedStderrLines;
        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.DoesNotContain("super-secret-value", line));
        Assert.Contains(lines, line => line.Contains("[redacted]", System.StringComparison.Ordinal));
        Assert.True(host.CapturedStderrLines.Sum(line => line.Length) <= AcpProcessLifecycleLimits.MaxStderrBytes);
    }

    [Fact]
    public void EnvironmentPolicy_RejectsInheritedSecretKeys()
    {
        var options = new AcpProcessLaunchOptions("/bin/true", new[] { "arg" })
        {
            AllowlistedEnvironment = new System.Collections.Generic.Dictionary<string, string>
            {
                ["OPENAI_API_KEY"] = "secret",
            },
        };

        var launcher = new AcpSystemDiagnosticsProcessLauncher();
        var ex = Assert.Throws<AcpProcessLifecycleException>(
            () => launcher.StartAsync(options, CancellationToken.None).GetAwaiter().GetResult());

        Assert.Equal(AcpProcessLifecycleFailureKind.ProtocolFailure, ex.Kind);
    }
}
