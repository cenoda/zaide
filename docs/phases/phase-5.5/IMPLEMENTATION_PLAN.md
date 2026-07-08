# Phase 5.5: Docs Sync and Exit Audit — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm Phase 5.1.1 through 5.4 are complete
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx`
- [ ] Re-check the live code paths touched by Phase 5 rather than relying on prior plan wording

## Scope

**Goal:** Verify that Phase 5 is actually complete in live code and update the root docs to match the implemented result.

**In scope:**

- Re-verify Phase 5 against live code
- Final regression pass
- Manual smoke for panel rendering, direct execution, and Townhall visibility
- Update `docs/roadmap/PHASES.md`, `docs/architecture/OVERVIEW.md`, and `README.md`
- Mark the Phase 5 umbrella and 5.1 umbrella accurately

**Out of scope:**

- New Phase 5 behavior
- Starting Phase 6 routing work
- Retconning unimplemented design claims into root docs

## Audit Rule

Phase 5.5 must verify the final state from the source files and test results, not
from earlier plan documents. If a prior plan claimed something that did not land,
the docs must reflect the real implementation rather than the original intention.

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Re-check 5.1.1 through 5.4 exit conditions against live code | Repo review |
| M1 | Run regression and manual smoke for the final Phase 5 behavior | `dotnet build Zaide.slnx`, `dotnet test Zaide.slnx --no-build`, manual smoke |
| M2 | Sync root docs and umbrella docs to the implemented Phase 5 state | Diff review |
| M3 | Close the final Phase 5 exit audit with concrete evidence | Final checklist review |

## Manual Smoke Checklist

At minimum, manual smoke should cover:

- panel rendering in the live shell
- panel switching if multiple panels are present
- direct panel input
- one successful real request path if valid configuration is available
- visible failure behavior when configuration is missing or a request fails
- Townhall visibility for direct-agent interactions
- shell sanity after resize/open/close flows touched by Phase 5

## Exit Conditions

- [ ] Phase 5 behavior is verified against live code, not just plan checkboxes
- [ ] `dotnet build Zaide.slnx` passes
- [ ] `dotnet test Zaide.slnx --no-build` passes
- [ ] Manual smoke confirms panel rendering, direct execution, visible failure behavior, and Townhall visibility
- [ ] `docs/roadmap/PHASES.md`, `docs/architecture/OVERVIEW.md`, and `README.md` match the implemented Phase 5 state
- [ ] `docs/phases/phase-5/IMPLEMENTATION_PLAN.md` and `docs/phases/phase-5.1/IMPLEMENTATION_PLAN.md` accurately reflect the final umbrella/sub-phase status

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
