using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Application.Acp;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Agents.Infrastructure.Acp;
using Zaide.Tests.Features.Agents.Acp.Transport;

namespace Zaide.Tests.Features.Agents.Acp.Integration;

public sealed class Phase20IntegrationTests
{
    [Fact]
    public void ProductionDi_ResolvesBothBackendsAndBindingServices()
    {
        var services = new ServiceCollection();
        Program.ConfigureServices(services);
        using var provider = services.BuildServiceProvider();

        var backends = provider.GetServices<IAgentBackend>().ToArray();
        Assert.Equal(2, backends.Length);
        Assert.Contains(backends, backend => backend is NativeHarnessAgentBackend);
        Assert.Contains(backends, backend => backend is AcpActionCapableAgentBackend);

        Assert.IsType<NativeHarnessAgentBackend>(provider.GetRequiredService<IAgentBackend>());
        Assert.IsType<AgentActorBackendBindingStore>(provider.GetRequiredService<IAgentActorBackendBindingStore>());
        Assert.IsType<AgentActorBackendSelectionService>(provider.GetRequiredService<IAgentActorBackendSelectionService>());
        Assert.IsType<AcpProductionSessionClientFactory>(provider.GetRequiredService<IAcpSessionClientFactory>());
        Assert.IsType<AcpSystemDiagnosticsProcessLauncher>(provider.GetRequiredService<IAcpProcessLauncher>());
    }

    [Fact]
    public async Task FakeAgentFixture_HealthyMode_CompletesInitializeAndPrompt()
    {
        await using var host = await AcpFakeAgentFixture.StartHealthyHostAsync();
        var negotiated = await host.InitializeAsync(default);
        Assert.Equal("acp-fake-agent", negotiated.AgentInfo?.Name);

        var sessionId = await host.CreateSessionAsync(Environment.CurrentDirectory, default);
        Assert.False(string.IsNullOrWhiteSpace(sessionId));

        var turn = await host.PromptAsync(
            sessionId,
            new[] { AcpContentBlock.FromText("hello") },
            default);
        Assert.Equal(AcpStopReason.EndTurn, turn.StopReason);
    }
}
