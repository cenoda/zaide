# Phase 2.1: TOFIX

Code quality issues found during Phase 2.1 review. Check these before resuming
Phase 2.1 or starting Phase 3.

---

## Verified

### Pre-implementation gate (YYYY-MM-DD)
- [x] `dotnet build Zaide.slnx` passes with 0 warnings (2026-06-27)
- [x] `dotnet test Zaide.slnx` passes: 79 tests, 0 failures (2026-06-27)

---

## Open

- [x] Re-approach indent guides from first principles.
  The reverted attempt was replaced by an M3 renderer plus pure helper logic on
  2026-06-26, but the live-editor visual gate still remains open.
- [x] Verify whether a minimal live prototype still exists in the repo.
  That spike existed during the audit and was used as the basis for M3.
- [x] Decide whether to remove the active spike and return to a true baseline,
  or adopt the spike as the official M2 checkpoint for M3 work.
  M3 adopted the spike and replaced it with `IndentGuideRenderer`.
- [x] Define the M3 "first guide" rule in writing before implementation:
  draw one guide only for lines that reach at least one full indent level;
  blank lines do not render a guide in M3.
- [x] Verify the new M3 renderer visually in a real `.cs` file.
  Screenshot review on 2026-06-27 confirmed the first guide is visible and
  looks aligned well enough for M3 across spaces, tabs, mixed indentation, and
  deeper nested blocks.
- [x] Re-check mixed tabs/spaces in the live editor and document any remaining
  limitation before moving to M4.
  No obvious M3-blocking misalignment was found in the manual sample review.

---

## Phase 2 Carry-Over

_None._
