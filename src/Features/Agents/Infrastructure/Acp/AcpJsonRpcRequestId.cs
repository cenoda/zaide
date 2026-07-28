using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// JSON-RPC request id (string, integer, or null).
/// </summary>
[JsonConverter(typeof(AcpJsonRpcRequestIdJsonConverter))]
internal readonly struct AcpJsonRpcRequestId : IEquatable<AcpJsonRpcRequestId>
{
    public AcpJsonRpcRequestId(string? stringValue)
    {
        Kind = RequestIdKind.String;
        StringValue = stringValue;
        NumberValue = default;
    }

    public AcpJsonRpcRequestId(long numberValue)
    {
        Kind = RequestIdKind.Number;
        StringValue = null;
        NumberValue = numberValue;
    }

    private AcpJsonRpcRequestId(RequestIdKind kind, string? stringValue, long numberValue)
    {
        Kind = kind;
        StringValue = stringValue;
        NumberValue = numberValue;
    }

    public RequestIdKind Kind { get; }

    public string? StringValue { get; }

    public long NumberValue { get; }

    public static AcpJsonRpcRequestId Null { get; } = new(RequestIdKind.Null, null, default);

    public static AcpJsonRpcRequestId FromNumber(long value) => new(value);

    public static AcpJsonRpcRequestId FromString(string value) =>
        new(value ?? throw new ArgumentNullException(nameof(value)));

    public bool IsNull => Kind == RequestIdKind.Null;

    public override string ToString() =>
        Kind switch
        {
            RequestIdKind.Null => "null",
            RequestIdKind.Number => NumberValue.ToString(CultureInfo.InvariantCulture),
            RequestIdKind.String => StringValue ?? string.Empty,
            _ => string.Empty,
        };

    public bool Equals(AcpJsonRpcRequestId other) =>
        Kind == other.Kind
        && StringValue == other.StringValue
        && NumberValue == other.NumberValue;

    public override bool Equals(object? obj) => obj is AcpJsonRpcRequestId other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Kind, StringValue, NumberValue);

    internal enum RequestIdKind
    {
        Null,
        Number,
        String,
    }
}

internal sealed class AcpJsonRpcRequestIdJsonConverter : JsonConverter<AcpJsonRpcRequestId>
{
    public override AcpJsonRpcRequestId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => AcpJsonRpcRequestId.Null,
            JsonTokenType.String => AcpJsonRpcRequestId.FromString(reader.GetString()!),
            JsonTokenType.Number when reader.TryGetInt64(out var number) =>
                AcpJsonRpcRequestId.FromNumber(number),
            _ => throw new JsonException("ACP request id must be null, string, or integer."),
        };
    }

    public override void Write(Utf8JsonWriter writer, AcpJsonRpcRequestId value, JsonSerializerOptions options)
    {
        switch (value.Kind)
        {
            case AcpJsonRpcRequestId.RequestIdKind.Null:
                writer.WriteNullValue();
                break;
            case AcpJsonRpcRequestId.RequestIdKind.Number:
                writer.WriteNumberValue(value.NumberValue);
                break;
            case AcpJsonRpcRequestId.RequestIdKind.String:
                writer.WriteStringValue(value.StringValue);
                break;
            default:
                throw new JsonException("Unsupported ACP request id kind.");
        }
    }
}
