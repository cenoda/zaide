using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zaide.Features.Agents.Infrastructure.Acp;

internal enum AcpContentBlockKind
{
    Text,
    Image,
    Audio,
    ResourceLink,
    Resource,
    Unknown,
}

[JsonConverter(typeof(AcpContentBlockJsonConverter))]
internal sealed class AcpContentBlock
{
    public AcpContentBlockKind Kind { get; init; }

    public string? Text { get; init; }

    public string? Uri { get; init; }

    public string? Name { get; init; }

    public string? MimeType { get; init; }

    public string? Data { get; init; }

    public JsonElement? Raw { get; init; }

    public static AcpContentBlock FromText(string text) =>
        new()
        {
            Kind = AcpContentBlockKind.Text,
            Text = text,
        };

    public static AcpContentBlock FromResourceLink(string uri, string? name = null, string? mimeType = null) =>
        new()
        {
            Kind = AcpContentBlockKind.ResourceLink,
            Uri = uri,
            Name = name,
            MimeType = mimeType,
        };
}

internal sealed class AcpContentBlockJsonConverter : JsonConverter<AcpContentBlock>
{
    public override AcpContentBlock? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (!root.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
        {
            return new AcpContentBlock { Kind = AcpContentBlockKind.Unknown, Raw = root.Clone() };
        }

        var type = typeElement.GetString();
        return type switch
        {
            "text" => new AcpContentBlock
            {
                Kind = AcpContentBlockKind.Text,
                Text = root.TryGetProperty("text", out var text) ? text.GetString() : null,
                Raw = root.Clone(),
            },
            "image" => new AcpContentBlock
            {
                Kind = AcpContentBlockKind.Image,
                Data = root.TryGetProperty("data", out var data) ? data.GetString() : null,
                MimeType = root.TryGetProperty("mimeType", out var mime) ? mime.GetString() : null,
                Uri = root.TryGetProperty("uri", out var uri) && uri.ValueKind == JsonValueKind.String
                    ? uri.GetString()
                    : null,
                Raw = root.Clone(),
            },
            "audio" => new AcpContentBlock
            {
                Kind = AcpContentBlockKind.Audio,
                Data = root.TryGetProperty("data", out var data) ? data.GetString() : null,
                MimeType = root.TryGetProperty("mimeType", out var mime) ? mime.GetString() : null,
                Raw = root.Clone(),
            },
            "resource_link" => new AcpContentBlock
            {
                Kind = AcpContentBlockKind.ResourceLink,
                Uri = root.TryGetProperty("uri", out var uri) ? uri.GetString() : null,
                Name = root.TryGetProperty("name", out var name) ? name.GetString() : null,
                MimeType = root.TryGetProperty("mimeType", out var mime) ? mime.GetString() : null,
                Raw = root.Clone(),
            },
            "resource" => new AcpContentBlock
            {
                Kind = AcpContentBlockKind.Resource,
                Raw = root.Clone(),
            },
            _ => new AcpContentBlock { Kind = AcpContentBlockKind.Unknown, Raw = root.Clone() },
        };
    }

    public override void Write(Utf8JsonWriter writer, AcpContentBlock value, JsonSerializerOptions options)
    {
        if (value.Raw is { } raw)
        {
            raw.WriteTo(writer);
            return;
        }

        writer.WriteStartObject();
        switch (value.Kind)
        {
            case AcpContentBlockKind.Text:
                writer.WriteString("type", "text");
                writer.WriteString("text", value.Text ?? string.Empty);
                break;
            case AcpContentBlockKind.ResourceLink:
                writer.WriteString("type", "resource_link");
                writer.WriteString("uri", value.Uri ?? string.Empty);
                if (value.Name is not null)
                {
                    writer.WriteString("name", value.Name);
                }

                if (value.MimeType is not null)
                {
                    writer.WriteString("mimeType", value.MimeType);
                }

                break;
            default:
                writer.WriteString("type", "text");
                writer.WriteString("text", value.Text ?? string.Empty);
                break;
        }

        writer.WriteEndObject();
    }
}
