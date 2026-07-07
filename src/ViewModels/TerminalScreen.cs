using System;
using System.Collections.Generic;

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

    /// <summary>256-color foreground index (0–255) or -1 for default.</summary>
    public readonly int Foreground256;

    /// <summary>256-color background index (0–255) or -1 for default.</summary>
    public readonly int Background256;

    /// <summary>Truecolor foreground RGB value (0xRRGGBB) or -1 for default.</summary>
    public readonly int ForegroundTrueColor;

    /// <summary>Truecolor background RGB value (0xRRGGBB) or -1 for default.</summary>
    public readonly int BackgroundTrueColor;

    public CellAttribute(int foreground, int background, bool bold, bool inverse)
        : this(foreground, background, bold, inverse, -1, -1, -1, -1)
    {
    }

    public CellAttribute(int foreground, int background, bool bold, bool inverse, int foreground256, int background256, int foregroundTrueColor, int backgroundTrueColor)
    {
        Foreground = foreground;
        Background = background;
        Bold = bold;
        Inverse = inverse;
        Foreground256 = foreground256;
        Background256 = background256;
        ForegroundTrueColor = foregroundTrueColor;
        BackgroundTrueColor = backgroundTrueColor;
    }
}

/// <summary>
/// Pure, UI-agnostic screen-buffer model. Maintains explicit main-screen and
/// alternate-screen state, a saved-cursor value, retained main-screen
/// scrollback, and the erase/resize/cursor/SGR behaviors needed for TUI
/// compatibility. No Avalonia types, no parser concerns.
/// </summary>
internal sealed class TerminalScreen
{
    private const int MaxScrollbackRows = 2000;
    private static readonly Cell EmptyCell = new(' ', CellAttribute.Default);

    private readonly ScreenBuffer _main;
    private readonly ScreenBuffer _alternate;
    private ScreenBuffer _active;
    private SavedCursorState _savedCursor = new(-1, -1);

    public TerminalScreen(int columns = 80, int rows = 24)
    {
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));

        Columns = columns;
        Rows = rows;
        // Main screen retains scrollback; alternate screen is a temporary
        // full-screen surface with no independent scrollback history.
        _main = new ScreenBuffer(columns, rows, retainsScrollback: true);
        _alternate = new ScreenBuffer(columns, rows, retainsScrollback: false);
        _active = _main;
    }

    /// <summary>Width of the visible viewport in cells.</summary>
    public int Columns { get; private set; }

    /// <summary>Height of the visible viewport in cells.</summary>
    public int Rows { get; private set; }

    /// <summary>0-based cursor row of the active buffer.</summary>
    public int CursorRow => _active.CursorRow;

    /// <summary>0-based cursor column of the active buffer.</summary>
    public int CursorCol => _active.CursorCol;

    /// <summary>Whether the alternate screen is the currently active buffer.</summary>
    public bool IsAlternateActive => _active == _alternate;

    /// <summary>Number of retained rows above the active viewport.</summary>
    public int ScrollbackRowCount => _active.ScrollbackRowCount;

    /// <summary>The currently saved cursor, or an invalid (-1,-1) value if none.</summary>
    public SavedCursorState SavedCursor => _savedCursor;

    /// <summary>Get a single cell at the given 0-based position of the active buffer.</summary>
    public Cell GetCell(int row, int col) => _active.GetCell(row, col);

    /// <summary>Get a retained scrollback row of the active buffer at the given 0-based index.</summary>
    public IReadOnlyList<Cell> GetScrollbackRow(int row) => _active.GetScrollbackRow(row);

    /// <summary>Get the text content of a single 0-based row (without attributes) of the active buffer.</summary>
    public ReadOnlySpan<char> GetLine(int row) => _active.GetLine(row);

    // ──────────────────────────────────────────────
    //  Write
    // ──────────────────────────────────────────────

    /// <summary>
    /// Writes a single character at the current cursor position, advances the
    /// cursor, wraps at the right edge, and scrolls if at the bottom row.
    /// </summary>
    public void Write(char ch) => _active.Write(ch);

    /// <summary>
    /// Writes a run of printable characters, each handled by <see cref="Write(char)"/>.
    /// </summary>
    public void WriteText(ReadOnlySpan<char> text) => _active.WriteText(text);

    // ──────────────────────────────────────────────
    //  C0 control execution
    // ──────────────────────────────────────────────

    /// <summary>
    /// Executes a C0 control operation using the parser's <see cref="AnsiC0Control"/>
    /// enumeration, ensuring a single point of truth for control semantics.
    /// </summary>
    public void ExecuteC0(AnsiC0Control control) => _active.ExecuteC0(control);

    // ──────────────────────────────────────────────
    //  Cursor motion
    // ──────────────────────────────────────────────

    /// <summary>Move cursor up <paramref name="n"/> rows, clamped at the top edge.</summary>
    public void CursorUp(int n) => _active.CursorUp(n);

    /// <summary>Move cursor down <paramref name="n"/> rows, clamped at the bottom edge.</summary>
    public void CursorDown(int n) => _active.CursorDown(n);

    /// <summary>Move cursor forward <paramref name="n"/> columns, clamped at the right edge.</summary>
    public void CursorForward(int n) => _active.CursorForward(n);

    /// <summary>Move cursor back <paramref name="n"/> columns, clamped at the left edge.</summary>
    public void CursorBack(int n) => _active.CursorBack(n);

    /// <summary>
    /// Sets the cursor to a 1-based position (terminal convention). Values are
    /// clamped to the viewport bounds.
    /// </summary>
    public void CursorPosition(int row, int col) => _active.CursorPosition(row, col);

    // ──────────────────────────────────────────────
    //  Erase
    // ──────────────────────────────────────────────

    /// <summary>
    /// Erase in Display. Operates on the active buffer only.
    /// <list type="table">
    ///   <item><term>0</term><description>From cursor (inclusive) to end of screen.</description></item>
    ///   <item><term>1</term><description>From start of screen to cursor (inclusive).</description></item>
    ///   <item><term>2</term><description>Entire visible screen.</description></item>
    ///   <item><term>3</term><description>Visible screen plus the active buffer's scrollback.</description></item>
    /// </list>
    /// While the alternate screen is active, <c>3</c> clears only the alternate
    /// surface; the main screen's retained scrollback is never touched.
    /// </summary>
    public void EraseDisplay(int param) => _active.EraseDisplay(param);

    /// <summary>
    /// Erase in Line. Operates on the active buffer only.
    /// </summary>
    public void EraseLine(int param) => _active.EraseLine(param);

    // ──────────────────────────────────────────────
    //  SGR — Select Graphic Rendition
    // ──────────────────────────────────────────────

    /// <summary>
    /// Applies SGR parameters to the active buffer's write attributes, affecting
    /// subsequent writes only.
    /// </summary>
    public void SetSgr(int[] parameters) => _active.SetSgr(parameters);

    // ──────────────────────────────────────────────
    //  Scroll
    // ──────────────────────────────────────────────

    /// <summary>
    /// Scrolls the active buffer up by one row.
    /// </summary>
    public void Scroll() => _active.Scroll();

    // ──────────────────────────────────────────────
    //  Alternate screen & saved cursor
    // ──────────────────────────────────────────────

    /// <summary>
    /// Switches to the alternate screen buffer. The alternate buffer is presented
    /// as a clean, empty surface; the main screen and its scrollback are
    /// preserved untouched. When <paramref name="saveCursor"/> is set, the
    /// current cursor cell is saved first (DEC 1049 entry semantics).
    /// </summary>
    public void EnterAlternateScreen(bool saveCursor = false)
    {
        if (IsAlternateActive)
        {
            return;
        }

        if (saveCursor)
        {
            SaveCursor();
        }

        _alternate.Reset();
        _active = _alternate;
    }

    /// <summary>
    /// Switches back to the main screen buffer. The main screen and its
    /// scrollback are restored exactly as they were before the alternate screen
    /// was entered. When <paramref name="restoreCursor"/> is set, the most
    /// recently saved cursor is restored (DEC 1049 exit semantics).
    /// </summary>
    public void ExitAlternateScreen(bool restoreCursor = false)
    {
        if (!IsAlternateActive)
        {
            return;
        }

        _active = _main;
        if (restoreCursor)
        {
            RestoreCursor();
        }
    }

    /// <summary>
    /// Captures the active buffer's current cursor row/column into the saved
    /// cursor state (ESC 7 / DEC 1048). Does not capture SGR attributes.
    /// </summary>
    public void SaveCursor() =>
        _savedCursor = new SavedCursorState(_active.CursorRow, _active.CursorCol);

    /// <summary>
    /// Restores the cursor to the saved cell (ESC 8 / DEC 1048). The saved
    /// coordinates are clamped to the current viewport. SGR attributes are left
    /// unchanged. No-op when no cursor has been saved.
    /// </summary>
    public void RestoreCursor()
    {
        if (!_savedCursor.IsValid)
        {
            return;
        }

        _active.CursorRow = Math.Clamp(_savedCursor.Row, 0, Rows - 1);
        _active.CursorCol = Math.Clamp(_savedCursor.Col, 0, Columns - 1);
    }

    // ──────────────────────────────────────────────
    //  Resize
    // ──────────────────────────────────────────────

    /// <summary>
    /// Resizes both the main and alternate screen buffers so that switching
    /// back to the main screen after a resize does not show stale dimensions.
    /// The saved cursor is clamped to the new bounds but not invalidated.
    /// </summary>
    public void Resize(int columns, int rows)
    {
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));

        _main.Resize(columns, rows);
        _alternate.Resize(columns, rows);
        Columns = columns;
        Rows = rows;

        _savedCursor = _savedCursor.Clamp(Rows, Columns);
    }

    /// <summary>
    /// A captured cursor position (row/column only) for DEC save/restore cursor.
    /// Row/Column are -1 when no cursor has been saved yet.
    /// </summary>
    public readonly struct SavedCursorState
    {
        /// <summary>0-based saved cursor row, or -1 when invalid.</summary>
        public readonly int Row;

        /// <summary>0-based saved cursor column, or -1 when invalid.</summary>
        public readonly int Col;

        public SavedCursorState(int row, int col)
        {
            Row = row;
            Col = col;
        }

        /// <summary>Whether a cursor has been saved (Row/Col both non-negative).</summary>
        public bool IsValid => Row >= 0 && Col >= 0;

        /// <summary>Returns a copy clamped to the given viewport bounds.</summary>
        public SavedCursorState Clamp(int rows, int columns) =>
            new(Math.Clamp(Row, 0, rows - 1), Math.Clamp(Col, 0, columns - 1));
    }

    /// <summary>
    /// One independent buffer surface (main or alternate). Owns its grid,
    /// cursor, scrollback, and write attributes. Scrolling on a buffer that does
    /// not retain scrollback discards the scrolled row instead of exposing it.
    /// </summary>
    private sealed class ScreenBuffer
    {
        private static readonly Cell EmptyCell = new(' ', CellAttribute.Default);

        public Cell[,] Buffer;
        public readonly List<Cell[]> Scrollback = new();
        public int CursorRow;          // 0-based
        public int CursorCol;          // 0-based
        public int Columns;
        public int Rows;
        public CellAttribute CurrentAttributes = CellAttribute.Default;
        public readonly bool RetainsScrollback;

        public ScreenBuffer(int columns, int rows, bool retainsScrollback)
        {
            Columns = columns;
            Rows = rows;
            RetainsScrollback = retainsScrollback;
            Buffer = new Cell[rows, columns];
            Fill(0, 0, rows, columns, EmptyCell);
        }

        /// <summary>Reset to a clean, empty surface at the home cell.</summary>
        public void Reset()
        {
            Buffer = new Cell[Rows, Columns];
            Fill(0, 0, Rows, Columns, EmptyCell);
            Scrollback.Clear();
            CursorRow = 0;
            CursorCol = 0;
            CurrentAttributes = CellAttribute.Default;
        }

        public Cell GetCell(int row, int col) => Buffer[row, col];

        public IReadOnlyList<Cell> GetScrollbackRow(int row) => Scrollback[row];

        public int ScrollbackRowCount => Scrollback.Count;

        public ReadOnlySpan<char> GetLine(int row)
        {
            var chars = new char[Columns];
            for (int c = 0; c < Columns; c++)
                chars[c] = Buffer[row, c].Char;
            return chars;
        }

        public void Write(char ch)
        {
            if (CursorCol >= Columns)
            {
                CursorCol = 0;
                CursorRow++;
                ClampRowAndScroll();
            }

            Buffer[CursorRow, CursorCol] = new Cell(ch, CurrentAttributes);
            CursorCol++;

            if (CursorCol >= Columns)
            {
                CursorCol = 0;
                CursorRow++;
                ClampRowAndScroll();
            }
        }

        public void WriteText(ReadOnlySpan<char> text)
        {
            foreach (char ch in text)
                Write(ch);
        }

        public void ExecuteC0(AnsiC0Control control)
        {
            switch (control)
            {
                case AnsiC0Control.CarriageReturn:
                    CursorCol = 0;
                    break;

                case AnsiC0Control.LineFeed:
                    CursorRow++;
                    ClampRowAndScroll();
                    break;

                case AnsiC0Control.Backspace:
                    if (CursorCol > 0)
                        CursorCol--;
                    break;

                case AnsiC0Control.Tab:
                    CursorCol = ((CursorCol / 8) + 1) * 8;
                    if (CursorCol >= Columns)
                        CursorCol = Columns - 1;
                    break;

                case AnsiC0Control.Bell:
                    // No-op this phase.
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(control), control, "Unsupported C0 control.");
            }
        }

        public void CursorUp(int n)
        {
            if (n <= 0) return;
            CursorRow = Math.Max(0, CursorRow - n);
        }

        public void CursorDown(int n)
        {
            if (n <= 0) return;
            CursorRow = Math.Min(Rows - 1, CursorRow + n);
        }

        public void CursorForward(int n)
        {
            if (n <= 0) return;
            CursorCol = Math.Min(Columns - 1, CursorCol + n);
        }

        public void CursorBack(int n)
        {
            if (n <= 0) return;
            CursorCol = Math.Max(0, CursorCol - n);
        }

        public void CursorPosition(int row, int col)
        {
            CursorRow = Math.Clamp(row - 1, 0, Rows - 1);
            CursorCol = Math.Clamp(col - 1, 0, Columns - 1);
        }

        public void EraseDisplay(int param)
        {
            switch (param)
            {
                case 0:
                    EraseRange(CursorRow, CursorCol, CursorRow, Columns - 1);
                    for (int r = CursorRow + 1; r < Rows; r++)
                        EraseRange(r, 0, r, Columns - 1);
                    break;

                case 1:
                    for (int r = 0; r < CursorRow; r++)
                        EraseRange(r, 0, r, Columns - 1);
                    EraseRange(CursorRow, 0, CursorRow, CursorCol);
                    break;

                case 2:
                    EraseRange(0, 0, Rows - 1, Columns - 1);
                    break;

                case 3:
                    Scrollback.Clear();
                    EraseRange(0, 0, Rows - 1, Columns - 1);
                    break;
            }
        }

        public void EraseLine(int param)
        {
            switch (param)
            {
                case 0:
                    EraseRange(CursorRow, CursorCol, CursorRow, Columns - 1);
                    break;

                case 1:
                    EraseRange(CursorRow, 0, CursorRow, CursorCol);
                    break;

                case 2:
                    EraseRange(CursorRow, 0, CursorRow, Columns - 1);
                    break;
            }
        }

        public void SetSgr(int[] parameters)
        {
            int i = 0;
            while (i < parameters.Length)
            {
                int p = parameters[i];

                if (p == 0)
                {
                    CurrentAttributes = CellAttribute.Default;
                }
                else if (p == 1)
                {
                    CurrentAttributes = WithBold(true);
                }
                else if (p == 7)
                {
                    CurrentAttributes = WithInverse(true);
                }
                else if (p >= 30 && p <= 37)
                {
                    CurrentAttributes = WithForeground(p - 30);
                }
                else if (p == 38 && i + 2 < parameters.Length && parameters[i + 1] == 5)
                {
                    int colorIndex = parameters[i + 2];
                    CurrentAttributes = WithForeground256(colorIndex);
                    i += 2;
                }
                else if (p == 38 && i + 4 < parameters.Length && parameters[i + 1] == 2)
                {
                    int r = parameters[i + 2];
                    int g = parameters[i + 3];
                    int b = parameters[i + 4];
                    int rgb = (r << 16) | (g << 8) | b;
                    CurrentAttributes = WithForegroundTrueColor(rgb);
                    i += 4;
                }
                else if (p == 39)
                {
                    CurrentAttributes = WithForeground(-1);
                }
                else if (p >= 40 && p <= 47)
                {
                    CurrentAttributes = WithBackground(p - 40);
                }
                else if (p == 48 && i + 2 < parameters.Length && parameters[i + 1] == 5)
                {
                    int colorIndex = parameters[i + 2];
                    CurrentAttributes = WithBackground256(colorIndex);
                    i += 2;
                }
                else if (p == 48 && i + 4 < parameters.Length && parameters[i + 1] == 2)
                {
                    int r = parameters[i + 2];
                    int g = parameters[i + 3];
                    int b = parameters[i + 4];
                    int rgb = (r << 16) | (g << 8) | b;
                    CurrentAttributes = WithBackgroundTrueColor(rgb);
                    i += 4;
                }
                else if (p == 49)
                {
                    CurrentAttributes = WithBackground(-1);
                }
                else if (p >= 90 && p <= 97)
                {
                    CurrentAttributes = WithForeground(p - 90 + 8);
                }
                else if (p >= 100 && p <= 107)
                {
                    CurrentAttributes = WithBackground(p - 100 + 8);
                }
                // else: unsupported parameter — silently ignored.

                i++;
            }
        }

        public void Scroll()
        {
            var scrolledRow = new Cell[Columns];
            for (int c = 0; c < Columns; c++)
            {
                scrolledRow[c] = Buffer[0, c];
            }

            if (RetainsScrollback)
            {
                Scrollback.Add(scrolledRow);
                if (Scrollback.Count > MaxScrollbackRows)
                {
                    Scrollback.RemoveAt(0);
                }
            }

            for (int r = 1; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                    Buffer[r - 1, c] = Buffer[r, c];
            }

            for (int c = 0; c < Columns; c++)
                Buffer[Rows - 1, c] = EmptyCell;

            CursorRow = Rows - 1;
        }

        public void Resize(int columns, int rows)
        {
            for (int i = 0; i < Scrollback.Count; i++)
            {
                var row = Scrollback[i];
                if (row.Length == columns)
                {
                    continue;
                }

                var resized = new Cell[columns];
                int copyScrollCols = Math.Min(row.Length, columns);
                for (int c = 0; c < copyScrollCols; c++)
                {
                    resized[c] = row[c];
                }

                for (int c = copyScrollCols; c < columns; c++)
                {
                    resized[c] = EmptyCell;
                }

                Scrollback[i] = resized;
            }

            var newBuffer = new Cell[rows, columns];

            int copyRows = Math.Min(Rows, rows);
            int copyCols = Math.Min(Columns, columns);

            for (int r = 0; r < copyRows; r++)
            {
                for (int c = 0; c < copyCols; c++)
                    newBuffer[r, c] = Buffer[r, c];
            }

            if (columns > Columns)
            {
                for (int r = 0; r < copyRows; r++)
                {
                    for (int c = Columns; c < columns; c++)
                        newBuffer[r, c] = EmptyCell;
                }
            }

            for (int r = Rows; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                    newBuffer[r, c] = EmptyCell;
            }

            Buffer = newBuffer;
            Columns = columns;
            Rows = rows;

            CursorRow = Math.Min(CursorRow, rows - 1);
            CursorCol = Math.Min(CursorCol, columns - 1);
        }

        private void ClampRowAndScroll()
        {
            if (CursorRow >= Rows)
            {
                CursorRow = Rows - 1;
                Scroll();
            }
        }

        private void EraseRange(int rowStart, int colStart, int rowEnd, int colEnd)
        {
            for (int r = rowStart; r <= rowEnd; r++)
            {
                for (int c = colStart; c <= colEnd; c++)
                    Buffer[r, c] = EmptyCell;
            }
        }

        private void Fill(int rowStart, int colStart, int rowEnd, int colEnd, Cell cell)
        {
            for (int r = rowStart; r < rowEnd; r++)
            {
                for (int c = colStart; c < colEnd; c++)
                    Buffer[r, c] = cell;
            }
        }

        private CellAttribute WithBold(bool bold) =>
            new(CurrentAttributes.Foreground, CurrentAttributes.Background, bold, CurrentAttributes.Inverse, CurrentAttributes.Foreground256, CurrentAttributes.Background256, CurrentAttributes.ForegroundTrueColor, CurrentAttributes.BackgroundTrueColor);

        private CellAttribute WithInverse(bool inverse) =>
            new(CurrentAttributes.Foreground, CurrentAttributes.Background, CurrentAttributes.Bold, inverse, CurrentAttributes.Foreground256, CurrentAttributes.Background256, CurrentAttributes.ForegroundTrueColor, CurrentAttributes.BackgroundTrueColor);

        private CellAttribute WithForeground(int fg) =>
            new(fg, CurrentAttributes.Background, CurrentAttributes.Bold, CurrentAttributes.Inverse, -1, CurrentAttributes.Background256, -1, CurrentAttributes.BackgroundTrueColor);

        private CellAttribute WithBackground(int bg) =>
            new(CurrentAttributes.Foreground, bg, CurrentAttributes.Bold, CurrentAttributes.Inverse, CurrentAttributes.Foreground256, -1, CurrentAttributes.ForegroundTrueColor, -1);

        private CellAttribute WithForeground256(int fg) =>
            new(-1, CurrentAttributes.Background, CurrentAttributes.Bold, CurrentAttributes.Inverse, fg, CurrentAttributes.Background256, -1, CurrentAttributes.BackgroundTrueColor);

        private CellAttribute WithBackground256(int bg) =>
            new(CurrentAttributes.Foreground, -1, CurrentAttributes.Bold, CurrentAttributes.Inverse, CurrentAttributes.Foreground256, bg, CurrentAttributes.ForegroundTrueColor, -1);

        private CellAttribute WithForegroundTrueColor(int fg) =>
            new(-1, CurrentAttributes.Background, CurrentAttributes.Bold, CurrentAttributes.Inverse, CurrentAttributes.Foreground256, CurrentAttributes.Background256, fg, CurrentAttributes.BackgroundTrueColor);

        private CellAttribute WithBackgroundTrueColor(int bg) =>
            new(CurrentAttributes.Foreground, -1, CurrentAttributes.Bold, CurrentAttributes.Inverse, CurrentAttributes.Foreground256, CurrentAttributes.Background256, CurrentAttributes.ForegroundTrueColor, bg);
    }
}
