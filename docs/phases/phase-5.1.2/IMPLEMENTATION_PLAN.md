# Phase 5.1.2: Multi-Panel Host Seam — Implementation Plan

## Pre-Implementation Verification

- [x] Confirm Phase 5.1.1 is complete
- [x] Verify current build succeeds: `dotnet build Zaide.slnx`
- [x] Verify current tests pass: `dotnet test Zaide.slnx --no-build`
- [x] Re-check the implemented single-panel types from Phase 5.1.1
- [x] Re-check `src/ViewModels/ITerminalHost.cs` and `src/ViewModels/TerminalHost.cs` as the live host-pattern precedent

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

## Host Ownership Decision

This decision is locked for Phase 5.1.2 and later slices unless live implementation proves a concrete blocker, mirroring the existing `ITerminalHost`/`TerminalHost` precedent:

1. **The agent-panel host owns panel collection and active selection.**
   Multi-panel collection/selection/lifecycle logic lives in the dedicated host seam, identical to how the terminal host owns terminal tabs and active session.

2. **Agent-panel collection/selection state stays out of `MainWindowViewModel` as direct state.**
   `MainWindowViewModel` may compose the injected host seam; it should not become the host.

3. **Retained view-only state stays in the view layer if needed later.**
   If Phase 5.2 later requires retained per-panel visual state (e.g. scroll position, expanded sections), that state belongs in the view layer rather than the ViewModel seam.

## Milestones

| Milestone | Description | Test | Status |
|-----------|-------------|------|--------|
| M0 | Define the minimal host contract (`Panels`, `ActivePanel`, `CreatePanel`, `ActivatePanel`) | `IAgentPanelHost.cs` + build | Done |
| M1 | Implement the host seam with panel collection ownership | `AgentPanelHost.cs` + host tests | Done |
| M2 | Add active-panel selection/lifecycle behavior | Host tests + `dotnet test Zaide.slnx --no-build` | Done |

## Limitations (by design)

- Panel collection may start from a fixed seeded set
- No dynamic plugin/registry discovery
- No execution state beyond what the single-panel shape already exposes
- No view-layer caching yet unless Phase 5.2 proves it necessary

## Exit Conditions

- [x] A dedicated agent-panel host seam exists in code (`IAgentPanelHost` + `AgentPanelHost`)
- [x] The host can represent multiple panels and an active panel
- [x] Seeded panel collection and selection behavior are covered by tests (`AgentPanelHostTests` — 17 tests)
- [x] `dotnet build Zaide.slnx` passes
- [x] Focused host tests pass
- [x] Build passes: 0 warnings, 0 errors
- [x] Tests pass: 632 total (615 existing + 17 new), 0 failures
- [x] No UI, execution, Townhall, routing, or persistence concerns introduced
- [x] No DI registration or MainWindowViewModel changes made yet (scope: M1 only)
- [x] Plan doc updated to reflect implemented state

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
