# Phase 3.6: Terminal Renderer Foundation — TOFIX

Track code quality issues found during Phase 3.6 review.

---

## Status

Phase 3.6 is **complete except manual smoke test**. M1–M5 are implemented and
verified in code; the only outstanding item is the human-run Linux smoke
checklist (see IMPLEMENTATION_PLAN.md §Phase Exit: Outstanding Items).

- `dotnet build Zaide.slnx` — 0 warnings, 0 errors
- `dotnet test Zaide.slnx --no-build` — 300 passed, 0 failed
- `TerminalOutputBuffer.cs` and `TerminalOutputBufferTests.cs` have been
  removed (superseded by AnsiParser + TerminalScreen).

## Open Issues

- **GAP-1: Resize guard test (accepted deferral).** The plan's M3 test list
  requires a test for zero-metric guard in `ForwardResize()`. The guard code
  exists but cannot be unit-tested without Avalonia headless infrastructure.
  ViewModel-level rejection of invalid dimensions is covered by
  `Resize_IgnoresInvalidDimensions`. Accept as deferred to when Avalonia
  headless is available.
- **GAP-3: Manual smoke test (outstanding).** The Linux smoke checklist has not
  been run. Requires human running the application on Linux, including the new
  click-drag selection and mouse-wheel scrollback behaviors.

## Resolved Issues

- [x] **M1-01: ESC in CSI state is silently swallowed** — `ProcessCsi()` now
  aborts the malformed CSI and starts a fresh escape when `\x1B` appears in
  CSI state. Covered by `Parse_EscInsideCsi_AbortsMalformedSequenceAndStartsNewEscape`.
  Code: `src/ViewModels/AnsiParser.cs:111`. Test: `tests/Zaide.Tests/ViewModels/AnsiParserTests.cs:168`.
- [x] **M1-02: Bare ESC consumes the following character (SCS leak)** —
  `ProcessEscape()` now re-processes unknown trailing bytes in Ground state,
  explicitly consumes SCS prefixes plus designators, and still silently drops
  known two-byte non-CSI escapes. Covered by `Parse_BareEscBeforePrintable_ReprocessesPrintableCharacter`
  and `Parse_ScsSequence_ConsumesPrefixAndDesignator`.
  Code: `src/ViewModels/AnsiParser.cs:77`. Tests: `tests/Zaide.Tests/ViewModels/AnsiParserTests.cs:181` and `tests/Zaide.Tests/ViewModels/AnsiParserTests.cs:193`.
- [x] **M1-03: Unterminated OSC/DCS swallows all subsequent output** — added a
  4096-character guard to unsupported string handling so malformed OSC/DCS
  sequences cannot silence the terminal forever. Covered by
  `Parse_UnterminatedOsc_ResetsAfterGuardLimitAndPrintsFollowingText`.
  Code: `src/ViewModels/AnsiParser.cs:136`. Test: `tests/Zaide.Tests/ViewModels/AnsiParserTests.cs:203`.
- [x] **M1-08: Guard-boundary ESC and control characters silently dropped
  (follow-up)** — when the 4096-char guard fires in
  `ProcessUnsupportedString()`, the original code dropped ESC and C0 controls
  at the boundary. Fixed to route ESC into Escape state and everything else
  through `ProcessGround()`. Tests:
  `Parse_UnterminatedOsc_GuardBoundaryEscStartsNewEscape` and
  `Parse_UnterminatedOsc_GuardBoundaryControlCharacterIsEmitted`.
  Code: `src/ViewModels/AnsiParser.cs:165`. Tests:
  `tests/Zaide.Tests/ViewModels/AnsiParserTests.cs:246` and `:260`.
- [x] **M1-04: Only one split-point tested for chunk boundaries** — added
  explicit split coverage for the final-byte boundary plus OSC/DCS mid-string
  continuation. Tests: `Parse_SplitSequenceAtFinalByteBoundary_CompletesOnSecondCall`,
  `Parse_SplitOscAcrossCalls_DropsSequenceWhenTerminatorArrives`, and
  `Parse_SplitDcsAcrossCalls_DropsSequenceWhenTerminatorArrives`.
  Tests: `tests/Zaide.Tests/ViewModels/AnsiParserTests.cs:129`, `:144`, and `:156`.
- [x] **M1-05: Negative CSI parameters are accepted** — CSI parameter parsing
  now accepts only digits and semicolons, so negative values are dropped
  before integer parsing. Covered by
  `Parse_NegativeCursorParameter_DropsUnsupportedSequence`.
  Code: `src/ViewModels/AnsiParser.cs:171`. Test: `tests/Zaide.Tests/ViewModels/AnsiParserTests.cs:216`.
- [x] **M1-06: Incomplete intermediate-byte rejection in CSI** — the same
  parameter-byte filter now rejects unsupported intermediate bytes instead of
  relying on incidental `int.TryParse` failures. Covered by
  `Parse_CsiIntermediateByte_DropsUnsupportedSequence`.
  Code: `src/ViewModels/AnsiParser.cs:192`. Test: `tests/Zaide.Tests/ViewModels/AnsiParserTests.cs:226`.
- [x] **M1-07: No tests for common non-CSI escape sequences** — added a
  representative RIS assertion documenting the current silent-drop contract.
  Covered by `Parse_RisEscape_IsDroppedWithoutEmittingText`.
  Test: `tests/Zaide.Tests/ViewModels/AnsiParserTests.cs:236`.
- **Render test strategy refined (planning only)** — During plan review, the
  required Avalonia headless/`RenderTargetBitmap` tests were removed from this
  phase because they would require a new test dependency that has not been
  approved or cataloged. The M3 automated coverage is now limited to pure
  snapshot/contract/geometry-guard tests, with real rendering verified by the
  manual smoke checklist. `IMPLEMENTATION_PLAN.md` is authoritative.
- **Resize metric guard added (planning only)** — The plan now requires
  `ForwardResize()` to return early while `CellWidth` or `LineHeight` is zero,
  because bounds notifications can arrive before the custom render control has
  measured its first glyph and `TerminalGeometry.Compute()` rejects non-positive
  metrics.
- **TerminalViewModel test migration made explicit (planning only)** — The plan
  now enumerates the constructor-seam and `OutputText` assertion rewrites needed
  when `TerminalOutputBuffer` is replaced by `ScreenSnapshot`.
- [x] **M4-01: Toolbar Clear bypasses shell redraw** — the Clear button now
  sends Ctrl+L (`0x0C`) to the PTY while the shell is running, matching terminal
  behavior instead of blanking only the renderer surface. Local clear remains as
  a fallback when the terminal is not running. Covered by
  `ClearCommand_SendsCtrlLToRunningTerminal` and
  `ClearCommand_ClearsScreen_WhenTerminalIsNotRunning`.
  Code: `src/ViewModels/TerminalViewModel.cs`. Tests:
  `tests/Zaide.Tests/ViewModels/TerminalViewModelTests.cs`.
- [x] **M4-02: Default terminal colors ignore Zaide palette** — the render
  control now resolves default foreground/background from the app theme
  (`TextActiveColor` / `DeepBaseColor`) instead of hardcoding ANSI black/white.
  Explicit ANSI colors still render as terminal colors.
  Code: `src/Views/TerminalRenderControl.cs`.
- [x] **M4-03: Mouse selection and wheel scroll absent from custom terminal** —
  the terminal now retains bounded scrollback, supports mouse-wheel navigation,
  click-drag selection, and selection-first clipboard copy.
  Code: `src/ViewModels/TerminalScreen.cs`,
  `src/ViewModels/TerminalSnapshot.cs`,
  `src/ViewModels/TerminalViewModel.cs`,
  `src/Views/TerminalRenderControl.cs`,
  `src/Views/TerminalPanel.cs`. Tests:
  `TerminalScreenTests`, `TerminalViewModelTests`,
  `TerminalRenderControlTests`.
- [x] **M4-04: Restart button is unusable during normal terminal operation** —
  restart now behaves like a true session restart: if the shell is running, the
  PTY session is stopped and a fresh shell is started automatically after the
  exit signal; if the shell is already exited or errored, restart starts it as
  before.
  Code: `src/Services/ITerminalService.cs`,
  `src/Services/LinuxTerminalService.cs`,
  `src/ViewModels/TerminalViewModel.cs`. Tests:
  `LinuxTerminalServiceTests`, `TerminalViewModelTests`.
- [x] **M4-05: Selection captured padded terminal blanks instead of only text** —
  selection bounds now clamp to each row's real text extent, so trailing empty
  cells after line end are neither highlighted nor copied.
  Code: `src/Views/TerminalRenderControl.cs`. Test:
  `TerminalRenderControlTests`.
- [x] **M4-06: Cursor blink missing from focused terminal** — the render control
  now runs a lightweight UI-thread blink timer and resets the blink phase when
  cursor visibility or position changes, so the focused terminal cursor pulses
  again without involving PTY state.
  Code: `src/Views/TerminalRenderControl.cs`.

## Deferred to Future Phases

Items listed here are planned regressions or out-of-scope behaviours that
must be re-checked once the renderer is built. They do not yet correspond to
real code.

| Issue | Reason | Target Phase |
|-------|--------|--------------|
| 256-color SGR (38;5;n / 48;5;n) | Parser will recognise and ignore; extended palette deferred | Future terminal renderer phase |
| Deep scrollback polish | Current renderer keeps bounded row scrollback and wheel navigation, but not a full history/search UX | Future terminal phase |
| Selection polish | Basic click-drag selection exists, but richer IDE selection/search ergonomics remain future work | Future terminal phase |
| Cursor hide/show (DECSET/DECRST) | Not in M1 supported sequence set | Phase 3.8 (TUI compatibility) |
| Alternate screen (`\x1B[?1049h`) | Needed for vim/htop — explicitly out of scope | Phase 3.8 (TUI compatibility) |
| OSC sequences (window title, etc.) | No user-facing need yet | Future terminal phase |
| DCS sequences (tmux, kitty) | Niche; no testable use case | Future terminal phase |
| Bold as separate weight vs. bright color | Plan matches xterm: bold = bright fg. True bold glyph weight is cosmetic | Future terminal renderer phase |
| Underline / italic / strikethrough | SGR 4/3/9 to be recognised but not rendered | Future terminal renderer phase |
