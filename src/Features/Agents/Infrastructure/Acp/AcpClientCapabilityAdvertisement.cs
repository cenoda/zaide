using System;
using System.Collections.Generic;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// Truthful Phase 20 M1 client capability advertisement.
/// Filesystem and terminal remain disabled until later milestones.
/// </summary>
internal static class AcpClientCapabilityAdvertisement
{
    public static AcpClientCapabilities CreateM1Profile() =>
        new()
        {
            Fs = new AcpFileSystemCapabilities
            {
                ReadTextFile = false,
                WriteTextFile = false,
            },
            Terminal = false,
            Session = null,
        };

    public static AcpInitializeParams CreateInitializeParams(int protocolVersion) =>
        CreateInitializeParams(protocolVersion, CreateM1Profile());

    public static AcpInitializeParams CreateInitializeParams(
        int protocolVersion,
        AcpClientCapabilities clientCapabilities) =>
        new()
        {
            ProtocolVersion = protocolVersion,
            ClientCapabilities = clientCapabilities,
            ClientInfo = new AcpImplementationInfo
            {
                Name = "zaide",
                Version = "phase-20-m1",
            },
        };
}

/// <summary>
/// Negotiated agent capabilities observed after initialize.
/// </summary>
internal sealed class AcpNegotiatedCapabilities
{
    public AcpNegotiatedCapabilities(
        int protocolVersion,
        AcpAgentCapabilities agentCapabilities,
        IReadOnlyList<AcpAuthMethod> authMethods,
        AcpImplementationInfo? agentInfo)
    {
        ProtocolVersion = protocolVersion;
        AgentCapabilities = agentCapabilities ?? throw new ArgumentNullException(nameof(agentCapabilities));
        AuthMethods = authMethods ?? throw new ArgumentNullException(nameof(authMethods));
        AgentInfo = agentInfo;
    }

    public int ProtocolVersion { get; }

    public AcpAgentCapabilities AgentCapabilities { get; }

    public IReadOnlyList<AcpAuthMethod> AuthMethods { get; }

    public AcpImplementationInfo? AgentInfo { get; }

    public bool SupportsSessionPrompt => ProtocolVersion == AcpSchemaProfile.WireProtocolVersion;

    public bool SupportsSessionCancel => ProtocolVersion == AcpSchemaProfile.WireProtocolVersion;

    public bool SupportsSessionUpdate => ProtocolVersion == AcpSchemaProfile.WireProtocolVersion;

    public bool AdvertisesFilesystemRead => false;

    public bool AdvertisesFilesystemWrite => false;

    public bool AdvertisesTerminal => false;
}
