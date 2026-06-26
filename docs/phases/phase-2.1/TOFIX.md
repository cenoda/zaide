# Phase 2.1: TOFIX

Code quality issues found during Phase 2.1 review. Check these before starting Phase 3.

---

## Verified

### Pre-implementation gate (YYYY-MM-DD)
- [ ] `dotnet build Zaide.slnx` passes with 0 warnings
- [ ] `dotnet test Zaide.slnx` passes: 69 tests, 0 failures

---

## Open

- [ ] Re-approach indent guides from first principles. The Phase 2.1 `IBackgroundRenderer`
  attempt was reverted on 2026-06-26 because guide placement did not match the
  editor's actual indentation reliably enough to ship.
- [ ] Verify a replacement approach with a minimal live prototype before touching
  `EditorView` again. Visual correctness in `.cs` files is the gate, not just
  build/test success.

---

## Phase 2 Carry-Over

_None._
