using System;
using AvaloniaEdit.Document;

namespace Zaide.Features.Editor.Presentation;

/// <summary>
/// Per-document-version cache of indent guide level counts.
/// Invalidates when the document version or indentation size changes.
/// </summary>
internal sealed class IndentGuideLevelCache
{
    private ITextSourceVersion? _version;
    private int _indentationSize;
    private int[] _levels = Array.Empty<int>();

    /// <summary>
    /// Returns the number of indent guides for the given 1-based line number.
    /// Rebuilds the full line cache when the document version or indentation size changes.
    /// </summary>
    public int GetGuideLevelCount(TextDocument document, int lineNumber, int indentationSize)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (indentationSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(indentationSize));
        if (lineNumber < 1 || lineNumber > document.LineCount)
            return 0;

        Ensure(document, indentationSize);
        return _levels[lineNumber - 1];
    }

    /// <summary>
    /// True when the cache is warm for the given document version and indentation size.
    /// Exposed for tests.
    /// </summary>
    internal bool IsWarmFor(TextDocument document, int indentationSize)
    {
        ArgumentNullException.ThrowIfNull(document);
        return ReferenceEquals(_version, document.Version)
            && _indentationSize == indentationSize
            && _levels.Length == document.LineCount;
    }

    /// <summary>
    /// Number of cached lines (0 when cold). Exposed for tests.
    /// </summary>
    internal int CachedLineCount => _levels.Length;

    private void Ensure(TextDocument document, int indentationSize)
    {
        if (IsWarmFor(document, indentationSize))
            return;

        Rebuild(document, indentationSize);
    }

    private void Rebuild(TextDocument document, int indentationSize)
    {
        var lineCount = document.LineCount;
        var levels = _levels.Length == lineCount ? _levels : new int[lineCount];

        for (var i = 0; i < lineCount; i++)
        {
            var line = document.GetLineByNumber(i + 1);
            if (line.TotalLength <= 0)
            {
                levels[i] = 0;
                continue;
            }

            // Leading whitespace is short; GetTextAsMemory avoids a full-line string when possible.
            var memory = document.GetTextAsMemory(line.Offset, line.TotalLength);
            levels[i] = IndentGuideMetrics.GetVisibleIndentGuideLevelCount(
                memory.Span,
                indentationSize);
        }

        _levels = levels;
        _version = document.Version;
        _indentationSize = indentationSize;
    }
}
