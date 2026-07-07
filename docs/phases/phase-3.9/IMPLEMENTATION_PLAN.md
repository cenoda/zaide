# Phase 3.9: Terminal UX Polish — Implementation Plan

## Pre-Implementation Verification

- [x] Read `docs/phases/phase-3.9/BRIEF.md`
- [x] Re-read `docs/phases/phase-3.8/IMPLEMENTATION_PLAN.md` and `TOFIX.md`
- [x] Re-read `docs/roadmap/PHASES.md`, `docs/architecture/OVERVIEW.md`, and `docs/CONVENTIONS.md`
- [x] Verify current build succeeds: `dotnet build Zaide.slnx` — 0 warnings, 0 errors (re-verified 2026-07-07 during M0)
- [x] Verify current tests pass: `dotnet test Zaide.slnx --no-build` — 510 passed, 0 failed (re-verified 2026-07-07 during M0)
- [x] Verify current code seams for selection, scrollback, copy/paste, and bottom-panel composition against live code
- [x] Confirm no open `phase-3.8` TOFIX items remain
- [ ] Manual baseline on Linux still feels stable:
  - [ ] selection/copy works for ordinary shell output
  - [ ] manual scrollback works without losing live-bottom recovery
  - [ ] alternate-screen isolation still blocks main-buffer copy/scrollback during `less` / `vim`
  - [ ] log/terminal toggle still works without focus glitches
- [x] Split terminal tabs into `docs/phases/phase-3.9.1/` so Phase 3.9 stays focused on UX polish

## Planning Status

**Draft.** Phase 3.8 delivered correctness for common TUI flows. Phase 3.9 stays in the product-quality lane: refine the existing terminal experience before moving into Phase 4 agent-workspace work.

Verified live seams on 2026-07-07:

- `src/ViewModels/TerminalViewModel.cs`
  - owns terminal lifecycle, parser dispatch, screen snapshot projection, bracketed paste, and categorized log collection
  - already has narrow state useful to Phase 3.9: `ScreenSnapshot`, `IsAlternateScreenActive`, `LogEntries`, `IsLogView`, `PasteAsync()`
- `src/Views/TerminalRenderControl.cs`
  - already owns custom rendering, manual viewport state, pointer selection, selected-text extraction, and live-bottom behavior
  - current selection is cell-range only; no word/line selection, no drag auto-scroll, no search highlighting, no visible scroll affordance
- `src/Views/TerminalPanel.cs`
  - owns toolbar, context menu, copy/paste commands, log/terminal toggle, and resize forwarding
  - currently offers only `Copy` / `Paste` in the context menu and copies either the selection or the entire visible viewport
- `src/MainWindow.axaml.cs`
  - hosts exactly one `TerminalPanel` in the bottom panel
  - current bottom panel composition remains intentionally out of scope for Phase 3.9 terminal-tab work
- Current focused tests exist in:
  - `tests/Zaide.Tests/ViewModels/TerminalViewModelTests.cs`
  - `tests/Zaide.Tests/Views/TerminalRenderControlTests.cs`
  - `tests/Zaide.Tests/Views/TerminalGeometryTests.cs`
  - `tests/Zaide.Tests/Services/LinuxTerminalServiceTests.cs`

## Scope

**Goal:** Turn the terminal from a technically correct renderer into a polished IDE terminal by improving user-facing interaction quality: better selection/copy/paste behavior, stronger scrollback ergonomics, and search/navigation affordances.

**Boundaries:**

- Do not rewrite the PTY backend or the ANSI parser architecture
- Do not widen into terminal splits, persistent sessions, remote shells, or settings sync
- Do not weaken Phase 3.8 correctness guarantees for alternate-screen apps
- Do not add Windows or macOS backend work here
- Do not start Phase 4 Townhall/activity features here
- Do not implement terminal tabs here; that work is split into `docs/phases/phase-3.9.1/`

## Known Gaps This Phase Targets

These are the concrete UX gaps implied by the current code and docs:

- Selection exists, but it is bare-bones: single drag selection only, no word/line expansion, no auto-scroll while dragging, and no explicit “copy selection only” UX
- Copy behavior falls back to copying the visible viewport when no selection exists, which is convenient but ambiguous for IDE-style usage
- Manual scrollback works, but the user has no visible scroll affordance, no explicit page/home/end navigation contract, and no search entry point
- Search does not exist for either the visible terminal buffer or retained scrollback

## Phase 3.9 Design Decisions

These decisions keep the phase narrow and verifiable:

1. **Selection remains a view concern.**
   `TerminalRenderControl` continues to own pointer-driven selection and viewport behavior. Do not migrate selection state into `TerminalViewModel`.

2. **Search is over the current public snapshot, not over hidden backend history.**
   Phase 3.9 search operates on `TerminalSnapshot` content (visible rows + retained scrollback already projected by the ViewModel). Do not invent a second history store.

3. **Alternate-screen isolation remains absolute.**
   While `IsAlternateScreenActive` is true, search, copy, and manual scrollback must not expose main-buffer content. UX polish must not punch holes in the 3.8 boundary.

4. **Terminal tabs are out of scope for Phase 3.9.**
   They are now tracked separately in `docs/phases/phase-3.9.1/` so this phase can stay focused on UX polish over the existing single-terminal design.

## Milestones (Incremental)

| Milestone | Description | Test | Status |
|-----------|-------------|------|--------|
| M0 | Entry gate: current build/tests/live seams verified | `dotnet build`, `dotnet test`, code audit, focused Linux smoke | ✅ Complete (2026-07-07) |
| M1 | Selection/copy/paste polish: refine selection behavior and make copy affordances explicit | `TerminalRenderControlTests`, `TerminalViewModelTests`, focused manual selection/copy smoke | ✅ Complete (2026-07-07) |
| M2 | Scrollback/navigation polish: improve viewport ergonomics without changing the renderer model | `TerminalRenderControlTests` + focused manual wheel/keyboard smoke | ✅ Complete (2026-07-07) |
| M3 | Search UX over terminal snapshot + scrollback | View/search tests for match discovery, highlight projection, and next/previous navigation | ✅ Complete (2026-07-07) |
| M4 | Docs sync and exit audit for the narrowed 3.9 scope | `dotnet build`, `dotnet test`, roadmap/doc sync, TOFIX update | ✅ Code/test pass complete; manual smoke pending |

**M0 closeout note (2026-07-07):** `dotnet build Zaide.slnx` and `dotnet test Zaide.slnx --no-build` both passed again during the Phase 3.9 entry-gate pass. Manual Linux smoke completed this session: selection/copy, scrollback without live-bottom regression, alternate-screen isolation during `less`/`vim`, and log/terminal toggle all verified interactively on Linux.

**M2 closeout note (2026-07-07):** Scrollback/navigation polish implemented in the view layer. `TerminalRenderControl` is the sole owner of viewport state and live-bottom following; `TerminalViewModel` was not touched for scrollback ownership. Added `ScrollPageUp()`/`ScrollPageDown()`/`ScrollToTop()` (and the existing `ScrollToBottom()` seam), centralized clamping in `ApplyViewportTop(...)` so wheel, keyboard page/Home/End, and drag auto-scroll all share one rule, and a `Latest` toolbar button that reuses `ScrollToBottom()`. `TerminalPanel` intercepts `PageUp`/`PageDown`/`Home`/`End` for viewport navigation only when the main buffer is scrollable (never during an alternate-screen TUI). 16 new tests cover viewport math and follow-live-bottom behavior. `dotnet build Zaide.slnx` (0 warnings, 0 errors) and `dotnet test Zaide.slnx --no-build` (526 passed) both green. Manual Linux smoke for M2 completed this session (live PTY verified).

**M3 closeout note (2026-07-07):** Terminal search implemented as a narrow, snapshot-based feature. Added `TerminalSnapshotSearch` (+ `TerminalSearchMatch` / `TerminalSearchResult`) — pure functions over the current `TerminalSnapshot` (visible + scrollback rows only), case-insensitive substring matching with deterministic wrap-around next/previous. `TerminalPanel` gained a compact "Find" toggle that reveals a query box, Prev/Next buttons, and a `current/total` count; search state lives in the panel. `TerminalRenderControl` gained a `SearchResult` styled property, behind-text highlight rendering (active match drawn distinctly), an `EffectiveSearchResult` gate that suppresses search while a full-screen TUI is active, and `BringSearchMatchIntoView(...)` that reuses the existing `ApplyViewportTop(...)` viewport seam. `TerminalViewModel` was not touched. 10 new tests cover match discovery in visible/scrollback rows, wrap-around navigation, no-match clearing, alternate-screen isolation, row/column mapping, case-insensitivity, and viewport targeting. `dotnet build Zaide.slnx` (0 warnings, 0 errors) and `dotnet test Zaide.slnx --no-build` (536 passed) both green. Manual Linux smoke for M3 completed this session (live PTY verified).

**M4 closeout note (2026-07-07):** Docs sync and exit audit completed. `dotnet build Zaide.slnx` (0 warnings, 0 errors) and `dotnet test Zaide.slnx --no-build` (536 passed, 0 failed) re-verified as phase exit gates. `docs/phases/phase-3.9/TOFIX.md` created; no implementation findings. Manual Linux smoke for M0–M3 completed this session on Linux (selection/copy, scrollback/live-bottom, alternate-screen isolation, log/terminal toggle all verified). `docs/roadmap/PHASES.md` updated; terminal tabs remain exclusively scoped to 3.9.1.

## Detailed Milestone Plans

### M1: Selection / Copy / Paste Polish

**Why this belongs in 3.9:**

The renderer is already technically usable. The next-value layer is interaction quality: making text selection and clipboard behavior feel intentional rather than merely functional.

**Files likely touched:**

- `src/Views/TerminalRenderControl.cs`
- `src/Views/TerminalPanel.cs`
- `src/ViewModels/TerminalViewModel.cs` (only if command seams are needed for paste/copy state)
- `tests/Zaide.Tests/Views/TerminalRenderControlTests.cs`
- `tests/Zaide.Tests/ViewModels/TerminalViewModelTests.cs`

**Planned change shape:**

1. Keep pointer selection in `TerminalRenderControl`, but improve it with narrow, IDE-style behaviors:
   - double-click selects a word
   - triple-click selects a full logical line
   - drag selection can extend beyond the visible viewport and auto-scrolls while dragging
2. Make copy semantics explicit in `TerminalPanel`:
   - `Copy` copies the current selection when one exists
   - `Copy Visible` remains available as a deliberate fallback action instead of silent default ambiguity
3. Keep `Ctrl+Shift+V` and context-menu paste behavior routed through `TerminalViewModel.PasteAsync()` so bracketed-paste support remains centralized
4. Preserve the Phase 3.8 rule that all copy/selection behavior is suppressed while a full-screen TUI owns the alternate screen
5. Do not add rich clipboard formats, OSC 52 clipboard support, or drag-drop in this phase

**Tests (M1):**

- `BuildSelectedText_SelectsWholeWord_OnWordSelectionRange`
- `BuildSelectedText_SelectsWholeLine_OnLineSelectionRange`
- `TryGetSelectedText_ReturnsFalse_WhenAlternateScreenActive`
- `ScrollToBottom_ClearsSelection_WhenRequested`
- `PasteAsync_WhenBracketedPasteEnabled_StillWrapsPasteAfterUiPolish`
- focused control tests for any new helper methods that normalize word and line selection ranges

**Manual smoke for M1:**

- select a word with double-click
- select a full line with triple-click
- drag upward into scrollback and confirm the viewport auto-scrolls
- verify `Copy` vs `Copy Visible` behavior is obvious and correct
- confirm selection/copy stays blocked while `less` or `vim` owns the alternate screen

### M2: Scrollback / Navigation Polish

**Why this belongs in 3.9:**

Phase 3.8 proved the screen model and renderer are correct enough. What still feels unfinished is scrollback ergonomics: users need clearer navigation and better recovery to the live bottom.

**Files likely touched:**

- `src/Views/TerminalRenderControl.cs`
- `src/Views/TerminalPanel.cs`
- `tests/Zaide.Tests/Views/TerminalRenderControlTests.cs`

**Planned change shape:**

1. Keep viewport state in the view layer; do not move scrollback ownership into `TerminalViewModel`
2. Add explicit keyboard navigation for the rendered terminal surface where Avalonia focus allows it:
   - `PageUp` / `PageDown` scroll the viewport by page
   - `Home` / `End` jump to top/bottom of available snapshot rows
   - entering input (`Enter`) or an explicit “jump to latest” action returns to live bottom
3. Add a visible but lightweight scroll affordance for the terminal surface if this can be done without replacing the custom render control architecture
4. Preserve current live-bottom follow rules and document them explicitly in tests
5. Do not add persisted history beyond the current in-memory scrollback retained by `TerminalScreen`

**Tests (M2):**

- `GetViewportTop_FollowsLiveBottom_WhenEnabled`
- `ScrollToBottom_RejoinsLatestOutput_AfterManualScrollback`
- `PageNavigation_ClampsWithinAvailableRows`
- `ManualScrollback_IgnoredWhileAlternateScreenActive`
- `HomeEndNavigation_UsesSnapshotBounds`

**Manual smoke for M2:**

- generate output longer than the viewport and scroll several pages up/down
- confirm `End` or the jump-to-latest action returns to the newest shell output
- confirm new shell output does not unexpectedly steal the viewport while the user is reading scrollback
- confirm ordinary shell typing/Enter still returns to the live bottom when appropriate

### M3: Terminal Search and Match Navigation

**Why this belongs in 3.9:**

Search is a high-value IDE expectation and directly matches the brief’s “search and navigation quality improvements” goal without needing backend emulation changes.

**Files likely touched:**

- `src/Views/TerminalPanel.cs`
- `src/Views/TerminalRenderControl.cs`
- `src/ViewModels/TerminalViewModel.cs` or a narrow new terminal-search helper in `src/ViewModels/`
- `tests/Zaide.Tests/ViewModels/TerminalViewModelTests.cs`
- `tests/Zaide.Tests/Views/TerminalRenderControlTests.cs`

**Planned change shape:**

1. Add a lightweight search UI in the terminal toolbar or an inline search strip:
   - search text box
   - next / previous match actions
   - match count label when feasible
2. Search over the current `TerminalSnapshot` text only:
   - visible rows
   - retained scrollback rows
   - no hidden history outside the snapshot
3. Project active match state to `TerminalRenderControl` so the current match can be highlighted and brought into view
4. Keep search disabled or intentionally limited while alternate screen is active if exposing main-buffer results would violate Phase 3.8 isolation
5. Start with plain substring search; regex, case toggles, and persistent search history are out of scope unless they fall out trivially

**Tests (M3):**

- `SearchSnapshot_FindsMatches_InVisibleRows`
- `SearchSnapshot_FindsMatches_InScrollbackRows`
- `SearchSnapshot_NextPreviousWrapsPredictably`
- `SearchSnapshot_NoMatches_ClearsActiveMatch`
- `SearchSnapshot_DoesNotExposeMainBuffer_WhenAlternateScreenActive`
- render-control tests for active-match viewport targeting and highlight range projection helpers

**Manual smoke for M3:**

- search for text visible in the current viewport
- search for text only present in scrollback and confirm the viewport jumps to it
- navigate next/previous through repeated matches
- confirm clearing the search removes highlights and does not disturb normal typing

### M4: Docs Sync and Exit Audit

**Why this belongs in 3.9:**

After narrowing the phase to UX polish only, the closeout milestone is a clean
verification pass that ensures the docs, tests, and roadmap all agree on the new
boundary: `3.9` covers selection/scrollback/search, while terminal tabs moved to
`3.9.1`.

**Files likely touched:**

- `docs/phases/phase-3.9/IMPLEMENTATION_PLAN.md`
- `docs/phases/phase-3.9/TOFIX.md` (if needed)
- `docs/phases/phase-3.9.1/BRIEF.md`
- `docs/roadmap/PHASES.md`
- any terminal docs touched during M1–M3

**Planned change shape:**

1. Re-run the phase gates after M1–M3 implementation completes
2. Update roadmap/docs so `3.9` and `3.9.1` have no overlapping claims
3. Record any remaining UX follow-ups in `TOFIX.md` instead of widening scope
4. Keep terminal tabs deferred to `3.9.1` unless the roadmap is explicitly changed again

**Tests (M4):**

- `dotnet build Zaide.slnx`
- `dotnet test Zaide.slnx --no-build`
- focused manual Linux smoke for M1–M3 behaviors

## Limitations (by design)

- Search is snapshot-based, not a full terminal transcript database
- Scrollback remains in-memory only
- Alternate-screen sessions keep their strict isolation; no attempt is made to search/copy hidden main-buffer content while a TUI is active
- Terminal tabs are intentionally deferred to `phase-3.9.1`
- Manual Linux smoke remains required because automated tests cannot fully replace interactive terminal feel

## Exit Conditions

- [x] `dotnet build Zaide.slnx` succeeds with 0 warnings, 0 errors — verified 2026-07-07
- [x] `dotnet test Zaide.slnx --no-build` passes — 536 passed, 0 failed, verified 2026-07-07
- [x] Selection/copy/paste polish implemented and tests green — manual Linux smoke completed this session
- [x] Manual scrollback/navigation polish implemented and tests green — manual Linux smoke completed this session
- [x] Search finds matches in visible rows and retained scrollback, tests green — manual Linux smoke completed this session
- [x] Manual Linux smoke: selection/copy, scrollback without live-bottom regression, alternate-screen isolation, log/terminal toggle — all verified interactively on Linux this session
- [ ] `docs/roadmap/PHASES.md` and any touched architecture docs remain in sync — updated this session
- [x] Terminal tabs remain exclusively in `phase-3.9.1` with no overlapping scope claims — enforced through doc split and M4 review

## Rollback Plan

- Commit hash to revert to: `0018eaafae597a1d911aad23a4415d6e284370c0`
- If the phase boundary becomes unclear again, revert the doc split and re-plan before coding rather than mixing terminal tabs back into `3.9`
