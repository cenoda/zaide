using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zaide.Features.Agents.Infrastructure.Acp;

internal sealed class AcpNewSessionParams
{
    [JsonPropertyName("cwd")]
    public string Cwd { get; init; } = string.Empty;

    [JsonPropertyName("additionalDirectories")]
    public IReadOnlyList<string>? AdditionalDirectories { get; init; }

    [JsonPropertyName("mcpServers")]
    public IReadOnlyList<JsonElement> McpServers { get; init; } = Array.Empty<JsonElement>();

    [JsonPropertyName("_meta")]
    public JsonElement? Meta { get; init; }
}

internal sealed class AcpNewSessionResult
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("modes")]
    public JsonElement? Modes { get; init; }

    [JsonPropertyName("configOptions")]
    public IReadOnlyList<JsonElement>? ConfigOptions { get; init; }

    [JsonPropertyName("_meta")]
    public JsonElement? Meta { get; init; }
}

internal sealed class AcpPromptParams
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("prompt")]
    public IReadOnlyList<AcpContentBlock> Prompt { get; init; } = Array.Empty<AcpContentBlock>();

    [JsonPropertyName("_meta")]
    public JsonElement? Meta { get; init; }
}

internal sealed class AcpPromptResult
{
    [JsonPropertyName("stopReason")]
    public string StopReason { get; init; } = string.Empty;

    [JsonPropertyName("_meta")]
    public JsonElement? Meta { get; init; }
}

internal sealed class AcpSessionCancelParams
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("_meta")]
    public JsonElement? Meta { get; init; }
}

internal sealed class AcpCancelRequestParams
{
    [JsonPropertyName("requestId")]
    public AcpJsonRpcRequestId RequestId { get; init; }

    [JsonPropertyName("_meta")]
    public JsonElement? Meta { get; init; }
}
