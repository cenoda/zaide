# Phase 3.7: Interactive Shell Quality — Closeout

This file records the final verified state of Phase 3.7.

## Status

- Phase 3.7 implementation is complete at the code and automated-test level.
- `docs/roadmap/PHASES.md` marks Phase 3.7 complete.
- `docs/architecture/OVERVIEW.md` was reviewed during closeout and did not require behavioral-contract changes for this phase.

## Verified

- [x] Extended shell color fidelity is implemented for 256-color and truecolor output.
- [x] Colon-form truecolor SGR parsing is supported.
- [x] Bracketed paste mode is parsed and forwarded correctly.
- [x] Paste uses bracketed paste markers when the shell enables bracketed paste mode.
- [x] Resize forwarding remains stable before start, during active sessions, and across restart.
- [x] Running-session restart raises `Restarted` after process exit and re-entry into the started state.
- [x] View-layer viewport recovery on restart is wired through `TerminalPanel` and `TerminalRenderControl`.
- [x] `dotnet build Zaide.slnx` succeeds.
- [x] `dotnet test Zaide.slnx --no-build` succeeds with 350/350 tests passing.

## Residual Notes

- Renderer tests still do not validate actual pixel output for extended background colors; correctness there is covered by code-path review plus broader terminal tests, not by a headless render assertion.
- Transcript-style prompt redraw tests were planned for M2, but prompt redraw quality is currently covered by the phase boundary through parser/screen behavior and should still be checked during manual terminal smoke.
- Linux manual smoke remains recommended for release confidence:
  - prompt redraw does not leave stale fragments
  - bracketed paste behaves safely in a real shell
  - resize/restart feel stable during active use
  - selection/copy/paste/scrollback behavior remains intact in the live UI

## Closeout Summary

- M1: Complete
- M2: Complete
- M3: Complete
- M4: Docs synced to current repo state
