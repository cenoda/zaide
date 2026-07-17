using System;
using System.Collections.Generic;
using System.Text;
using Zaide.Services;
using Zaide.Features.Language.Infrastructure.Lsp;

namespace Zaide.Features.Language.Application;

/// <summary>
/// Pure whole-document TextEdit validation and application.
/// Rejects any set that is empty-invalid, out of range, inverted, or overlapping.
/// </summary>
public static class LanguageFormattingEditApplier
{
    /// <summary>
    /// Validates every edit against <paramref name="sourceText"/> and, when all
    /// are safe, applies them atomically to produce <paramref name="formattedText"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> when zero edits (formatted text equals source) or when every
    /// edit is valid and non-overlapping; <c>false</c> when any edit is unsafe.
    /// </returns>
    public static bool TryApply(
        string sourceText,
        IReadOnlyList<LanguageTextEdit> edits,
        out string formattedText)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        ArgumentNullException.ThrowIfNull(edits);

        if (edits.Count == 0)
        {
            formattedText = sourceText;
            return true;
        }

        var mapped = new List<(int Start, int End, string NewText)>(edits.Count);
        foreach (var edit in edits)
        {
            if (edit.NewText is null)
            {
                formattedText = sourceText;
                return false;
            }

            if (!LspUtf16PositionMapper.TryMapRange(
                    sourceText,
                    edit.Range,
                    out var start,
                    out var end))
            {
                formattedText = sourceText;
                return false;
            }

            mapped.Add((start, end, edit.NewText));
        }

        // Sort ascending by start, then end, for overlap detection.
        mapped.Sort(static (a, b) =>
        {
            var cmp = a.Start.CompareTo(b.Start);
            return cmp != 0 ? cmp : a.End.CompareTo(b.End);
        });

        for (var i = 1; i < mapped.Count; i++)
        {
            // Overlap if previous end extends past (or into) next start.
            // Adjacent (end == next start) is allowed.
            if (mapped[i - 1].End > mapped[i].Start)
            {
                formattedText = sourceText;
                return false;
            }
        }

        // Apply from the end of the document so earlier offsets stay stable.
        var builder = new StringBuilder(sourceText);
        for (var i = mapped.Count - 1; i >= 0; i--)
        {
            var (start, end, newText) = mapped[i];
            builder.Remove(start, end - start);
            builder.Insert(start, newText);
        }

        formattedText = builder.ToString();
        return true;
    }
}
