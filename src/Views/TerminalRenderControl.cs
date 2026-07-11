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

    /// <summary>
    /// Mirrors <see cref="TerminalViewModel.IsAlternateScreenActive"/>. While a
    /// full-screen TUI owns the terminal, manual scrollback and selection are
    /// suppressed so no main-buffer cells can leak into the view.
    /// </summary>
    public static readonly StyledProperty<bool> IsAlternateScreenActiveProperty =
        AvaloniaProperty.Register<TerminalRenderControl, bool>(nameof(IsAlternateScreenActive));

    /// <summary>
    /// Current terminal search result, projected from <see cref="TerminalPanel"/>.
    /// When set, matches are highlighted and the active match is drawn distinctly.
    /// Ignored while a full-screen TUI owns the terminal (see
    /// <see cref="EffectiveSearchResult"/>) so no main-buffer content can leak.
    /// </summary>
    public static readonly StyledProperty<TerminalSearchResult?> SearchResultProperty =
        AvaloniaProperty.Register<TerminalRenderControl, TerminalSearchResult?>(nameof(SearchResult));

    static TerminalRenderControl()
    {
        AffectsRender<TerminalRenderControl>(SnapshotProperty);
        AffectsRender<TerminalRenderControl>(CursorRowProperty);
        AffectsRender<TerminalRenderControl>(CursorColProperty);
        AffectsRender<TerminalRenderControl>(CursorVisibleProperty);
        AffectsRender<TerminalRenderControl>(IsAlternateScreenActiveProperty);
        AffectsRender<TerminalRenderControl>(SearchResultProperty);
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

    public bool IsAlternateScreenActive
    {
        get => GetValue(IsAlternateScreenActiveProperty);
        set => SetValue(IsAlternateScreenActiveProperty, value);
    }

    public TerminalSearchResult? SearchResult
    {
        get => GetValue(SearchResultProperty);
        set => SetValue(SearchResultProperty, value);
    }

    // ── font and metrics ──────────────────────────────────────────

    private static readonly FontFamily DefaultFontFamily =
        FontFamily.Parse("Cascadia Code, JetBrains Mono, DejaVu Sans Mono, monospace");

    private Typeface _typeface;
    private double _fontSize = 14;

    internal string CurrentFontFamily => _typeface.FontFamily.Name;
    internal double CurrentFontSize => _fontSize;

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

    // Search highlight fills. The active match is drawn distinctly (stronger
    // opacity) so the user can tell which occurrence navigation will land on.
    private static readonly IBrush MatchHighlightBrush =
        new SolidColorBrush(Color.FromArgb(70, 255, 214, 102));
    private static readonly IBrush ActiveMatchHighlightBrush =
        new SolidColorBrush(Color.FromArgb(170, 255, 196, 0));

    private bool _isFocused;
    private bool _isCursorBlinkOn = true;
    private bool _followLiveBottom = true;
    private bool _isSelecting;
    private int _viewportTop;
    private (int Row, int Col)? _selectionAnchor;
    private (int Row, int Col)? _selectionEnd;
    private readonly DispatcherTimer _cursorBlinkTimer;
    private readonly DispatcherTimer _dragAutoScrollTimer;
    private double _lastPointerY;
    private int _pendingClickCount;
    private DateTime _lastClickTime;
    private (int Row, int Col)? _lastClickCell;

    private static readonly TimeSpan MultiClickInterval = TimeSpan.FromMilliseconds(400);
    private const int DragAutoScrollMarginPx = 24;

    // ── ctor ──────────────────────────────────────────────────────

    public TerminalRenderControl()
    {
        ClipToBounds = true;
        Focusable = true;
        Cursor = CreateIbeamCursorOrNull();

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

        // Drives auto-scroll while the pointer is dragging a selection past
        // the top or bottom edge of the viewport.
        _dragAutoScrollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _dragAutoScrollTimer.Tick += (_, _) => TickDragAutoScroll();

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

        // Drop any active selection when a full-screen TUI takes over so the
        // main buffer cannot be copied mid-session.
        this.GetObservable(IsAlternateScreenActiveProperty).Subscribe(active =>
        {
            if (active)
            {
                _selectionAnchor = null;
                _selectionEnd = null;
                _isSelecting = false;
                _dragAutoScrollTimer.Stop();
                InvalidateVisual();
            }
        });

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;

        // Key forwarding is wired by TerminalPanel.
    }

    public void ApplyFontSettings(string family, int size)
    {
        _typeface = new Typeface(new FontFamily(family), FontStyle.Normal, FontWeight.Normal);
        _fontSize = size;
        MeasureCellMetrics();
        InvalidateMeasureForFontChange();
        InvalidateVisualForFontChange();
    }

    internal virtual void InvalidateMeasureForFontChange() => InvalidateMeasure();

    internal virtual void InvalidateVisualForFontChange() => InvalidateVisual();

    // ── render ────────────────────────────────────────────────────

    public override void Render(DrawingContext context)
    {
        Color defaultBackground = GetThemeColor("SurfaceBaseBrushColor", AnsiColors[0]);
        Color defaultForeground = GetThemeColor("TextPrimaryBrushColor", AnsiColors[7]);
        Color selectionBackground = GetThemeColor("SecondaryAccentBrushColor", Color.FromArgb(160, 194, 194, 229));
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
        var search = EffectiveSearchResult;
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

            // Search highlights are drawn behind the cell text. For cells with
            // their own background the fill wins, which is an acceptable edge
            // case; default terminal cells keep the highlight visible.
            if (search is not null)
            {
                DrawSearchHighlights(context, search, absoluteRow, y, cw, lh);
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
        try
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
        catch (InvalidOperationException)
        {
            // No text-formatting platform service is registered (e.g. under
            // headless unit tests with no windowing platform configured).
            // Selection/copy logic does not depend on real pixel metrics, so
            // leave the metrics at their default zero values.
        }
    }

    public bool TryGetSelectedText(out string? text)
    {
        // While a full-screen TUI is open, the main buffer must not be exposed
        // through selection/copy even if a stale selection exists.
        if (!IsMainBufferSelectionEnabled(IsAlternateScreenActive))
        {
            text = null;
            return false;
        }

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
    /// Whether main-buffer selection, manual scrollback, and copy are permitted.
    /// Suppressed while a full-screen TUI owns the terminal so no main-buffer
    /// cells can leak into the view; this is the single decision point the
    /// pointer/scroll/copy handlers consult.
    /// </summary>
    internal static bool IsMainBufferSelectionEnabled(bool isAlternateScreenActive) =>
        !isAlternateScreenActive;

    /// <summary>
    /// The search result to actually render. Returns <c>null</c> while a
    /// full-screen TUI owns the terminal, so highlighting can never expose
    /// hidden main-buffer content during <c>less</c> / <c>vim</c> sessions. This
    /// is the single seam the search UI consults for the alternate-screen gate.
    /// </summary>
    internal TerminalSearchResult? EffectiveSearchResult =>
        IsMainBufferSelectionEnabled(IsAlternateScreenActive) ? SearchResult : null;

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
            _dragAutoScrollTimer.Stop();
        }

        InvalidateVisual();
    }

    /// <summary>
    /// Scrolls the viewport up by one page (leaving a single overlap row, like
    /// typical terminals). No-op while a full-screen TUI owns the surface.
    /// </summary>
    public void ScrollPageUp()
    {
        if (!IsMainBufferSelectionEnabled(IsAlternateScreenActive))
        {
            return;
        }

        var snapshot = Snapshot;
        if (snapshot is null)
        {
            return;
        }

        ApplyViewportTop(GetViewportTop(snapshot) - GetPageStep(snapshot));
    }

    /// <summary>
    /// Scrolls the viewport down by one page (leaving a single overlap row).
    /// Rejoins live-bottom following when it reaches the newest output.
    /// No-op while a full-screen TUI owns the surface.
    /// </summary>
    public void ScrollPageDown()
    {
        if (!IsMainBufferSelectionEnabled(IsAlternateScreenActive))
        {
            return;
        }

        var snapshot = Snapshot;
        if (snapshot is null)
        {
            return;
        }

        ApplyViewportTop(GetViewportTop(snapshot) + GetPageStep(snapshot));
    }

    /// <summary>
    /// Jumps the viewport to the top of all available snapshot rows
    /// (scrollback + visible). No-op while a full-screen TUI owns the surface.
    /// </summary>
    public void ScrollToTop()
    {
        if (!IsMainBufferSelectionEnabled(IsAlternateScreenActive))
        {
            return;
        }

        if (Snapshot is null)
        {
            return;
        }

        ApplyViewportTop(0);
    }

    /// <summary>Whether the viewport is currently pinned to the live bottom.</summary>
    internal bool IsFollowingLiveBottom => _followLiveBottom;

    /// <summary>
    /// The number of rows a single page-up / page-down step moves the viewport.
    /// One overlap row is retained so context is preserved between pages.
    /// </summary>
    internal int GetPageStep(TerminalSnapshot snapshot) =>
        Math.Max(1, snapshot.Rows - 1);

    /// <summary>
    /// The highest scrollable <c>viewportTop</c> for the given snapshot
    /// (newest output shown at the bottom).
    /// </summary>
    internal int GetMaxViewportTop(TerminalSnapshot snapshot) =>
        Math.Max(0, snapshot.TotalRows - snapshot.Rows);

    /// <summary>
    /// Centralizes clamping for every viewport movement (wheel, keyboard page,
    /// keyboard home/end, drag auto-scroll). Keeps <see cref="_followLiveBottom"/>
    /// in sync: arriving at the bottom row rejoins live-bottom following,
    /// leaving it disables following so new output cannot yank the viewport.
    /// </summary>
    internal void ApplyViewportTop(int top)
    {
        var snapshot = Snapshot;
        if (snapshot is null)
        {
            return;
        }

        int maxTop = GetMaxViewportTop(snapshot);
        int clamped = Math.Clamp(top, 0, maxTop);
        _viewportTop = clamped;
        _followLiveBottom = clamped >= maxTop;
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

        // No main-buffer selection while a full-screen TUI owns the surface.
        if (IsAlternateScreenActive)
        {
            return;
        }

        Focus();
        if (!TryMapPointerToCell(e.GetPosition(this), Snapshot, out var cell))
        {
            return;
        }

        int clickCount = GetClickCount(cell);

        if (clickCount == 2)
        {
            TryGetWordSelectionRange(Snapshot, cell, out var wordStart, out var wordEnd);
            _selectionAnchor = wordStart;
            _selectionEnd = wordEnd;
            _isSelecting = false;
        }
        else if (clickCount >= 3)
        {
            GetLineSelectionRange(Snapshot, cell.Row, out var lineStart, out var lineEnd);
            _selectionAnchor = lineStart;
            _selectionEnd = lineEnd;
            _isSelecting = false;
        }
        else
        {
            _selectionAnchor = cell;
            _selectionEnd = cell;
            _isSelecting = true;
        }

        _renderSelectionLiveBottomState();
        e.Pointer.Capture(this);
        e.Handled = true;
        InvalidateVisual();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isSelecting || Snapshot is null || IsAlternateScreenActive)
        {
            return;
        }

        double y = e.GetPosition(this).Y;
        _lastPointerY = y;
        UpdateDragAutoScroll(y);

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
        _dragAutoScrollTimer.Stop();
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

        // No manual scrollback into the main buffer while a full-screen TUI is open.
        if (IsAlternateScreenActive)
        {
            return;
        }

        int maxTop = GetMaxViewportTop(snapshot);
        if (maxTop == 0 || Math.Abs(e.Delta.Y) < double.Epsilon)
        {
            return;
        }

        int step = Math.Max(1, (int)Math.Ceiling(Math.Abs(e.Delta.Y) * 3));
        int currentTop = GetViewportTop(snapshot);
        int nextTop = e.Delta.Y > 0
            ? Math.Max(0, currentTop - step)
            : Math.Min(maxTop, currentTop + step);

        ApplyViewportTop(nextTop);
        e.Handled = true;
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

    internal int GetViewportTop(TerminalSnapshot snapshot)
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

    /// <summary>
    /// Determines the click count (1, 2, or 3+) for a press on the given cell,
    /// based on elapsed time and cell proximity since the last press. Used to
    /// drive single/double/triple-click selection behavior.
    /// </summary>
    private int GetClickCount((int Row, int Col) cell)
    {
        var now = DateTime.UtcNow;
        bool sameCell = _lastClickCell == cell;
        bool withinInterval = (now - _lastClickTime) <= MultiClickInterval;

        if (sameCell && withinInterval)
        {
            _pendingClickCount++;
        }
        else
        {
            _pendingClickCount = 1;
        }

        _lastClickTime = now;
        _lastClickCell = cell;

        // Cap at 3 — anything beyond triple-click still selects the full line.
        if (_pendingClickCount > 3)
        {
            _pendingClickCount = 3;
        }

        return _pendingClickCount;
    }

    /// <summary>
    /// Whether a character participates in "word" boundaries for double-click
    /// word selection. Matches common IDE conventions: letters, digits, and
    /// underscore are part of a word; everything else (including punctuation
    /// and whitespace) is a boundary.
    /// </summary>
    internal static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>
    /// Expands a single clicked cell into the (start, end) range covering the
    /// contiguous run of word characters under the click, for double-click
    /// word selection. If the clicked cell is not a word character, selects
    /// just that single cell.
    /// </summary>
    internal static bool TryGetWordSelectionRange(
        TerminalSnapshot snapshot,
        (int Row, int Col) cell,
        out (int Row, int Col) start,
        out (int Row, int Col) end)
    {
        string line = GetAbsoluteLine(snapshot, cell.Row);
        int lineLength = GetSelectableLineLength(line);

        if (lineLength == 0 || cell.Col >= lineLength)
        {
            start = cell;
            end = cell;
            return false;
        }

        if (!IsWordChar(line[cell.Col]))
        {
            start = cell;
            end = cell;
            return true;
        }

        int startCol = cell.Col;
        while (startCol > 0 && IsWordChar(line[startCol - 1]))
        {
            startCol--;
        }

        int endCol = cell.Col;
        while (endCol < lineLength - 1 && IsWordChar(line[endCol + 1]))
        {
            endCol++;
        }

        start = (cell.Row, startCol);
        end = (cell.Row, endCol);
        return true;
    }

    /// <summary>
    /// Returns the (start, end) selection range covering the full logical
    /// line at the given absolute row, for triple-click line selection.
    /// </summary>
    internal static void GetLineSelectionRange(
        TerminalSnapshot snapshot,
        int row,
        out (int Row, int Col) start,
        out (int Row, int Col) end)
    {
        string line = GetAbsoluteLine(snapshot, row);
        int lineLength = GetSelectableLineLength(line);
        int endCol = Math.Max(0, lineLength - 1);
        start = (row, 0);
        end = (row, endCol);
    }

    /// <summary>
    /// Starts or stops the drag auto-scroll timer based on whether the
    /// pointer is within the auto-scroll margin near the top/bottom edge
    /// of the viewport while a drag selection is in progress.
    /// </summary>
    private void UpdateDragAutoScroll(double pointerY)
    {
        bool nearTop = pointerY < DragAutoScrollMarginPx;
        bool nearBottom = pointerY > Bounds.Height - DragAutoScrollMarginPx;

        if (nearTop || nearBottom)
        {
            if (!_dragAutoScrollTimer.IsEnabled)
            {
                _dragAutoScrollTimer.Start();
            }
        }
        else
        {
            _dragAutoScrollTimer.Stop();
        }
    }

    /// <summary>
    /// Scrolls the viewport by one row toward the drag direction and extends
    /// the selection to the new edge row, while a drag selection is active
    /// and the pointer remains near the top/bottom edge.
    /// </summary>
    private void TickDragAutoScroll()
    {
        var snapshot = Snapshot;
        if (!_isSelecting || snapshot is null || IsAlternateScreenActive)
        {
            _dragAutoScrollTimer.Stop();
            return;
        }

        int maxTop = Math.Max(0, snapshot.TotalRows - snapshot.Rows);
        int currentTop = GetViewportTop(snapshot);
        bool nearTop = _lastPointerY < DragAutoScrollMarginPx;
        bool nearBottom = _lastPointerY > Bounds.Height - DragAutoScrollMarginPx;

        int nextTop = currentTop;
        if (nearTop)
        {
            nextTop = Math.Max(0, currentTop - 1);
        }
        else if (nearBottom)
        {
            nextTop = Math.Min(maxTop, currentTop + 1);
        }

        if (nextTop == currentTop)
        {
            return;
        }

        ApplyViewportTop(nextTop);

        int edgeRow = nearTop ? nextTop : nextTop + snapshot.Rows - 1;
        edgeRow = Math.Clamp(edgeRow, 0, snapshot.TotalRows - 1);
        string line = GetAbsoluteLine(snapshot, edgeRow);
        int lineLength = GetSelectableLineLength(line);
        int col = Math.Max(0, lineLength - 1);
        _selectionEnd = (edgeRow, col);

        InvalidateVisual();
    }

    private void _renderSelectionLiveBottomState()
    {
        _followLiveBottom = false;
    }

    /// <summary>
    /// Draws highlight rectangles for every search match on <paramref name="absoluteRow"/>,
    /// behind the cell text. The active match uses a stronger fill so it stands out.
    /// </summary>
    private void DrawSearchHighlights(
        DrawingContext context,
        TerminalSearchResult search,
        int absoluteRow,
        double y,
        double cw,
        double lh)
    {
        TerminalSearchMatch? active = search.ActiveMatch;
        foreach (var match in search.Matches)
        {
            if (match.Row != absoluteRow)
            {
                continue;
            }

            bool isActive = active.HasValue && active.Value.Equals(match);
            var brush = isActive ? ActiveMatchHighlightBrush : MatchHighlightBrush;
            context.FillRectangle(
                brush,
                new Rect(match.StartCol * cw, y, (match.EndCol - match.StartCol) * cw, lh));
        }
    }

    /// <summary>
    /// Scrolls the viewport (through the existing <see cref="ApplyViewportTop"/>
    /// seam) so the given search match row becomes visible, without inventing a
    /// parallel scroll state. No-op while a full-screen TUI owns the surface.
    /// If the match is already on screen, the viewport is left untouched.
    /// </summary>
    public void BringSearchMatchIntoView(TerminalSearchMatch match)
    {
        if (!IsMainBufferSelectionEnabled(IsAlternateScreenActive))
        {
            return;
        }

        var snapshot = Snapshot;
        if (snapshot is null)
        {
            return;
        }

        int top = GetViewportTop(snapshot);
        int visibleRows = snapshot.Rows;
        if (match.Row >= top && match.Row < top + visibleRows)
        {
            return;
        }

        int maxTop = GetMaxViewportTop(snapshot);
        // Center the match vertically when possible; clamp keeps it on screen.
        int target = Math.Clamp(match.Row - visibleRows / 2, 0, maxTop);
        ApplyViewportTop(target);
    }

    private static Cursor? CreateIbeamCursorOrNull()
    {
        try
        {
            return new Cursor(StandardCursorType.Ibeam);
        }
        catch (InvalidOperationException)
        {
            // No windowing platform is registered (e.g. under headless unit
            // tests). The render control is still fully usable without a
            // custom pointer cursor in that context.
            return null;
        }
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
