using System;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
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

    private static readonly Color[] Palette256 = Build256ColorPalette();

    private static Color[] Build256ColorPalette()
    {
        var palette = new Color[256];
        
        // Standard ANSI colors (0-15)
        for (int i = 0; i < 16; i++)
        {
            palette[i] = AnsiColors[i];
        }
        
        // 6x6x6 color cube (16-231)
        for (int r = 0; r < 6; r++)
        {
            for (int g = 0; g < 6; g++)
            {
                for (int b = 0; b < 6; b++)
                {
                    int index = 16 + r * 36 + g * 6 + b;
                    if (index >= 232) break;
                    
                    byte red = (byte)(r == 0 ? 0 : r * 40 + 55);
                    byte green = (byte)(g == 0 ? 0 : g * 40 + 55);
                    byte blue = (byte)(b == 0 ? 0 : b * 40 + 55);
                    palette[index] = Color.FromRgb(red, green, blue);
                }
            }
        }
        
        // Grayscale (232-255)
        for (int i = 232; i < 256; i++)
        {
            byte gray = (byte)((i - 232) * 10 + 8);
            palette[i] = Color.FromRgb(gray, gray, gray);
        }
        
        return palette;
    }

    private static Color ColorFromRgb(int rgb)
    {
        byte r = (byte)((rgb >> 16) & 0xFF);
        byte g = (byte)((rgb >> 8) & 0xFF);
        byte b = (byte)(rgb & 0xFF);
        return Color.FromRgb(r, g, b);
    }

    private static readonly IBrush CursorBrush = new SolidColorBrush(Colors.White);
    private static readonly IBrush DimmedCursorBrush = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255));
    private static readonly TimeSpan CursorBlinkInterval = TimeSpan.FromMilliseconds(530);

    private bool _isFocused;
    private bool _isCursorBlinkOn = true;
    private bool _followLiveBottom = true;
    private bool _isSelecting;
    private int _viewportTop;
    private (int Row, int Col)? _selectionAnchor;
    private (int Row, int Col)? _selectionEnd;
    private readonly DispatcherTimer _cursorBlinkTimer;

    // ── ctor ──────────────────────────────────────────────────────

    public TerminalRenderControl()
    {
        ClipToBounds = true;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Ibeam);

        _typeface = new Typeface(DefaultFontFamily, FontStyle.Normal, FontWeight.Normal);
        MeasureCellMetrics();

        _cursorBlinkTimer = new DispatcherTimer
        {
            Interval = CursorBlinkInterval
        };
        _cursorBlinkTimer.Tick += (_, _) =>
        {
            if (!_isFocused || !CursorVisible)
            {
                return;
            }

            _isCursorBlinkOn = !_isCursorBlinkOn;
            InvalidateVisual();
        };

        GotFocus += (_, _) =>
        {
            _isFocused = true;
            ResetCursorBlink();
            InvalidateVisual();
        };
        LostFocus += (_, _) =>
        {
            _isFocused = false;
            ResetCursorBlink();
            InvalidateVisual();
        };

        this.GetObservable(CursorVisibleProperty).Subscribe(_ => ResetCursorBlink());
        this.GetObservable(CursorRowProperty).Subscribe(_ => ResetCursorBlink());
        this.GetObservable(CursorColProperty).Subscribe(_ => ResetCursorBlink());

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;

        // Key forwarding is wired by TerminalPanel.
    }

    // ── render ────────────────────────────────────────────────────

    public override void Render(DrawingContext context)
    {
        Color defaultBackground = GetThemeColor("SurfaceBaseColor", AnsiColors[0]);
        Color defaultForeground = GetThemeColor("TextActiveColor", AnsiColors[7]);
        Color selectionBackground = GetThemeColor("SoftAccentColor", Color.FromArgb(160, 194, 194, 229));
        Color selectionForeground = defaultBackground;

        // Always clear the full control bounds first to avoid stale pixels
        // in the right/bottom gutter after resize.
        context.FillRectangle(new SolidColorBrush(defaultBackground), new Rect(0, 0, Bounds.Width, Bounds.Height));

        var snapshot = Snapshot;
        if (snapshot is null)
            return;

        int cols = snapshot.Columns;
        int rows = snapshot.Rows;
        double cw = CellWidth;
        double lh = LineHeight;

        if (cw <= 0 || lh <= 0) return;

        int viewportTop = GetViewportTop(snapshot);
        for (int row = 0; row < rows; row++)
        {
            int absoluteRow = viewportTop + row;
            double y = row * lh;
            if (TryGetSelectionSpanForRow(absoluteRow, cols, out int selectionStartCol, out int selectionEndCol))
            {
                context.FillRectangle(
                    new SolidColorBrush(selectionBackground),
                    new Rect(selectionStartCol * cw, y, (selectionEndCol - selectionStartCol + 1) * cw, lh));
            }

            for (int col = 0; col < cols; col++)
            {
                var cell = GetAbsoluteCell(snapshot, absoluteRow, col);
                double x = col * cw;
                var rect = new Rect(x, y, cw, lh);

                Color bg = defaultBackground;
                Color fg = defaultForeground;

                if (cell.BackgroundTrueColor != -1)
                {
                    bg = ColorFromRgb(cell.BackgroundTrueColor);
                }
                else if (cell.Background256 != -1 && cell.Background256 < 256)
                {
                    bg = Palette256[cell.Background256];
                }
                else if (cell.Background >= 0 && cell.Background < 16)
                {
                    bg = AnsiColors[cell.Background];
                }

                if (cell.ForegroundTrueColor != -1)
                {
                    fg = ColorFromRgb(cell.ForegroundTrueColor);
                }
                else if (cell.Foreground256 != -1 && cell.Foreground256 < 256)
                {
                    fg = Palette256[cell.Foreground256];
                }
                else if (cell.Foreground >= 0 && cell.Foreground < 16)
                {
                    fg = AnsiColors[cell.Foreground];
                }

                if (cell.Inverse)
                    (fg, bg) = (bg, fg);

                if (cell.Bold && cell.Foreground >= 0 && cell.Foreground <= 7 && cell.Foreground256 == -1 && cell.ForegroundTrueColor == -1)
                    fg = AnsiColors[cell.Foreground + 8];

                bool isSelected = IsSelected(absoluteRow, col);
                if (isSelected)
                {
                    fg = selectionForeground;
                }
                else if (cell.Background != -1 || cell.Background256 != -1 || cell.BackgroundTrueColor != -1 || cell.Inverse)
                {
                    context.FillRectangle(new SolidColorBrush(bg), rect);
                }

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
        bool shouldDrawCursor = CursorVisible && (!_isFocused || _isCursorBlinkOn);
        if (shouldDrawCursor)
        {
            int absoluteCursorRow = snapshot.ScrollbackLines.Count + CursorRow;
            if (absoluteCursorRow < viewportTop || absoluteCursorRow >= viewportTop + rows)
            {
                return;
            }

            int cr = Math.Clamp(absoluteCursorRow - viewportTop, 0, rows - 1);
            int cc = Math.Clamp(CursorCol, 0, cols - 1);
            var crRect = new Rect(cc * cw, cr * lh, cw, lh);

            IBrush cursorBg = _isFocused ? CursorBrush : DimmedCursorBrush;
            context.FillRectangle(cursorBg, crRect);

            var ccCell = GetAbsoluteCell(snapshot, absoluteCursorRow, cc);
            var cursorFt = new FormattedText(
                ccCell.Char.ToString(),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                _fontSize,
                new SolidColorBrush(defaultBackground));
            context.DrawText(cursorFt, new Point(cc * cw, cr * lh));
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

    public bool TryGetSelectedText(out string? text)
    {
        var snapshot = Snapshot;
        if (snapshot is null || !HasSelection())
        {
            text = null;
            return false;
        }

        text = BuildSelectedText(snapshot, _selectionAnchor!.Value, _selectionEnd!.Value);
        return !string.IsNullOrEmpty(text);
    }

    /// <summary>
    /// Returns the viewport to the live bottom so newly arriving shell output
    /// is shown again after the user has scrolled back.
    /// </summary>
    public void ScrollToBottom(bool clearSelection = false)
    {
        _followLiveBottom = true;
        if (clearSelection)
        {
            _selectionAnchor = null;
            _selectionEnd = null;
            _isSelecting = false;
        }

        InvalidateVisual();
    }

    internal static string BuildSelectedText(
        TerminalSnapshot snapshot,
        (int Row, int Col) start,
        (int Row, int Col) end)
    {
        NormalizeSelection(start, end, out var first, out var last);

        var builder = new StringBuilder();
        for (int row = first.Row; row <= last.Row; row++)
        {
            string line = GetAbsoluteLine(snapshot, row);
            int lineLength = GetSelectableLineLength(line);
            int startCol = row == first.Row ? first.Col : 0;
            int endCol = row == last.Row ? last.Col : lineLength - 1;

            if (lineLength > 0 && startCol <= endCol && startCol < lineLength)
            {
                int safeEnd = Math.Min(endCol, lineLength - 1);
                if (safeEnd >= startCol)
                {
                    builder.Append(line.Substring(startCol, safeEnd - startCol + 1));
                }
            }

            if (row < last.Row)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Snapshot is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus();
        if (!TryMapPointerToCell(e.GetPosition(this), Snapshot, out var cell))
        {
            return;
        }

        _selectionAnchor = cell;
        _selectionEnd = cell;
        _isSelecting = true;
        _renderSelectionLiveBottomState();
        e.Pointer.Capture(this);
        e.Handled = true;
        InvalidateVisual();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isSelecting || Snapshot is null)
        {
            return;
        }

        if (!TryMapPointerToCell(e.GetPosition(this), Snapshot, out var cell))
        {
            return;
        }

        _selectionEnd = cell;
        e.Handled = true;
        InvalidateVisual();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isSelecting)
        {
            return;
        }

        _isSelecting = false;
        e.Pointer.Capture(null);
        e.Handled = true;

        if (_selectionAnchor == _selectionEnd)
        {
            _selectionAnchor = null;
            _selectionEnd = null;
        }

        InvalidateVisual();
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var snapshot = Snapshot;
        if (snapshot is null)
        {
            return;
        }

        int maxTop = Math.Max(0, snapshot.TotalRows - snapshot.Rows);
        if (maxTop == 0 || Math.Abs(e.Delta.Y) < double.Epsilon)
        {
            return;
        }

        int step = Math.Max(1, (int)Math.Ceiling(Math.Abs(e.Delta.Y) * 3));
        int currentTop = GetViewportTop(snapshot);
        int nextTop = e.Delta.Y > 0
            ? Math.Max(0, currentTop - step)
            : Math.Min(maxTop, currentTop + step);

        _viewportTop = nextTop;
        _followLiveBottom = nextTop >= maxTop;
        e.Handled = true;
        InvalidateVisual();
    }

    private static Color GetThemeColor(string resourceKey, Color fallback)
    {
        if (Application.Current?.Resources.TryGetResource(resourceKey, ThemeVariant.Default, out object? value) == true)
        {
            if (value is Color color)
            {
                return color;
            }

            if (value is ISolidColorBrush brush)
            {
                return brush.Color;
            }
        }

        return fallback;
    }

    private int GetViewportTop(TerminalSnapshot snapshot)
    {
        int maxTop = Math.Max(0, snapshot.TotalRows - snapshot.Rows);
        if (_followLiveBottom)
        {
            _viewportTop = maxTop;
            return maxTop;
        }

        _viewportTop = Math.Clamp(_viewportTop, 0, maxTop);
        return _viewportTop;
    }

    private bool TryMapPointerToCell(Point point, TerminalSnapshot snapshot, out (int Row, int Col) cell)
    {
        cell = default;
        if (CellWidth <= 0 || LineHeight <= 0 || snapshot.Columns <= 0 || snapshot.TotalRows <= 0)
        {
            return false;
        }

        int row = Math.Clamp((int)(point.Y / LineHeight), 0, snapshot.Rows - 1);
        int absoluteRow = GetViewportTop(snapshot) + row;
        string line = GetAbsoluteLine(snapshot, absoluteRow);
        int lineLength = GetSelectableLineLength(line);
        if (lineLength == 0)
        {
            return false;
        }

        int col = Math.Clamp((int)(point.X / CellWidth), 0, lineLength - 1);
        cell = (absoluteRow, col);
        return true;
    }

    private bool HasSelection() =>
        _selectionAnchor.HasValue &&
        _selectionEnd.HasValue &&
        _selectionAnchor.Value != _selectionEnd.Value;

    private bool IsSelected(int row, int col)
    {
        if (!HasSelection())
        {
            return false;
        }

        NormalizeSelection(_selectionAnchor!.Value, _selectionEnd!.Value, out var first, out var last);

        if (row < first.Row || row > last.Row)
        {
            return false;
        }

        if (first.Row == last.Row)
        {
            return col >= first.Col && col <= Math.Min(last.Col, GetSelectableLineLength(GetAbsoluteLine(Snapshot!, row)) - 1);
        }

        if (row == first.Row)
        {
            int lineLength = GetSelectableLineLength(GetAbsoluteLine(Snapshot!, row));
            return lineLength > 0 && col >= first.Col && col < lineLength;
        }

        if (row == last.Row)
        {
            return col <= Math.Min(last.Col, GetSelectableLineLength(GetAbsoluteLine(Snapshot!, row)) - 1);
        }

        return col < GetSelectableLineLength(GetAbsoluteLine(Snapshot!, row));
    }

    private bool TryGetSelectionSpanForRow(int row, int columns, out int startCol, out int endCol)
    {
        startCol = 0;
        endCol = 0;

        if (!HasSelection())
        {
            return false;
        }

        NormalizeSelection(_selectionAnchor!.Value, _selectionEnd!.Value, out var first, out var last);

        if (row < first.Row || row > last.Row)
        {
            return false;
        }

        string line = GetAbsoluteLine(Snapshot!, row);
        int lineLength = GetSelectableLineLength(line);
        if (lineLength == 0)
        {
            return false;
        }

        startCol = row == first.Row ? first.Col : 0;
        endCol = row == last.Row ? Math.Min(last.Col, lineLength - 1) : lineLength - 1;
        return startCol <= endCol;
    }

    private static void NormalizeSelection(
        (int Row, int Col) start,
        (int Row, int Col) end,
        out (int Row, int Col) first,
        out (int Row, int Col) last)
    {
        if (start.Row < end.Row || (start.Row == end.Row && start.Col <= end.Col))
        {
            first = start;
            last = end;
        }
        else
        {
            first = end;
            last = start;
        }
    }

    private static TerminalCell GetAbsoluteCell(TerminalSnapshot snapshot, int absoluteRow, int col)
    {
        if (absoluteRow < snapshot.ScrollbackLines.Count)
        {
            return snapshot.ScrollbackCells[(absoluteRow * snapshot.Columns) + col];
        }

        int viewportRow = absoluteRow - snapshot.ScrollbackLines.Count;
        return snapshot.Cells[(viewportRow * snapshot.Columns) + col];
    }

    private static string GetAbsoluteLine(TerminalSnapshot snapshot, int absoluteRow)
    {
        if (absoluteRow < snapshot.ScrollbackLines.Count)
        {
            return snapshot.ScrollbackLines[absoluteRow];
        }

        return snapshot.Lines[absoluteRow - snapshot.ScrollbackLines.Count];
    }

    private static int GetSelectableLineLength(string line) => line.TrimEnd().Length;

    private void _renderSelectionLiveBottomState()
    {
        _followLiveBottom = false;
    }

    private void ResetCursorBlink()
    {
        _isCursorBlinkOn = true;

        if (_isFocused && CursorVisible)
        {
            _cursorBlinkTimer.Start();
        }
        else
        {
            _cursorBlinkTimer.Stop();
        }
    }

}
