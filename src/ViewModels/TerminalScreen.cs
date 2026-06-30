using System;

namespace Zaide.ViewModels;

/// <summary>
/// A single styled character cell in the terminal screen buffer.
/// </summary>
internal readonly struct Cell
{
    public readonly char Char;
    public readonly CellAttribute Attribute;

    public Cell(char ch, CellAttribute attribute)
    {
        Char = ch;
        Attribute = attribute;
    }
}

/// <summary>
/// Visual attributes for a single terminal cell. Foreground and background are
/// ANSI color indices (0–15) or -1 for the terminal default.
/// </summary>
internal readonly struct CellAttribute
{
    public static readonly CellAttribute Default = new(-1, -1, bold: false, inverse: false);

    /// <summary>ANSI foreground color index (0–15) or -1 for default.</summary>
    public readonly int Foreground;

    /// <summary>ANSI background color index (0–15) or -1 for default.</summary>
    public readonly int Background;

    /// <summary>Bold / bright foreground flag.</summary>
    public readonly bool Bold;

    /// <summary>Inverse / reverse-video flag.</summary>
    public readonly bool Inverse;

    public CellAttribute(int foreground, int background, bool bold, bool inverse)
    {
        Foreground = foreground;
        Background = background;
        Bold = bold;
        Inverse = inverse;
    }
}

/// <summary>
/// Pure, UI-agnostic screen-buffer model representing the visible terminal
/// viewport. Maintains a 2D grid of <see cref="Cell"/> instances, a cursor
/// position, and current SGR attributes. No scrollback, no render concerns,
/// no Avalonia types.
/// </summary>
internal sealed class TerminalScreen
{
    private static readonly Cell EmptyCell = new(' ', CellAttribute.Default);

    private Cell[,] _buffer;
    private int _cursorRow;          // 0-based
    private int _cursorCol;          // 0-based
    private CellAttribute _currentAttributes = CellAttribute.Default;

    public TerminalScreen(int columns = 80, int rows = 24)
    {
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));

        Columns = columns;
        Rows = rows;
        _buffer = new Cell[rows, columns];
        Fill(0, 0, rows, columns, EmptyCell);
    }

    /// <summary>Width of the visible viewport in cells.</summary>
    public int Columns { get; private set; }

    /// <summary>Height of the visible viewport in cells.</summary>
    public int Rows { get; private set; }

    /// <summary>0-based cursor row.</summary>
    public int CursorRow => _cursorRow;

    /// <summary>0-based cursor column.</summary>
    public int CursorCol => _cursorCol;

    /// <summary>Get a single cell at the given 0-based position.</summary>
    public Cell GetCell(int row, int col) => _buffer[row, col];

    /// <summary>Get the text content of a single 0-based row (without attributes).</summary>
    public ReadOnlySpan<char> GetLine(int row)
    {
        var chars = new char[Columns];
        for (int c = 0; c < Columns; c++)
            chars[c] = _buffer[row, c].Char;
        return chars;
    }

    // ──────────────────────────────────────────────
    //  Write
    // ──────────────────────────────────────────────

    /// <summary>
    /// Writes a single character at the current cursor position, advances the
    /// cursor, wraps at the right edge, and scrolls if at the bottom row.
    /// </summary>
    public void Write(char ch)
    {
        // If cursor is past the right edge, wrap first.
        if (_cursorCol >= Columns)
        {
            _cursorCol = 0;
            _cursorRow++;
            ClampRowAndScroll();
        }

        _buffer[_cursorRow, _cursorCol] = new Cell(ch, _currentAttributes);
        _cursorCol++;

        if (_cursorCol >= Columns)
        {
            _cursorCol = 0;
            _cursorRow++;
            ClampRowAndScroll();
        }
    }

    /// <summary>
    /// Writes a run of printable characters, each handled by <see cref="Write(char)"/>.
    /// </summary>
    public void WriteText(ReadOnlySpan<char> text)
    {
        foreach (char ch in text)
            Write(ch);
    }

    // ──────────────────────────────────────────────
    //  C0 control execution
    // ──────────────────────────────────────────────

    /// <summary>
    /// Executes a C0 control operation using the parser's <see cref="AnsiC0Control"/>
    /// enumeration, ensuring a single point of truth for control semantics.
    /// </summary>
    public void ExecuteC0(AnsiC0Control control)
    {
        switch (control)
        {
            case AnsiC0Control.CarriageReturn:
                _cursorCol = 0;
                break;

            case AnsiC0Control.LineFeed:
                _cursorRow++;
                ClampRowAndScroll();
                break;

            case AnsiC0Control.Backspace:
                if (_cursorCol > 0)
                    _cursorCol--;
                break;

            case AnsiC0Control.Tab:
                _cursorCol = ((_cursorCol / 8) + 1) * 8;
                if (_cursorCol >= Columns)
                    _cursorCol = Columns - 1;
                break;

            case AnsiC0Control.Bell:
                // No-op this phase.
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(control), control, "Unsupported C0 control.");
        }
    }

    // ──────────────────────────────────────────────
    //  Cursor motion
    // ──────────────────────────────────────────────

    /// <summary>Move cursor up <paramref name="n"/> rows, clamped at the top edge.</summary>
    public void CursorUp(int n)
    {
        if (n <= 0) return;
        _cursorRow = Math.Max(0, _cursorRow - n);
    }

    /// <summary>Move cursor down <paramref name="n"/> rows, clamped at the bottom edge.</summary>
    public void CursorDown(int n)
    {
        if (n <= 0) return;
        _cursorRow = Math.Min(Rows - 1, _cursorRow + n);
    }

    /// <summary>Move cursor forward <paramref name="n"/> columns, clamped at the right edge.</summary>
    public void CursorForward(int n)
    {
        if (n <= 0) return;
        _cursorCol = Math.Min(Columns - 1, _cursorCol + n);
    }

    /// <summary>Move cursor back <paramref name="n"/> columns, clamped at the left edge.</summary>
    public void CursorBack(int n)
    {
        if (n <= 0) return;
        _cursorCol = Math.Max(0, _cursorCol - n);
    }

    /// <summary>
    /// Sets the cursor to a 1-based position (terminal convention). Values are
    /// clamped to the viewport bounds.
    /// </summary>
    public void CursorPosition(int row, int col)
    {
        _cursorRow = Math.Clamp(row - 1, 0, Rows - 1);
        _cursorCol = Math.Clamp(col - 1, 0, Columns - 1);
    }

    // ──────────────────────────────────────────────
    //  Erase
    // ──────────────────────────────────────────────

    /// <summary>
    /// Erase in Display.
    /// <list type="table">
    ///   <item><term>0</term><description>From cursor (inclusive) to end of screen.</description></item>
    ///   <item><term>1</term><description>From start of screen to cursor (inclusive).</description></item>
    ///   <item><term>2</term><description>Entire visible screen.</description></item>
    ///   <item><term>3</term><description>Visible screen (scrollback no-op this phase; same as 2).</description></item>
    /// </list>
    /// </summary>
    public void EraseDisplay(int param)
    {
        switch (param)
        {
            case 0:
                EraseRange(_cursorRow, _cursorCol, _cursorRow, Columns - 1);
                for (int r = _cursorRow + 1; r < Rows; r++)
                    EraseRange(r, 0, r, Columns - 1);
                break;

            case 1:
                for (int r = 0; r < _cursorRow; r++)
                    EraseRange(r, 0, r, Columns - 1);
                EraseRange(_cursorRow, 0, _cursorRow, _cursorCol);
                break;

            case 2:
            case 3: // 3 = scrollback clear; no scrollback yet, so same as 2.
                EraseRange(0, 0, Rows - 1, Columns - 1);
                break;
        }
    }

    /// <summary>
    /// Erase in Line.
    /// <list type="table">
    ///   <item><term>0</term><description>From cursor (inclusive) to end of current line.</description></item>
    ///   <item><term>1</term><description>From start of current line to cursor (inclusive).</description></item>
    ///   <item><term>2</term><description>Entire current line.</description></item>
    /// </list>
    /// </summary>
    public void EraseLine(int param)
    {
        switch (param)
        {
            case 0:
                EraseRange(_cursorRow, _cursorCol, _cursorRow, Columns - 1);
                break;

            case 1:
                EraseRange(_cursorRow, 0, _cursorRow, _cursorCol);
                break;

            case 2:
                EraseRange(_cursorRow, 0, _cursorRow, Columns - 1);
                break;
        }
    }

    // ──────────────────────────────────────────────
    //  SGR — Select Graphic Rendition
    // ──────────────────────────────────────────────

    /// <summary>
    /// Applies SGR parameters to <see cref="_currentAttributes"/>, which affect
    /// subsequent writes only — previously written cells are unchanged.
    /// </summary>
    public void SetSgr(int[] parameters)
    {
        int i = 0;
        while (i < parameters.Length)
        {
            int p = parameters[i];

            if (p == 0)
            {
                _currentAttributes = CellAttribute.Default;
            }
            else if (p == 1)
            {
                _currentAttributes = WithBold(true);
            }
            else if (p == 7)
            {
                _currentAttributes = WithInverse(true);
            }
            else if (p >= 30 && p <= 37)
            {
                _currentAttributes = WithForeground(p - 30);
            }
            else if (p == 38 && i + 2 < parameters.Length && parameters[i + 1] == 5)
            {
                // 256-color foreground — consumed and ignored this phase.
                i += 2;
            }
            else if (p == 39)
            {
                _currentAttributes = WithForeground(-1);
            }
            else if (p >= 40 && p <= 47)
            {
                _currentAttributes = WithBackground(p - 40);
            }
            else if (p == 48 && i + 2 < parameters.Length && parameters[i + 1] == 5)
            {
                // 256-color background — consumed and ignored this phase.
                i += 2;
            }
            else if (p == 49)
            {
                _currentAttributes = WithBackground(-1);
            }
            else if (p >= 90 && p <= 97)
            {
                _currentAttributes = WithForeground(p - 90 + 8);
            }
            else if (p >= 100 && p <= 107)
            {
                _currentAttributes = WithBackground(p - 100 + 8);
            }
            // else: unsupported parameter — silently ignored.

            i++;
        }
    }

    // ──────────────────────────────────────────────
    //  Scroll
    // ──────────────────────────────────────────────

    /// <summary>
    /// Scrolls the buffer up by one row. The top row is discarded, all other rows
    /// shift up, and the bottom row is cleared with empty cells. Cursor is
    /// reset to the bottom row.
    /// </summary>
    public void Scroll()
    {
        for (int r = 1; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
                _buffer[r - 1, c] = _buffer[r, c];
        }

        for (int c = 0; c < Columns; c++)
            _buffer[Rows - 1, c] = EmptyCell;

        _cursorRow = Rows - 1;
    }

    // ──────────────────────────────────────────────
    //  Resize
    // ──────────────────────────────────────────────

    /// <summary>
    /// Resizes the screen buffer to the new dimensions. Overlapping cells in the
    /// top-left area are preserved; new cells are filled with spaces and default
    /// attributes. Content that falls outside the new grid is discarded. No line
    /// reflow is performed. The cursor is clamped into the new bounds.
    /// </summary>
    public void Resize(int columns, int rows)
    {
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));

        var newBuffer = new Cell[rows, columns];

        int copyRows = Math.Min(Rows, rows);
        int copyCols = Math.Min(Columns, columns);

        for (int r = 0; r < copyRows; r++)
        {
            for (int c = 0; c < copyCols; c++)
                newBuffer[r, c] = _buffer[r, c];
        }

        // Fill any new (non-overlapping) cells with empty cells.
        // Fill new columns in preserved rows.
        if (columns > Columns)
        {
            for (int r = 0; r < copyRows; r++)
            {
                for (int c = Columns; c < columns; c++)
                    newBuffer[r, c] = EmptyCell;
            }
        }

        // Fill new rows entirely.
        for (int r = Rows; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
                newBuffer[r, c] = EmptyCell;
        }

        _buffer = newBuffer;
        Columns = columns;
        Rows = rows;

        // Clamp cursor into new bounds.
        _cursorRow = Math.Min(_cursorRow, rows - 1);
        _cursorCol = Math.Min(_cursorCol, columns - 1);
    }

    // ──────────────────────────────────────────────
    //  Internal helpers
    // ──────────────────────────────────────────────

    private void ClampRowAndScroll()
    {
        if (_cursorRow >= Rows)
        {
            _cursorRow = Rows - 1;
            Scroll();
        }
    }

    private void EraseRange(int rowStart, int colStart, int rowEnd, int colEnd)
    {
        for (int r = rowStart; r <= rowEnd; r++)
        {
            for (int c = colStart; c <= colEnd; c++)
                _buffer[r, c] = EmptyCell;
        }
    }

    private void Fill(int rowStart, int colStart, int rowEnd, int colEnd, Cell cell)
    {
        for (int r = rowStart; r < rowEnd; r++)
        {
            for (int c = colStart; c < colEnd; c++)
                _buffer[r, c] = cell;
        }
    }

    private CellAttribute WithBold(bool bold) =>
        new(_currentAttributes.Foreground, _currentAttributes.Background, bold, _currentAttributes.Inverse);

    private CellAttribute WithInverse(bool inverse) =>
        new(_currentAttributes.Foreground, _currentAttributes.Background, _currentAttributes.Bold, inverse);

    private CellAttribute WithForeground(int fg) =>
        new(fg, _currentAttributes.Background, _currentAttributes.Bold, _currentAttributes.Inverse);

    private CellAttribute WithBackground(int bg) =>
        new(_currentAttributes.Foreground, bg, _currentAttributes.Bold, _currentAttributes.Inverse);
}