using System;
using System.Collections.Generic;
using System.Text.Json;
using Zaide.Features.Language.Application;

namespace Zaide.Services;

/// <summary>Parses <c>textDocument/definition</c> JSON into structured locations.</summary>
internal static class LanguageServerDefinitionParser
{
    public static LanguageServerDefinitionResult? Parse(JsonElement? element)
    {
        if (element is null || element.Value.ValueKind == JsonValueKind.Null)
            return new LanguageServerDefinitionResult(Array.Empty<LanguageLocation>());

        var root = element.Value;
        if (root.ValueKind == JsonValueKind.Array)
            return new LanguageServerDefinitionResult(ParseLocationArray(root));

        if (root.ValueKind != JsonValueKind.Object)
            return null;

        if (TryParseLocation(root, out var single))
            return new LanguageServerDefinitionResult(new[] { single });

        return new LanguageServerDefinitionResult(Array.Empty<LanguageLocation>());
    }

    private static IReadOnlyList<LanguageLocation> ParseLocationArray(JsonElement array)
    {
        var items = new List<LanguageLocation>();
        foreach (var item in array.EnumerateArray())
        {
            if (TryParseLocation(item, out var location))
                items.Add(location);
        }

        return items;
    }

    private static bool TryParseLocation(JsonElement element, out LanguageLocation location)
    {
        location = null!;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        // LocationLink: prefer targetSelectionRange, fall back to targetRange.
        if (element.TryGetProperty("targetUri", out var targetUriElement) &&
            targetUriElement.ValueKind == JsonValueKind.String)
        {
            var uri = targetUriElement.GetString();
            if (string.IsNullOrWhiteSpace(uri))
                return false;

            LspRange range;
            if (element.TryGetProperty("targetSelectionRange", out var selectionRange) &&
                TryParseRange(selectionRange, out range))
            {
                // ok
            }
            else if (element.TryGetProperty("targetRange", out var targetRange) &&
                     TryParseRange(targetRange, out range))
            {
                // ok
            }
            else
            {
                return false;
            }

            location = CreateLocation(uri, range, containerName: null, name: null);
            return true;
        }

        // Location: { uri, range }
        if (!element.TryGetProperty("uri", out var uriElement) ||
            uriElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var locationUri = uriElement.GetString();
        if (string.IsNullOrWhiteSpace(locationUri))
            return false;

        if (!element.TryGetProperty("range", out var rangeElement) ||
            !TryParseRange(rangeElement, out var locationRange))
        {
            return false;
        }

        location = CreateLocation(locationUri, locationRange, containerName: null, name: null);
        return true;
    }

    internal static LanguageLocation CreateLocation(
        string uri,
        LspRange range,
        string? containerName,
        string? name)
    {
        var normalized = LanguageDocumentUri.Normalize(uri);
        LanguageDocumentUri.TryGetPath(normalized, out var path);
        return new LanguageLocation(
            normalized,
            string.IsNullOrWhiteSpace(path) ? null : path,
            range,
            containerName,
            name);
    }

    internal static bool TryParseRange(JsonElement rangeElement, out LspRange range)
    {
        range = default;
        if (rangeElement.ValueKind != JsonValueKind.Object)
            return false;

        if (!rangeElement.TryGetProperty("start", out var startElement) ||
            !rangeElement.TryGetProperty("end", out var endElement))
            return false;

        if (!TryParsePosition(startElement, out var startLine, out var startCharacter) ||
            !TryParsePosition(endElement, out var endLine, out var endCharacter))
            return false;

        if (startLine < 0 || startCharacter < 0 || endLine < 0 || endCharacter < 0)
            return false;

        range = new LspRange(startLine, startCharacter, endLine, endCharacter);
        return true;
    }

    private static bool TryParsePosition(JsonElement positionElement, out int line, out int character)
    {
        line = 0;
        character = 0;

        if (positionElement.ValueKind != JsonValueKind.Object)
            return false;

        if (!positionElement.TryGetProperty("line", out var lineElement) ||
            lineElement.ValueKind != JsonValueKind.Number ||
            !lineElement.TryGetInt32(out line))
            return false;

        if (!positionElement.TryGetProperty("character", out var characterElement) ||
            characterElement.ValueKind != JsonValueKind.Number ||
            !characterElement.TryGetInt32(out character))
            return false;

        return true;
    }
}
