using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Zaide.Services;

/// <summary>Parses <c>textDocument/formatting</c> JSON into structured edits.</summary>
internal static class LanguageServerFormattingParser
{
    public static LanguageServerFormattingResult? Parse(JsonElement? element)
    {
        if (element is null || element.Value.ValueKind == JsonValueKind.Null)
            return LanguageServerFormattingResult.Empty;

        var root = element.Value;
        if (root.ValueKind != JsonValueKind.Array)
            return null;

        var edits = new List<LanguageTextEdit>();
        foreach (var item in root.EnumerateArray())
        {
            if (!TryParseEdit(item, out var edit))
                return null;

            edits.Add(edit);
        }

        return new LanguageServerFormattingResult(edits);
    }

    private static bool TryParseEdit(JsonElement element, out LanguageTextEdit edit)
    {
        edit = null!;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        if (!element.TryGetProperty("range", out var rangeElement) ||
            !TryParseRange(rangeElement, out var range))
        {
            return false;
        }

        if (!element.TryGetProperty("newText", out var newTextElement) ||
            newTextElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var newText = newTextElement.GetString() ?? string.Empty;
        edit = new LanguageTextEdit(range, newText);
        return true;
    }

    private static bool TryParseRange(JsonElement element, out LanguageDiagnosticRange range)
    {
        range = default;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        if (!element.TryGetProperty("start", out var start) ||
            !element.TryGetProperty("end", out var end))
        {
            return false;
        }

        if (!TryParsePosition(start, out var startLine, out var startCharacter) ||
            !TryParsePosition(end, out var endLine, out var endCharacter))
        {
            return false;
        }

        range = new LanguageDiagnosticRange(startLine, startCharacter, endLine, endCharacter);
        return true;
    }

    private static bool TryParsePosition(JsonElement element, out int line, out int character)
    {
        line = 0;
        character = 0;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        if (!element.TryGetProperty("line", out var lineElement) ||
            lineElement.ValueKind != JsonValueKind.Number ||
            !lineElement.TryGetInt32(out line))
        {
            return false;
        }

        if (!element.TryGetProperty("character", out var characterElement) ||
            characterElement.ValueKind != JsonValueKind.Number ||
            !characterElement.TryGetInt32(out character))
        {
            return false;
        }

        return line >= 0 && character >= 0;
    }
}
