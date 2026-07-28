using System;
using System.Text;
using System.Text.Json;

namespace Zaide.Features.Agents.Infrastructure.Acp;

internal enum AcpJsonRpcMessageKind
{
    Request,
    Response,
    Notification,
    Invalid,
}

internal static class AcpMessageCodec
{
    public static byte[] SerializeRequest(AcpJsonRpcRequest request) =>
        JsonSerializer.SerializeToUtf8Bytes(request, AcpJsonSerializerOptionsFactory.SharedOptions);

    public static byte[] SerializeResponse(AcpJsonRpcResponse response) =>
        JsonSerializer.SerializeToUtf8Bytes(response, AcpJsonSerializerOptionsFactory.SharedOptions);

    public static byte[] SerializeNotification(AcpJsonRpcNotification notification) =>
        JsonSerializer.SerializeToUtf8Bytes(notification, AcpJsonSerializerOptionsFactory.SharedOptions);

    public static AcpJsonRpcMessageKind ClassifyMessage(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            using var document = JsonDocument.Parse(utf8Json.ToArray());
            var root = document.RootElement;
            if (!root.TryGetProperty("jsonrpc", out var jsonRpc)
                || jsonRpc.GetString() != "2.0")
            {
                return AcpJsonRpcMessageKind.Invalid;
            }

            var hasMethod = root.TryGetProperty("method", out _);
            var hasId = root.TryGetProperty("id", out _);
            var hasResult = root.TryGetProperty("result", out _);
            var hasError = root.TryGetProperty("error", out _);

            if (hasMethod && !hasId)
            {
                return AcpJsonRpcMessageKind.Notification;
            }

            if (hasMethod && hasId)
            {
                return AcpJsonRpcMessageKind.Request;
            }

            if (hasId && (hasResult || hasError))
            {
                return AcpJsonRpcMessageKind.Response;
            }

            return AcpJsonRpcMessageKind.Invalid;
        }
        catch (JsonException)
        {
            return AcpJsonRpcMessageKind.Invalid;
        }
    }

    public static AcpJsonRpcRequest DeserializeRequest(ReadOnlySpan<byte> utf8Json) =>
        JsonSerializer.Deserialize<AcpJsonRpcRequest>(utf8Json, AcpJsonSerializerOptionsFactory.SharedOptions)
        ?? throw new AcpProtocolException("ACP request payload deserialized to null.");

    public static AcpJsonRpcResponse DeserializeResponse(ReadOnlySpan<byte> utf8Json) =>
        JsonSerializer.Deserialize<AcpJsonRpcResponse>(utf8Json, AcpJsonSerializerOptionsFactory.SharedOptions)
        ?? throw new AcpProtocolException("ACP response payload deserialized to null.");

    public static AcpJsonRpcNotification DeserializeNotification(ReadOnlySpan<byte> utf8Json) =>
        JsonSerializer.Deserialize<AcpJsonRpcNotification>(utf8Json, AcpJsonSerializerOptionsFactory.SharedOptions)
        ?? throw new AcpProtocolException("ACP notification payload deserialized to null.");

    public static T DeserializeParams<T>(JsonElement? parameters)
    {
        if (parameters is null || parameters.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return JsonSerializer.Deserialize<T>("{}", AcpJsonSerializerOptionsFactory.SharedOptions)
                   ?? throw new AcpProtocolException("ACP empty params deserialized to null.");
        }

        return JsonSerializer.Deserialize<T>(parameters.Value.GetRawText(), AcpJsonSerializerOptionsFactory.SharedOptions)
               ?? throw new AcpProtocolException("ACP params deserialized to null.");
    }

    public static T DeserializeResult<T>(JsonElement? result)
    {
        if (result is null || result.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new AcpProtocolException("ACP success response is missing result payload.");
        }

        return JsonSerializer.Deserialize<T>(result.Value.GetRawText(), AcpJsonSerializerOptionsFactory.SharedOptions)
               ?? throw new AcpProtocolException("ACP result deserialized to null.");
    }

    public static string ValidateUtf8Frame(ReadOnlySpan<byte> frameBytes)
    {
        if (frameBytes.Length > AcpProtocolLimits.MaxFrameBytes)
        {
            throw new AcpProtocolException("ACP frame exceeds the configured byte limit.");
        }

        if (frameBytes.Contains((byte)'\n'))
        {
            throw new AcpProtocolException("ACP frame must not contain embedded newline characters.");
        }

        try
        {
            return Encoding.UTF8.GetString(frameBytes);
        }
        catch (DecoderFallbackException ex)
        {
            throw new AcpProtocolException("ACP frame is not valid UTF-8.", ex);
        }
    }
}
