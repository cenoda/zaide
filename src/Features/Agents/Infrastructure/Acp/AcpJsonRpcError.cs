using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// JSON-RPC 2.0 and ACP-specific error codes from schema-v1.20.0.
/// </summary>
internal static class AcpJsonRpcErrorCode
{
    public const int ParseError = -32700;

    public const int InvalidRequest = -32600;

    public const int MethodNotFound = -32601;

    public const int InvalidParams = -32602;

    public const int InternalError = -32603;

    public const int RequestCancelled = -32800;

    public const int AuthenticationRequired = -32000;

    public const int ResourceNotFound = -32002;
}

/// <summary>
/// JSON-RPC 2.0 error object.
/// </summary>
internal sealed class AcpJsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public JsonElement? Data { get; init; }
}
