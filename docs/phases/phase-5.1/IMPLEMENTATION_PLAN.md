# Phase 5.1: Agent Panel State and Host Seam — Umbrella Plan

## Implementation Status

**Implemented (2026-07-08).**

All three Phase 5.1 sub-phases are complete. The umbrella split was necessary
because the first draft bundled three different kinds of work:

- deciding the state shape for a single agent panel
- designing and implementing the multi-panel host seam
- exposing that seam through main-window composition

That is still too much architectural weight for one uninterrupted pass. Phase
5.1 is now an umbrella. Each sub-phase gets its own
`docs/phases/phase-5.1.x/IMPLEMENTATION_PLAN.md` and should stay narrow.

## Goal

Lock in the minimum state and composition seams needed for agent panels without
widening into full UI work, provider execution, or Townhall mirroring.

## Why Split 5.1 Again

Phase 5.1 carries high decision weight even though it should remain a small
implementation slice. If host ownership, panel state shape, and shell exposure
all move at once, later Phase 5 work is likely to churn.

The split below is meant to reduce that risk:

- `5.1.1` decides and implements the smallest useful single-panel state shape
- `5.1.2` introduces the dedicated multi-panel host seam
- `5.1.3` exposes that seam through `MainWindowViewModel`/DI and closes the
  Phase 5.1 exit audit

## Sub-Phases

| Sub-phase | Scope | Status |
|-----------|-------|--------|
| [5.1.1](../phase-5.1.1/IMPLEMENTATION_PLAN.md) | Single-panel state shape + host ownership decision | Implemented |
| [5.1.2](../phase-5.1.2/IMPLEMENTATION_PLAN.md) | Multi-panel host seam + seeded panel collection/selection behavior | Implemented |
| [5.1.3](../phase-5.1.3/IMPLEMENTATION_PLAN.md) | `MainWindowViewModel`/DI composition seam + Phase 5.1 exit audit | Implemented |

## Out of Scope (all 5.1.x sub-phases)

- Final agent-panel UI surfaces (`phase-5.2`)
- Real provider calls or endpoint integration (`phase-5.3`)
- Townhall mirroring for direct-agent interactions (`phase-5.4`)
- Agent-to-agent routing or `@mention` semantics (Phase 6)
- Persistence architecture or transcript storage
- Shell redesign beyond the smallest seam exposure needed for later phases

## Phase 5.1 Exit Conditions

These are only satisfied once 5.1.1 through 5.1.3 are all complete:

- [x] A minimal single-agent panel state shape exists and is covered by tests (`AgentPanelState.cs`, 5.1.1)
- [x] A dedicated host seam exists for a collection of agent panels and active-panel selection (`IAgentPanelHost`/`AgentPanelHost`, 5.1.2)
- [x] `MainWindowViewModel` composes the host seam without moving host logic into code-behind (5.1.3 M1)
- [x] The Phase 5 shell exposure point is decided without committing Phase 5.2 UI details too early (5.1.3 M0)
- [x] `dotnet build Zaide.slnx` passes
- [x] Tests for the new state/host/composition seams pass

## Exact Next Step

Phase 5.1 is complete. The next phase is `docs/phases/phase-5.2/IMPLEMENTATION_PLAN.md`
which will introduce agent-panel UI surfaces in the existing right-side shell column.

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
