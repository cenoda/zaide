# Phase 1: TOFIX

Code quality issues found during Phase 1 review. Check these before starting Phase 2.

---

## Verified

### Pre-implementation gate (2025-06-25)
- [x] `dotnet build Zaide.slnx` passes with 0 warnings
- [x] `dotnet test Zaide.slnx` passes: 2 tests, 0 failures
- [x] Phase 0 TOFIX.md items are all resolved

### Post-implementation (2025-06-26)
- [x] `dotnet build Zaide.slnx`: 0 warnings, 0 errors
- [x] `dotnet test Zaide.slnx`: 22 passed, 0 failed
- [x] Phase 0 → Phase 1 exit conditions all met

---

## Open

_No issues yet._

---

## Phase 0 Carry-Over

### [x] Hardcoded colors in `MainWindow.axaml.cs` — sidebar + center (Phase 1)
Sidebar (`FileTreeView`) and center panel now use `App.axaml` resources.

### [x] Hardcoded colors in `MainWindow.axaml.cs` — agent area + bottom
Right agent area, bottom panel, and grid background now use `App.axaml`
resources. See Phase 2 TOFIX for details.
