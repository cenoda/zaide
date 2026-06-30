using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Zaide.ViewModels;

namespace Zaide.Views;

/// <summary>
/// Custom terminal rendering surface that replaces the TextBox. Owns its font
/// metrics and draws the cell grid via <see cref="DrawingContext"/>.
/// </summary>
public class TerminalRenderControl : Control
{
    // ── styled properties ─────────────────────────────────────────

    public static readonly StyledProperty<TerminalSnapshot?> SnapshotProperty =
        AvaloniaProperty.Register<TerminalRenderControl, TerminalSnapshot?>(nameof(Snapshot));

    public static readonly StyledProperty<int> CursorRowProperty =
        AvaloniaProperty.Register<TerminalRenderControl, int>(nameof(CursorRow));

    public static readonly StyledProperty<int> CursorColProperty =
        AvaloniaProperty.Register<TerminalRenderControl, int>(nameof(CursorCol));

    public static readonly StyledProperty<bool> CursorVisibleProperty =
        AvaloniaProperty.Register<TerminalRenderControl, bool>(nameof(CursorVisible));

    static TerminalRenderControl()
    {
        AffectsRender<TerminalRenderControl>(SnapshotProperty);
        AffectsRender<TerminalRenderControl>(CursorRowProperty);
        AffectsRender<TerminalRenderControl>(CursorColProperty);
        AffectsRender<TerminalRenderControl>(CursorVisibleProperty);
    }

    public TerminalSnapshot? Snapshot
    {
        get => GetValue(SnapshotProperty);
        set => SetValue(SnapshotProperty, value);
    }

    public int CursorRow
    {
        get => GetValue(CursorRowProperty);
        set => SetValue(CursorRowProperty, value);
    }

    public int CursorCol
    {
        get => GetValue(CursorColProperty);
        set => SetValue(CursorColProperty, value);
    }

    public bool CursorVisible
    {
        get => GetValue(CursorVisibleProperty);
        set => SetValue(CursorVisibleProperty, value);
    }

    // ── font and metrics ──────────────────────────────────────────

    private static readonly FontFamily DefaultFontFamily =
        FontFamily.Parse("Cascadia Code, JetBrains Mono, DejaVu Sans Mono, monospace");

    private readonly Typeface _typeface;
    private readonly double _fontSize = 14;

    /// <summary>Pixel width of a single monospace character cell.</summary>
    public double CellWidth { get; private set; }

    /// <summary>Pixel height of a single terminal line.</summary>
    public double LineHeight { get; private set; }

    /// <summary>Alias for <see cref="LineHeight"/> for terminal sizing APIs.</summary>
    public double CellHeight => LineHeight;

    // ── color palette ────────────────────────────────────────────

    private static readonly Color[] AnsiColors =
    {
        Color.FromRgb(0,   0,   0),   // 0  Black
        Color.FromRgb(205, 0,   0),   // 1  Red
        Color.FromRgb(0,   205, 0),   // 2  Green
        Color.FromRgb(205, 205, 0),   // 3  Yellow
        Color.FromRgb(0,   0,   238), // 4  Blue
        Color.FromRgb(205, 0,   205), // 5  Magenta
        Color.FromRgb(0,   205, 205), // 6  Cyan
        Color.FromRgb(229, 229, 229), // 7  White
        Color.FromRgb(127, 127, 127), // 8  Bright Black
        Color.FromRgb(255, 0,   0),   // 9  Bright Red
        Color.FromRgb(0,   255, 0),   // 10 Bright Green
        Color.FromRgb(255, 255, 0),   // 11 Bright Yellow
        Color.FromRgb(92,  92,  255), // 12 Bright Blue
        Color.FromRgb(255, 0,   255), // 13 Bright Magenta
        Color.FromRgb(0,   255, 255), // 14 Bright Cyan
        Color.FromRgb(255, 255, 255), // 15 Bright White
    };

    private static readonly IBrush DefaultBackground = new SolidColorBrush(AnsiColors[0]);
    private static readonly IBrush CursorBrush = new SolidColorBrush(Colors.White);
    private static readonly IBrush DimmedCursorBrush = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255));

    private bool _isFocused;

    // ── ctor ──────────────────────────────────────────────────────

    public TerminalRenderControl()
    {
        ClipToBounds = true;
        Focusable = true;

        _typeface = new Typeface(DefaultFontFamily, FontStyle.Normal, FontWeight.Normal);
        MeasureCellMetrics();

        GotFocus += (_, _) =>
        {
            _isFocused = true;
            InvalidateVisual();
        };
        LostFocus += (_, _) =>
        {
            _isFocused = false;
            InvalidateVisual();
        };

        // Key forwarding is wired by TerminalPanel.
    }

    // ── render ────────────────────────────────────────────────────

    public override void Render(DrawingContext context)
    {
        // Always clear the full control bounds first to avoid stale pixels
        // in the right/bottom gutter after resize.
        context.FillRectangle(DefaultBackground, new Rect(0, 0, Bounds.Width, Bounds.Height));

        var snapshot = Snapshot;
        if (snapshot is null)
            return;

        int cols = snapshot.Columns;
        int rows = snapshot.Rows;
        var cells = snapshot.Cells;
        double cw = CellWidth;
        double lh = LineHeight;

        if (cw <= 0 || lh <= 0) return;

        int idx = 0;
        for (int row = 0; row < rows; row++)
        {
            double y = row * lh;
            for (int col = 0; col < cols; col++)
            {
                var cell = cells[idx];
                idx++;
                double x = col * cw;
                var rect = new Rect(x, y, cw, lh);

                Color bg = cell.Background >= 0 && cell.Background < 16
                    ? AnsiColors[cell.Background] : AnsiColors[0];
                Color fg = cell.Foreground >= 0 && cell.Foreground < 16
                    ? AnsiColors[cell.Foreground] : AnsiColors[7];

                if (cell.Inverse)
                    (fg, bg) = (bg, fg);

                if (cell.Bold && cell.Foreground >= 0 && cell.Foreground <= 7)
                    fg = AnsiColors[cell.Foreground + 8];

                if (cell.Background != -1 || cell.Inverse)
                    context.FillRectangle(new SolidColorBrush(bg), rect);

                var ft = new FormattedText(
                    cell.Char.ToString(),
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    _typeface,
                    _fontSize,
                    new SolidColorBrush(fg));

                context.DrawText(ft, new Point(x, y));
            }
        }

        // Cursor — dimmed when unfocused, full brightness when focused.
        if (CursorVisible)
        {
            int cr = Math.Clamp(CursorRow, 0, rows - 1);
            int cc = Math.Clamp(CursorCol, 0, cols - 1);
            var crRect = new Rect(cc * cw, cr * lh, cw, lh);

            IBrush cursorBg = _isFocused ? CursorBrush : DimmedCursorBrush;
            context.FillRectangle(cursorBg, crRect);

            int ci = cr * cols + cc;
            if (ci < cells.Count)
            {
                var ccCell = cells[ci];
                var cursorFt = new FormattedText(
                    ccCell.Char.ToString(),
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    _typeface,
                    _fontSize,
                    new SolidColorBrush(AnsiColors[0]));
                context.DrawText(cursorFt, new Point(cc * cw, cr * lh));
            }
        }
    }

    // ── measure ───────────────────────────────────────────────────

    private void MeasureCellMetrics()
    {
        var ft = new FormattedText(
            "M",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            _typeface,
            _fontSize,
            Brushes.Black);

        CellWidth = Math.Ceiling(ft.Width);
        LineHeight = Math.Ceiling(ft.Height);
    }

}