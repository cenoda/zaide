# Phase 3.6: Terminal Renderer Foundation — TOFIX

Track code quality issues found during Phase 3.6 review.

---

## Status

Phase 3.6 is **ready for implementation** but has not been implemented yet.
M1–M5 work items in `IMPLEMENTATION_PLAN.md` are all unchecked. The current
terminal still uses the Phase 3.5 `TerminalOutputBuffer` (TextBox-backed).

Planning audit passed after review feedback was incorporated. Entry gates were
run serially:

- `dotnet build Zaide.slnx` — 0 warnings, 0 errors
- `dotnet test Zaide.slnx --no-build` — 208 passed, 0 failed

This file currently has no real entries — it exists as the destination for
issues discovered once implementation begins.

## Open Issues

_(none yet — populated as M1–M5 are implemented and reviewed)_

## Resolved Issues

- **Render test strategy refined (planning only)** — During plan review, the
  required Avalonia headless/`RenderTargetBitmap` tests were removed from this
  phase because they would require a new test dependency that has not been
  approved or cataloged. The M3 automated coverage is now limited to pure
  snapshot/contract/geometry-guard tests, with real rendering verified by the
  manual smoke checklist. `IMPLEMENTATION_PLAN.md` is authoritative.
- **Resize metric guard added (planning only)** — The plan now requires
  `ForwardResize()` to return early while `CellWidth` or `LineHeight` is zero,
  because bounds notifications can arrive before the custom render control has
  measured its first glyph and `TerminalGeometry.Compute()` rejects non-positive
  metrics.
- **TerminalViewModel test migration made explicit (planning only)** — The plan
  now enumerates the constructor-seam and `OutputText` assertion rewrites needed
  when `TerminalOutputBuffer` is replaced by `ScreenSnapshot`.

## Deferred to Future Phases

Items listed here are planned regressions or out-of-scope behaviours that
must be re-checked once the renderer is built. They do not yet correspond to
real code.

| Issue | Reason | Target Phase |
|-------|--------|--------------|
| Scrollback history ring | Planned screen buffer is viewport-only; deliberate regression from Phase 3.5 | Future terminal phase |
| 256-color SGR (38;5;n / 48;5;n) | Parser will recognise and ignore; extended palette deferred | Future terminal renderer phase |
| Mouse selection in terminal | Complex highlight rendering, mouse capture; deliberate regression from Phase 3.5 | Future terminal phase |
| Blinking cursor | Cosmetic; no visual impact on functionality | Future terminal phase |
| Cursor hide/show (DECSET/DECRST) | Not in M1 supported sequence set | Phase 3.8 (TUI compatibility) |
| Alternate screen (`\x1B[?1049h`) | Needed for vim/htop — explicitly out of scope | Phase 3.8 (TUI compatibility) |
| OSC sequences (window title, etc.) | No user-facing need yet | Future terminal phase |
| DCS sequences (tmux, kitty) | Niche; no testable use case | Future terminal phase |
| Bold as separate weight vs. bright color | Plan matches xterm: bold = bright fg. True bold glyph weight is cosmetic | Future terminal renderer phase |
| Underline / italic / strikethrough | SGR 4/3/9 to be recognised but not rendered | Future terminal renderer phase |
