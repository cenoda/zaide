using System;
using System.Collections.Generic;

namespace Zaide.Features.Editor.Domain;

/// <summary>
/// A deterministic, syntax-neutral brace-region discovery heuristic for
/// active-editor code folding. Does NOT parse C# or depend on any language
/// service. Works on any text that contains balanced <c>{</c> … <c>}</c>
/// pairs.
/// </summary>
/// <remarks>
/// <para><b>Heuristic contract (locked for Phase 9 M4):</b></para>
/// <list type="bullet">
/// <item><b>Eligible text:</b> Any <c>{</c> character that has a matching
/// <c>}</c> character later in the text, provided the region spans at least
/// <see cref="MinRegionLines"/> lines.</item>
/// <item><b>No-folding cases:</b> Text with no <c>{</c> characters; text where
/// every <c>{</c> is unbalanced; regions with fewer than
/// <see cref="MinRegionLines"/> newlines between the opening and closing
/// braces.</item>
/// <item><b>Malformed/unbalanced behavior:</b> Unmatched <c>{</c> (no closing
/// <c>}</c>) are ignored. Unmatched <c>}</c> (no opening <c>{</c>) are
/// ignored. No error is raised — the strategy silently skips unmatched
/// braces.</item>
/// <item><b>Minimum region size:</b> <see cref="MinRegionLines"/> lines (2).
/// A region that opens and closes on the same line, or on the immediately
/// following line, is not foldable.</item>
/// <item><b>Fold title:</b> The trimmed text on the same line after the
/// opening <c>{</c>, truncated to <see cref="MaxTitleLength"/> characters.
/// If the remainder is empty, the title is <c>"{...}"</c>.</item>
/// <item><b>Nested regions:</b> Fully supported via a stack-based matcher.
/// Inner regions are discovered and reported alongside outer regions.</item>
/// <item><b>Ordering:</b> Depth-first by start offset. For regions with the
/// same start offset (impossible in well-formed text), outer-first.</item>
/// <item><b>Current-fold selection (for toggle):</b> When the caret is inside
/// one or more regions, the innermost containing region is the "current"
/// fold. Callers must use <see cref="BraceRegion"/> depth or span-length to
/// select the innermost.</item>
/// </list>
/// <para>
/// This class has zero dependencies on AvaloniaEdit. It returns
/// <see cref="BraceRegion"/> values that the View layer converts to
/// <c>AvaloniaEdit.Folding.NewFolding</c> objects.
/// </para>
/// </remarks>
public static class BraceFoldingStrategy
{
    /// <summary>
    /// Minimum number of lines a brace region must span to be considered
    /// foldable. A region must have at least this many newlines between the
    /// opening <c>{</c> line and the closing <c>}</c> line.
    /// </summary>
    public const int MinRegionLines = 2;

    /// <summary>
    /// Maximum characters in a fold title before truncation.
    /// </summary>
    public const int MaxTitleLength = 80;

    /// <summary>
    /// Discovers all foldable brace regions in <paramref name="text"/>.
    /// Returns an empty list when there are no eligible regions.
    /// </summary>
    /// <param name="text">The full document text to scan.</param>
    /// <returns>
    /// Deterministically ordered list of discovered regions (depth-first by
    /// start offset).
    /// </returns>
    public static IReadOnlyList<BraceRegion> Discover(string text)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<BraceRegion>();

        var regions = new List<BraceRegion>();
        var stack = new Stack<BraceOpen>();

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '{')
            {
                // Extract the rest of the line for the fold title.
                var title = ExtractTitle(text, i + 1);
                stack.Push(new BraceOpen(i, title, stack.Count));
            }
            else if (ch == '}')
            {
                if (stack.Count > 0)
                {
                    var open = stack.Pop();
                    // EndOffset = exclusive (position after '}')
                    var endOffset = i + 1;

                    if (MeetsMinLineCount(text, open.StartOffset, i))
                    {
                        regions.Add(new BraceRegion(
                            startOffset: open.StartOffset,
                            endOffset: endOffset,
                            title: open.Title,
                            depth: open.Depth));
                    }
                }
                // Unmatched '}' is silently ignored.
            }
        }
        // Unmatched '{' on the stack are silently ignored.

        // Sort by start offset (depth-first ordering) so outermost regions
        // appear before the nested regions they contain.
        regions.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));

        return regions;
    }

    /// <summary>
    /// Returns the innermost <see cref="BraceRegion"/> that contains
    /// <paramref name="caretOffset"/>, or null when the caret is not inside
    /// any region.
    /// </summary>
    /// <param name="regions">The ordered region list from <see cref="Discover"/>.</param>
    /// <param name="caretOffset">The caret offset (0-based) in the document.</param>
    public static BraceRegion? FindInnermostContaining(
        IReadOnlyList<BraceRegion> regions,
        int caretOffset)
    {
        BraceRegion? best = null;
        foreach (var r in regions)
        {
            if (caretOffset >= r.StartOffset && caretOffset < r.EndOffset)
            {
                if (best is null || r.Depth > best.Depth)
                    best = r;
            }
        }
        return best;
    }

    private static string ExtractTitle(string text, int fromIndex)
    {
        var end = fromIndex;
        while (end < text.Length && text[end] != '\n' && text[end] != '\r')
            end++;

        var length = Math.Min(end - fromIndex, MaxTitleLength);
        if (length == 0)
            return "{...}";

        // Trim leading whitespace after the brace.
        var start = fromIndex;
        while (start < end && (text[start] == ' ' || text[start] == '\t'))
            start++;

        if (start >= end)
            return "{...}";

        var trimmedLength = Math.Min(end - start, MaxTitleLength);
        var title = text.Substring(start, trimmedLength);

        // Also trim trailing whitespace.
        title = title.TrimEnd();

        return string.IsNullOrEmpty(title) ? "{...}" : title;
    }

    private static bool MeetsMinLineCount(string text, int openOffset, int closeOffset)
    {
        var lineCount = 0;
        for (var i = openOffset; i < closeOffset; i++)
        {
            if (text[i] == '\n')
            {
                lineCount++;
                if (lineCount >= MinRegionLines)
                    return true;
            }
        }
        return false;
    }

    private readonly struct BraceOpen
    {
        public readonly int StartOffset;
        public readonly string Title;
        public readonly int Depth;

        public BraceOpen(int startOffset, string title, int depth)
        {
            StartOffset = startOffset;
            Title = title;
            Depth = depth;
        }
    }
}

/// <summary>
/// A discovered brace region ready for folding. Immutable data object —
/// callers in the View layer convert these to
/// <c>AvaloniaEdit.Folding.NewFolding</c> values.
/// </summary>
public sealed class BraceRegion
{
    /// <summary>Offset of the opening <c>{</c> character.</summary>
    public int StartOffset { get; }

    /// <summary>Offset AFTER the closing <c>}</c> character (exclusive end).</summary>
    public int EndOffset { get; }

    /// <summary>Human-readable placeholder shown when the region is folded.</summary>
    public string Title { get; }

    /// <summary>Nesting depth: 0 = outermost, 1 = first nested, etc.</summary>
    public int Depth { get; }

    public BraceRegion(int startOffset, int endOffset, string title, int depth)
    {
        StartOffset = startOffset;
        EndOffset = endOffset;
        Title = title;
        Depth = depth;
    }

    /// <summary>Length of this region in characters (EndOffset - StartOffset).</summary>
    public int Length => EndOffset - StartOffset;

    public override string ToString()
        => $"[{StartOffset}..{EndOffset}) depth={Depth} \"{Title}\"";
}
