# Phase 22.1: Language Intelligence / LSP — TOFIX

## Status

**Phase 22.1 complete (M0–M4).** A4 package 1 and `A1-FN-09`…`A1-FN-13` re-smoked
with current evidence under `docs/phases/v3/phase-22.1/evidence/`.

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

## Next Task

None for Phase 22.1. Phase 22.2+ and V4 planning remain out of scope here.
