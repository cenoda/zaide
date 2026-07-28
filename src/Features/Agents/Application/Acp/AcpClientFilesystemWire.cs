using System.Text.Json.Serialization;

namespace Zaide.Features.Agents.Application.Acp;

/// <summary>
/// Wire DTOs for ACP client filesystem methods from schema-v1.20.0.
/// </summary>
internal sealed class AcpReadTextFileRequestWire
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("line")]
    public uint? Line { get; init; }

    [JsonPropertyName("limit")]
    public uint? Limit { get; init; }
}

internal sealed class AcpReadTextFileResponseWire
{
    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}

internal sealed class AcpWriteTextFileRequestWire
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}

internal sealed class AcpWriteTextFileResponseWire
{
}
