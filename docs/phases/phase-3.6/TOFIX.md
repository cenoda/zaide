# Phase 3.6: Terminal Renderer Foundation — TOFIX

Track code quality issues found during Phase 3.6 review.

---

## Open Issues

- **`TerminalCell` is a `readonly struct`, not a record or enum** — it lives in `TerminalSnapshot.cs` alongside the `TerminalSnapshot` class. This is acceptable under the repo's "colocate small related types" convention, but was worth calling out deliberately since the AGENTS.md convention is one class per file. The struct is only used as the element type of `TerminalSnapshot.Cells`, so splitting it into its own file adds ceremony without clarity.

## Resolved Issues

- **Render test strategy refined** — The initial plan proposed an exact pixel-color `RenderTargetBitmap` snapshot test. Review noted that font rendering, antialiasing, and DPI make pixel matching brittle. The plan was revised to a **coarse nonblank render smoke test** (assert dimensions only, not pixel colors) plus a pure-logic `TerminalSnapshot` structure test. The doc in `IMPLEMENTATION_PLAN.md` is authoritative; this entry confirms alignment.

## Deferred to Future Phases

| Issue | Reason | Target Phase |
|-------|--------|--------------|
| Scrollback history ring | Screen buffer is viewport-only; deliberate regression from Phase 3.5 | Future terminal phase |
| 256-color SGR (38;5;n / 48;5;n) | Parser recognises but clamps; need extended palette | Future terminal renderer phase |
| Mouse selection in terminal | Complex highlight rendering, mouse capture; deliberate regression from Phase 3.5 | Future terminal phase |
| Blinking cursor | Cosmetic; no visual impact on functionality | Future terminal phase |
| Cursor hide/show (DECSET/DECRST) | Not in M1 supported sequence set | Phase 3.8 (TUI compatibility) |
| Alternate screen (`\x1B[?1049h`) | Needed for vim/htop — explicitly out of scope | Phase 3.8 (TUI compatibility) |
| OSC sequences (window title, etc.) | No user-facing need yet | Future terminal phase |
| DCS sequences (tmux, kitty) | Niche; no testable use case | Future terminal phase |
| Bold as separate weight vs. bright color | Current behaviour matches xterm: bold = bright fg. True bold glyph weight is cosmetic | Future terminal renderer phase |
| Underline / italic / strikethrough | SGR 4/3/9 are recognised but not rendered | Future terminal renderer phase |