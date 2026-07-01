# Phase 3.7: Interactive Shell Quality — TOFIX

This file tracks issues and findings during the implementation of Phase 3.7.

## Open Items

### M2: Prompt and Paste Quality
- [ ] Add bracketed paste mode support in `AnsiParser` and `TerminalViewModel`.
- [ ] Implement `PasteAsync` method in `TerminalViewModel` to wrap paste text with bracketed paste markers.
- [ ] Add transcript-style tests for prompt redraw patterns.

### M3: Resize and Session Stability
- [ ] Audit and tighten resize forwarding in `TerminalViewModel` and `TerminalPanel`.
- [ ] Ensure viewport behavior is consistent during resize and restart.
- [ ] Add tests for resize and scrollback behavior.

### M4: Docs and Exit Audit
- [ ] Update `docs/roadmap/PHASES.md` upon completion of Phase 3.7.
- [ ] Review and update `docs/architecture/OVERVIEW.md` if necessary.

## Completed Items

### M1: Extended Shell Color Fidelity
- [x] Implement 256-color and truecolor support in `AnsiParser`, `TerminalScreen`, and `TerminalRenderControl`.
- [x] Update `TerminalSnapshot` to expose extended color metadata.
- [x] Add tests for 256-color SGR handling.
- [x] Add tests for truecolor SGR handling.
- [x] Manual smoke tests for 256-color and truecolor output.
- [x] Fixed extended background colors not being painted in the renderer.
- [x] Fixed colon-form truecolor sequence parsing in `AnsiParser`.
- [x] Added test to validate renderer's handling of extended background colors.
- [x] Fixed syntax error in `TerminalRenderControlTests.cs`.
- [x] Improved renderer validation test to better validate rendering behavior.
- [x] Added tests to ensure colon-form SGR works and non-SGR CSI behavior remains unchanged.

## Notes

- Ensure all changes align with the goals and boundaries defined in the `IMPLEMENTATION_PLAN.md`.
- Focus on ordinary shell interaction quality without expanding into full TUI compatibility.
- Milestone 1 (M1) is complete and all issues have been addressed.
- All tests pass and the build succeeds.
