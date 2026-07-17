using System;
using System.Collections.Generic;

namespace Zaide.Features.Terminal.Presentation;

/// <summary>
/// Lightweight, immutable snapshot of the visible terminal surface for view
/// binding. Projected from internal <c>TerminalScreen</c> so the control's
/// styled property does not expose an internal type.
/// </summary>
public sealed class TerminalSnapshot
{
    /// <summary>Width of the terminal viewport in cells.</summary>
    public int Columns { get; }

    /// <summary>Height of the terminal viewport in cells.</summary>
    public int Rows { get; }

    /// <summary>
    /// Visible text content, row by row, without style info. Used for clipboard
    /// copy and accessibility.
    /// </summary>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>
    /// Retained text rows above the visible viewport, oldest first. Used for
    /// scrollback rendering and selection.
    /// </summary>
    public IReadOnlyList<string> ScrollbackLines { get; }

    /// <summary>
    /// Raw cell data for rendering. Row-major; length equals
    /// <c>Columns * Rows</c>.
    /// </summary>
    public IReadOnlyList<TerminalCell> Cells { get; }

    /// <summary>
    /// Retained scrollback cell data, row-major; length equals
    /// <c>Columns * ScrollbackLines.Count</c>.
    /// </summary>
    public IReadOnlyList<TerminalCell> ScrollbackCells { get; }

    /// <summary>Total rows available for rendering and selection.</summary>
    public int TotalRows => ScrollbackLines.Count + Rows;

    public TerminalSnapshot(
        int columns,
        int rows,
        IReadOnlyList<string> lines,
        IReadOnlyList<TerminalCell> cells,
        IReadOnlyList<string>? scrollbackLines = null,
        IReadOnlyList<TerminalCell>? scrollbackCells = null)
    {
        Columns = columns;
        Rows = rows;
        Lines = lines ?? throw new ArgumentNullException(nameof(lines));
        Cells = cells ?? throw new ArgumentNullException(nameof(cells));
        ScrollbackLines = scrollbackLines ?? Array.Empty<string>();
        ScrollbackCells = scrollbackCells ?? Array.Empty<TerminalCell>();
    }
}

/// <summary>
/// A single styled cell in a <see cref="TerminalSnapshot"/>. Mirrors the
/// internal <c>Cell</c>/<c>CellAttribute</c> pairing in a public, UI-agnostic
/// value type.
/// </summary>
public readonly struct TerminalCell
{
    /// <summary>The character to display.</summary>
    public readonly char Char;

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

    public TerminalCell(char ch, int foreground, int background, bool bold, bool inverse)
        : this(ch, foreground, background, bold, inverse, -1, -1, -1, -1)
    {
    }

    public TerminalCell(char ch, int foreground, int background, bool bold, bool inverse, int foreground256, int background256, int foregroundTrueColor, int backgroundTrueColor)
    {
        Char = ch;
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
