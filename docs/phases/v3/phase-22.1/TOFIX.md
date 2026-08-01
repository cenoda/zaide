# Phase 22.1: Language Intelligence / LSP — TOFIX

## Status

**M1 complete; M2–M4 in progress.** A4 package 1 and `A1-FN-09`…`A1-FN-13` are
assigned here. M0 accepted at `8856bdf7`.

## Work Board

- [x] Draft package/goal ownership, milestones, and A3 re-smoke boundary.
- [x] Verify live completion, hover, navigation, symbol, DI, editor, and test
  seams in M0.
- [x] Record exact focused filters, A3 producer path/command, rollback
  boundaries, and current seam names.
- [x] Receive and record explicit human G2 / M0 acceptance.
- [x] M1 — completion UI dispatch/projection on `IEditorUiDispatcher`.
- [ ] M2 — hover UI dispatch/projection.
- [ ] M3 — definition timeout + navigation/symbol dispatch/projection.
- [ ] M4 — regression gates and `A1-FN-09`…`A1-FN-13` re-smoke.

## M0 Findings

- The five A3 failures share an unscheduled synchronous-observer family across
  distinct lifecycle points; definition additionally has no request timeout or
  terminalization deadline. Detailed traces are in
  [M0_SEAM_VERIFICATION.md](./M0_SEAM_VERIFICATION.md).
- Production already registers Avalonia `IScheduler`, but the raw language
  streams reach `EditorLanguageInputViewModel` and `EditorView` without an
  `ObserveOn`/dispatcher boundary.
- Hover can fault while publishing `Loading` after its dwell delay, before the
  LSP request begins. Completion, definition, and document/workspace symbol
  terminal results publish after `ConfigureAwait(false)` and reach UI-owned
  projection off-thread.
- Existing service tests protect cancellation, active-document, generation,
  document-version, capability, failure, and stale-result contracts, but do not
  compose the live editor UI projection under Avalonia scheduling.
- M1-M3 remain separate because their trigger, navigation, query, presentation,
  regression, and rollback contracts differ despite the shared failure family.
- The retained Phase 10 smoke tools omit `EditorView`; the temporary A3 runner
  is absent and must be recreated out-of-tree for M4. No A3 re-smoke ran in M0.

## Next Task

Implement M2 hover UI dispatch/projection and its focused regression tests.
