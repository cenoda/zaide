using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zaide.Features.Agents.Infrastructure.Acp;

internal sealed class AcpFileSystemCapabilities
{
    [JsonPropertyName("readTextFile")]
    public bool ReadTextFile { get; init; }

    [JsonPropertyName("writeTextFile")]
    public bool WriteTextFile { get; init; }

    [JsonPropertyName("_meta")]
    public JsonElement? Meta { get; init; }
}

internal sealed class AcpClientCapabilities
{
    [JsonPropertyName("fs")]
    public AcpFileSystemCapabilities Fs { get; init; } = new();

    [JsonPropertyName("terminal")]
    public bool Terminal { get; init; }

    [JsonPropertyName("session")]
    public JsonElement? Session { get; init; }

    [JsonPropertyName("_meta")]
    public JsonElement? Meta { get; init; }
}

internal sealed class AcpPromptCapabilities
{
    [JsonPropertyName("image")]
    public bool Image { get; init; }

    [JsonPropertyName("audio")]
    public bool Audio { get; init; }

    [JsonPropertyName("embeddedContext")]
    public bool EmbeddedContext { get; init; }

    [JsonPropertyName("_meta")]
    public JsonElement? Meta { get; init; }
}

internal sealed class AcpMcpCapabilities
{
    [JsonPropertyName("http")]
    public bool Http { get; init; }

    [JsonPropertyName("sse")]
    public bool Sse { get; init; }

    [JsonPropertyName("_meta")]
    public JsonElement? Meta { get; init; }
}

internal sealed class AcpAgentCapabilities
{
    [JsonPropertyName("loadSession")]
    public bool LoadSession { get; init; }

    [JsonPropertyName("promptCapabilities")]
    public AcpPromptCapabilities PromptCapabilities { get; init; } = new();

    [JsonPropertyName("mcpCapabilities")]
    public AcpMcpCapabilities McpCapabilities { get; init; } = new();

    [JsonPropertyName("sessionCapabilities")]
    public JsonElement? SessionCapabilities { get; init; }

    [JsonPropertyName("auth")]
    public JsonElement? Auth { get; init; }

    [JsonPropertyName("_meta")]
    public JsonElement? Meta { get; init; }
}
