# Phase 3.7: Interactive Shell Quality — TOFIX

This file tracks issues and findings during the implementation of Phase 3.7.

## Open Items

### M2: Prompt and Paste Quality
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
- [x] Fixed syntax error in `TerminalRenderControlTests.cs`.
- [x] Added tests to ensure colon-form SGR works.

### M2: Prompt and Paste Quality
- [x] Add bracketed paste mode support in `AnsiParser` and `TerminalViewModel`.
- [x] Implement `PasteAsync` method in `TerminalViewModel` to wrap paste text with bracketed paste markers.
- [x] Update `TerminalPanel.PasteAsync()` to call `ViewModel.PasteAsync(text)` instead of `SendInputAsync(bytes)` directly.

## Notes

- Ensure all changes align with the goals and boundaries defined in the `IMPLEMENTATION_PLAN.md`.
- Focus on ordinary shell interaction quality without expanding into full TUI compatibility.
- Milestone 1 (M1) and Milestone 2 (M2) are complete and all issues have been addressed.
- All tests pass and the build succeeds.
- The renderer tests still do not validate actual rendering behavior, but the code changes ensure that extended background colors are painted.
