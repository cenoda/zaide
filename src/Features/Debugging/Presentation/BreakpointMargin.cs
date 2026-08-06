using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Utils;
using Zaide.App.Composition;
using Zaide.Features.Debugging.Application;
using Zaide.UI.DesignSystem;

namespace Zaide.Features.Debugging.Presentation;

/// <summary>
/// Left margin that projects persisted breakpoints for the active on-disk document.
/// Verified / pending / rejected adapter outcomes are distinguishable without
/// changing persisted breakpoint intent.
/// </summary>
internal sealed class BreakpointMargin : AbstractMargin
{
    private IBrush? _enabledFill;
    private IBrush? _verifiedFill;
    private IBrush? _pendingFill;
    private IBrush? _rejectedFill;
    private IBrush? _rejectedStroke;
    private IBrush? _disabledFill;
    private IBrush? _disabledStroke;

    private IBrush EnabledFill => _enabledFill ??= CreateFill(229, 20, 75);
    private IBrush VerifiedFill => _verifiedFill ??= CreateFill(229, 20, 75);
    private IBrush PendingFill => _pendingFill ??= CreateFill(230, 170, 40);
    private IBrush RejectedFill => _rejectedFill ??= CreateFill(120, 120, 140);
    private IBrush RejectedStroke => _rejectedStroke ??= CreateFill(220, 80, 80);
    private IBrush DisabledFill => _disabledFill ??= new SolidColorBrush(Color.FromArgb(120, 180, 180, 200));
    private IBrush DisabledStroke => _disabledStroke ??= CreateFill(180, 180, 200);

    private readonly Action<int>? _toggleLine;
    private IReadOnlyList<EditorBreakpointMarker> _markers = Array.Empty<EditorBreakpointMarker>();

    public BreakpointMargin(Action<int>? toggleLine)
    {
        _toggleLine = toggleLine;
        Width = 16;
        Cursor = new Cursor(StandardCursorType.Hand);
        ActualThemeVariantChanged += OnThemeVariantChanged;
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

    private void OnThemeVariantChanged(object? sender, EventArgs e)
    {
        InvalidateBrushCaches();
        InvalidateVisual();
    }

    private void InvalidateBrushCaches()
    {
        _enabledFill = null;
        _verifiedFill = null;
        _pendingFill = null;
        _rejectedFill = null;
        _rejectedStroke = null;
        _disabledFill = null;
        _disabledStroke = null;
    }

    private static IBrush CreateFill(byte r, byte g, byte b) =>
        new SolidColorBrush(Color.FromRgb(r, g, b));

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
