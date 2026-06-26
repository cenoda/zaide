# Phase 2: TOFIX

Code quality issues found during Phase 2 review. Check these before starting Phase 3.

---

## Verified

### Pre-implementation gate (2026-06-26)
- [x] `dotnet build Zaide.slnx` passes with 0 warnings
- [x] `dotnet test Zaide.slnx` passes: 22 tests, 0 failures
- [x] Phase 1 TOFIX.md items reviewed — one open item ("Hardcoded colors") deferred to Phase 3/5

### Post-implementation (YYYY-MM-DD)
- [ ] `dotnet build Zaide.slnx`: 0 warnings, 0 errors
- [ ] `dotnet test Zaide.slnx`: N passed, 0 failed
- [ ] Phase 1 → Phase 2 exit conditions all met

---

## Open

_No issues yet._

---

## Phase 1 Carry-Over

### [ ] Hardcoded colors in `MainWindow.axaml.cs` — agent area + bottom (deferred)
Right agent area and bottom panel still use `BuildPanel` with `Color.Parse`.
Deferred to Phase 5 (agent) and Phase 3 (terminal).