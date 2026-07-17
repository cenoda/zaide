using System;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Zaide.Features.Language.Infrastructure.Lsp;

/// <summary>Parses <c>textDocument/hover</c> JSON into displayable content.</summary>
internal static class LanguageServerHoverParser
{
    public static LanguageServerHoverResult? Parse(JsonElement? element)
    {
        if (element is null || element.Value.ValueKind == JsonValueKind.Null)
            return new LanguageServerHoverResult(null);

        if (element.Value.ValueKind != JsonValueKind.Object)
            return null;

        if (!element.Value.TryGetProperty("contents", out var contents))
            return new LanguageServerHoverResult(null);

        return new LanguageServerHoverResult(ExtractContents(contents));
    }

    private static string? ExtractContents(JsonElement contents)
    {
        return contents.ValueKind switch
        {
            JsonValueKind.String => contents.GetString(),
            JsonValueKind.Array => string.Join(
                Environment.NewLine,
                contents.EnumerateArray()
                    .Select(ExtractMarkedContent)
                    .Where(text => !string.IsNullOrWhiteSpace(text))),
            JsonValueKind.Object => ExtractMarkedContent(contents),
            _ => null,
        };
    }

    private static string? ExtractMarkedContent(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return element.GetString();

        if (element.ValueKind != JsonValueKind.Object)
            return null;

        if (element.TryGetProperty("value", out var valueElement) &&
            valueElement.ValueKind == JsonValueKind.String)
        {
            return valueElement.GetString();
        }

        return element.ToString();
    }
}
