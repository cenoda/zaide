# Phase 3.5: Terminal UI Normalization — Implementation Plan

## Pre-Implementation Verification

- [ ] Read `docs/phases/phase-3/IMPLEMENTATION_PLAN.md`
- [ ] Verify current terminal code in `src/Views/TerminalPanel.cs`
- [ ] Verify current terminal state in `src/ViewModels/TerminalViewModel.cs`
- [ ] Verify PTY resize support in `src/Services/ITerminalService.cs`
- [ ] Confirm entry gate: `dotnet build`
- [ ] Confirm entry gate: `dotnet test`

## Scope

**Goal:** Make the Linux PTY terminal feel like a normal IDE terminal before
starting agent-facing phases.

**Boundaries:** This phase improves the shared terminal UI and Linux MVP
behavior. It does not add Windows/macOS backends, terminal tabs, split panes,
or a full ANSI/cell renderer.

## Milestones (Incremental)

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Entry gate: current build and tests pass | `dotnet build`, `dotnet test` |
| M1 | Wire terminal resize from UI bounds/font metrics to PTY rows/columns | Unit tests for geometry + resize forwarding; manual shell resize check |
| M2 | Expand key forwarding for common shell/readline controls | Unit tests for key mapping helper |
| M3 | Add visible terminal controls and state: clear, clipboard actions if feasible, restart, running/exited/error | ViewModel tests for command/state behavior |
| M4 | Improve raw output experience within MVP bounds | Manual check with `echo`, `ls --color`, `clear`, Ctrl+C |
| M5 | Documentation and exit audit | Update roadmap/TOFIX if needed; `dotnet build`, `dotnet test` |

### M1: Resize Wiring

The PTY backend already supports `Resize(columns, rows)`, but no UI code calls
it yet. Implement resize in three small, testable steps:

- Add a pure geometry helper that converts terminal surface pixels and font
  metrics into terminal columns/rows.
- Add a `TerminalViewModel.Resize(columns, rows)` pass-through.
- Subscribe to terminal panel bounds/font changes, throttle them, and call the
  ViewModel resize method.

### M2: Key Forwarding

Extract terminal key mapping out of `TerminalPanel` before expanding it. The
helper should cover at least:

- Enter, Backspace, Tab, arrows
- Ctrl+C, Ctrl+D, Ctrl+L
- Home, End, Delete

Escape, Page Up/Down, and function keys may be added if the mapping stays small
and testable. Otherwise document them as deferred.

### M3: Controls And State

Add a small visible terminal control strip and keep the first pass practical:

- Clear output through the existing `ClearCommand`.
- Show running, exited, and startup/error states inside the terminal area.
- Add restart behavior deliberately, with tests proving event subscriptions do
  not duplicate across restart.
- Prefer explicit Ctrl+Shift+C / Ctrl+Shift+V clipboard actions over relying on
  TextBox mouse selection, which is unreliable in the Phase 3 surface.

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

- [ ] `dotnet build` succeeds with 0 warnings
- [ ] `dotnet test` succeeds
- [ ] Terminal resize updates PTY rows/columns
- [ ] Common shell keys work: Enter, Backspace, Tab, arrows, Ctrl+C, Ctrl+D,
      Ctrl+L, Home/End, Delete
- [ ] Terminal clear/restart/error state is visible and tested
- [ ] Restart does not duplicate terminal service event subscriptions
- [ ] Manual terminal smoke test passes on Linux

## Rollback Plan

- Revert changes in `src/Views/TerminalPanel.cs`, `src/ViewModels/TerminalViewModel.cs`,
  terminal tests, and this phase's docs.
