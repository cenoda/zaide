# Phase 5.2: Agent Panel UI Surfaces — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm Phase 5.1 is complete
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx`
- [ ] Re-check live shell layout in `src/MainWindow.axaml.cs`

## Scope

**Goal:** Render the first dedicated agent panel UI in the existing shell.

**In scope:**

- Agent panel view/control
- Status area
- Output area
- Direct input area
- Minimal panel switching/selection UI if needed

**Out of scope:**

- Real provider execution
- Townhall logging behavior
- Routing between panels

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Decide initial shell placement | Manual layout review |
| M1 | Render first agent panel surface | View tests + manual smoke |
| M2 | Support switching/selection for multiple panels | ViewModel/view tests |

## Exit Conditions

- [ ] At least one dedicated agent panel renders in the live shell
- [ ] The panel exposes status, output, and input surfaces
- [ ] Townhall remains visually primary
- [ ] `dotnet build Zaide.slnx` passes
