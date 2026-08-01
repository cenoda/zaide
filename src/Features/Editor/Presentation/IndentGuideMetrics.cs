using System;
using System.Collections.Generic;

namespace Zaide.Features.Editor.Presentation;

/// <summary>
/// Pure helper logic for deciding how many indent guides a line should show
/// and where their horizontal midpoints sit in visual columns.
/// Tabs advance to the next indentation boundary.
/// Whitespace-only lines intentionally do not render guides.
/// </summary>
internal static class IndentGuideMetrics
{
    public static int GetVisibleIndentGuideLevelCount(
        string lineText,
        int indentationSize)
    {
        ArgumentNullException.ThrowIfNull(lineText);
        return GetVisibleIndentGuideLevelCount(lineText.AsSpan(), indentationSize);
    }

    public static int GetVisibleIndentGuideLevelCount(
        ReadOnlySpan<char> lineText,
        int indentationSize)
    {
        if (indentationSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(indentationSize));

        int leadingVisualColumns = 0;
        int index = 0;

        while (index < lineText.Length)
        {
            var c = lineText[index];

            if (c == ' ')
            {
                leadingVisualColumns++;
                index++;
                continue;
            }

            if (c == '\t')
            {
                leadingVisualColumns =
                    (leadingVisualColumns / indentationSize + 1) * indentationSize;
                index++;
                continue;
            }

            break;
        }

        if (index >= lineText.Length)
        {
            return 0;
        }

        var firstContentChar = lineText[index];
        if (firstContentChar == '\r' || firstContentChar == '\n')
        {
            return 0;
        }

        return leadingVisualColumns / indentationSize;
    }

    /// <summary>
    /// Visual-column midpoint for a 1-based guide level under monospaced metrics.
    /// Level 1 sits at the center of visual columns [0, indentationSize),
    /// level 2 at the center of [indentationSize, 2*indentationSize), and so on.
    /// Matches the midpoint of AvaloniaEdit document-column boundaries for
    /// complete indent levels (spaces, tabs, and mixed prefixes).
    /// </summary>
    public static double GetGuideVisualColumnMidpoint(
        int guideLevel,
        int indentationSize)
    {
        if (guideLevel < 1)
            throw new ArgumentOutOfRangeException(nameof(guideLevel));
        if (indentationSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(indentationSize));

        return (guideLevel - 0.5) * indentationSize;
    }

    /// <summary>
    /// Viewport X for a guide level given monospaced column width and horizontal scroll.
    /// </summary>
    public static double GetGuideViewportX(
        int guideLevel,
        int indentationSize,
        double wideSpaceWidth,
        double scrollOffsetX)
    {
        var visualColumn = GetGuideVisualColumnMidpoint(guideLevel, indentationSize);
        return visualColumn * wideSpaceWidth - scrollOffsetX;
    }

    public static IReadOnlyList<int> GetIndentBoundaryDocumentColumns(
        string lineText,
        int indentationSize)
    {
        ArgumentNullException.ThrowIfNull(lineText);
        if (indentationSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(indentationSize));

        var boundaryColumns = new List<int>();
        int leadingVisualColumns = 0;
        int documentColumn = 1;
        int index = 0;

        while (index < lineText.Length)
        {
            var c = lineText[index];

            if (c == ' ')
            {
                leadingVisualColumns++;
                documentColumn++;
                index++;
            }
            else if (c == '\t')
            {
                leadingVisualColumns =
                    (leadingVisualColumns / indentationSize + 1) * indentationSize;
                documentColumn++;
                index++;
            }
            else
            {
                break;
            }

            if (leadingVisualColumns > 0 && leadingVisualColumns % indentationSize == 0)
                boundaryColumns.Add(documentColumn);
        }

        if (index >= lineText.Length)
            return Array.Empty<int>();

        var firstContentChar = lineText[index];
        if (firstContentChar == '\r' || firstContentChar == '\n')
            return Array.Empty<int>();

        return boundaryColumns;
    }
}
