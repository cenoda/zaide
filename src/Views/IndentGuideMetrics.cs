using System;
using System.Collections.Generic;

namespace Zaide.Views;

/// <summary>
/// Pure helper logic for deciding how many indent guides a line should show.
/// Tabs advance to the next indentation boundary.
/// Whitespace-only lines intentionally do not render guides.
/// </summary>
internal static class IndentGuideMetrics
{
    public static int GetVisibleIndentGuideLevelCount(
        string lineText,
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

    public static bool TryGetFirstGuideVisualColumn(
        string lineText,
        int indentationSize,
        out int visualColumn)
    {
        var levelCount = GetVisibleIndentGuideLevelCount(lineText, indentationSize);
        if (levelCount == 0)
        {
            visualColumn = 0;
            return false;
        }

        visualColumn = indentationSize;
        return true;
    }

    public static IReadOnlyList<int> GetIndentBoundaryDocumentColumns(
        string lineText,
        int indentationSize)
    {
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
