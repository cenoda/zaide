using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zaide.Features.Agents.Infrastructure.Acp;

internal static class AcpSessionUpdateKindWire
{
    public const string UserMessageChunk = "user_message_chunk";

    public const string AgentMessageChunk = "agent_message_chunk";

    public const string AgentThoughtChunk = "agent_thought_chunk";

    public const string ToolCall = "tool_call";

    public const string ToolCallUpdate = "tool_call_update";

    public const string Plan = "plan";

    public const string AvailableCommandsUpdate = "available_commands_update";

    public const string CurrentModeUpdate = "current_mode_update";

    public const string ConfigOptionUpdate = "config_option_update";

    public const string SessionInfoUpdate = "session_info_update";

    public const string UsageUpdate = "usage_update";
}

internal enum AcpSessionUpdateKind
{
    UserMessageChunk,
    AgentMessageChunk,
    AgentThoughtChunk,
    ToolCall,
    ToolCallUpdate,
    Plan,
    AvailableCommandsUpdate,
    CurrentModeUpdate,
    ConfigOptionUpdate,
    SessionInfoUpdate,
    UsageUpdate,
    Unknown,
}

internal sealed class AcpContentChunk
{
    [JsonPropertyName("content")]
    public AcpContentBlock Content { get; init; } = AcpContentBlock.FromText(string.Empty);

    [JsonPropertyName("messageId")]
    public string? MessageId { get; init; }

    [JsonPropertyName("_meta")]
    public JsonElement? Meta { get; init; }
}

internal sealed class AcpToolCallWire
{
    [JsonPropertyName("toolCallId")]
    public string ToolCallId { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("content")]
    public IReadOnlyList<JsonElement>? Content { get; init; }

    [JsonPropertyName("locations")]
    public IReadOnlyList<JsonElement>? Locations { get; init; }

    [JsonPropertyName("rawInput")]
    public JsonElement? RawInput { get; init; }

    [JsonPropertyName("rawOutput")]
    public JsonElement? RawOutput { get; init; }

    [JsonPropertyName("_meta")]
    public JsonElement? Meta { get; init; }
}

internal sealed class AcpToolCallUpdateWire
{
    [JsonPropertyName("toolCallId")]
    public string ToolCallId { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("content")]
    public IReadOnlyList<JsonElement>? Content { get; init; }

    [JsonPropertyName("locations")]
    public IReadOnlyList<JsonElement>? Locations { get; init; }

    [JsonPropertyName("rawOutput")]
    public JsonElement? RawOutput { get; init; }

    [JsonPropertyName("_meta")]
    public JsonElement? Meta { get; init; }
}

[JsonConverter(typeof(AcpSessionUpdateJsonConverter))]
internal sealed class AcpSessionUpdate
{
    public AcpSessionUpdateKind Kind { get; init; }

    public AcpContentChunk? ContentChunk { get; init; }

    public AcpToolCallWire? ToolCall { get; init; }

    public AcpToolCallUpdateWire? ToolCallUpdate { get; init; }

    public JsonElement? Raw { get; init; }
}

internal sealed class AcpSessionUpdateNotification
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("update")]
    public AcpSessionUpdate Update { get; init; } = new();

    [JsonPropertyName("_meta")]
    public JsonElement? Meta { get; init; }
}

internal sealed class AcpSessionUpdateJsonConverter : JsonConverter<AcpSessionUpdate>
{
    public override AcpSessionUpdate? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (!root.TryGetProperty("sessionUpdate", out var discriminator)
            || discriminator.ValueKind != JsonValueKind.String)
        {
            return new AcpSessionUpdate { Kind = AcpSessionUpdateKind.Unknown, Raw = root.Clone() };
        }

        var kindWire = discriminator.GetString();
        return kindWire switch
        {
            AcpSessionUpdateKindWire.UserMessageChunk or
            AcpSessionUpdateKindWire.AgentMessageChunk or
            AcpSessionUpdateKindWire.AgentThoughtChunk =>
                new AcpSessionUpdate
                {
                    Kind = MapChunkKind(kindWire),
                    ContentChunk = JsonSerializer.Deserialize<AcpContentChunk>(root.GetRawText(), options),
                    Raw = root.Clone(),
                },
            AcpSessionUpdateKindWire.ToolCall =>
                new AcpSessionUpdate
                {
                    Kind = AcpSessionUpdateKind.ToolCall,
                    ToolCall = JsonSerializer.Deserialize<AcpToolCallWire>(root.GetRawText(), options),
                    Raw = root.Clone(),
                },
            AcpSessionUpdateKindWire.ToolCallUpdate =>
                new AcpSessionUpdate
                {
                    Kind = AcpSessionUpdateKind.ToolCallUpdate,
                    ToolCallUpdate = JsonSerializer.Deserialize<AcpToolCallUpdateWire>(root.GetRawText(), options),
                    Raw = root.Clone(),
                },
            AcpSessionUpdateKindWire.Plan => new AcpSessionUpdate
            {
                Kind = AcpSessionUpdateKind.Plan,
                Raw = root.Clone(),
            },
            AcpSessionUpdateKindWire.AvailableCommandsUpdate => new AcpSessionUpdate
            {
                Kind = AcpSessionUpdateKind.AvailableCommandsUpdate,
                Raw = root.Clone(),
            },
            AcpSessionUpdateKindWire.CurrentModeUpdate => new AcpSessionUpdate
            {
                Kind = AcpSessionUpdateKind.CurrentModeUpdate,
                Raw = root.Clone(),
            },
            AcpSessionUpdateKindWire.ConfigOptionUpdate => new AcpSessionUpdate
            {
                Kind = AcpSessionUpdateKind.ConfigOptionUpdate,
                Raw = root.Clone(),
            },
            AcpSessionUpdateKindWire.SessionInfoUpdate => new AcpSessionUpdate
            {
                Kind = AcpSessionUpdateKind.SessionInfoUpdate,
                Raw = root.Clone(),
            },
            AcpSessionUpdateKindWire.UsageUpdate => new AcpSessionUpdate
            {
                Kind = AcpSessionUpdateKind.UsageUpdate,
                Raw = root.Clone(),
            },
            _ => new AcpSessionUpdate { Kind = AcpSessionUpdateKind.Unknown, Raw = root.Clone() },
        };
    }

    public override void Write(Utf8JsonWriter writer, AcpSessionUpdate value, JsonSerializerOptions options)
    {
        if (value.Raw is { } raw)
        {
            raw.WriteTo(writer);
            return;
        }

        throw new NotSupportedException("ACP session update serialization requires raw payload or explicit builder.");
    }

    private static AcpSessionUpdateKind MapChunkKind(string kindWire) =>
        kindWire switch
        {
            AcpSessionUpdateKindWire.UserMessageChunk => AcpSessionUpdateKind.UserMessageChunk,
            AcpSessionUpdateKindWire.AgentMessageChunk => AcpSessionUpdateKind.AgentMessageChunk,
            AcpSessionUpdateKindWire.AgentThoughtChunk => AcpSessionUpdateKind.AgentThoughtChunk,
            _ => AcpSessionUpdateKind.Unknown,
        };
}
