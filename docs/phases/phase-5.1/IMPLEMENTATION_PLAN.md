# Phase 5.1: Agent Panel State and Host Seam — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm `docs/phases/phase-5/IMPLEMENTATION_PLAN.md` is the current umbrella
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx`
- [ ] Re-check `src/MainWindow.axaml.cs`, `src/ViewModels/MainWindowViewModel.cs`, and `src/Program.cs`

## Scope

**Goal:** Add the minimum state and host seam needed to represent dedicated
agent panels in the existing shell.

**In scope:**

- Agent panel model/ViewModel shape
- Agent panel host seam (collection + active panel)
- Minimal shell composition changes needed to expose that seam

**Out of scope:**

- Real provider calls
- Townhall integration details
- Agent-to-agent routing

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Decide host ownership seam | Build + repo review |
| M1 | Add minimal agent panel model/ViewModel | New ViewModel tests |
| M2 | Add host seam and wire shell composition | Build + host tests |

## Exit Conditions

- [ ] A dedicated host seam exists for multiple agent panels
- [ ] At least one agent panel can be represented in ViewModel state
- [ ] `dotnet build Zaide.slnx` passes
- [ ] Tests for the new host/panel seam pass
