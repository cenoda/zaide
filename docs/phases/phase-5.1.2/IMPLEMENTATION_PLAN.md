# Phase 5.1.2: Multi-Panel Host Seam — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm Phase 5.1.1 is complete
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx --no-build`
- [ ] Re-check the implemented single-panel types from Phase 5.1.1
- [ ] Re-check `src/ViewModels/ITerminalHost.cs` and `src/ViewModels/TerminalHost.cs` as the live host-pattern precedent

## Scope

**Goal:** Introduce a dedicated host seam that owns a collection of agent panels and active-panel selection, without widening into real UI or execution work.

**In scope:**

- Host interface/class for agent panels
- Seeded panel collection behavior
- Active-panel selection behavior
- Minimal create/show/hide behavior if required by the host contract
- DI registration if the host is introduced in this slice

**Out of scope:**

- Rendering the panel surfaces
- Real provider execution
- Townhall logging side effects
- Routing between panels or agents
- Final shell placement details

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Define the minimal host contract (`Tabs`/`Panels`, active item, activation command shape) | Design note + build |
| M1 | Implement the host seam with seeded panel collection behavior | Host tests |
| M2 | Add active-panel selection/lifecycle behavior | Host tests + `dotnet test Zaide.slnx --no-build` |

## Limitations (by design)

- Panel collection may start from a fixed seeded set
- No dynamic plugin/registry discovery
- No execution state beyond what the single-panel shape already exposes
- No view-layer caching yet unless Phase 5.2 proves it necessary

## Exit Conditions

- [ ] A dedicated agent-panel host seam exists in code
- [ ] The host can represent multiple panels and an active panel
- [ ] Seeded panel collection and selection behavior are covered by tests
- [ ] `dotnet build Zaide.slnx` passes
- [ ] Focused host tests pass

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
