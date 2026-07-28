using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// ACP prompt-turn stop reasons from schema-v1.20.0.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
internal enum AcpStopReason
{
    EndTurn,
    MaxTokens,
    MaxTurnRequests,
    Refusal,
    Cancelled,
}

internal static class AcpStopReasonWire
{
    public const string EndTurn = "end_turn";

    public const string MaxTokens = "max_tokens";

    public const string MaxTurnRequests = "max_turn_requests";

    public const string Refusal = "refusal";

    public const string Cancelled = "cancelled";

    public static string ToWire(AcpStopReason reason) =>
        reason switch
        {
            AcpStopReason.EndTurn => EndTurn,
            AcpStopReason.MaxTokens => MaxTokens,
            AcpStopReason.MaxTurnRequests => MaxTurnRequests,
            AcpStopReason.Refusal => Refusal,
            AcpStopReason.Cancelled => Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
        };

    public static bool TryParse(string? wire, out AcpStopReason reason)
    {
        switch (wire)
        {
            case EndTurn:
                reason = AcpStopReason.EndTurn;
                return true;
            case MaxTokens:
                reason = AcpStopReason.MaxTokens;
                return true;
            case MaxTurnRequests:
                reason = AcpStopReason.MaxTurnRequests;
                return true;
            case Refusal:
                reason = AcpStopReason.Refusal;
                return true;
            case Cancelled:
                reason = AcpStopReason.Cancelled;
                return true;
            default:
                reason = default;
                return false;
        }
    }
}
