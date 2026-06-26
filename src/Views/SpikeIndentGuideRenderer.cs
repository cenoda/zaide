// M2 SPIKE — prove leading-whitespace → X coordinate mapping.
// Draws a green marker at the first indentation boundary of each visible line.
// Replaced by IndGuideRenderer after M2 exit gate passes.
using System;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Utils;
using Zaide.Views;

namespace Zaide.Views;

/// <summary>
/// M2 spike: for each visible line, find the leading whitespace run and
/// draw a marker at the X position of the first non-whitespace character.
/// Proves that the editor exposes stable coordinates for indent boundaries
/// across spaces, tabs, and mixed indentation.
/// </summary>
internal sealed class SpikeIndentGuideRenderer : IBackgroundRenderer
{
    private readonly TextView _textView;

    public SpikeIndentGuideRenderer(TextView textView)
    {
        _textView = textView;
        _textView.BackgroundRenderers.Add(this);
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        // Skip if visual lines aren't ready yet.
        if (!textView.VisualLinesValid)
            return;

        var pen = new Pen(Brushes.Lime, 1);
        var pixelSize = PixelSnapHelpers.GetPixelSize(textView);
        var doc = textView.Document;

        foreach (var visualLine in textView.VisualLines)
        {
            // Get the full text of this document line.
            var line = visualLine.FirstDocumentLine;
            var lineStart = line.Offset;
            var lineLength = line.TotalLength;
            if (lineLength <= 0)
                continue;

            var text = doc.GetText(lineStart, lineLength);

            // Find the first non-whitespace character (the indent boundary).
            int indentCols = 0;
            for (int i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\t')
                {
                    // Tab advances to next tab stop (IndentationSize columns).
                    indentCols = (indentCols / textView.Options.IndentationSize + 1)
                        * textView.Options.IndentationSize;
                }
                else if (c == ' ')
                {
                    indentCols++;
                }
                else
                {
                    break; // first non-whitespace char
                }
            }

            // Skip lines with no indentation.
            if (indentCols == 0)
                continue;

            // Use the editor's own layout engine to get the X of this column.
            var pos = new TextViewPosition(line.LineNumber, indentCols);
            var visualPoint = textView.GetVisualPosition(pos, VisualYPosition.TextTop);

            // Convert from document space to screen space.
            var markerX = PixelSnapHelpers.PixelAlign(visualPoint.X, pixelSize.Width)
                - textView.ScrollOffset.X;

            var y = visualLine.VisualTop - textView.ScrollOffset.Y;
            var start = new Point(markerX, y);
            var end = new Point(markerX, y + visualLine.Height);
            drawingContext.DrawLine(pen, start, end);
        }
    }
}
