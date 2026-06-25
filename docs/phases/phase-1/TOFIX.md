# Phase 1: TOFIX

Code quality issues found during Phase 1 review. Check these before starting Phase 2.

---

## Verified

### Pre-implementation gate (2025-06-25)
- [x] `dotnet build Zaide.slnx` passes with 0 warnings
- [x] `dotnet test Zaide.slnx` passes: 2 tests, 0 failures
- [x] Phase 0 TOFIX.md items are all resolved

---

## Open

_No issues yet._

---

## Phase 0 Carry-Over (addressed during Phase 1)

### [ ] Hardcoded colors in `MainWindow.axaml.cs` `BuildPanel`
Phase 0 placeholder panels use `Color.Parse("#...")` instead of `App.axaml` resources.
Phase 1 M3 replaces the sidebar panel; M4 replaces the center panel.
Right agent area and bottom panel remain as Phase 0 placeholders — defer to their respective phases.
