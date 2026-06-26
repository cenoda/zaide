# Phase 2: TOFIX

Code quality issues found during Phase 2 review. Check these before starting Phase 3.

---

## Verified

### Pre-implementation gate (2026-06-26)
- [ ] `dotnet build Zaide.slnx` passes with 0 warnings
- [ ] `dotnet test Zaide.slnx` passes: N tests, 0 failures
- [ ] Phase 1 TOFIX.md items are all resolved

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