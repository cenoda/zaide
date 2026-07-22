using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Phase16NativeHarnessEvaluation;

public static class ManifestCanonicalSerializer
{
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static string SerializeForHash(object value)
    {
        var json = JsonSerializer.Serialize(value, CanonicalOptions);
        using var document = JsonDocument.Parse(json);
        return SerializeElement(document.RootElement);
    }

    public static string ComputeSha256Hex(string canonicalUtf8Text)
    {
        var bytes = Encoding.UTF8.GetBytes(canonicalUtf8Text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string SerializeElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => SerializeObject(element),
            JsonValueKind.Array => SerializeArray(element),
            JsonValueKind.String => JsonSerializer.Serialize(element.GetString()),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => throw new InvalidOperationException($"Unsupported JSON kind: {element.ValueKind}"),
        };
    }

    private static string SerializeObject(JsonElement element)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        var first = true;
        foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append(JsonSerializer.Serialize(property.Name));
            builder.Append(':');
            builder.Append(SerializeElement(property.Value));
        }

        builder.Append('}');
        return builder.ToString();
    }

    private static string SerializeArray(JsonElement element)
    {
        var builder = new StringBuilder();
        builder.Append('[');
        var first = true;
        foreach (var item in element.EnumerateArray())
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append(SerializeElement(item));
        }

        builder.Append(']');
        return builder.ToString();
    }
}
