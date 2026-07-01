# Phase 3.7: Interactive Shell Quality — Implementation Plan

## Pre-Implementation Verification

- [x] Read `docs/phases/phase-3.7/BRIEF.md`
- [x] Re-read `docs/phases/phase-3.6/IMPLEMENTATION_PLAN.md` and `TOFIX.md`
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx`
- [x] Manually confirm the current Phase 3.6 baseline still works on Linux:
  - [x] Shell starts and shows a prompt
  - [x] `clear` works (already implemented in Phase 3.6 M4-01; verify no regression)
  - [x] Basic selection/copy/paste works
  - [x] Resize still reaches the PTY
- [x] Confirm no new NuGet packages are needed for this phase

## Planning Status

**Draft.** This phase starts from the completed Phase 3.6 renderer pipeline and
improves normal interactive shell behavior without widening into full TUI
compatibility work.

Verified live seams on 2026-07-01:

- `src/ViewModels/TerminalViewModel.cs`
  - owns UTF-8 decode continuity, parser/screen application, lifecycle, clear,
    restart, and resize forwarding
- `src/ViewModels/TerminalScreen.cs`
  - owns visible grid state, scrollback retention, cursor movement, erase, SGR,
    and resize behavior
- `src/Views/TerminalRenderControl.cs`
  - owns terminal drawing, cursor blink, scrollback viewport, and selection
- `src/Views/TerminalPanel.cs`
  - owns toolbar, keyboard/text input forwarding, clipboard copy/paste, and
    resize throttling
- `src/Services/LinuxTerminalService.cs`
  - keeps PTY ownership, `TERM=xterm-256color`, process lifecycle, and native
    resize signaling
- Current focused tests already exist in:
  - `tests/Zaide.Tests/ViewModels/TerminalViewModelTests.cs`
  - `tests/Zaide.Tests/ViewModels/TerminalScreenTests.cs`
  - `tests/Zaide.Tests/ViewModels/TerminalSnapshotTests.cs`
  - `tests/Zaide.Tests/Views/TerminalRenderControlTests.cs`
  - `tests/Zaide.Tests/Views/TerminalKeyMapperTests.cs`
  - `tests/Zaide.Tests/Services/LinuxTerminalServiceTests.cs`

## Scope

**Goal:** Make the terminal feel correct for everyday shell use on Linux after
the renderer migration: prompts redraw naturally, common shell colors render
accurately, paste behavior matches modern shell expectations, and resize/session
behavior stays stable during normal interaction.

**Boundaries:**

- Do not implement alternate-screen heavy app support (`vim`, `htop`, `less`)
- Do not add Windows or macOS terminal backends
- Do not add terminal tabs, search, or deep history UX
- Do not start Townhall / Phase 4 work here
- Do not attempt full VT compatibility; support only the narrow behaviors needed
  for ordinary shell workflows

## Known Gaps This Phase Targets

These are the concrete quality gaps implied by the current code and docs:

- The backend advertises `TERM=xterm-256color`, but the current cell model only
  stores the base 16 ANSI colors, so richer shell color output is degraded
- Paste currently sends raw clipboard text directly; modern shells often expect
  bracketed paste mode for safe multiline paste
- Phase 3.6 proves resize works, but interactive drag/resize stability and
  viewport-follow behavior during active sessions still need a dedicated pass
- Prompt redraw quality is only covered indirectly today; this phase should
  codify ordinary shell redraw transcripts in automated tests before expanding
  behavior further

## Milestones (Incremental)

| Milestone | Description | Test | Status |
|-----------|-------------|------|--------|
| M0 | Entry gate: current terminal build/tests/manual baseline verified | `dotnet build`, `dotnet test`, focused Linux smoke check | ⬜ |
| M1 | Extended shell color fidelity: 256-color and truecolor SGR support through parser → screen → snapshot → render control | Parser/screen/viewmodel tests for `38;5;n`, `48;5;n`, `38;2;r;g;b`, `48;2;r;g;b`; manual color smoke | ⬜ |
| M2 | Prompt and paste quality: add narrow bracketed-paste support and codify ordinary shell redraw transcripts | ViewModel/parser tests for bracketed-paste mode and redraw sequences; manual multiline paste smoke | ⬜ |
| M3 | Resize and session stability: improve active-session resize behavior, live-bottom recovery, and restart consistency | ViewModel/render-control tests plus manual drag-resize / restart smoke | ⬜ |
| M4 | Docs and exit audit | `dotnet build`, `dotnet test`, manual Linux smoke, roadmap/doc sync | ⬜ |

## Detailed Milestone Plans

### M1: Extended Shell Color Fidelity

**Why this belongs in 3.7:**

The brief calls out “common shell color output.” The current renderer still
matches the intentionally narrow Phase 3.6 plan, but `LinuxTerminalService`
already exports `TERM=xterm-256color`, so the UI should stop pretending that
16-color output is the full truth for ordinary shell use.

**Files likely touched:**

- `src/ViewModels/AnsiParser.cs`
- `src/ViewModels/TerminalScreen.cs`
- `src/ViewModels/TerminalSnapshot.cs`
- `src/ViewModels/TerminalViewModel.cs`
- `src/Views/TerminalRenderControl.cs`
- tests in `tests/Zaide.Tests/ViewModels/` and `tests/Zaide.Tests/Views/`

**Planned change shape:**

1. Replace the current “ANSI index only” color model with a richer internal
   representation that can express:
   - default color
   - ANSI 0-15 palette index
   - 256-color palette index
   - truecolor RGB

   Use a tagged union approach in `CellAttribute` and `TerminalCell`:
   ```csharp
   enum ColorKind { Default, AnsiIndex, Palette256, TrueColor }
   struct CellAttribute { ColorKind FgKind; int FgValue; ColorKind BgKind; int BgValue; bool Bold; bool Inverse; }
   ```
   Encoding convention:
   - `AnsiIndex` and `Palette256`: value is the palette index
   - `TrueColor`: value is packed `0xRRGGBB`

2. Extend SGR handling in `TerminalScreen.SetSgr()` for:
   - `38;5;n` foreground (256-color palette)
   - `48;5;n` background (256-color palette)
   - `38;2;r;g;b` foreground (truecolor RGB)
   - `48;2;r;g;b` background (truecolor RGB)

   The parser already emits raw parameter arrays for SGR, so no parser changes
   are needed for 256-color or truecolor parameter detection.

3. Preserve current reset/default behavior for `39`, `49`, and `0`
4. Keep unsupported style attributes such as italic/underline/strikethrough
   deferred
5. Ensure `TerminalSnapshot` exposes enough color data for the render control
   without leaking UI types into the ViewModel layer. Keep “default color” as a
   semantic value in snapshot/viewmodel layers; resolve concrete theme colors in
   `TerminalRenderControl`.
6. Map 256-color indices and truecolor values in `TerminalRenderControl`
   directly to actual rendered colors using a standard 256-color palette
   lookup table (ANSI 0-15, 6×6×6 cube 16-231, grayscale 232-255)

**Tests (M1):**

- Parser / screen:
  - `SetSgr_256ColorForeground_AppliesToSubsequentWrites`
  - `SetSgr_256ColorBackground_AppliesToSubsequentWrites`
  - `SetSgr_TrueColorForeground_AppliesToSubsequentWrites`
  - `SetSgr_TrueColorBackground_AppliesToSubsequentWrites`
  - `SetSgr_ResetRestoresDefaultColorsAfterExtendedPaletteUse`
- ViewModel snapshot projection:
  - decoded screen content carries the expected extended color metadata
- Render-control contract tests:
  - color projection helpers resolve ANSI / 256 / RGB values deterministically
  - 256-color palette lookup table matches standard xterm-256color mapping

**Manual smoke for M1:**

- `printf '\033[38;5;208morange-256\033[0m\n'`
- `printf '\033[48;5;25mblue-bg\033[0m\n'`
- `printf '\033[38;2;255;105;180mtruecolor-pink\033[0m\n'`
- Confirm default theme colors still apply when no explicit SGR is active

### M2: Prompt and Paste Quality

**Why this belongs in 3.7:**

This phase is about ordinary shell interaction, not TUI compatibility. The
highest-value missing behavior in that lane is bracketed paste and explicit
coverage of prompt redraw transcripts used by readline-style shells.

**Files likely touched:**

- `src/ViewModels/AnsiParser.cs`
- `src/ViewModels/TerminalViewModel.cs`
- `src/Views/TerminalPanel.cs`
- `tests/Zaide.Tests/ViewModels/TerminalViewModelTests.cs`
- `tests/Zaide.Tests/ViewModels/AnsiParserTests.cs`

**Planned change shape:**

1. Add narrow private-mode handling for bracketed paste only:
   - `CSI ? 2004 h` → bracketed paste enabled
   - `CSI ? 2004 l` → bracketed paste disabled
   - All other DEC private modes are ignored in this phase (no emitted action)

   Parser changes required:
   - Add `h` and `l` to `IsSupportedCsiFinalByte()` set
   - Modify `HasSupportedCsiParameterBytes()` to allow `?` prefix for DECSET/DECRST
   - Add new action type `DecSetResetAction(int mode, bool enabled)` or filter to emit only mode 2004

2. Keep other DECSET/DECRST private modes deferred to Phase 3.8
3. Move paste wrapping logic to ViewModel:
   - Add `_bracketedPasteEnabled` flag to `TerminalViewModel`
   - Add `PasteAsync(string text)` method that wraps with `ESC [ 200 ~` / `ESC [ 201 ~` when enabled
   - Update `TerminalPanel.PasteAsync()` to call `ViewModel.PasteAsync(text)` instead of `SendInputAsync(bytes)` directly
4. Preserve current plain-text paste behavior when bracketed paste mode is not
   enabled
5. Add transcript-style tests for ordinary prompt redraw patterns that rely on
   existing supported controls:
   - carriage return + erase line
   - overwrite-in-place status updates
   - prompt continuation after interrupted commands

**Tests (M2):**

- `PasteAsync_WhenBracketedPasteDisabled_SendsPlainUtf8`
- `PasteAsync_WhenBracketedPasteEnabled_WrapsWithBracketedPasteMarkers`
- `OutputReceived_BracketedPasteEnable_TogglesPasteMode`
- `OutputReceived_BracketedPasteDisable_TogglesPasteModeOff`
- transcript integration tests such as:
  - `"prompt> abc\r\033[Kprompt> abcd"` renders as the latest prompt state
  - interrupted command followed by fresh prompt does not leave stale prompt
    fragments behind

**Manual smoke for M2:**

- Paste a multiline command into a shell with bracketed paste enabled (for
  example a default readline-enabled Bash/Zsh setup) and confirm it does not
  execute line-by-line unexpectedly
- Confirm single-line paste still feels normal
- Confirm in-place shell redraws no longer leave stale text fragments

### M3: Resize and Session Stability

**Why this belongs in 3.7:**

Phase 3.6 proved the renderer can resize and restart. This phase hardens the
interactive behavior during active use so drag-resize, scrollback, and session
restarts feel predictable instead of merely functional.

**Files likely touched:**

- `src/ViewModels/TerminalViewModel.cs`
- `src/ViewModels/TerminalScreen.cs`
- `src/Views/TerminalPanel.cs`
- `src/Views/TerminalRenderControl.cs`
- tests in `tests/Zaide.Tests/ViewModels/` and `tests/Zaide.Tests/Views/`

**Planned change shape:**

1. Audit and tighten resize forwarding so active splitter drags do not leave the
   viewport, cursor, or scrollback position in a surprising state
2. Add scroll viewport tracking:
   - Add `FollowLiveBottom` property to `TerminalViewModel`
   - Track scroll offset in render control and notify ViewModel
   - User scrolls up via mouse wheel or drag; Enter or click-to-bottom resumes live tracking
3. Preserve or intentionally reset "follow live bottom" behavior with explicit
   rules:
   - user-scrolled viewport stays put during passive output
   - explicit resume actions return to live output (click-to-bottom and restart)
   - avoid implicit jump-to-bottom on normal typing while user is intentionally
     reviewing scrollback
4. Re-check restart behavior with cached dimensions and scrollback state so a
   restarted shell opens in the correct size and a clean live viewport
5. Add focused regression coverage for:
   - resize before start
   - resize during running session
   - restart after resize
   - input while scrolled back

**Tests (M3):**

- `Resize_WhenScrolledBack_PreservesScrollOffset`
- `Resize_DuringRunningSession_UpdatesSnapshotDimensionsWithoutCorruption`
- `Restart_AfterResize_ReappliesLatestViewportSize`
- `Enter_WhenScrolledBack_ReturnsViewportToLiveBottom`
- `ManualScrollback_DoesNotAutoJumpOnPassiveOutput`
- any pure `TerminalScreen` tests needed if resize semantics change

**Manual smoke for M3:**

- Drag the bottom splitter repeatedly while the shell is producing output
- Run a command that emits many lines, scroll up, then press Enter and confirm
  the viewport returns to live output intentionally
- Restart the terminal after a non-default resize and confirm the prompt
  returns at the correct dimensions

### M4: Docs and Exit Audit

- [ ] Create/update `docs/phases/phase-3.7/TOFIX.md` with review findings
- [ ] Update `docs/roadmap/PHASES.md` when Phase 3.7 is complete
- [ ] Update `docs/architecture/OVERVIEW.md` only if the terminal behavior
      contract meaningfully changed
- [ ] `dotnet build Zaide.slnx` succeeds
- [ ] `dotnet test Zaide.slnx --no-build` succeeds
- [ ] Linux manual smoke checklist passes

## Limitations (by design)

- Alternate screen, cursor hide/show, and wider private-mode handling remain
  Phase 3.8 work
- Terminal tabs, search, and full-history UX remain Phase 3.9 or later work
- Rich mouse reporting, shell integration features, and platform-specific
  terminal settings remain out of scope
- This phase can improve prompt redraw quality, but it is not a promise of full
  TUI correctness

## Exit Conditions

- [ ] Build succeeds: `dotnet build Zaide.slnx`
- [ ] Tests succeed: `dotnet test Zaide.slnx`
- [ ] 256-color and truecolor shell output render correctly
- [ ] Bracketed paste works when the shell enables it
- [ ] Ordinary prompt redraws do not leave visible stale text fragments
- [ ] Resize during active use feels stable and restart preserves the latest
      terminal size
- [ ] No regressions in Phase 3.6 selection/copy/paste/scrollback behavior
- [ ] `docs/roadmap/PHASES.md` is updated if the phase is completed

## Manual Smoke Checklist (Linux)

- [ ] Toggle terminal (Ctrl+`) and confirm the prompt appears
- [ ] Run `printf '\033[38;5;208morange-256\033[0m\n'` and confirm 256-color
      foreground renders correctly
- [ ] Run `printf '\033[48;5;25mblue-bg\033[0m\n'` and confirm 256-color
      background renders correctly
- [ ] Run `printf '\033[38;2;255;105;180mtruecolor-pink\033[0m\n'` and confirm
      truecolor foreground renders correctly
- [ ] Trigger a shell redraw pattern and confirm no stale prompt fragments
      remain
- [ ] Paste multiline text into a shell that enables bracketed paste and confirm
      the paste is handled safely
- [ ] Resize the terminal repeatedly during active output and confirm the prompt
      recovers cleanly
- [ ] Scroll up, then resume input and confirm the viewport intentionally
      returns to live output
- [ ] Restart the terminal after a resize and confirm the prompt returns with
      the correct dimensions

## Rollback Plan

If this phase introduces regressions:

1. Revert the extended color model changes in parser/screen/snapshot/render
2. Remove bracketed-paste state handling and restore plain paste behavior
3. Revert any resize/session-stability changes in ViewModel and render control
4. Restore the prior Phase 3.6 docs if the phase is abandoned
