# Phase 3.6: Terminal Renderer Foundation — TOFIX

Track code quality issues found during Phase 3.6 review.

---

## Open Issues

- **Snapshot projection allocation pressure:** Each PTY output chunk that mutates the screen triggers a full `TerminalSnapshot` allocation (≈1920 `TerminalCell` structs for 80×24) plus four property-change notifications (`ScreenSnapshot`, `CursorRow`, `CursorCol`, `CursorVisible`). Fast-scrolling output (`ls -la` of a large directory, `find /`) may cause allocation spikes. Record as a known cost; revisit if profiling shows pressure. Possible mitigations: consolidate cursor into the snapshot, or coalesce projection/render via throttling.
- **Headless render-test infrastructure:** The `RenderTargetBitmap` snapshot test requires an Avalonia headless platform provider in the test project. If `Zaide.Tests` does not already initialize one, add the provider setup before M3 tests land.
- **Phase 3.5 manual smoke test outstanding:** The predecessor phase's exit checklist has an unchecked manual-smoke checkbox. Before closing 3.6, run the 3.5 checklist so the "Phase-3.5 manual smoke test items still pass" exit condition rests on an actually-passed baseline.

## Resolved Issues

- **Alternate screen target phase corrected:** Changed from Phase 3.7 to Phase 3.8 (matches `docs/phases/phase-3.8/BRIEF.md`).

## Deferred to Future Phases

| Issue | Reason | Target Phase |
|-------|--------|--------------|
| Scrollback history ring | Screen buffer is viewport-only | Future terminal phase |
| 256-color SGR (38;5;n / 48;5;n) | Parser recognises but clamps; need extended palette | Future terminal renderer phase |
| Mouse selection in terminal | Complex highlight rendering, mouse capture | Future terminal phase |
| Blinking cursor | Cosmetic; no visual impact on functionality | Future terminal phase |
| Cursor hide/show (DECSET/DECRST) | Not in M1 supported sequence set | Phase 3.8 (TUI compatibility) |
| Alternate screen (`\x1B[?1049h`) | Needed for vim/htop — explicitly out of scope | Phase 3.8 (TUI compatibility) |
| OSC sequences (window title, etc.) | No user-facing need yet | Future terminal phase |
| DCS sequences (tmux, kitty) | Niche; no testable use case | Future terminal phase |
| Bold as separate weight vs. bright color | Current behaviour matches xterm: bold = bright fg. True bold glyph weight is cosmetic | Future terminal renderer phase |
| Underline / italic / strikethrough | SGR 4/3/9 are recognised but not rendered | Future terminal renderer phase |
