using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Zaide.Services;

/// <summary>Parses <c>textDocument/completion</c> JSON into structured items.</summary>
internal static class LanguageServerCompletionParser
{
    public static LanguageServerCompletionResult? Parse(JsonElement? element)
    {
        if (element is null)
            return new LanguageServerCompletionResult(Array.Empty<LanguageServerCompletionItem>());

        if (element.Value.ValueKind == JsonValueKind.Null)
            return new LanguageServerCompletionResult(Array.Empty<LanguageServerCompletionItem>());

        var root = element.Value;
        if (root.ValueKind == JsonValueKind.Array)
            return new LanguageServerCompletionResult(ParseItems(root));

        if (root.ValueKind != JsonValueKind.Object)
            return null;

        if (root.TryGetProperty("items", out var itemsElement) &&
            itemsElement.ValueKind == JsonValueKind.Array)
        {
            return new LanguageServerCompletionResult(ParseItems(itemsElement));
        }

        return new LanguageServerCompletionResult(Array.Empty<LanguageServerCompletionItem>());
    }

    private static IReadOnlyList<LanguageServerCompletionItem> ParseItems(JsonElement array)
    {
        var items = new List<LanguageServerCompletionItem>();
        foreach (var item in array.EnumerateArray())
        {
            if (!TryParseItem(item, out var parsed))
                continue;

            items.Add(parsed);
        }

        return items;
    }

    private static bool TryParseItem(JsonElement item, out LanguageServerCompletionItem parsed)
    {
        parsed = null!;

        if (item.ValueKind != JsonValueKind.Object)
            return false;

        if (!item.TryGetProperty("label", out var labelElement))
            return false;

        var label = labelElement.ValueKind switch
        {
            JsonValueKind.String => labelElement.GetString(),
            JsonValueKind.Object when labelElement.TryGetProperty("label", out var nested) &&
                                      nested.ValueKind == JsonValueKind.String => nested.GetString(),
            _ => null,
        };

        if (string.IsNullOrWhiteSpace(label))
            return false;

        string? insertText = null;
        if (item.TryGetProperty("insertText", out var insertElement) &&
            insertElement.ValueKind == JsonValueKind.String)
        {
            insertText = insertElement.GetString();
        }

        string? detail = null;
        if (item.TryGetProperty("detail", out var detailElement) &&
            detailElement.ValueKind == JsonValueKind.String)
        {
            detail = detailElement.GetString();
        }

        string? sortText = null;
        if (item.TryGetProperty("sortText", out var sortElement) &&
            sortElement.ValueKind == JsonValueKind.String)
        {
            sortText = sortElement.GetString();
        }

        LspRange? textEditRange = null;
        string? textEditNewText = null;
        if (item.TryGetProperty("textEdit", out var textEditElement) &&
            textEditElement.ValueKind == JsonValueKind.Object)
        {
            if (textEditElement.TryGetProperty("newText", out var newTextElement) &&
                newTextElement.ValueKind == JsonValueKind.String)
            {
                textEditNewText = newTextElement.GetString();
            }

            if (textEditElement.TryGetProperty("range", out var rangeElement) &&
                TryParseRange(rangeElement, out var range))
            {
                textEditRange = range;
            }
        }

        parsed = new LanguageServerCompletionItem(
            label,
            insertText,
            detail,
            sortText,
            textEditRange,
            textEditNewText);
        return true;
    }

    private static bool TryParseRange(JsonElement rangeElement, out LspRange range)
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
