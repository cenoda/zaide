using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zaide.Features.Agents.Infrastructure.Acp;

internal static class AcpJsonSerializerOptionsFactory
{
    private static readonly JsonSerializerOptions Shared = Create();

    public static JsonSerializerOptions Create() =>
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            MaxDepth = AcpProtocolLimits.MaxJsonDepth,
            Converters =
            {
                new AcpJsonRpcRequestIdJsonConverter(),
                new AcpContentBlockJsonConverter(),
                new AcpSessionUpdateJsonConverter(),
            },
        };

    public static JsonSerializerOptions SharedOptions => Shared;
}
