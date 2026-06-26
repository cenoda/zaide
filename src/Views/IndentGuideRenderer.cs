using System;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Utils;

namespace Zaide.Views;

/// <summary>
/// M4 renderer: draws indent guides for each full indentation level reached
/// by a non-blank line.
/// </summary>
internal sealed class IndentGuideRenderer : IBackgroundRenderer
{
    private readonly TextView _textView;
    private readonly Pen[] _guidePens;

    public IndentGuideRenderer(TextView textView, IBrush guideBrush)
    {
        _textView = textView;
        _guidePens =
        [
            new Pen(guideBrush, 1),
            new Pen(new SolidColorBrush(Color.FromArgb(255, 0, 255, 0)), 1),
            new Pen(new SolidColorBrush(Color.FromArgb(255, 255, 215, 0)), 1),
            new Pen(new SolidColorBrush(Color.FromArgb(255, 0, 255, 255)), 1),
            new Pen(new SolidColorBrush(Color.FromArgb(255, 255, 105, 180)), 1)
        ];
        _textView.BackgroundRenderers.Add(this);
    }

    public bool IsEnabled { get; set; }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!IsEnabled || !textView.VisualLinesValid || textView.Document is null)
            return;

        var pixelSize = PixelSnapHelpers.GetPixelSize(textView);
        var indentationSize = textView.Options.IndentationSize;

        foreach (var visualLine in textView.VisualLines)
        {
            var line = visualLine.FirstDocumentLine;
            if (line.TotalLength <= 0)
                continue;

            var text = textView.Document.GetText(line.Offset, line.TotalLength);
            var guideLevelCount = IndentGuideMetrics.GetVisibleIndentGuideLevelCount(
                text,
                indentationSize);
            if (guideLevelCount == 0)
                continue;

            // Ask AvaloniaEdit for the rendered X at the line start, then place
            // each guide inside its indentation block instead of on the content
            // boundary. That avoids drawing over code glyphs.
            var lineStart = textView.GetVisualPosition(
                new TextViewPosition(line.LineNumber, 1, 1),
                VisualYPosition.TextTop);

            var top = visualLine.VisualTop - textView.ScrollOffset.Y;
            for (int guideLevel = 1; guideLevel <= guideLevelCount; guideLevel++)
            {
                var guideVisualColumn = ((guideLevel - 1) * indentationSize)
                    + (indentationSize / 2.0);
                var guideX = PixelSnapHelpers.PixelAlign(
                        lineStart.X + guideVisualColumn * textView.WideSpaceWidth,
                        pixelSize.Width)
                    - textView.ScrollOffset.X;

                drawingContext.DrawLine(
                    GetGuidePen(guideLevel),
                    new Point(guideX, top),
                    new Point(guideX, top + visualLine.Height));
            }
        }
    }

    private Pen GetGuidePen(int guideLevel)
    {
        var index = Math.Min(guideLevel, _guidePens.Length - 1);
        return _guidePens[index];
    }
}
