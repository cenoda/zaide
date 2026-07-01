# Refactor 3 Plan Audit + Remediation TODO

## Scope
- Target file: `docs/refactor/refactor-3/IMPLEMENTATION_PLAN.md`
- User direction: skip testing and modify the plan based on audit findings.

## Steps
- [x] Read Refactor 3 implementation plan
- [x] Produce audit findings
- [x] Confirm testing preference with user
- [x] Apply plan corrections in IMPLEMENTATION_PLAN.md:
  - [x] Unify Townhall model naming (`TownhallMessage`, `WorkspaceAgent`) across all sections
  - [x] Resolve left-panel behavior contradiction (single-slot nav mode switching)
  - [x] Clarify layout ownership (`MainWindow.axaml` structure, `.axaml.cs` wiring only)
  - [x] Add explicit grid/span contract for bottom terminal area under center+right
  - [x] Add milestone-level testing expectations (M1–M5)
  - [x] Make “editor quieter” guidance objective via token/contrast constraints
- [x] Re-read updated plan for internal consistency
- [ ] Finalize and report completion
