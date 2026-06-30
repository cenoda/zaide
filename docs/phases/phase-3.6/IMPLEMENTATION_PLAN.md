# Phase 3.6: Terminal Renderer Foundation — Implementation Plan

## Planning Status

**In progress.** Planning audit is complete, Fable review feedback has been
incorporated, M1 is now closed, and the latest verification gates passed
serially:

- `dotnet build Zaide.slnx` — 0 warnings, 0 errors
- `dotnet test Zaide.slnx --no-build` — 288 passed, 0 failed

Preparation was re-verified against live code on **2026-06-30**. The current
terminal surface still matches the expected pre-renderer baseline:

- `src/Views/TerminalPanel.cs` still renders terminal output with a read-only
  `TextBox`
- `src/ViewModels/TerminalViewModel.cs` still exposes `OutputText` backed by
  `TerminalOutputBuffer`
- `src/Program.cs` still registers `ITerminalService` as
  `LinuxTerminalService` and `TerminalViewModel` as a singleton

M1 is complete after the parser audit fixes landed on **2026-06-30**. M2 is
complete after the screen-buffer model and its 53 tests landed on **2026-06-30**.
M3–M5 remain unchecked.

## Pre-Implementation Verification

- [x] Read `docs/phases/phase-3.6/BRIEF.md`
- [x] Read `docs/phases/phase-3.5/IMPLEMENTATION_PLAN.md` (predecessor)
- [x] Read `docs/phases/phase-3.5/TOFIX.md` (deferred items)
- [x] Verify current terminal panel: `src/Views/TerminalPanel.cs`
- [x] Verify current output buffer: `src/ViewModels/TerminalOutputBuffer.cs`
- [x] Verify current ViewModel: `src/ViewModels/TerminalViewModel.cs`
- [x] Verify current tests: `TerminalOutputBufferTests`, `TerminalKeyMapperTests`, `TerminalGeometryTests`
- [x] Verify composition root: `src/Program.cs`
- [x] Verify additional consumers of `TerminalViewModel.OutputText`: `src/MainWindow.axaml.cs`, `tests/Zaide.Tests/MainWindowViewModelTests.cs`
- [x] Confirm entry gate: `dotnet build`
- [x] Confirm entry gate: `dotnet test`
- [x] Phase 3.5 manual Linux smoke test — run by user (informal pass; Phase 3.5 exit checklist not yet updated)

## Scope

**Goal:** Replace the `TextBox`-based terminal surface with a custom rendering pipeline: an ANSI/CSI parser, a screen-buffer model of styled character cells, and a `DrawingContext`-based control that renders them.

**Boundaries:**
- Do not aim for full `vim` / `htop` compatibility yet
- Do not add Windows or macOS backends here
- Do not mix renderer work with terminal tabs, splits, or settings UI
- Keep the supported escape-sequence set intentionally small and testable
- The `TerminalOutputBuffer` carryover (raw text with `\r`/`\b`/`\n`) is replaced; it is not extended or refactored

## Verified Live Touch Points

These are the concrete code seams verified on 2026-06-30 that Phase 3.6 will
change or must preserve:

- `src/Views/TerminalPanel.cs`
  - owns the toolbar, focus behavior, clipboard handlers, key forwarding, and
    resize forwarding
  - currently measures glyph width locally and binds `ViewModel.OutputText`
    into `_outputTextBox.Text`
- `src/ViewModels/TerminalViewModel.cs`
  - owns UTF-8 decoder continuity across output chunks
  - owns terminal lifecycle (`EnsureStartedAsync`, `RestartAsync`, process-exit
    handling) that should remain intact during renderer migration
  - currently raises property changes for `OutputText`
- `src/ViewModels/TerminalOutputBuffer.cs`
  - pure raw-text buffer that is intentionally superseded in this phase rather
    than evolved
- `src/Views/TerminalGeometry.cs`
  - already provides the reusable row/column math and should stay as the pure
    geometry seam for resize tests
- `src/Services/ITerminalService.cs` and `src/Services/LinuxTerminalService.cs`
  - PTY lifecycle and byte-stream contracts remain unchanged in Phase 3.6; the
    renderer work sits above this seam
- Tests that will require migration:
  - `tests/Zaide.Tests/ViewModels/TerminalOutputBufferTests.cs`
  - `tests/Zaide.Tests/ViewModels/TerminalViewModelTests.cs`
  - `tests/Zaide.Tests/MainWindowViewModelTests.cs`

## Milestones (Incremental)

| Milestone | Description | Test | Status |
|-----------|-------------|------|--------|
| M0 | Entry gate: current build and tests pass | `dotnet build`, `dotnet test` | ✅ Ready |
| M1 | ANSI/CSI sequence parser (state machine) | Unit tests for known sequences | ✅ Complete |
| M2 | Screen-buffer model (2D cell grid with attributes) | Unit tests for write, scroll, clear, cursor moves | ✅ Complete |
| M3 | Custom terminal render control (Avalonia `DrawingContext`) | Unit tests for geometry; visual smoke test | ✅ Complete (2 test items deferred to M4 -- see TOFIX) |
| M4 | Wire pipeline: parser → screen buffer → render control | Integration test with mock PTY output | ✅ Complete |
| M5 | Documentation and exit audit | `dotnet build`, `dotnet test`, TOFIX update | ✅ Complete |

### M1: ANSI/CSI Sequence Parser

**File:** `src/ViewModels/AnsiParser.cs` (new, pure, UI-agnostic)

Create a state-machine parser that converts a stream of decoded characters into a sequence of structured "actions." The parser processes printable characters and escape sequences, producing action tokens that the screen buffer consumes.

**Parser states (simplified VT100):**
- `Ground` — printing characters, newlines, tabs accumulate into text runs
- `Escape` — saw `\x1B` (ESC), waiting for the next byte
- `CsiEntry` — saw `\x1B[`, accumulating parameter bytes
- `CsiParam` — collecting numeric parameters and intermediate bytes until a final byte

**Parser output — action types (discriminated union / record hierarchy):**

```csharp
// Discriminated union via inheritance or a tagged struct
enum ActionKind { Print, Execute, EscDispatch, CsiDispatch }

// Print: a run of printable characters to write at the cursor
// Execute: C0 control character (\r, \n, \b, \t, \a, etc.)
// EscDispatch: private escape sequence (e.g. ESC 7 / ESC 8 save/restore cursor)
// CsiDispatch: parsed CSI sequence with params and final byte
record CsiAction(int[] Params, char FinalByte);
```

**Supported CSI sequences (the "small and testable" set — this phase only):**

| Sequence | Final Byte | Meaning | Params |
|----------|-----------|---------|--------|
| `CSI n A` | `'A'` | Cursor Up | n = 1 (default) |
| `CSI n B` | `'B'` | Cursor Down | n = 1 |
| `CSI n C` | `'C'` | Cursor Forward | n = 1 |
| `CSI n D` | `'D'` | Cursor Back | n = 1 |
| `CSI n ; m H` | `'H'` | Cursor Position | n=row(1), m=col(1) |
| `CSI n J` | `'J'` | Erase in Display | 0=after cursor, 1=before, 2=all, 3=saved |
| `CSI n K` | `'K'` | Erase in Line | 0=after cursor, 1=before, 2=all |
| `CSI n ; ... m` | `'m'` | SGR (Select Graphic Rendition) | see below |

**SGR subset (basic 8/16 colors, bold, reset):**

| Param(s) | Effect |
|----------|--------|
| 0 | Reset all attributes |
| 1 | Bold / Bright foreground |
| 7 | Inverse / Reverse |
| 30–37 | Foreground color (0–7 standard ANSI) |
| 38;5;n | 256-color foreground (consumed and ignored — deferred) |
| 39 | Default foreground |
| 40–47 | Background color (0–7 standard ANSI) |
| 48;5;n | 256-color background (consumed and ignored — deferred) |
| 49 | Default background |
| 90–97 | Bright foreground (8–15) |
| 100–107 | Bright background (8–15) |

**Deferred (documented gap in parser tests):**
- `CSI s` / `CSI u` — save/restore cursor
- `CSI ? ... h/l` — DECSET/DECRST (alternate screen, cursor visibility)
- DCS (Device Control String) — used by `tmux`/`kitty`; not supported
- OSC (Operating System Command) — window title, clipboard; not supported
- SGR 3 (italic), 4 (underline), 9 (strikethrough) — recognised but treated as no-op if attribute not yet in `CellAttribute`
- SGR 38;5;n / 48;5;n — consumed and ignored; extended palette support is deferred

**C0 control characters handled:**
- `\r` (0x0D) → Execute: CR
- `\n` (0x0A) → Execute: LF
- `\b` (0x08) → Execute: BS
- `\t` (0x09) → Execute: TAB (advance to next tab stop, default every 8)
- `\a` (0x07) → Execute: BEL (no-op this phase — documented)

**Design rules:**
- The parser is pure: input is `ReadOnlySpan<char>`, output is `IEnumerable<AnsiAction>` or an action list. No dependencies on Avalonia, `ITerminalService`, or screen buffer types.
- The parser **drops unsupported sequences silently** at the parser level. Unknown/incomplete escape sequences (including private DECSET/DECRST, DCS, OSC, and unrecognised CSI final bytes) are consumed but produce no action output. This avoids visible escape junk (the main UX goal) and keeps the screen buffer free of "unknown action" handling. Tests assert unsupported sequences produce zero actions.
- **The parser fully owns partial-sequence state.** Split escape sequences across chunk boundaries (e.g. `\x1B` at end of chunk A, `[31m` in chunk B) are handled entirely inside the parser. Each call to `Parse(nextChunk)` continues from its internal state and emits complete actions only. The ViewModel simply calls `Parse(nextChunk)` — it does not inspect or stitch parser internals.

**Tests (M1):**
- Parse plain text unchanged
- Parse `\x1B[31m` → CSI dispatch with param [31], final byte 'm'
- Parse `\x1B[2J` → CSI dispatch with param [2], final byte 'J'
- Parse `\x1B[A` (cursor up, default param 1)
- Parse `\x1B[3;5H` (cursor position with row=3, col=5)
- Parse `\x1B[0m` → SGR reset
- Parse `\x1B[1;31m` → SGR bright + red
- Unknown sequence `\x1B[?25h` → no actions emitted (silently dropped)
- Split sequence across two `Append` calls (partial persists)
- Newline and carriage return in ground state produce Execute actions

### M2: Screen-Buffer Model

**File:** `src/ViewModels/TerminalScreen.cs` (new, pure, UI-agnostic)

A 2D grid of character cells representing the visible terminal surface. Replaces `TerminalOutputBuffer` as the authoritative terminal state.

```csharp
internal sealed class TerminalScreen
{
    private readonly Cell[,] _buffer;
    private readonly int _columns;
    private readonly int _rows;
    private int _cursorRow;      // 0-based
    private int _cursorCol;      // 0-based
    private CellAttribute _currentAttributes;

    public ReadOnlySpan<char> GetLine(int row);   // for testing/debug
    public Cell GetCell(int row, int col);
}
```

```csharp
internal readonly struct Cell
{
    public readonly char Char;
    public readonly CellAttribute Attribute;
}

internal readonly struct CellAttribute
{
    public readonly int Foreground;      // -1 = default, 0-15 for ANSI
    public readonly int Background;      // -1 = default, 0-15 for ANSI
    public readonly bool Bold;
    public readonly bool Inverse;
}
```

**Operations (applied by the ViewModel after parsing a chunk):**

- **`Write(char c)`** — writes a single character at the cursor, advances cursor, scrolls if needed
- **`WriteText(ReadOnlySpan<char> text)`** — writes a run, handling wraps at right margin (classic scroll-wrap: cursor to next line, scroll if at bottom)
- **`ExecuteC0(C0Kind kind)`** — handles CR (cursor to col 0), LF (cursor down, scroll), BS (cursor back), TAB (advance to next 8-col stop), BEL (no-op)
- **`CursorUp(int n)`** / **`CursorDown(int n)`** / **`CursorForward(int n)`** / **`CursorBack(int n)`** — clamped at edges
- **`CursorPosition(int row, int col)`** — 1-based (terminal convention); clamped
- **`EraseDisplay(int param)`** — 0 = from cursor to end, 1 = from start to cursor, 2 = all, 3 = all + scrollback (scrollback = no-op this phase)
- **`EraseLine(int param)`** — 0 = cursor to end of line, 1 = start to cursor, 2 = entire line
- **`SetSgr(int[] params)`** — updates `_currentAttributes`
- **`Scroll()`** — scroll buffer up by one row, clear bottom row, reset cursor to bottom row
- **`Resize(int columns, int rows)`** — reallocate buffer, copy overlapping cells (row/col clamped to min of old and new dimensions), fill new cells with space/default attributes, and clamp the cursor into the new bounds. No line reflow: content that falls outside the new grid is discarded. (Required by M4; initial buffer defaults to 80×24.)

**Scrollback:** The screen buffer is exactly the visible area (rows × cols). No scrollback history in this phase. The `TerminalOutputBuffer`'s ring-buffer scrollback (200K chars of history) is replaced entirely — full history is deferred to a future phase when scrollback in the custom control is added.

> ⚠️ **Deliberate temporary regressions from Phase 3.5:**
> 1. **Scrollback lost:** Phase 3.5 users could see ~200K chars of scrollback via the TextBox's built-in scrollbar. Phase 3.6 removes this — the terminal only shows the visible viewport (rows × cols). Scrollback returns in a future phase once the render control supports scrolling.
> 2. **No interactive selection:** Phase 3.5 users could select arbitrary text with the mouse in the TextBox and copy it. Phase 3.6 has no mouse selection. Ctrl+Shift+C copies all visible text row-by-row (not just the cursor line). This is strictly worse for targeted copying, but acceptable for the renderer foundation.
> Both are checked in the manual smoke test before closing the phase.

**Initial size:** The screen buffer is created with the default size **80 columns × 24 rows**. `TerminalViewModel.Resize()` is called by `TerminalPanel.ForwardResize()` as soon as the panel is measured; the first resize call updates to the actual viewport dimensions.

**Cursor visibility:** The cursor is always visible at `(_cursorRow, _cursorCol)`. Blinking, hide/show, and block/bar cursor shapes are deferred.

**Tests (M2):**
- Write text at cursor, wraps at right edge
- Newline advances cursor row, scrolls at bottom
- Carriage return moves cursor to column 0
- Cursor up/down/forward/back clamped at buffer edges
- Cursor position (1-based) sets correct cell
- Erase display (param 2) clears all cells
- Erase line (param 2) clears current line
- SGR bold + red sets attributes on subsequent writes
- SGR reset restores defaults
- Scroll pushes content up, clears bottom row
- Backspace does not cross into previous row
- Tab advances to next 8-column stop
- Content survives ordering: write "AB", cursor back, write "C" → "AC"
- Resize to larger grid: preserved cells at top-left, new cells are spaces
- Resize to smaller grid: overflowing cells discarded, cursor clamped

### M3: Custom Terminal Render Control

**File:** `src/Views/TerminalRenderControl.cs` (new, Avalonia `Control` subclass)

Replaces the `TextBox` as the terminal rendering surface. Uses `DrawingContext` for pixel-exact cell rendering.

```csharp
// File: src/ViewModels/TerminalSnapshot.cs — public snapshot for view binding
// (one class per file: TerminalSnapshot.cs, TerminalCell colocated as same file
//  since TerminalCell is only used as the element type of TerminalSnapshot.Cells)

namespace Zaide.ViewModels;

/// <summary>
/// Lightweight, immutable snapshot of the visible terminal surface for view
/// binding. Projected from internal <c>TerminalScreen</c> so the control's
/// styled property does not expose an internal type.
/// </summary>
public sealed class TerminalSnapshot
{
    public int Columns { get; }
    public int Rows { get; }
    /// <summary>Visible text content, row by row, without style info.
    /// Used for clipboard copy and accessibility.</summary>
    public IReadOnlyList<string> Lines { get; }
    /// <summary>Raw cell data for rendering. Row-major; length = Columns * Rows.</summary>
    public IReadOnlyList<TerminalCell> Cells { get; }
}

public readonly struct TerminalCell
{
    public readonly char Char;
    public readonly int Foreground;   // -1 = default, 0-15 ANSI
    public readonly int Background;   // -1 = default, 0-15 ANSI
    public readonly bool Bold;
    public readonly bool Inverse;
}
```

```csharp
// File: src/Views/TerminalRenderControl.cs

namespace Zaide.Views;

public class TerminalRenderControl : Control
{
    public static readonly StyledProperty<TerminalSnapshot?> SnapshotProperty;
    public static readonly StyledProperty<int> CursorRowProperty;
    public static readonly StyledProperty<int> CursorColProperty;
    public static readonly StyledProperty<bool> CursorVisibleProperty;

    public double CellWidth { get; private set; }
    public double CellHeight => LineHeight;
    public double LineHeight { get; private set; }

    public override void Render(DrawingContext context);
}
```

**Rendering approach:**
- Override `Render(DrawingContext)`
- Iterate each visible cell `(row, col)`, compute its pixel bounds, draw background rect (if non-default), then draw the character glyph with the appropriate foreground
- Use `FormattedText` for each cell (or optimize with cached glyph runs if profiling shows need)
- Cache `Typeface` and `FontSize` across render cycles
- Draw a cursor block at `(CursorRow, CursorCol)` using the inverted foreground/background
- Only re-render when the screen buffer or cursor position changes (invalidate via `InvalidateVisual()`)

**Focusability and input handling:**
- The constructor must set `Focusable = true` and handle `GotFocus`/`LostFocus` to toggle cursor visibility (cursor fully visible when focused, dimmed when unfocused)
- Subscribe to `KeyDownEvent` (tunnel routing, matching the existing `TextBox` handler pattern) for key-mapped input (arrows, Ctrl+C, etc.)
- Subscribe to `TextInputEvent` (tunnel routing) for printable character input
- Without this, Home/End/arrows and text input will not work — the control must be explicitly focusable and must forward keyboard events the same way the old TextBox did

**Performance notes:**
- For a typical 80×24 terminal, that's 1,920 `FormattedText` instances per frame. This is acceptable for a first pass — real terminals batch glyph rendering, but we avoid premature optimization.
- If profiling shows frame drops, the optimization path is: batch characters per unique attribute, then render each row as a single `FormattedText` with ` spans.
- The control owns the terminal font family and font size, and calculates cell metrics (`CellWidth`, `LineHeight`) internally. `TerminalPanel.ForwardResize()` reads `_renderControl.CellWidth` and `_renderControl.LineHeight` rather than hosting a duplicate `MeasureCellWidth()` helper. The `TerminalGeometry` helper is reused for the columns/rows math.
- `ForwardResize()` must skip resize forwarding until `_renderControl.CellWidth > 0` and `_renderControl.LineHeight > 0`. `TerminalGeometry.Compute()` rejects zero or negative metrics, and bounds can arrive before the control has measured its first glyph.

**Integration into TerminalPanel:**
- `TerminalPanel` swaps `_outputTextBox` for `_renderControl = new TerminalRenderControl()`
- `TerminalPanel` still owns the toolbar (status, clear, restart) — unchanged
- Key forwarding (`OnKeyDown`, `OnTextInput`) moves from `_outputTextBox` event handlers to `_renderControl` event handlers
- Clipboard (`CopySelectionAsync`) copies all visible text via `string.Join("\n", ScreenSnapshot.Lines)` — every visible row concatenated with newlines. This is the best achievable without mouse selection; once interactive selection lands in a future phase, it can restrict to the selected range.
- `FocusTerminal()` focuses `_renderControl` instead of `_outputTextBox`
- `ScrollToEnd()` becomes a no-op (no scrollbar yet — deferred)
- `ForwardResize()` uses `_renderControl.Bounds` and `_renderControl.CellWidth` / `_renderControl.LineHeight`, and returns early while either metric is zero

**Tests (M3):**

- **TerminalSnapshot structure test** (pure logic, no Avalonia): Construct a `TerminalSnapshot` with known content and assert `Cells` is row-major, correctly sized (Columns × Rows), and `Lines` matches the expected per-row strings. This validates the snapshotter's projection from `TerminalScreen`.
- **Render-control contract test** (no new test dependency): Verify the control exposes the expected styled properties and declares render invalidation with `AffectsRender<TerminalRenderControl>(SnapshotProperty, CursorRowProperty, CursorColProperty, CursorVisibleProperty)`. Do not add Avalonia.Headless for this phase unless separately approved under the dependency rules.
- **Resize guard test:** Exercise the resize forwarding path with zero metrics and assert it returns without calling `ViewModel.Resize()`. Then exercise positive metrics through `TerminalGeometryTests` as the pure geometry coverage.
- **Manual smoke test:** Terminal panel renders characters in correct positions and colors.
- Key forwarding tests remain in `TerminalKeyMapperTests` (unchanged).

### M4: Wire Pipeline — Parser → Screen Buffer → Render Control

**ViewModel changes (`TerminalViewModel.cs`):**

- Replace `TerminalOutputBuffer _outputBuffer` with:
  - `AnsiParser _parser` — processes decoded characters
  - `TerminalScreen _screen` — holds the cell grid
- Replace `OutputText` property with:
  - `TerminalSnapshot? ScreenSnapshot` — a public, view-bindable snapshot projected from the internal `_screen` on each change
  - `int CursorRow`, `int CursorCol`, `bool CursorVisible` — for cursor rendering
- `Append(string text)` changes:
  1. Feed `text` to `_parser.Parse(text)` — the parser owns all partial-sequence state internally, so no caller-side stitching is needed
  2. Iterate the returned actions and apply each to `_screen` (write, execute, CSI dispatch, SGR, etc.)
  3. Project `_screen` into a `TerminalSnapshot` for view binding
  4. Raise property changes
- `Clear()` clears the screen instead of the output buffer
- `EnsureStartedAsync()` / `RestartAsync()` — unchanged (lifetime logic)

**View changes (`TerminalPanel.cs`):**

- Replace `_outputTextBox` TextBox with `_renderControl = new TerminalRenderControl()`
- Bind `ScreenSnapshot`, `CursorRow`, `CursorCol` from ViewModel to render control
- Move key event handlers from TextBox to the render control
- Remove `ScrollToEnd()` and `_outputTextBox.CaretIndex` references
- `CopySelectionAsync` copies all visible text via `string.Join("\n", ScreenSnapshot.Lines)` — every visible row concatenated with newlines

**Integration tests:**

- Feed a known PTY output chunk (e.g. `"hello\x1B[31m red\x1B[0m"`) through parser → screen → verify `GetCell(0, 0)` = `('h', default)`, `GetCell(0, 6)` = `('r', Red)`, etc.
- Feed `"line1\r\nline2"` → verify two rows in screen
- Feed `"\x1B[2J"` → verify all cells cleared
- Feed resize (80×24), write to column 80, verify wrap
- Cursor visibility toggle (deferred — `\x1B[?25l`/`\x1B[?25h` not in M1 support set, so cursor stays visible)

**ViewModel test updates:**

- `TerminalOutputBuffer` is removed, so `TerminalOutputBufferTests` are deleted (or marked as superseded)
- New tests: `TerminalScreenTests` (M2), `AnsiParserTests` (M1), integration flow through ViewModel
- Existing `TerminalViewModelTests` updates:
  - `BufferTrimsWhenFull` — **delete**: the screen-buffer model replaces the ring buffer and has no size-bounded trimming
  - Constructor seam — remove `maxBufferChars` from `CreateViewModel(...)` and the internal ViewModel constructor; keep only the `uiPost` seam needed for deterministic event dispatch in tests
  - `OutputReceived_AppendsToBuffer` — rewrite to assert `ScreenSnapshot` line/cell content
  - `OutputReceived_DecodesUtf8AcrossChunkBoundaries` — **keep and rewrite**: the ViewModel still owns the `Decoder` state, but assert against screen content instead of `OutputText`
  - `ClearCommand_EmptiesBuffer` — rewrite to assert screen cells are cleared and `ScreenSnapshot` is updated
  - `ProcessExited_UpdatesIsRunning` — rewrite the "[Process exited]" assertion against visible screen content
  - `Dispose_UnsubscribesAndDisposesService` — rewrite late-output assertion against unchanged `ScreenSnapshot`
  - `Restart_DoesNotDuplicateEventHandling` — rewrite duplicate-output assertion against visible screen content
  - Restart/state tests — keep lifecycle assertions (`State`, `StatusLabel`, `IsRunning`) and remove `OutputText` usage

### M5: Documentation and Exit Audit

- [ ] Remove `TerminalOutputBuffer.cs` and `TerminalOutputBufferTests.cs` (superseded by screen buffer + parser)
- [ ] Update `TOFIX.md` — remove M4 clear-screen deferral since `\x1B[2J` is now supported; add new deferrals from M1 gaps
- [ ] Update `docs/spec/` or `CONVENTIONS.md` if new naming conventions were established
- [ ] Update `docs/roadmap/PHASES.md` — rename/number the existing Phase 3 terminal bullets for Phase 3.6 (Terminal Renderer Foundation), 3.7 (Interactive Shell Quality), 3.8 (TUI Compatibility), and 3.9 (Terminal UX Polish); do not create duplicate bullets
- [ ] `dotnet build` — 0 warnings, 0 errors
- [ ] `dotnet test` — all tests pass
- [ ] Manual smoke test on Linux (see below)

#### Manual smoke test checklist (run on Linux)

- [ ] Toggle terminal (Ctrl+`); it starts and shows a prompt
- [ ] Type `ls` — output renders as styled text, no escape junk visible
- [ ] Type `echo -e '\033[31mred\033[0m'` — "red" is red
- [ ] Type `echo -e '\033[1;32mbold green\033[0m'` — bold green
- [ ] Type `clear` — screen clears (no raw escape text)
- [ ] Type a long command that wraps to a second line
- [ ] Resize terminal — grid resizes, overlapping cells preserved, no line reflow (rows/cols recalculated)
- [ ] Backspace, arrows, Home/End work at shell prompt
- [ ] Ctrl+C interrupts a running command
- [ ] Ctrl+Shift+C — copies all visible text (no mouse selection yet); Ctrl+Shift+V — pastes from clipboard
- [ ] Focus terminal by clicking or Tab-navigating to the render control; verify cursor is visible and key input works
- [ ] Type `exit` → "[Process exited]" shows, Restart re-spawns

## Design Notes

- **Parser replaces TerminalOutputBuffer:** The old `TerminalOutputBuffer`'s hand-coded `\r`/`\b`/`\n` logic is superseded by the ANSI parser. The parser handles these as C0 Execute actions.
- **Parser silently drops unknown sequences:** No "unknown action" type flows to the screen buffer. Unsupported escape sequences are consumed at parser level and produce zero actions. Tests verify this. Supported SGR sequences still dispatch to the screen; unsupported SGR parameters are consumed and ignored by `SetSgr`.
- **Screen buffer is the single source of truth:** All visible terminal state lives in `TerminalScreen`. The render control reads from it; parsing writes to it.
- **View layer owns rendering only:** `TerminalRenderControl` does not parse or interpret escape sequences. It only renders the cell grid it receives.
- **Three pure types, one control:** `AnsiParser` and `TerminalScreen` are pure (no Avalonia, no `ITerminalService`). `TerminalRenderControl` is the only Avalonia-dependent addition. `TerminalSnapshot` and `TerminalCell` are public but pure value types in the ViewModel namespace. `Cell` and `CellAttribute` may stay colocated in `TerminalScreen.cs`, and `TerminalCell` may stay colocated in `TerminalSnapshot.cs`, because each is a small helper type only meaningful inside that owning type's API.
- **No scrollback in this phase:** The screen buffer is exactly the visible viewport. Scrollback (history ring) is deferred to a future terminal phase.
- **256-color SGR is deferred** (`38;5;n` / `48;5;n`). The parser recognises the parameter sequence but the screen buffer only supports the base 16 ANSI colors. The sequence is consumed (not passed through as junk) and ignored; no clamping is attempted in this phase.
- **Copy (Ctrl+Shift+C) copies all visible text row-by-row.** There is no interactive selection with the mouse — that is deferred to a future phase. This is a **deliberate UX regression** from Phase 3.5's TextBox-based selection; the smoke test must confirm it's acceptable.
- **Cursor is always rendered** as a block cursor at the current position. Blinking is deferred.

## Deferred Items (logged in TOFIX)

| Issue | Reason | Target Phase |
|-------|--------|--------------|
| Scrollback history ring | Screen buffer is viewport-only | Future terminal phase |
| 256-color SGR (38;5;n / 48;5;n) | Parser recognises and ignores; need extended palette | Future terminal renderer phase |
| Mouse selection in terminal | Complex highlight rendering, mouse capture | Future terminal phase |
| Blinking cursor | Cosmetic; no visual impact on functionality | Future terminal phase |
| Cursor hide/show (DECSET/DECRST) | Not in M1 supported sequence set | Phase 3.8 (TUI compatibility) |
| Alternate screen (`\x1B[?1049h`) | Needed for vim/htop — explicitly out of scope | Phase 3.8 (TUI compatibility) |
| OSC sequences (window title, etc.) | No user-facing need yet | Future terminal phase |
| DCS sequences (tmux, kitty) | Niche; no testable use case | Future terminal phase |
| Bold as separate weight vs. bright color | Current behaviour matches xterm: bold = bright fg. True bold glyph weight is cosmetic | Future terminal renderer phase |
| Underline / italic / strikethrough | SGR 4/3/9 are recognised but not rendered | Future terminal renderer phase |

## Files Changed / Created

| File | Action |
|------|--------|
| `src/ViewModels/AnsiParser.cs` | **Create** — ANSI/CSI state machine |
| `src/ViewModels/TerminalScreen.cs` | **Create** — screen-buffer cell grid |
| `src/ViewModels/TerminalSnapshot.cs` | **Create** — public snapshot type for view binding (includes `TerminalCell` struct) |
| `src/Views/TerminalRenderControl.cs` | **Create** — custom render control |
| `src/ViewModels/TerminalViewModel.cs` | **Modify** — replace `TerminalOutputBuffer` with parser + screen |
| `src/Views/TerminalPanel.cs` | **Modify** — replace TextBox with render control |
| `src/ViewModels/TerminalOutputBuffer.cs` | **Delete** — superseded by parser + screen |
| `tests/Zaide.Tests/ViewModels/AnsiParserTests.cs` | **Create** |
| `tests/Zaide.Tests/ViewModels/TerminalScreenTests.cs` | **Create** |
| `tests/Zaide.Tests/ViewModels/TerminalViewModelTests.cs` | **Modify** — update for screen model |
| `tests/Zaide.Tests/ViewModels/TerminalOutputBufferTests.cs` | **Delete** — superseded |
| `tests/Zaide.Tests/Views/TerminalGeometryTests.cs` | Unchanged (geometry helper reused) |
| `tests/Zaide.Tests/Views/TerminalKeyMapperTests.cs` | Unchanged |
| `docs/phases/phase-3.6/TOFIX.md` | **Create** |

## Rollback Plan

1. Revert changes to `TerminalViewModel.cs` and `TerminalPanel.cs`
2. Delete `AnsiParser.cs`, `TerminalScreen.cs`, `TerminalSnapshot.cs`, `TerminalRenderControl.cs`, and their tests
3. Restore `TerminalOutputBuffer.cs` and `TerminalOutputBufferTests.cs`
4. Revert this phase's docs

#### Manual smoke test: scrollback and selection regression checks

The Phase 3.6 renderer intentionally loses two Phase 3.5 capabilities. Before accepting the phase:

- [ ] Run `for i in $(seq 1 50); do echo "line $i"; done` — verify only the last ~24 lines are visible (no scrollbar)
- [ ] Confirm the loss of scrollback is acceptable for the MVP; if not, flag scrollback as an immediate follow-up
- [ ] Run `cat /etc/passwd`, then press Ctrl+Shift+C — verify all visible text is copied, confirm no mouse selection is available
- [ ] Confirm the copy-all-visible-text behaviour is acceptable; if not, flag mouse selection as an immediate follow-up

## Exit Conditions

- [ ] `dotnet build` succeeds with 0 warnings
- [ ] `dotnet test` succeeds
- [ ] ANSI parser handles the defined CSI subset (tests pass)
- [ ] Screen buffer correctly models the terminal grid (tests pass)
- [ ] Custom render control replaces TextBox in the terminal panel
- [ ] `clear`, colored output, cursor movement work without visible escape junk
- [ ] Clipboard copy copies all visible text (selection regression noted and accepted)
- [ ] Phase-3.5 manual smoke test items still pass (scrollback and selection regressions noted and accepted)
- [ ] Phase-3.5 `TerminalOutputBuffer` is removed (no dead code)
- [ ] TOFIX documents new deferrals
