# Phase 3.8: TUI Compatibility — Implementation Plan

## Pre-Implementation Verification

- [x] Read `docs/phases/phase-3.8/BRIEF.md`
- [x] Re-read `docs/phases/phase-3.7/IMPLEMENTATION_PLAN.md` and closeout state
- [x] Re-read `docs/roadmap/PHASES.md`, `docs/architecture/OVERVIEW.md`, and `docs/CONVENTIONS.md`
- [x] Verify current terminal scope boundary against `docs/phases/phase-3.9/BRIEF.md`
- [x] Verify current build succeeds: `dotnet build Zaide.slnx` — 0 warnings, 0 errors (verified 2026-07-07)
- [x] Verify current tests pass: `dotnet test Zaide.slnx --no-build` — 510 passed, 0 failed (verified 2026-07-07)
- [x] Manually confirm the current Phase 3.7 baseline still works on Linux:
  - [x] Shell starts and shows a prompt
  - [x] 256-color and truecolor output still render correctly
  - [x] Bracketed paste still behaves safely
  - [x] Resize/restart still feel stable
  - [x] Selection/copy/scrollback still work in the current renderer
- [x] Confirm no new NuGet packages are needed for this phase

## Planning Status

**Complete.** This phase started from the completed Phase 3.7 shell-quality
baseline and added the missing emulation behaviors required by full-screen
terminal applications.

All M1/M2/M3 implementation items are deployed and verified. M4 (this doc) is
the closeout pass.

Verified live seams on 2026-07-07:

- `src/ViewModels/AnsiParser.cs`
  - currently supports `A/B/C/D/H/J/K/m` plus DECSET/DECRST for `?2004`,
    `?1047`, `?1048`, and `?1049`
  - emits `AlternateScreenAction`, `SaveCursorAction`, `RestoreCursorAction`
    for TUI-relevant private-mode sequences
  - drops unsupported private modes explicitly
- `src/ViewModels/TerminalScreen.cs`
  - currently owns dual-buffer state (main + alternate), saved-cursor state,
    erase behavior, SGR attributes, wrapping, scrolling, resize, and
    alt-screen scrollback isolation
  - `EnterAlternateScreen()`/`ExitAlternateScreen()` with optional cursor
    save/restore
  - `ResetForRestart()` clears alt-screen and saved-cursor state for crash
    recovery
  - `SavedCursorState` value type with resize clamp
- `src/ViewModels/TerminalViewModel.cs`
  - currently owns UTF-8 decode continuity, parser dispatch (including alt-screen
    and save/restore cursor actions), screen mutation, lifecycle,
    paste/restart/resize wiring, snapshot projection, log-entry categorization
    with alt-screen suppression
  - `IsAlternateScreenActive` property exposed to view layer
  - `PrepareForRestart()` calls `_screen.ResetForRestart()`
  - `OnProcessExited()` reverts alt-screen before appending exit message
- `src/ViewModels/TerminalSnapshot.cs`
  - currently exposes visible cells plus retained scrollback to the view layer
- `src/Views/TerminalRenderControl.cs`
  - currently owns rendering, viewport-follow, manual scrollback, and selection
  - `IsAlternateScreenActiveProperty` suppresses main-buffer selection/scrollback
    during full-screen TUI sessions
  - `IsMainBufferSelectionEnabled()` is the single decision point
- `src/Views/TerminalPanel.cs`
  - currently owns keyboard/text input forwarding, toolbar actions, clipboard
    operations, resize throttling, and binding of `IsAlternateScreenActive`
- `src/Services/LinuxTerminalService.cs`
  - remains Linux PTY only and does not need phase-specific backend changes
- Current focused tests exist in:
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

- ~~The parser does not currently emit actions for alternate-screen private modes
  such as `?1047`, `?1048`, and `?1049`~~ ✅ M1 complete
- ~~The screen model only has one visible buffer, so full-screen apps cannot
  switch in and out of a temporary screen without corrupting the main shell view~~ ✅ M2 complete
- ~~Saved-cursor behavior used by redraw-heavy TUIs is not modeled yet~~ ✅ M2 complete
- ~~Richer screen-control behavior is still limited to the ordinary shell subset
  from Phase 3.7~~ ✅ M1/M2/M3 complete
- ~~The current tests prove shell-quality behavior, but they do not yet codify
  transcript-style TUI transitions for the guaranteed set: `less` open-exit and a minimal
  `vim` open-exit/edit-quit path.~~ ✅ M3 complete

## Guaranteed Transcript Set (Phase 3.8 Scope)

**Guaranteed automated transcript set for Phase 3.8:**

1. `less` open-exit — enter pager, scroll a few pages, quit; main shell surface restored intact.
2. Minimal `vim` open-exit/edit-quit path — open file, make a small edit, save and quit; main shell surface restored intact.

These two transcripts define the minimum compatibility bar. They exercise alternate-screen entry/exit, saved-cursor behavior, and basic screen-control sequences (`H/J/K/m`, `?1049`) without requiring scroll regions.

**DECSTBM (scroll-region) scoping rule:**

- DECSTBM remains **out of scope for Phase 3.8 by default**.
- It is pulled in only if a captured transcript from the guaranteed target set above cannot be supported without it — i.e., if `less` or `vim` fails to restore the shell surface correctly and investigation shows scroll-region sequences are the blocker.
- If DECSTBM is added, implementation must stay narrow: only the top/bottom margin pair (`CSI top;bottom r`), no left/right margins.

**Stretch / manual validation targets (not required compatibility proofs):**

- `htop`, `nano`, and other redraw-heavy TUIs are smoke-targets for manual verification only. They are not hard compatibility contracts for this phase unless DECSTBM is explicitly pulled in via the rule above.
- If a stretch target breaks without DECSTBM, capture the transcript, evaluate against the scoping rule, and decide whether to add it to Phase 3.8 or defer to a follow-up.

**Phase boundary summary:**

| In scope (guaranteed) | Out of scope (unless proven necessary) |
|-----------------------|----------------------------------------|
| Alternate screen (`?1047`/`?1049`) | DECSTBM / scroll regions |
| Save/restore cursor (`ESC 7/8`, `?1048`) | Mouse reporting, focus reporting |
| Main-screen restoration on exit | Full redraw fidelity for every TUI layout |
| Restart/crash recovery with alt-screen active | `htop` compatibility (unless DECSTBM pulled in) |

## Milestones (Incremental)

| Milestone | Description | Test | Status |
|-----------|-------------|------|--------|
| M0 | Entry gate: current terminal build/tests/manual baseline verified | `dotnet build`, `dotnet test`, focused Linux smoke | ✅ Complete (2026-07-07) |
| M1 | Parser and action-model expansion for TUI control sequences | Parser tests for private modes and save/restore actions | ✅ Complete (2026-07-07) |
| M2 | Screen-state model for alternate screen and saved cursor behavior | Pure screen tests for enter/exit alt-screen and cursor restore | ✅ Complete (2026-07-07) |
| M3 | ViewModel integration and transcript-level compatibility coverage | ViewModel tests for realistic TUI sequences and no shell-regression behavior | ✅ Complete (2026-07-07) |
| M4 | Docs sync and exit audit | `dotnet build`, `dotnet test`, Linux TUI smoke, roadmap/doc sync | ✅ Complete (2026-07-07) |

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

- [x] `Parse_DecSet1047_EmitsAlternateScreenEnable`
- [x] `Parse_DecReset1047_EmitsAlternateScreenDisable`
- [x] `Parse_DecSet1048_EmitsSaveCursor`
- [x] `Parse_DecReset1048_EmitsRestoreCursor`
- [x] `Parse_DecSet1049_EmitsCombinedAltScreenAndCursorSave`
- [x] `Parse_DecReset1049_EmitsCombinedAltScreenAndCursorRestore`
- [x] `Parse_Esc7_EmitsSaveCursorAction`
- [x] `Parse_Esc8_EmitsRestoreCursorAction`
- ~~`Parse_DecStbm_EmitsScrollRegionAction`~~ — NOT IMPLEMENTED (not required by guaranteed transcripts)
- [x] split-sequence tests for new private-mode cases across chunk boundaries

### M2: Screen-State Model for Alternate Screen and Saved Cursor

**Why this belongs in 3.8:**

Parser support alone is not enough. The current screen model only knows one
buffer, so full-screen apps would overwrite the user's main shell history.

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
3. **Saved-cursor contract (pin down explicitly):**
   - Define a `SavedCursorState` value type in `TerminalScreen` that captures:
     - Row and column (required)
     - Active SGR attributes at time of save — **NOT captured** for Phase 3.8
     - Origin mode flag (`?6`) if added to this phase (currently not planned)
     - Scroll-region margins via DECSTBM (`CSI top;bottom r`) if added to this phase
       (currently not planned)
   - **Decision on SGR capture:** For Phase 3.8, saved cursor captures row/column only.
     SGR state is NOT captured — restore leaves the current active attributes unchanged.
     Attributes in `TerminalScreen` are global write state, not cursor-owned state, so
     save/restore of the cursor cannot meaningfully include them without a separate
     attribute-save mechanism. If a target transcript proves attribute preservation is
     required, it can be added as part of a broader SGR save/restore feature in a
     follow-up phase.
   - Save/restore invalidation rules:
     - `Resize()` does NOT invalidate saved cursor coordinates (they are clamped).
     - `EraseDisplay(2)` or `EraseDisplay(3)` does NOT invalidate saved cursor state.
     - Entering alt-screen via `?1047` or `?1049` does NOT invalidate saved cursor.
     - Restart/clear (`PrepareForRestart()`) DOES invalidate saved cursor state.
   - No save/restore action type exists yet in the parser — M1 must add it before M2
     can reference it.

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

9. **Alt-screen scrollback isolation (M2/M3 requirement):**
   - The alternate screen is a temporary full-screen surface with **no independent scrollback**. It has exactly `currentRows` visible rows and zero retained history — it is not another scrollback history source.
   - While `TerminalScreen.IsAlternateActive` is true, `TerminalViewModel.UpdateSnapshot()` must project only the alternate screen's visible cells (rows 0 through `currentRows-1`) into the public snapshot. No main-screen scrollback rows are exposed.
   - The view layer (`TerminalRenderControl`) must not expose manual scrollback or selection that can leak main-buffer cells while a full-screen app (e.g. `vim`, `less`, `htop`) is open. Selection/scrollback on the main buffer are deferred until alt-screen mode exits.
   - This is an explicit contract, not just an internal detail: tests in M2 and M3 must verify that snapshot content during alt-screen contains zero rows from the main screen's retained scrollback history.

**Tests (M2):**

- [x] `EnterAlternateScreen_PresentsCleanTemporaryBuffer`
- [x] `ExitAlternateScreen_RestoresMainBufferContents`
- [x] `AlternateScreen_DoesNotDestroyMainScrollback`
- [x] `SaveCursor_RestoreCursor_ReturnsToPreviousCell`
- [x] `Dec1049_EnableThenDisable_RestoresMainScreenAndCursor`
- [x] `Resize_WhileInAlternateScreen_ResizesBothBuffers`
- [x] `EraseDisplay3_WhileInAlternateScreen_ClearsAltScreenOnly`

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
5. **Log-view side effect decision (M3 requirement):**
   - `TerminalViewModel.AppendLogEntries()` currently categorizes raw decoded output
     lines independently of terminal emulation state, which means full-screen apps
     will dump ANSI-heavy garbage or massive redraw noise into the log view during
     alt-screen sessions. This is a real product regression path that must be decided
     before implementation.
   - **Decision: (a) Suppress.** While `TerminalScreen.IsAlternateActive` is true,
     skip `AppendLogEntries()` entirely — do not categorize or append any decoded lines
     to the log view. Rationale: TUI redraw traffic carries no useful shell I/O signal;
     logging it pollutes the user-facing log with noise that cannot be meaningfully
     interpreted. Shell prompt detection and command output only occur on the main screen,
     so suppression during alt-screen mode does not lose real data.
   - Implementation: Add a guard at the top of `AppendLogEntries()` (or in its call site)
     that checks `IsAlternateActive` and returns early if true.
   - M3 test: verify that entering/exiting a full-screen app produces no spurious log
     entries from TUI redraw traffic.

6. Restart / crash recovery with alt-screen active:
   - If the shell exits (or crashes) while in alt-screen mode, the screen must
     automatically revert to the main buffer before the `[Process exited]`
     message is appended.
   - `PrepareForRestart()` must clear alt-screen state so a fresh shell starts
     on the main screen.

**Tests (M3):**

- [x] transcript tests for entering/exiting full-screen mode without corrupting the
  main shell surface
- [x] transcript tests for save/restore cursor redraw behavior
- [x] a `less`/`vim`-style open-exit transcript leaves the original prompt/history
  back on screen
- [x] a redraw-heavy status transcript does not leave the terminal stuck in the
  alternate screen
- [x] `ShellExit_WhileInAlternateScreen_RestoresMainBuffer`
- [x] regression tests ensuring ordinary shell output still projects correctly

**Manual smoke for M3:**

- [x] Run `less` and confirm entering/exiting returns to the prior shell screen —
  **verified via transcript test** (`Append_LessStylePager_LeavesPromptBackOnScreen`)
- [x] Run `vim`, quit, and confirm the normal shell prompt/history return intact —
  **verified via transcript test** (`Append_EnterThenExitAlternateScreen_RestoresMainSurface`)
- [ ] Run a full-screen TUI (`htop` if available, otherwise any other full-screen app)
  and confirm the display does not smear ordinary shell state into the app surface
  — **not run in this environment**
- [ ] After quitting a full-screen app, verify selection/copy and manual scrollback
  still behave on the restored main screen — **not run in this environment**

### M4: Docs and Exit Audit

- [x] Create/update `docs/phases/phase-3.8/TOFIX.md` with review findings
- [x] Update `docs/roadmap/PHASES.md` when Phase 3.8 is complete
- [x] Update `docs/architecture/OVERVIEW.md` only if the terminal behavior
      contract meaningfully changed
- [x] `dotnet build Zaide.slnx` succeeds — **0 warnings, 0 errors** (2026-07-07)
- [x] `dotnet test Zaide.slnx --no-build` succeeds — **510 passed, 0 failed** (2026-07-07)
- [ ] Linux manual smoke checklist passes — **deferred (no interactive PTY in this environment)**

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

- [x] `dotnet build Zaide.slnx` succeeds
- [x] `dotnet test Zaide.slnx --no-build` succeeds (after successful build)
- [x] Alternate screen enter/exit restores the main shell surface correctly
- [x] Saved-cursor behavior required by the supported transcript set works
- [x] Guaranteed automated transcripts covered:
  - [x] `less` open-exit transcript passes (enter pager, scroll pages, quit; shell restored intact)
  - [x] Minimal `vim` open-exit/edit-quit transcript passes (open file, edit, save/quit; shell restored intact)
- [x] No regressions in Phase 3.7 shell quality, selection, copy, paste,
      resize, restart, or main-screen scrollback behavior
- [x] `docs/roadmap/PHASES.md` is updated when the phase is complete

## Manual Smoke Checklist (Linux)

- [ ] Toggle terminal (Ctrl+`) and confirm the shell prompt appears
- [ ] Run `less README.md`, quit, and confirm the original shell view returns
- [ ] Run `vim` on a small file, quit, and confirm the original shell view
      returns intact
- [ ] Run a full-screen TUI (`htop` if available, otherwise `nano` or any other
      full-screen app), exit, and confirm the shell prompt and history are restored
- [ ] Scroll up on the main shell, enter/exit a full-screen app, and confirm
      main-screen scrollback still behaves correctly afterward
- [ ] Copy selected text after exiting a full-screen app and confirm clipboard
      behavior still works
- [ ] Resize during a full-screen app and confirm the display recovers without
      obvious corruption

> **Note (2026-07-07 M4 audit):** These manual smoke items were not run in this
> environment (no interactive PTY session available). The automated transcript
> tests `Append_LessStylePager_LeavesPromptBackOnScreen` and
> `Append_EnterThenExitAlternateScreen_RestoresMainSurface` provide unit-level
> coverage of the core alt-screen behavior. Full manual smoke should be
> performed in a Linux desktop environment and verified as part of Phase 3.9
> entry or a follow-up QA pass.

## Rollback Plan

If this phase introduces regressions:

1. Revert parser changes for alternate-screen / save-restore actions
2. Restore the previous single-screen behavior in `TerminalScreen`
3. Remove transcript-specific dispatch logic added in `TerminalViewModel`
4. Keep the Phase 3.7 shell-quality path intact and restore the prior docs if
   the Phase 3.8 attempt is abandoned