using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Utils;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Features.Debugging.Application;

namespace Zaide.Views;

/// <summary>
/// Left margin that projects persisted breakpoints for the active on-disk document.
/// Verified / pending / rejected adapter outcomes are distinguishable without
/// changing persisted breakpoint intent.
/// </summary>
internal sealed class BreakpointMargin : AbstractMargin
{
    private static readonly IBrush EnabledFill = new SolidColorBrush(Color.FromRgb(229, 20, 75));
    private static readonly IBrush VerifiedFill = new SolidColorBrush(Color.FromRgb(229, 20, 75));
    private static readonly IBrush PendingFill = new SolidColorBrush(Color.FromRgb(230, 170, 40));
    private static readonly IBrush RejectedFill = new SolidColorBrush(Color.FromRgb(120, 120, 140));
    private static readonly IBrush RejectedStroke = new SolidColorBrush(Color.FromRgb(220, 80, 80));
    private static readonly IBrush DisabledFill = new SolidColorBrush(Color.FromArgb(120, 180, 180, 200));
    private static readonly IBrush DisabledStroke = new SolidColorBrush(Color.FromRgb(180, 180, 200));

    private readonly Action<int>? _toggleLine;
    private IReadOnlyList<EditorBreakpointMarker> _markers = Array.Empty<EditorBreakpointMarker>();

    public BreakpointMargin(Action<int>? toggleLine)
    {
        _toggleLine = toggleLine;
        Width = 16;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public void SetMarkers(IReadOnlyList<EditorBreakpointMarker> markers)
    {
        _markers = markers ?? Array.Empty<EditorBreakpointMarker>();
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            TextView is not null &&
            TryGetLineFromPoint(e.GetPosition(this), out var line))
        {
            _toggleLine?.Invoke(line);
            e.Handled = true;
        }
    }

    public override void Render(DrawingContext drawingContext)
    {
        base.Render(drawingContext);

        if (TextView is null || !TextView.VisualLinesValid || _markers.Count == 0)
            return;

        var pixelSize = PixelSnapHelpers.GetPixelSize(TextView);
        const double markerSize = 9;

        foreach (var marker in _markers)
        {
            var visualLine = TextView.GetVisualLine(marker.Line);
            if (visualLine is null)
                continue;

            var centerY = visualLine.VisualTop - TextView.ScrollOffset.Y + (visualLine.Height / 2.0);
            var centerX = Bounds.Width / 2.0;
            var left = PixelSnapHelpers.PixelAlign(centerX - (markerSize / 2.0), pixelSize.Width);
            var top = PixelSnapHelpers.PixelAlign(centerY - (markerSize / 2.0), pixelSize.Height);
            var rect = new Rect(left, top, markerSize, markerSize);

            if (!marker.Enabled)
            {
                drawingContext.DrawEllipse(DisabledFill, new Pen(DisabledStroke, 1), rect);
                continue;
            }

            switch (marker.Verification)
            {
                case DebugBreakpointVerificationState.Verified:
                    drawingContext.DrawEllipse(VerifiedFill, null, rect);
                    break;
                case DebugBreakpointVerificationState.Pending:
                    drawingContext.DrawEllipse(PendingFill, null, rect);
                    break;
                case DebugBreakpointVerificationState.Rejected:
                    drawingContext.DrawEllipse(RejectedFill, new Pen(RejectedStroke, 1.5), rect);
                    break;
                default:
                    drawingContext.DrawEllipse(EnabledFill, null, rect);
                    break;
            }
        }
    }

    private bool TryGetLineFromPoint(Point point, out int line)
    {
        line = 0;
        if (TextView is null)
            return false;

        var visualLine = TextView.GetVisualLineFromVisualTop(point.Y + TextView.ScrollOffset.Y);
        if (visualLine is null)
            return false;

        line = visualLine.FirstDocumentLine.LineNumber;
        return line >= 1;
    }
}