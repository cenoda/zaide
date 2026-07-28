using Xunit;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Tests.Features.Agents.Acp.Protocol;

public sealed class Phase20ProtocolCapabilityTests
{
    [Fact]
    public void Phase20ProtocolCapabilities_M1Profile_DoesNotOverstateFilesystemOrTerminal()
    {
        var capabilities = AcpClientCapabilityAdvertisement.CreateM1Profile();

        Assert.False(capabilities.Fs.ReadTextFile);
        Assert.False(capabilities.Fs.WriteTextFile);
        Assert.False(capabilities.Terminal);
    }

    [Fact]
    public void Phase20ProtocolCapabilities_NegotiatedSnapshot_RemainsTruthfulAfterInitialize()
    {
        var negotiated = new AcpNegotiatedCapabilities(
            1,
            new AcpAgentCapabilities
            {
                PromptCapabilities = new AcpPromptCapabilities
                {
                    Image = true,
                    Audio = true,
                    EmbeddedContext = true,
                },
            },
            [],
            null);

        Assert.True(negotiated.SupportsSessionPrompt);
        Assert.True(negotiated.SupportsSessionCancel);
        Assert.True(negotiated.SupportsSessionUpdate);
        Assert.False(negotiated.AdvertisesFilesystemRead);
        Assert.False(negotiated.AdvertisesFilesystemWrite);
        Assert.False(negotiated.AdvertisesTerminal);
    }
}
