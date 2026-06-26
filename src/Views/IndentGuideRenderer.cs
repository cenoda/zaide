using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Utils;

namespace Zaide.Views;

/// <summary>
/// M3 renderer: draws only the first indent guide level for lines whose
/// leading whitespace reaches at least one full indentation level.
/// </summary>
internal sealed class IndentGuideRenderer : IBackgroundRenderer
{
    private readonly TextView _textView;
    private readonly Pen _guidePen;

    public IndentGuideRenderer(TextView textView, IBrush guideBrush)
    {
        _textView = textView;
        _guidePen = new Pen(guideBrush, 1);
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
            if (!IndentGuideMetrics.TryGetFirstGuideVisualColumn(
                text,
                indentationSize,
                out var guideVisualColumn))
            {
                continue;
            }

            // Ask AvaloniaEdit for the rendered X at the line start, then move
            // one full indent level to the right using the editor's space width.
            var lineStart = textView.GetVisualPosition(
                new TextViewPosition(line.LineNumber, 1, 1),
                VisualYPosition.TextTop);

            var guideX = PixelSnapHelpers.PixelAlign(
                    lineStart.X + guideVisualColumn * textView.WideSpaceWidth,
                    pixelSize.Width)
                - textView.ScrollOffset.X;

            var top = visualLine.VisualTop - textView.ScrollOffset.Y;
            drawingContext.DrawLine(
                _guidePen,
                new Point(guideX, top),
                new Point(guideX, top + visualLine.Height));
        }
    }
}
