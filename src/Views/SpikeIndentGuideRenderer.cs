// M1 SPIKE — temporary renderer to prove the drawing surface.
// Draws a single obvious vertical marker at a fixed X position.
// To be replaced by IndGuideRenderer after M1 exit gate passes.
using System;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Utils;
using Zaide.Views;

namespace Zaide.Views;

/// <summary>
/// Spike: prove a custom background renderer can draw one vertical marker
/// at a fixed X position that scrolls correctly with the text.
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
        // Fixed X = 80 logical pixels (before scroll adjustment).
        const double fixedX = 80;
        var pixelSize = PixelSnapHelpers.GetPixelSize(textView);
        var markerX = PixelSnapHelpers.PixelAlign(fixedX, pixelSize.Width) - textView.ScrollOffset.X;

        var pen = new Pen(Brushes.Red, 1);
        var start = new Point(markerX, 0);
        var end = new Point(markerX, Math.Max(textView.DocumentHeight, textView.Bounds.Height));
        drawingContext.DrawLine(pen, start, end);
    }
}
