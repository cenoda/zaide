using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Utils;

namespace Zaide.Features.Editor.Presentation;

/// <summary>
/// Draws indent guides for each full indentation level reached by a non-blank line.
/// Hot path avoids per-guide <see cref="TextView.GetVisualPosition"/> calls: X is
/// derived from monospaced visual-column midpoints and level counts are cached per
/// document version / indentation size.
/// </summary>
internal sealed class IndentGuideRenderer : IBackgroundRenderer
{
    private readonly TextView _textView;
    private readonly Pen _guidePen;
    private readonly IndentGuideLevelCache _levelCache = new();

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
        // Fast-path: disabled for non-.cs (and other gated) files — no work per scroll frame.
        if (!IsEnabled || !textView.VisualLinesValid || textView.Document is null)
            return;

        var document = textView.Document;
        var pixelSize = PixelSnapHelpers.GetPixelSize(textView);
        var indentationSize = textView.Options.IndentationSize;
        if (indentationSize <= 0)
            return;

        // Monospaced column width used by AvaloniaEdit for tabs; matches code-font .cs path.
        var wideSpaceWidth = textView.WideSpaceWidth;
        if (wideSpaceWidth <= 0)
            return;

        var scrollOffsetX = textView.ScrollOffset.X;
        var scrollOffsetY = textView.ScrollOffset.Y;

        foreach (var visualLine in textView.VisualLines)
        {
            var line = visualLine.FirstDocumentLine;
            if (line.TotalLength <= 0)
                continue;

            var guideLevelCount = _levelCache.GetGuideLevelCount(
                document,
                line.LineNumber,
                indentationSize);
            if (guideLevelCount == 0)
                continue;

            var top = visualLine.VisualTop - scrollOffsetY;
            var bottom = top + visualLine.Height;

            for (var guideLevel = 1; guideLevel <= guideLevelCount; guideLevel++)
            {
                var rawX = IndentGuideMetrics.GetGuideViewportX(
                    guideLevel,
                    indentationSize,
                    wideSpaceWidth,
                    scrollOffsetX);
                var guideX = PixelSnapHelpers.PixelAlign(rawX, pixelSize.Width);

                drawingContext.DrawLine(
                    _guidePen,
                    new Point(guideX, top),
                    new Point(guideX, bottom));
            }
        }
    }
}
