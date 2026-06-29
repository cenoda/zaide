# Phase 3.5: Terminal UI Normalization — Implementation Plan

## Pre-Implementation Verification

- [x] Read `docs/phases/phase-3/IMPLEMENTATION_PLAN.md`
- [x] Verify current terminal code in `src/Views/TerminalPanel.cs`
- [x] Verify current terminal state in `src/ViewModels/TerminalViewModel.cs`
- [x] Verify PTY resize support in `src/Services/ITerminalService.cs`
- [x] Confirm entry gate: `dotnet build`
- [x] Confirm entry gate: `dotnet test`

## Scope

**Goal:** Make the Linux PTY terminal feel like a normal IDE terminal before
starting agent-facing phases.

**Boundaries:** This phase improves the shared terminal UI and Linux MVP
behavior. It does not add Windows/macOS backends, terminal tabs, split panes,
or a full ANSI/cell renderer.

## Milestones (Incremental)

| Milestone | Description | Test | Status |
|-----------|-------------|------|--------|
| M0 | Entry gate: current build and tests pass | `dotnet build`, `dotnet test` | ✅ Done |
| M1 | Wire terminal resize from UI bounds/font metrics to PTY rows/columns | Unit tests for geometry + resize forwarding; manual shell resize check | ✅ Done |
| M2 | Expand key forwarding for common shell/readline controls | Unit tests for key mapping helper | ✅ Done |
| M3 | Add visible terminal controls and state: clear, clipboard actions if feasible, restart, running/exited/error | ViewModel tests for command/state behavior | ⏳ Pending |
| M4 | Improve raw output behavior within a defined MVP subset | Unit tests for supported control characters; manual check with `echo`, `clear`, Ctrl+C | ⏳ Pending |
| M5 | Documentation and exit audit | Update roadmap/TOFIX if needed; `dotnet build`, `dotnet test` | ⏳ Pending |

### M1: Resize Wiring

The PTY backend already supports `Resize(columns, rows)`, but no UI code calls
it yet. Implement resize in three small, testable steps:

- Add a pure geometry helper that converts terminal surface pixels and font
  metrics into terminal columns/rows.
- Add a `TerminalViewModel.Resize(columns, rows)` pass-through.
- Subscribe to terminal panel bounds/font changes, throttle them, and call the
  ViewModel resize method.

The geometry helper must use an explicit character-cell measurement source,
such as `FormattedText` glyph advance for the configured monospace font and the
terminal line height. Do not assume `TextBox` exposes terminal cell metrics.

### M2: Key Forwarding

Extract terminal key mapping out of `TerminalPanel` before expanding it. The
helper should cover at least:

- Enter, Backspace, Tab, arrows
- Ctrl+C, Ctrl+D, Ctrl+L
- Home, End, Delete

Escape, Page Up/Down, and function keys may be added if the mapping stays small
and testable. Otherwise document them as deferred.

**Status:** ✅ Implemented in `TerminalKeyMapper.Map(Key, KeyModifiers)`.

**Keys mapped:** Enter (`\r`), Backspace (`0x7F`), Tab (`0x09`),
Left/Right/Up/Down (`\x1B[D/C/A/B`), Home (`\x1B[H`), End (`\x1B[F`),
Delete (`\x1B[3~`), Ctrl+C (`0x03`), Ctrl+D (`0x04`), Ctrl+L (`0x0C`).

**Intentional guard:** All non-control base keys (Enter, Backspace, Tab,
arrows, Home, End, Delete) only map when `modifiers == KeyModifiers.None`.
This prevents `Shift+Enter`, `Alt+arrows`, etc. from silently collapsing into
plain terminal input, keeping those combinations available for future View-level
actions (clipboard, pane splits, etc.).

**Deferred keys (null-mapped, documented gap):**
- Escape — may be needed for `vi` mode or `meta` key support
- PageUp / PageDown — shell scrollback if terminal history is added
- F1–F12 — function key bindings (readline, `mc`, `htop`, etc.)
- These return `null` from the mapper and will be added in a future phase
  when shell usage provides a clear test case.

### M3: Controls And State

Add a small visible terminal control strip and keep the first pass practical:

- Clear output through the existing `ClearCommand`.
- Show running, exited, and startup/error states inside the terminal area.
- Make `LinuxTerminalService` restart-safe: reset exit/reader state in
  `StartAsync`, re-validate `_master` / `_reader` state before spawning, and add
  a service-level start -> exit -> restart -> exit test that proves the second
  exit still reaps the child and raises `ProcessExited`.
- Add a ViewModel-level restart path, such as `RestartCommand`, that can start
  the singleton service again after a clean exit. `EnsureStartedAsync()` leaves
  `_startRequested` true today, so restart must deliberately reset or bypass
  that gate.
- Test restart lifecycle at the real risk points: stale reader state, missed
  `ProcessExited`, unreaped child process, and accidental duplicate event
  handling.
- Prefer explicit Ctrl+Shift+C / Ctrl+Shift+V clipboard actions over relying on
  TextBox mouse selection, which is unreliable in the Phase 3 surface.

Clipboard access must stay in the View layer. ViewModels may expose terminal
text or accept raw input bytes, but must not reference Avalonia clipboard APIs.

### M4: Raw Output MVP

Keep raw-output improvement intentionally small and verifiable:

- Support carriage return (`\r`) overwrite behavior if feasible.
- Support backspace (`\b`) deletion behavior if feasible.
- Treat clear-screen ANSI from commands like `clear` as either a toolbar-driven
  Clear substitute or a documented limitation.
- Defer full ANSI/VT100 parsing, color rendering, cursor addressing, and
  alternate-screen behavior to a later renderer phase.

## Design Notes

- Keep the existing PTY backend. Phase 3 proved the service boundary and Linux
  PTY lifecycle.
- Prefer small helpers for key mapping and terminal geometry so behavior can be
  tested without instantiating Avalonia controls.
- Keep terminal rendering intentionally modest. If ANSI parsing grows beyond a
  small normalization step, defer it to a later renderer phase.
- Surface terminal failure states where the user can see them, not only through
  `MainWindowViewModel.StatusText`.
- If paste support is added, avoid blocking the UI thread on a large synchronous
  PTY write. Chunk large paste input or document it as a known MVP limit.

## Limitations (by design)

- Linux remains the only implemented terminal backend.
- ANSI/VT100 rendering remains incomplete unless a small safe subset is added.
- The terminal still uses one session.
- No terminal tabs, splits, profiles, or persistent shell settings.
- No project-wide command framework in this phase; that belongs to Refactor 2
  if Phase 4 increases command pressure.

## Exit Conditions

- [x] `dotnet build` succeeds with 0 warnings
- [x] `dotnet test` succeeds
- [x] Terminal resize updates PTY rows/columns
- [x] Common shell keys work: Enter, Backspace, Tab, arrows, Ctrl+C, Ctrl+D,
      Ctrl+L, Home/End, Delete
- [ ] Terminal clear/restart/error state is visible and tested
- [ ] `LinuxTerminalService` supports start -> exit -> restart -> exit without
      missed `ProcessExited` events or zombie child processes
- [ ] ViewModel restart path works after clean process exit
- [ ] Restart does not duplicate terminal event handling
- [ ] Raw output MVP subset is implemented or explicitly documented as deferred
- [ ] Manual terminal smoke test passes on Linux

## Rollback Plan

- Revert changes in `src/Views/TerminalPanel.cs`, `src/ViewModels/TerminalViewModel.cs`,
  terminal tests, and this phase's docs.
