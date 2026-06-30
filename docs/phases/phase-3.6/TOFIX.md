# Phase 3.6: Terminal Renderer Foundation — TOFIX

Track code quality issues found during Phase 3.6 review.

---

## Status

Phase 3.6 is **complete except manual smoke test**. M1–M5 are implemented and
verified in code; the only outstanding item is the human-run Linux smoke
checklist (see IMPLEMENTATION_PLAN.md §Phase Exit: Outstanding Items).

- `dotnet build Zaide.slnx` — 0 warnings, 0 errors
- `dotnet test Zaide.slnx --no-build` — 292 passed, 0 failed
- `TerminalOutputBuffer.cs` and `TerminalOutputBufferTests.cs` have been
  removed (superseded by AnsiParser + TerminalScreen).

## Open Issues

- **GAP-1: Resize guard test (accepted deferral).** The plan's M3 test list
  requires a test for zero-metric guard in `ForwardResize()`. The guard code
  exists but cannot be unit-tested without Avalonia headless infrastructure.
  ViewModel-level rejection of invalid dimensions is covered by
  `Resize_IgnoresInvalidDimensions`. Accept as deferred to when Avalonia
  headless is available.
- **GAP-3: Manual smoke test (outstanding).** The 12-item Linux smoke checklist
  and scrollback/selection regression checks have not been run. Requires human
  running the application on Linux.

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

## Deferred to Future Phases

Items listed here are planned regressions or out-of-scope behaviours that
must be re-checked once the renderer is built. They do not yet correspond to
real code.

| Issue | Reason | Target Phase |
|-------|--------|--------------|
| Scrollback history ring | Planned screen buffer is viewport-only; deliberate regression from Phase 3.5 | Future terminal phase |
| 256-color SGR (38;5;n / 48;5;n) | Parser will recognise and ignore; extended palette deferred | Future terminal renderer phase |
| Mouse selection in terminal | Complex highlight rendering, mouse capture; deliberate regression from Phase 3.5 | Future terminal phase |
| Blinking cursor | Cosmetic; no visual impact on functionality | Future terminal phase |
| Cursor hide/show (DECSET/DECRST) | Not in M1 supported sequence set | Phase 3.8 (TUI compatibility) |
| Alternate screen (`\x1B[?1049h`) | Needed for vim/htop — explicitly out of scope | Phase 3.8 (TUI compatibility) |
| OSC sequences (window title, etc.) | No user-facing need yet | Future terminal phase |
| DCS sequences (tmux, kitty) | Niche; no testable use case | Future terminal phase |
| Bold as separate weight vs. bright color | Plan matches xterm: bold = bright fg. True bold glyph weight is cosmetic | Future terminal renderer phase |
| Underline / italic / strikethrough | SGR 4/3/9 to be recognised but not rendered | Future terminal renderer phase |
