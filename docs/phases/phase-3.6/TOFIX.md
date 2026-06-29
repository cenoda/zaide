# Phase 3.6: Terminal Renderer Foundation — TOFIX

Track code quality issues found during Phase 3.6 review.

---

## Status

Phase 3.6 has **not been implemented yet**. M1–M5 work items in
`IMPLEMENTATION_PLAN.md` are all unchecked. The current terminal still uses
the Phase 3.5 `TerminalOutputBuffer` (TextBox-backed).

This file currently has no real entries — it exists as the destination for
issues discovered once implementation begins.

## Open Issues

_(none yet — populated as M1–M5 are implemented and reviewed)_

## Resolved Issues

- **Render test strategy refined (planning only)** — During plan review, an
  initial proposal of an exact pixel-color `RenderTargetBitmap` snapshot test
  was rejected as too brittle (font rendering, antialiasing, DPI variance).
  The plan was revised to a **coarse nonblank render smoke test** (assert
  dimensions only, not pixel colors) plus a pure-logic screen-buffer
  structure test. `IMPLEMENTATION_PLAN.md` is authoritative.

## Deferred to Future Phases

Items listed here are planned regressions or out-of-scope behaviours that
must be re-checked once the renderer is built. They do not yet correspond to
real code.

| Issue | Reason | Target Phase |
|-------|--------|--------------|
| Scrollback history ring | Planned screen buffer is viewport-only; deliberate regression from Phase 3.5 | Future terminal phase |
| 256-color SGR (38;5;n / 48;5;n) | Parser will recognise but clamp; extended palette deferred | Future terminal renderer phase |
| Mouse selection in terminal | Complex highlight rendering, mouse capture; deliberate regression from Phase 3.5 | Future terminal phase |
| Blinking cursor | Cosmetic; no visual impact on functionality | Future terminal phase |
| Cursor hide/show (DECSET/DECRST) | Not in M1 supported sequence set | Phase 3.8 (TUI compatibility) |
| Alternate screen (`\x1B[?1049h`) | Needed for vim/htop — explicitly out of scope | Phase 3.8 (TUI compatibility) |
| OSC sequences (window title, etc.) | No user-facing need yet | Future terminal phase |
| DCS sequences (tmux, kitty) | Niche; no testable use case | Future terminal phase |
| Bold as separate weight vs. bright color | Plan matches xterm: bold = bright fg. True bold glyph weight is cosmetic | Future terminal renderer phase |
| Underline / italic / strikethrough | SGR 4/3/9 to be recognised but not rendered | Future terminal renderer phase |
