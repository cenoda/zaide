# Phase 2.1: Revert Log

## What Was Reverted

- **Reverted from:** working tree only (Phase 2.1 was not committed)
- **Intended revert target:** Phase 2 baseline without indent-guide implementation
- **Commits discarded:** none
- **Files removed:**
  - `src/Views/IndentGuideRenderer.cs`
  - `tests/Zaide.Tests/Views/IndentGuideRendererTests.cs`
- **Files modified during revert attempt:**
  - `src/Views/EditorView.cs`
  - `docs/phases/phase-2.1/TOFIX.md`

## Post-Revert Audit (2026-06-26)

A later live-code audit found that the repository did not fully land at the
intended revert target.

- `src/Views/SpikeIndentGuideRenderer.cs` still exists
- `src/Views/EditorView.cs` still enables the spike renderer
- The current tree therefore contains an active M2 experiment path even though
  the earlier production `IndentGuideRenderer.cs` was removed

This matters because future M3 work should start from the actual repo state,
not from the assumed clean baseline described in the original revert summary.

## Follow-Up Implementation (2026-06-26)

The active spike identified in the post-revert audit has now been replaced.

- `src/Views/SpikeIndentGuideRenderer.cs` was removed
- `src/Views/IndentGuideRenderer.cs` now implements the M3 first-guide path
- `EditorView` enables the renderer only for the current C# experiment path
- Focused helper tests were added for the pure indentation logic

The original root-cause notes still stand: M3 is not done until the live editor
result is visually trustworthy.

## Root Cause

The implementation did not reach the bar for visual correctness. This was not a
case where a small follow-up patch would make the feature acceptable; the core
problem was that the approach never produced trustworthy guide placement in the
editor.

1. The renderer logic was implemented and tested at the helper level, but the
   actual UI result was still wrong. Build/test success did not prove the
   feature worked in the only place that mattered: the live editor surface.
2. The implementation focused too early on patching calculations inside
   `IndentGuideRenderer` without first locking down a minimal visual proof that
   AvaloniaEdit would expose stable coordinates for this use case.
3. The phase exit condition that mattered most, "indent guides look correct in
   real files," was not satisfied, so leaving the code in place would make the
   repository look healthier than it actually was.

## Rules Added

No new global rules were added. The failure is recorded in the phase-local
`TOFIX.md` so the next attempt starts with an explicit visual-verification gate.

## Revert Commit

Not committed. Phase 2.1 was reverted directly in the working tree on
2026-06-26.
