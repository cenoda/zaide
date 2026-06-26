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
            var boundaryColumns = IndentGuideMetrics.GetIndentBoundaryDocumentColumns(
                text,
                indentationSize);
            var guideLevelCount = boundaryColumns.Count;
            if (guideLevelCount == 0)
                continue;

            var top = visualLine.VisualTop - textView.ScrollOffset.Y;
            for (int guideLevel = 1; guideLevel <= guideLevelCount; guideLevel++)
            {
                var blockStartPosition = textView.GetVisualPosition(
                    new TextViewPosition(
                        line.LineNumber,
                        guideLevel == 1 ? 1 : boundaryColumns[guideLevel - 2]),
                    VisualYPosition.TextTop);
                var blockEndPosition = textView.GetVisualPosition(
                    new TextViewPosition(
                        line.LineNumber,
                        boundaryColumns[guideLevel - 1]),
                    VisualYPosition.TextTop);
                var guideX = PixelSnapHelpers.PixelAlign(
                        (blockStartPosition.X + blockEndPosition.X) / 2.0,
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
