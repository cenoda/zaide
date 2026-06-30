using System;
using System.Collections.Generic;

namespace Zaide.ViewModels;

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
    /// Raw cell data for rendering. Row-major; length equals
    /// <c>Columns * Rows</c>.
    /// </summary>
    public IReadOnlyList<TerminalCell> Cells { get; }

    public TerminalSnapshot(
        int columns,
        int rows,
        IReadOnlyList<string> lines,
        IReadOnlyList<TerminalCell> cells)
    {
        Columns = columns;
        Rows = rows;
        Lines = lines ?? throw new ArgumentNullException(nameof(lines));
        Cells = cells ?? throw new ArgumentNullException(nameof(cells));
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

    public TerminalCell(char ch, int foreground, int background, bool bold, bool inverse)
    {
        Char = ch;
        Foreground = foreground;
        Background = background;
        Bold = bold;
        Inverse = inverse;
    }
}