using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Utils;
using Zaide.ViewModels;

namespace Zaide.Views;

/// <summary>
/// Left margin marker for the current debug execution line, distinct from breakpoints.
/// </summary>
internal sealed class InstructionPointerMargin : AbstractMargin
{
    private static readonly IBrush MarkerFill = new SolidColorBrush(Color.FromRgb(252, 187, 71));

    private EditorInstructionPointerMarker? _marker;

    public InstructionPointerMargin()
    {
        Width = 10;
    }

    public void SetMarker(EditorInstructionPointerMarker? marker)
    {
        _marker = marker;
        InvalidateVisual();
    }

    public override void Render(DrawingContext drawingContext)
    {
        base.Render(drawingContext);

        if (_marker is null || TextView is null || !TextView.VisualLinesValid)
            return;

        var visualLine = TextView.GetVisualLine(_marker.Line);
        if (visualLine is null)
            return;

        var pixelSize = PixelSnapHelpers.GetPixelSize(TextView);
        var centerY = visualLine.VisualTop - TextView.ScrollOffset.Y + (visualLine.Height / 2.0);
        var centerX = Bounds.Width / 2.0;
        const double size = 8;
        var left = PixelSnapHelpers.PixelAlign(centerX - (size / 2.0), pixelSize.Width);
        var top = PixelSnapHelpers.PixelAlign(centerY - (size / 2.0), pixelSize.Height);

        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = new Point(left, top),
            IsClosed = true,
        };
        figure.Segments!.Add(new LineSegment { Point = new Point(left + size, top + (size / 2.0)) });
        figure.Segments.Add(new LineSegment { Point = new Point(left, top + size) });
        geometry.Figures!.Add(figure);
        drawingContext.DrawGeometry(MarkerFill, null, geometry);
    }
}