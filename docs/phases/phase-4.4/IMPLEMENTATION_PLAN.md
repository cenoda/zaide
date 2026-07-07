# Phase 4.4: Phase 4 Docs Sync and Exit Audit — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm Phase 4.1, 4.2, and 4.3 are all complete (see their exit conditions)
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx`

## Planning Status

**Draft — depends on 4.1, 4.2, and 4.3.**

This sub-phase does not add features. It closes out Phase 4 by verifying the
umbrella's exit conditions are genuinely met and syncing all docs that
describe Phase 4's status. See `docs/phases/phase-4/IMPLEMENTATION_PLAN.md`
for the umbrella exit conditions this sub-phase must satisfy.

## Scope

**Goal:** Verify Phase 4 as a whole is behaviorally complete, and bring every
doc that references Phase 4's status back into sync with reality.

**In scope:**

- Full regression pass: `dotnet build`, `dotnet test`, manual Townhall smoke
  check (send message, switch channel, verify auto-logged entries, verify
  filter behavior)
- Update `docs/roadmap/PHASES.md`:
  - Mark Phase 4's checklist items complete
  - Update the Phase 5 entry condition reference if its wording needs
    adjusting now that Phase 4 is behaviorally real
- Update `docs/architecture/OVERVIEW.md`:
  - Move "Agent Workspace Foundations" from "Planned layers" to the
    implemented-layers table
  - Update the "Current Layout Scaffold" section if the activity-entry
    model changed anything structurally worth describing
  - Record the 4.1 agent-format decision in "Future Technical
    Considerations" or promote it out of that table if it's no longer
    purely aspirational
- Update `README.md` if it references Townhall/Phase 4 status
- Record final status/date on the Phase 4 umbrella doc

**Out of scope:**

- Any new behavior, model, or UI change (all prior sub-phases own that)
- Starting Phase 5 (agent panels) work

## Milestones

| Milestone | Description | Test | Status |
|-----------|-------------|------|--------|
| M0 | Entry gate: confirm 4.1–4.3 exit conditions are actually met, not just marked done | Manual review against each sub-phase's exit conditions | ⬜ |
| M1 | Full regression sweep | `dotnet build`, `dotnet test`, manual Townhall smoke check | ⬜ |
| M2 | Docs sync: `PHASES.md`, `OVERVIEW.md`, `README.md` | Diff review against actual implemented behavior | ⬜ |
| M3 | Phase 4 umbrella marked complete with date | — | ⬜ |

## Exit Conditions

- [ ] All of 4.1, 4.2, and 4.3's individual exit conditions are verified true against live code, not just checked off historically
- [ ] `dotnet build` (0 warnings) and `dotnet test` pass
- [ ] Manual Townhall smoke check confirms: real session state (no sample-only behavior), auto-logged entries on send/switch, visual chat-vs-activity distinction, working filter, correct scrolling
- [ ] `docs/roadmap/PHASES.md`, `docs/architecture/OVERVIEW.md`, and `README.md` all match implemented Phase 4 state
- [ ] `docs/phases/phase-4/IMPLEMENTATION_PLAN.md` marked complete with a completion date
