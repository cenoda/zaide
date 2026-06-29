using System;
using System.Text;

namespace Zaide.ViewModels;

/// <summary>
/// Owns the terminal scrollback buffer and applies a small, MVP subset of
/// control-character behavior so raw PTY output renders sensibly without a
/// full ANSI/VT100 parser:
///
/// <list type="bullet">
///   <item><description><c>\r</c> — move the write cursor to the start of the
///   current line so following characters overwrite it (progress bars).</description></item>
///   <item><description><c>\b</c> — move the write cursor back one column,
///   bounded at the line start (shells erase with <c>\b \b</c>).</description></item>
///   <item><description><c>\n</c> — break to a new line.</description></item>
/// </list>
///
/// <para>Carriage-return overwrite intentionally leaves trailing characters of
/// a longer previous line in place, matching real terminal behavior. Full
/// ANSI/CSI sequences (color, cursor addressing, clear-screen) are left in the
/// text verbatim — see the phase-3.5 plan and TOFIX for deferred work.</para>
///
/// <para>This type is UI-agnostic and not thread-safe; callers must serialize
/// access (the ViewModel mutates it under its buffer lock).</para>
/// </summary>
internal sealed class TerminalOutputBuffer
{
    private readonly StringBuilder _sb = new();
    private readonly int _maxChars;

    // Index into _sb where the next printable character is written. Stays
    // within the last (current) line except when it equals _sb.Length.
    private int _cursor;

    public TerminalOutputBuffer(int maxChars)
    {
        if (maxChars <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maxChars), maxChars, "Capacity must be positive.");
        _maxChars = maxChars;
    }

    /// <summary>Current buffer length in characters.</summary>
    public int Length => _sb.Length;

    /// <summary>The accumulated terminal text.</summary>
    public string Text => _sb.ToString();

    public override string ToString() => _sb.ToString();

    /// <summary>Empties the buffer and resets the cursor.</summary>
    public void Clear()
    {
        _sb.Clear();
        _cursor = 0;
    }

    /// <summary>
    /// Appends decoded terminal text, applying the supported control-character
    /// subset, then trims the front if the capacity is exceeded.
    /// </summary>
    public void Append(string text)
    {
        foreach (char c in text)
        {
            switch (c)
            {
                case '\r':
                    _cursor = CurrentLineStart();
                    break;

                case '\b':
                    int lineStart = CurrentLineStart();
                    if (_cursor > lineStart) _cursor--;
                    break;

                case '\n':
                    _sb.Append('\n');
                    _cursor = _sb.Length;
                    break;

                default:
                    WritePrintable(c);
                    break;
            }
        }

        Trim();
    }

    private void WritePrintable(char c)
    {
        if (_cursor >= _sb.Length)
        {
            _sb.Append(c);
            _cursor = _sb.Length;
        }
        else if (_sb[_cursor] == '\n')
        {
            // Defensive: never overwrite a line break (would merge two lines).
            _sb.Insert(_cursor, c);
            _cursor++;
        }
        else
        {
            _sb[_cursor] = c;
            _cursor++;
        }
    }

    // Start of the line the cursor is on: the index just after the most recent
    // '\n' before the cursor, or 0 if there is none.
    private int CurrentLineStart()
    {
        int end = Math.Min(_cursor, _sb.Length);
        for (int i = end - 1; i >= 0; i--)
            if (_sb[i] == '\n') return i + 1;
        return 0;
    }

    private void Trim()
    {
        int excess = _sb.Length - _maxChars;
        if (excess <= 0) return;

        _sb.Remove(0, excess);
        _cursor = Math.Max(0, _cursor - excess);
    }
}
