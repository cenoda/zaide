using System;
using System.Collections.Generic;
using Zaide.Services;
using Zaide.Features.Language.Infrastructure.Lsp;

namespace Zaide.Features.Language.Application;

/// <summary>
/// Maps raw LSP completion items to document offsets for the active version.
/// </summary>
internal static class LanguageCompletionItemMapper
{
    public static IReadOnlyList<LanguageCompletionItem> MapItems(
        string documentText,
        int caretOffset,
        IReadOnlyList<LanguageServerCompletionItem> rawItems)
    {
        var items = new List<LanguageCompletionItem>();
        foreach (var raw in rawItems)
        {
            if (!TryMapItem(documentText, caretOffset, raw, out var mapped))
                continue;

            items.Add(mapped);
        }

        items.Sort(static (a, b) =>
        {
            var sort = string.Compare(a.SortText ?? a.Label, b.SortText ?? b.Label, StringComparison.Ordinal);
            return sort != 0 ? sort : string.Compare(a.Label, b.Label, StringComparison.Ordinal);
        });

        return items;
    }

    private static bool TryMapItem(
        string documentText,
        int caretOffset,
        LanguageServerCompletionItem raw,
        out LanguageCompletionItem mapped)
    {
        mapped = null!;

        var insertText = raw.TextEditNewText ?? raw.InsertText ?? raw.Label;
        if (string.IsNullOrEmpty(insertText))
            return false;

        int replaceStart;
        int replaceLength;

        if (raw.TextEditRange is LspRange range &&
            LspUtf16PositionMapper.TryMapRange(documentText, range, out var start, out var end))
        {
            replaceStart = start;
            replaceLength = Math.Max(0, end - start);
        }
        else
        {
            replaceStart = FindIdentifierPrefixStart(documentText, caretOffset);
            replaceLength = Math.Max(0, caretOffset - replaceStart);
        }

        if (replaceStart < 0 || replaceStart > documentText.Length)
            return false;

        if (replaceStart + replaceLength > documentText.Length)
            return false;

        mapped = new LanguageCompletionItem(
            raw.Label,
            insertText,
            replaceStart,
            replaceLength,
            raw.Detail,
            raw.SortText);
        return true;
    }

    internal static int FindIdentifierPrefixStart(string text, int caretOffset)
    {
        if (caretOffset < 0)
            caretOffset = 0;
        if (caretOffset > text.Length)
            caretOffset = text.Length;

        var index = caretOffset;
        while (index > 0 && IsIdentifierPart(text[index - 1]))
            index--;

        return index;
    }

    private static bool IsIdentifierPart(char ch) =>
        char.IsLetterOrDigit(ch) || ch is '_' or '@';
}
