using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zaide.Features.Agents.Infrastructure.Acp;

internal sealed class AcpInitializeParams
{
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; init; }

    [JsonPropertyName("clientCapabilities")]
    public AcpClientCapabilities ClientCapabilities { get; init; } = new();

    [JsonPropertyName("clientInfo")]
    public AcpImplementationInfo? ClientInfo { get; init; }

    [JsonPropertyName("_meta")]
    public JsonElement? Meta { get; init; }
}

internal sealed class AcpInitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; init; }

    [JsonPropertyName("agentCapabilities")]
    public AcpAgentCapabilities AgentCapabilities { get; init; } = new();

    [JsonPropertyName("authMethods")]
    public IReadOnlyList<AcpAuthMethod> AuthMethods { get; init; } = Array.Empty<AcpAuthMethod>();

    [JsonPropertyName("agentInfo")]
    public AcpImplementationInfo? AgentInfo { get; init; }

    [JsonPropertyName("_meta")]
    public JsonElement? Meta { get; init; }
}
