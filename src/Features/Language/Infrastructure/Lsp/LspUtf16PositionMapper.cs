using System;

namespace Zaide.Features.Language.Infrastructure.Lsp;

/// <summary>
/// Converts between zero-based LSP utf-16 positions and document offsets.
/// Matches AvaloniaEdit/TextDocument UTF-16 code-unit column semantics
/// (M0 locked position encoding).
/// </summary>
public static class LspUtf16PositionMapper
{
    /// <summary>
    /// Maps a zero-based LSP (line, character) position to a document offset.
    /// Returns false when the position is outside the document or not on a line.
    /// </summary>
    public static bool TryGetOffset(string text, int line, int character, out int offset)
    {
        ArgumentNullException.ThrowIfNull(text);
        offset = 0;

        if (line < 0 || character < 0)
            return false;

        var length = text.Length;
        var currentLine = 0;
        var lineStart = 0;

        for (var i = 0; i <= length; i++)
        {
            var atEnd = i == length;
            var isNewline = !atEnd && text[i] == '\n';

            if (currentLine == line)
            {
                var lineEnd = isNewline || atEnd ? i : -1;
                if (lineEnd < 0)
                    continue;

                // Handle CRLF: if the character before '\n' is '\r', exclude it from line length.
                var contentEnd = lineEnd;
                if (isNewline && contentEnd > lineStart && text[contentEnd - 1] == '\r')
                    contentEnd--;

                var lineLength = contentEnd - lineStart;
                // LSP allows character == lineLength (end of line / before newline).
                if (character > lineLength)
                    return false;

                offset = lineStart + character;
                return true;
            }

            if (isNewline)
            {
                currentLine++;
                lineStart = i + 1;
            }
            else if (atEnd)
            {
                break;
            }
        }

        // Empty document: only (0, 0) is valid.
        if (length == 0 && line == 0 && character == 0)
        {
            offset = 0;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Maps a document offset to a zero-based LSP (line, character) position.
    /// Clamps offset into <c>[0, text.Length]</c>.
    /// </summary>
    public static (int Line, int Character) GetPosition(string text, int offset)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (offset < 0)
            offset = 0;
        if (offset > text.Length)
            offset = text.Length;

        var line = 0;
        var lineStart = 0;
        for (var i = 0; i < offset; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        return (line, offset - lineStart);
    }

    /// <summary>
    /// Validates and maps an LSP range to inclusive-start / exclusive-end offsets.
    /// Rejects inverted ranges and positions that do not resolve in the text.
    /// </summary>
    public static bool TryMapRange(
        string text,
        LspRange range,
        out int startOffset,
        out int endOffset)
    {
        startOffset = 0;
        endOffset = 0;

        if (!TryGetOffset(text, range.StartLine, range.StartCharacter, out startOffset))
            return false;

        if (!TryGetOffset(text, range.EndLine, range.EndCharacter, out endOffset))
            return false;

        if (endOffset < startOffset)
            return false;

        return true;
    }
}
