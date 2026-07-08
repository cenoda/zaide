# Phase 5.4: Townhall Integration for Direct-Agent Interaction — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm Phase 5.1.1 through 5.3 are complete
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx --no-build`
- [ ] Re-check `src/ViewModels/TownhallViewModel.cs` and the Phase 4 activity model
- [ ] Re-confirm which direct-agent events must appear in Townhall and which do not belong there yet

## Scope

**Goal:** Keep Townhall truthful when a user interacts with a dedicated agent panel, without introducing routing semantics.

**In scope:**

- Log direct user-to-agent requests into Townhall at a useful activity level
- Log direct agent responses and visible failures into Townhall at a useful activity level
- Keep panel activity and Townhall activity aligned for direct interactions
- Add only the narrow orchestration seam required to mirror these events cleanly

**Out of scope:**

- Agent-to-agent routing
- Mention parsing
- Debate/thread orchestration
- Full transcript synchronization semantics
- Provider-service awareness of Townhall

## Boundary Rule

Townhall mirroring in this slice must happen through the app/ViewModel orchestration
layer, not by having provider services reference `TownhallViewModel` directly.

This preserves the current MVVM/service boundaries:

- Services do execution work only.
- ViewModels/app-layer seams decide what Townhall should record.
- Views only render the resulting state.

## Logging Decision Constraints

Phase 5.4 should log only what is necessary to keep the shared workspace honest:

- user sent direct message to agent X
- agent X responded
- agent X failed / request failed visibly

Do not log speculative routing concepts, invisible internal retries, or detailed
provider internals unless Phase 5 already exposes them to the user.

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Record the exact direct-agent events that must appear in Townhall | Design note |
| M1 | Implement minimal request/response/error mirroring through the non-service orchestration seam | ViewModel tests |
| M2 | Verify panel-visible state and Townhall-visible state remain aligned for the direct interaction flow | ViewModel tests + manual smoke |

## Test Budget

At minimum, budget tests for:

- Townhall entry creation when a direct-agent request is sent
- Townhall entry creation when a direct-agent response arrives
- Townhall entry creation when the direct request fails visibly
- no routing behavior being introduced by the Townhall sync seam
- alignment between panel-visible and Townhall-visible state transitions

Likely files to extend or add:

- `tests/Zaide.Tests/ViewModels/` new Townhall-integration tests
- `tests/Zaide.Tests/ViewModels/TownhallViewModelTests.cs` if direct assertions are added there
- `tests/Zaide.Tests/ViewModels/` panel/Townhall coordination tests

## Limitations (by design)

- Townhall integration may remain summary-level rather than full transcript mirroring
- No agent-to-agent delivery semantics
- No mention parsing or thread branching
- No persistence of mirrored events beyond whatever Phase 4 already supports in memory

## Exit Conditions

- [ ] Direct user-to-agent interactions appear in Townhall
- [ ] Direct agent responses and visible failures appear in Townhall at the intended Phase 5 level
- [ ] No provider service directly references `TownhallViewModel`
- [ ] No routing behavior is introduced
- [ ] `dotnet build Zaide.slnx` passes
- [ ] Focused ViewModel/orchestration tests pass
- [ ] Manual smoke confirms panel activity and Townhall activity remain aligned

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
