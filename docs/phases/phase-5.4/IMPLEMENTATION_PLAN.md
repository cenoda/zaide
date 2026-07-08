# Phase 5.4: Townhall Integration for Direct-Agent Interaction — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm Phase 5.1 through 5.3 are complete
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx`
- [ ] Re-check `src/ViewModels/TownhallViewModel.cs` and the Phase 4 activity model

## Scope

**Goal:** Keep Townhall truthful when a user interacts with a dedicated agent panel.

**In scope:**

- Log direct user-to-agent interactions into Townhall
- Log agent responses/errors into Townhall at a minimal useful level
- Keep panel activity and Townhall activity aligned

**Out of scope:**

- Agent-to-agent routing
- Mention parsing
- Debate/thread orchestration

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Decide what panel events must appear in Townhall | Design note |
| M1 | Log direct-agent requests/responses to Townhall | ViewModel tests |
| M2 | Verify panel activity and Townhall stay aligned | Manual smoke + tests |

## Exit Conditions

- [ ] Direct user-to-agent interactions appear in Townhall
- [ ] Agent responses/errors appear in Townhall at the intended Phase 5 level
- [ ] No routing behavior is introduced
- [ ] `dotnet build Zaide.slnx` passes
- [ ] ViewModel tests for Townhall side effects pass
