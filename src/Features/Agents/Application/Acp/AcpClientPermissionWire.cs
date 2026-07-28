using System.Collections.Generic;
using System.Text.Json.Serialization;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Features.Agents.Application.Acp;

/// <summary>
/// Wire DTOs for ACP session/request_permission from schema-v1.20.0.
/// </summary>
internal sealed class AcpRequestPermissionRequestWire
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("toolCall")]
    public AcpToolCallWire? ToolCall { get; init; }

    [JsonPropertyName("options")]
    public IReadOnlyList<AcpPermissionOptionWire> Options { get; init; } = [];
}

internal sealed class AcpPermissionOptionWire
{
    [JsonPropertyName("optionId")]
    public string OptionId { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;
}

internal sealed class AcpRequestPermissionResponseWire
{
    [JsonPropertyName("outcome")]
    public AcpRequestPermissionOutcomeWire Outcome { get; init; } = new();
}

internal sealed class AcpRequestPermissionOutcomeWire
{
    [JsonPropertyName("outcome")]
    public string Outcome { get; init; } = string.Empty;

    [JsonPropertyName("optionId")]
    public string? OptionId { get; init; }
}
