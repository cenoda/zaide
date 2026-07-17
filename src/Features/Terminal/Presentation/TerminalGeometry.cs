using System;

namespace Zaide.Features.Terminal.Presentation;

/// <summary>
/// Pure geometry helper that converts a terminal surface's pixel dimensions
/// and font metrics into terminal columns and rows. Extracted from the view
/// layer so the calculation is testable without instantiating Avalonia
/// controls.
/// </summary>
public static class TerminalGeometry
{
    /// <summary>
    /// Computes the number of terminal columns and rows that fit within the
    /// given surface area.
    /// </summary>
    /// <param name="surfaceWidth">Available pixel width of the terminal surface.</param>
    /// <param name="surfaceHeight">Available pixel height of the terminal surface.</param>
    /// <param name="cellWidth">
    /// Pixel width of a single monospace character cell (e.g. the glyph
    /// advance of 'M' in the configured terminal font).
    /// </param>
    /// <param name="lineHeight">
    /// Pixel height of a single terminal line (including inter-line spacing).
    /// </param>
    /// <param name="paddingLeft">Left padding in pixels.</param>
    /// <param name="paddingTop">Top padding in pixels.</param>
    /// <param name="paddingRight">Right padding in pixels.</param>
    /// <param name="paddingBottom">Bottom padding in pixels.</param>
    /// <returns>
    /// A tuple of (columns, rows). Both are clamped to a minimum of 1 so
    /// the PTY always receives a valid size.
    /// </returns>
    public static (int columns, int rows) Compute(
        double surfaceWidth,
        double surfaceHeight,
        double cellWidth,
        double lineHeight,
        double paddingLeft = 0,
        double paddingTop = 0,
        double paddingRight = 0,
        double paddingBottom = 0)
    {
        if (cellWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(cellWidth), cellWidth,
                "Cell width must be positive.");
        if (lineHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(lineHeight), lineHeight,
                "Line height must be positive.");

        double usableWidth = surfaceWidth - paddingLeft - paddingRight;
        double usableHeight = surfaceHeight - paddingTop - paddingBottom;

        int columns = usableWidth > 0 ? Math.Max(1, (int)(usableWidth / cellWidth)) : 1;
        int rows = usableHeight > 0 ? Math.Max(1, (int)(usableHeight / lineHeight)) : 1;

        return (columns, rows);
    }
}