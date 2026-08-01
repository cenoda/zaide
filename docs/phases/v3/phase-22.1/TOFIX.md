# Phase 22.1: Language Intelligence / LSP — TOFIX

## Status

**Phase 22.1 complete (M0–M4) with post-closeout UI responsiveness hot fix.**
A4 package 1 and `A1-FN-09`…`A1-FN-13` re-smoked with evidence under
`docs/phases/v3/phase-22.1/evidence/`. Post-M4 regression: blocking
`Invoke` projection stalled the UI under scroll/caret activity; fixed via
non-blocking `Post` + latest-wins coalescing (see below).

## Work Board

- [x] Draft package/goal ownership, milestones, and A3 re-smoke boundary.
- [x] Verify live completion, hover, navigation, symbol, DI, editor, and test
  seams in M0.
- [x] Record exact focused filters, A3 producer path/command, rollback
  boundaries, and current seam names.
- [x] Receive and record explicit human G2 / M0 acceptance.
- [x] M1 — completion UI dispatch/projection on `IEditorUiDispatcher`.
- [x] M2 — hover UI dispatch/projection.
- [x] M3 — definition timeout + navigation/symbol dispatch/projection.
- [x] M4 — regression gates and `A1-FN-09`…`A1-FN-13` re-smoke.
- [x] Post-M4 hot fix — non-blocking projection (`Post` + latest-wins
  coalesce); keep `Invoke` for synchronous reconciler callers.

## M0 Findings

- The five A3 failures shared an unscheduled synchronous-observer family across
  distinct lifecycle points; definition additionally had no request timeout or
  terminalization deadline. Detailed traces are in
  [M0_SEAM_VERIFICATION.md](./M0_SEAM_VERIFICATION.md).
- Production already registers Avalonia `IScheduler`, but the raw language
  streams reached `EditorLanguageInputViewModel` and `EditorView` without an
  `ObserveOn`/dispatcher boundary.
- Hover could fault while publishing `Loading` after its dwell delay, before the
  LSP request begins. Completion, definition, and document/workspace symbol
  terminal results published after `ConfigureAwait(false)` and reached UI-owned
  projection off-thread.
- Existing service tests protect cancellation, active-document, generation,
  document-version, capability, failure, and stale-result contracts, but do not
  compose the live editor UI projection under Avalonia scheduling.
- M1-M3 remain separate because their trigger, navigation, query, presentation,
  regression, and rollback contracts differ despite the shared failure family.
- The retained Phase 10 smoke tools omit `EditorView`; the temporary A3 runner
  was recreated out-of-tree for M4 at `/tmp/zaide-a3-lang/`.

## Closeout

- M1–M3: `EditorLanguageUiProjection` marshals completion, hover, navigation,
  and symbol snapshots through `IEditorUiDispatcher`; definition requests use a
  30s `LanguageNavigationPolicy.RequestTimeout`.
- M3 follow-up: `IEditorUiDispatcher` is public for MS DI resolution;
  `MainLayoutBuilder` receives the dispatcher via constructor injection (no
  `CompositionRoot` service locator).
- M4: build, focused filters, fast suite (3744 passed), and out-of-tree A3
  re-smoke for all five scenarios passed. Evidence:
  `docs/phases/v3/phase-22.1/evidence/A1-FN-09.json` … `A1-FN-13.json`.

## Post-M4 regression — UI slow-motion after scroll / language activity

**Symptom:** After Phase 22.1 M1–M3 marshaling landed, the editor could feel
slow-motion under rapid scroll/caret and language snapshot traffic.

**Root cause:** `EditorLanguageUiProjection` used `IEditorUiDispatcher.Invoke`
→ `Dispatcher.UIThread.Invoke` (synchronous). Hover Idle on caret churn and
Ready snapshots after `ConfigureAwait(false)` blocked publishers and could
backlog the UI thread. Terminal/tab code already preferred non-blocking
`Post` for similar UI work.

**Fix (keep A1-FN-09…13 thread-affinity correctness):**

1. `IEditorUiDispatcher.Post(Action)` — non-blocking marshal path.
2. `AvaloniaEditorUiDispatcher.Post` → always `Dispatcher.UIThread.Post`
   (never block the publisher; always queue so coalescing can batch).
3. `EditorLanguageUiProjection` uses `Post` with per-subscription latest-wins
   coalescing (one pending item; apply only the newest).
4. `Invoke` retained for callers that need synchronous UI work
   (`WorkspaceEditorDocumentReconciler`).
5. Optional: `LanguageHoverService` skips redundant Idle→Idle publishes.
6. Tests: projection Post + coalesce; `SynchronousEditorUiDispatcher.Post`
   runs inline for unit tests.

**Verification:** build `Zaide.slnx`; focused projection + language
routing/completion/hover/nav/symbol filters (77 passed); fast suite
3748 passed. Out-of-tree A3 re-smoke of `A1-FN-09`…`A1-FN-13` all WORKS
(evidence written only under `/tmp/zaide-a3-lang/evidence/`; historical
`docs/.../evidence/` files not rewritten). See
[UI_PROJECTION_POST_COALESCE.md](./UI_PROJECTION_POST_COALESCE.md).

## Next Task

None for Phase 22.1. Phase 22.2+ and V4 planning remain out of scope here.
