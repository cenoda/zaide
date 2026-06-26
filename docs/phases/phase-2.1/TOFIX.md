# Phase 2.1: TOFIX

Code quality issues found during Phase 2.1 review. Check these before resuming
Phase 2.1 or starting Phase 3.

---

## Verified

### Pre-implementation gate (YYYY-MM-DD)
- [ ] `dotnet build Zaide.slnx` passes with 0 warnings
- [x] `dotnet test Zaide.slnx` passes: 69 tests, 0 failures (2026-06-26)

---

## Open

- [ ] Re-approach indent guides from first principles. The Phase 2.1 `IBackgroundRenderer`
  attempt was reverted on 2026-06-26 because guide placement did not match the
  editor's actual indentation reliably enough to ship.
- [x] Verify whether a minimal live prototype still exists in the repo.
  `src/Views/SpikeIndentGuideRenderer.cs` is still present and `EditorView`
  still enables it as of 2026-06-26.
- [ ] Decide whether to remove the active spike and return to a true baseline,
  or adopt the spike as the official M2 checkpoint for M3 work.
- [ ] Verify the current spike visually in a real `.cs` file before changing the
  renderer path again. Visual correctness in `.cs` files is the gate, not just
  build/test success.
- [ ] Define the M3 "first guide" rule in writing before implementation:
  at least one full indent level, plus explicit behavior for blank lines and
  mixed tab/space indentation.

---

## Phase 2 Carry-Over

_None._
