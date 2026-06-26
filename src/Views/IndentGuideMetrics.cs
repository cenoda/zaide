using System;

namespace Zaide.Views;

/// <summary>
/// Pure helper logic for deciding whether a line should show the first
/// indent guide. Tabs advance to the next indentation boundary.
/// Whitespace-only lines intentionally do not render a guide in M3.
/// </summary>
internal static class IndentGuideMetrics
{
    public static bool TryGetFirstGuideVisualColumn(
        string lineText,
        int indentationSize,
        out int visualColumn)
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
            visualColumn = 0;
            return false;
        }

        var firstContentChar = lineText[index];
        if (firstContentChar == '\r' || firstContentChar == '\n')
        {
            visualColumn = 0;
            return false;
        }

        if (leadingVisualColumns < indentationSize)
        {
            visualColumn = 0;
            return false;
        }

        visualColumn = indentationSize;
        return true;
    }
}
