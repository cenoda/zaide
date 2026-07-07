# Phase 3.8: TUI Compatibility — Implementation Plan

## Pre-Implementation Verification

- [x] Read `docs/phases/phase-3.8/BRIEF.md`
- [x] Re-read `docs/phases/phase-3.7/IMPLEMENTATION_PLAN.md` and closeout state
- [x] Re-read `docs/roadmap/PHASES.md`, `docs/architecture/OVERVIEW.md`, and `docs/CONVENTIONS.md`
- [x] Verify current terminal scope boundary against `docs/phases/phase-3.9/BRIEF.md`
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx`
- [ ] Manually confirm the current Phase 3.7 baseline still works on Linux:
  - [ ] Shell starts and shows a prompt
  - [ ] 256-color and truecolor output still render correctly
  - [ ] Bracketed paste still behaves safely
  - [ ] Resize/restart still feel stable
  - [ ] Selection/copy/scrollback still work in the current renderer
- [x] Confirm no new NuGet packages are needed for this phase

## Planning Status

**Draft.** This phase starts from the completed Phase 3.7 shell-quality
baseline and adds the missing emulation behaviors required by full-screen
terminal applications.

Verified live seams on 2026-07-07:

- `src/ViewModels/AnsiParser.cs`
  - currently supports `A/B/C/D/H/J/K/m` plus DECSET/DECRST only for bracketed
    paste mode `?2004`
  - currently drops alternate-screen and other richer private-mode sequences
- `src/ViewModels/TerminalScreen.cs`
  - currently owns a single visible buffer, retained scrollback, cursor state,
    erase behavior, SGR attributes, wrapping, scrolling, and resize
  - currently has no alternate-screen state, no saved cursor state, and no
    scroll-region semantics
- `src/ViewModels/TerminalViewModel.cs`
  - currently owns UTF-8 decode continuity, parser dispatch, screen mutation,
    lifecycle, paste/restart/resize wiring, and snapshot projection
- `src/ViewModels/TerminalSnapshot.cs`
  - currently exposes visible cells plus retained scrollback to the view layer
- `src/Views/TerminalRenderControl.cs`
  - currently owns rendering, viewport-follow, manual scrollback, and selection
  - should stay view-focused in this phase; do not move emulation state here
- `src/Views/TerminalPanel.cs`
  - currently owns keyboard/text input forwarding, toolbar actions, clipboard
    operations, and resize throttling
- `src/Services/LinuxTerminalService.cs`
  - remains Linux PTY only and does not need phase-specific backend changes
- Current focused tests already exist in:
  - `tests/Zaide.Tests/ViewModels/AnsiParserTests.cs`
  - `tests/Zaide.Tests/ViewModels/TerminalScreenTests.cs`
  - `tests/Zaide.Tests/ViewModels/TerminalSnapshotTests.cs`
  - `tests/Zaide.Tests/ViewModels/TerminalViewModelTests.cs`
  - `tests/Zaide.Tests/Views/TerminalRenderControlTests.cs`
  - `tests/Zaide.Tests/Services/LinuxTerminalServiceTests.cs`

## Scope

**Goal:** Make the renderer and screen model behave correctly enough for common
full-screen terminal applications by adding the missing stateful control
behaviors that ordinary shell usage did not require.

**Boundaries:**

- Do not widen into search, tabs, enhanced selection UX, or polish work from
  Phase 3.9
- Do not add Windows or macOS terminal backends
- Do not chase total xterm compatibility in one pass
- Do not start Townhall / Phase 4 work here
- Keep unsupported escape families explicitly documented instead of hiding them
  behind partial heuristics

## Known Gaps This Phase Targets

These are the concrete compatibility gaps implied by the current code and docs:

- The parser does not currently emit actions for alternate-screen private modes
  such as `?1047`, `?1048`, and `?1049`
- The screen model only has one visible buffer, so full-screen apps cannot
  switch in and out of a temporary screen without corrupting the main shell view
- Saved-cursor behavior used by redraw-heavy TUIs is not modeled yet
- Richer screen-control behavior is still limited to the ordinary shell subset
  from Phase 3.7
- The current tests prove shell-quality behavior, but they do not yet codify
  transcript-style TUI transitions for `vim` / `less` / `htop`-style flows

## Milestones (Incremental)

| Milestone | Description | Test | Status |
|-----------|-------------|------|--------|
| M0 | Entry gate: current terminal build/tests/manual baseline verified | `dotnet build`, `dotnet test`, focused Linux smoke | ⬜ |
| M1 | Parser and action-model expansion for TUI control sequences | Parser tests for private modes and save/restore actions | ⬜ |
| M2 | Screen-state model for alternate screen and saved cursor behavior | Pure screen tests for enter/exit alt-screen and cursor restore | ⬜ |
| M3 | ViewModel integration and transcript-level compatibility coverage | ViewModel tests for realistic TUI sequences and no shell-regression behavior | ⬜ |
| M4 | Docs sync and exit audit | `dotnet build`, `dotnet test`, Linux TUI smoke, roadmap/doc sync | ⬜ |

## Detailed Milestone Plans

### M1: Parser and Action-Model Expansion

**Why this belongs in 3.8:**

Phase 3.7 intentionally stopped at ordinary shell quality. The next
compatibility wall is parser-level: the current ANSI contract cannot even
describe the state changes needed by full-screen apps.

**Files likely touched:**

- `src/ViewModels/AnsiParser.cs`
- `tests/Zaide.Tests/ViewModels/AnsiParserTests.cs`

**Planned change shape:**

1. Extend the parser contract so it can represent the narrow TUI-oriented
   control sequences this phase needs, while still dropping unrelated escape
   families.
2. Add support for DEC private-mode actions for:
   - `CSI ? 1047 h/l` alternate screen
   - `CSI ? 1048 h/l` save/restore cursor
   - `CSI ? 1049 h/l` alternate screen + cursor save/restore combined
3. Add support for ESC save/restore cursor actions if needed by target
   transcripts:
   - `ESC 7` save cursor
   - `ESC 8` restore cursor
4. Keep unsupported private modes such as cursor visibility, mouse reporting,
   focus in/out, and bracketed variants beyond the current phase explicitly
   ignored unless a target transcript proves they are required.
5. Add narrow scroll-region support (`CSI top;bottom r` / DECSTBM) if justified
   by target transcripts. Keep it narrow: only the top/bottom margin pair, no
   left/right margins.
6. Preserve current chunk-boundary behavior: split sequences must still complete
   correctly across multiple parser calls.

**Tests (M1):**

- `Parse_DecSet1047_EmitsAlternateScreenEnable`
- `Parse_DecReset1047_EmitsAlternateScreenDisable`
- `Parse_DecSet1048_EmitsSaveCursor`
- `Parse_DecReset1048_EmitsRestoreCursor`
- `Parse_DecSet1049_EmitsCombinedAltScreenAndCursorSave`
- `Parse_DecReset1049_EmitsCombinedAltScreenAndCursorRestore`
- `Parse_Esc7_EmitsSaveCursorAction`
- `Parse_Esc8_EmitsRestoreCursorAction`
- `Parse_DecStbm_EmitsScrollRegionAction` (if scroll-region is added)
- split-sequence tests for new private-mode cases across chunk boundaries

### M2: Screen-State Model for Alternate Screen and Saved Cursor

**Why this belongs in 3.8:**

Parser support alone is not enough. The current screen model only knows one
buffer, so full-screen apps would overwrite the user’s main shell history.

**Files likely touched:**

- `src/ViewModels/TerminalScreen.cs`
- `src/ViewModels/TerminalSnapshot.cs`
- `tests/Zaide.Tests/ViewModels/TerminalScreenTests.cs`

**Planned change shape:**

1. Introduce explicit main-screen versus alternate-screen state inside
   `TerminalScreen`, rather than encoding alt-screen as scattered flags.
2. Keep retained shell scrollback attached to the main screen. Alternate screen
   should behave as a temporary full-screen surface, not as another scrollback
   history source.
3. Add saved cursor state with clear ownership rules:
   - save/restore should preserve row/column
   - decide explicitly whether active attributes are included and test that rule
4. Support the common behavioral contract:
   - entering alternate screen preserves the main screen state
   - exiting alternate screen restores the prior main screen state
   - combined `1049` behavior saves/restores cursor with the screen switch
5. Add only the richer control primitives that are justified by the target TUI
   transcript set. If scroll-region behavior is required, keep it narrow and
   testable instead of implementing broad VT semantics speculatively.
6. Keep `TerminalSnapshot` public enough for rendering/tests, but do not leak
   internal mutable screen-state types to the view layer.
7. Resize interaction with alt-screen:
   - Both main and alternate buffers must be resized when `Resize()` is called,
     so that switching back to the main screen after a resize does not show
     stale dimensions.
   - Saved cursor coordinates are clamped to the new bounds after resize.
8. `EraseDisplay(3)` (clear scrollback + screen) during alt-screen mode:
   - Clears the alternate screen surface only.
   - Does NOT clear the main screen's retained scrollback.
   - This matches the common xterm contract where alt-screen is a temporary
     surface isolated from the main scrollback history.

**Tests (M2):**

- `EnterAlternateScreen_PresentsCleanTemporaryBuffer`
- `ExitAlternateScreen_RestoresMainBufferContents`
- `AlternateScreen_DoesNotDestroyMainScrollback`
- `SaveCursor_RestoreCursor_ReturnsToPreviousCell`
- `Dec1049_EnableThenDisable_RestoresMainScreenAndCursor`
- `Resize_WhileInAlternateScreen_ResizesBothBuffers`
- `EraseDisplay3_WhileInAlternateScreen_ClearsAltScreenOnly`
- any pure-screen tests needed for narrow scroll-region behavior if added

### M3: ViewModel Integration and Transcript Compatibility

**Why this belongs in 3.8:**

The correctness target is not abstract VT compliance; it is better behavior for
real full-screen app flows. Those behaviors land through parser dispatch and
must be proven with transcript-style tests.

**Files likely touched:**

- `src/ViewModels/TerminalViewModel.cs`
- `tests/Zaide.Tests/ViewModels/TerminalViewModelTests.cs`

**Planned change shape:**

1. Update `TerminalViewModel.Append()` dispatch to apply the new parser actions
   without pushing emulation logic into the view.
2. Preserve the view-layer responsibilities already established:
   - rendering
   - viewport follow / manual scrollback
   - selection
3. Re-check snapshot projection so main-screen restoration and alternate-screen
   rendering remain deterministic across restart and resize.
4. Codify transcript-level compatibility around a few concrete TUI-style flows
   instead of trying to simulate whole applications end-to-end.
5. Guard Phase 3.7 behavior explicitly:
   - shell prompt path still works
   - scrollback/selection do not regress on the main screen
   - bracketed paste path remains intact
6. Restart / crash recovery with alt-screen active:
   - If the shell exits (or crashes) while in alt-screen mode, the screen must
     automatically revert to the main buffer before the `[Process exited]`
     message is appended.
   - `PrepareForRestart()` must clear alt-screen state so a fresh shell starts
     on the main screen.

**Tests (M3):**

- transcript tests for entering/exiting full-screen mode without corrupting the
  main shell surface
- transcript tests for save/restore cursor redraw behavior
- a `less`/`vim`-style open-exit transcript leaves the original prompt/history
  back on screen
- a redraw-heavy status transcript does not leave the terminal stuck in the
  alternate screen
- `ShellExit_WhileInAlternateScreen_RestoresMainBuffer`
- regression tests ensuring ordinary shell output still projects correctly

**Manual smoke for M3:**

- Run `less` and confirm entering/exiting returns to the prior shell screen
- Run `vim`, quit, and confirm the normal shell prompt/history return intact
- Run `htop` or another full-screen dashboard and confirm the display does not
  smear ordinary shell state into the app surface
- After quitting a full-screen app, verify selection/copy and manual scrollback
  still behave on the restored main screen

### M4: Docs and Exit Audit

- [ ] Create/update `docs/phases/phase-3.8/TOFIX.md` with review findings
- [ ] Update `docs/roadmap/PHASES.md` when Phase 3.8 is complete
- [ ] Update `docs/architecture/OVERVIEW.md` only if the terminal behavior
      contract meaningfully changed
- [ ] `dotnet build Zaide.slnx` succeeds
- [ ] `dotnet test Zaide.slnx --no-build` succeeds
- [ ] Linux manual smoke checklist passes

## Limitations (by design)

- This phase improves common TUI compatibility, not full terminal-emulator
  completeness
- Mouse reporting, cursor visibility, focus-reporting, and OSC/DCS-heavy
  protocols remain out of scope unless a narrowly justified transcript proves
  otherwise
- Terminal search, deeper selection polish, terminal tabs, and richer scroll UX
  remain Phase 3.9 work
- Windows and macOS backend work remain deferred platform phases, not part of
  this mainline roadmap step

## Exit Conditions

- [ ] `dotnet build Zaide.slnx` succeeds
- [ ] `dotnet test Zaide.slnx` succeeds
- [ ] Alternate screen enter/exit restores the main shell surface correctly
- [ ] Saved-cursor behavior required by the supported transcript set works
- [ ] At least one `vim`/`less`/`htop`-style transcript path is covered by
      automated tests
- [ ] No regressions in Phase 3.7 shell quality, selection, copy, paste,
      resize, restart, or main-screen scrollback behavior
- [ ] `docs/roadmap/PHASES.md` is updated when the phase is complete

## Manual Smoke Checklist (Linux)

- [ ] Toggle terminal (Ctrl+`) and confirm the shell prompt appears
- [ ] Run `less README.md`, quit, and confirm the original shell view returns
- [ ] Run `vim` on a small file, quit, and confirm the original shell view
      returns intact
- [ ] Run `htop` or another full-screen TUI, exit, and confirm the shell prompt
      and history are restored
- [ ] Scroll up on the main shell, enter/exit a full-screen app, and confirm
      main-screen scrollback still behaves correctly afterward
- [ ] Copy selected text after exiting a full-screen app and confirm clipboard
      behavior still works
- [ ] Resize during a full-screen app and confirm the display recovers without
      obvious corruption

## Rollback Plan

If this phase introduces regressions:

1. Revert parser changes for alternate-screen / save-restore actions
2. Restore the previous single-screen behavior in `TerminalScreen`
3. Remove transcript-specific dispatch logic added in `TerminalViewModel`
4. Keep the Phase 3.7 shell-quality path intact and restore the prior docs if
   the Phase 3.8 attempt is abandoned
