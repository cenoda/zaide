using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Features.Agents.Application.Acp;

/// <summary>
/// Truthful ACP client capability profiles for Phase 20 milestones.
/// </summary>
internal static class AcpClientCapabilityProfiles
{
    public static AcpClientCapabilities CreateWithoutFilesystemBridge() =>
        AcpClientCapabilityAdvertisement.CreateM1Profile();

    public static AcpClientCapabilities CreateWithFilesystemBridge() =>
        new()
        {
            Fs = new AcpFileSystemCapabilities
            {
                ReadTextFile = true,
                WriteTextFile = true,
            },
            Terminal = false,
            Session = null,
        };

    public static AcpInitializeParams CreateInitializeParams(
        AcpClientCapabilities capabilities,
        int protocolVersion) =>
        new()
        {
            ProtocolVersion = protocolVersion,
            ClientCapabilities = capabilities,
            ClientInfo = new AcpImplementationInfo
            {
                Name = "zaide",
                Version = "phase-20-m4",
            },
        };
}
