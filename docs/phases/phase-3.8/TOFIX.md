# Phase 3.8: TUI Compatibility — TOFIX

## Audit Date: 2026-07-07

## Status: No remaining Phase 3.8 findings

The exit audit against the live repo (commit `08da4cd8c3da685bc30c789e91b31e26a0795193`) confirms that all M1/M2/M3 implementation items are present and verified by passing tests.

### What was verified

| Milestone | Status | Evidence |
|-----------|--------|----------|
| M1: Parser expansion | ✅ Complete | `AnsiParser.cs` emits `AlternateScreenAction`, `SaveCursorAction`, `RestoreCursorAction` for `?1047`/`?1048`/`?1049` and `ESC 7`/`ESC 8`. Split-sequence tests pass. |
| M2: Screen-state model | ✅ Complete | `TerminalScreen.cs` has dual-buffer (main + alternate), `SavedCursorState`, `EnterAlternateScreen()`/`ExitAlternateScreen()` with optional save/restore cursor, `ResetForRestart()`, resize-clamp for saved cursor, alt-screen scrollback isolation. |
| M3: ViewModel integration | ✅ Complete | `TerminalViewModel.cs` dispatches alt-screen/save/restore actions, suppresses log entries during alt-screen (`AppendLogEntries` guard), handles shell-exit-while-alt-screen via `OnProcessExited`, exposes `IsAlternateScreenActive`. |
| M3: View layer | ✅ Complete | `TerminalRenderControl.cs` has `IsAlternateScreenActiveProperty`, suppresses selection/scrollback/copy during alt-screen via `IsMainBufferSelectionEnabled()`. `TerminalPanel.cs` binds `IsAlternateScreenActive`. |

### Test coverage

- **AnsiParserTests.cs**: 555 lines, covers all M1 parser actions including split-sequence variants.
- **TerminalScreenTests.cs**: 946 lines, covers all M2 screen-state behaviors including alt-screen enter/exit, save/restore cursor, resize interaction, `EraseDisplay(3)` isolation, scrollback isolation.
- **TerminalViewModelTests.cs**: 1082 lines, covers all M3 integration including transcript-style `less`/`vim` flows, save/restore cursor redraw, shell-exit-while-alt-screen, log suppression during alt-screen, ordinary shell regression.
- **Total**: 510 tests pass, 0 fail.

### DECSTBM (scroll-region) decision

DECSTBM was **not implemented** and was **not required**. The guaranteed transcript set (`less` open-exit, `vim` open-exit/edit-quit) passes without scroll-region support. Per the Phase 3.8 scoping rule, DECSTBM remains out of scope.

### Manual smoke (Linux)

Manual smoke items were **not run** in this environment (no interactive PTY session available). See `IMPLEMENTATION_PLAN.md` for the unchecked manual smoke checklist. These are deferred to Phase 3.9 or a follow-up verification pass.

### Build/test results (this run)

- `dotnet build Zaide.slnx`: **0 warnings, 0 errors** (2026-07-07)
- `dotnet test Zaide.slnx --no-build`: **510 passed, 0 failed** (2026-07-07)

### No residual gaps

All Phase 3.8 scope items are implemented and tested. No TOFIX items remain.