using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// JSON-RPC 2.0 request envelope.
/// </summary>
internal sealed class AcpJsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public AcpJsonRpcRequestId Id { get; init; }

    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }
}

/// <summary>
/// JSON-RPC 2.0 response envelope.
/// </summary>
internal sealed class AcpJsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public AcpJsonRpcRequestId Id { get; init; }

    [JsonPropertyName("result")]
    public JsonElement? Result { get; init; }

    [JsonPropertyName("error")]
    public AcpJsonRpcError? Error { get; init; }

    public bool IsSuccess => Error is null;
}

/// <summary>
/// JSON-RPC 2.0 notification envelope.
/// </summary>
internal sealed class AcpJsonRpcNotification
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }
}
