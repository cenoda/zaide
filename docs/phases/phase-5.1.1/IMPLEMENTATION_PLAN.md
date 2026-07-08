# Phase 5.1.1: Single-Panel State Shape and Host Ownership Decision — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm `docs/phases/phase-5/IMPLEMENTATION_PLAN.md` is still the Phase 5 umbrella
- [ ] Confirm `docs/phases/phase-5.1/IMPLEMENTATION_PLAN.md` is the current Phase 5.1 umbrella
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx --no-build`
- [ ] Re-check `src/MainWindow.axaml.cs`, `src/ViewModels/MainWindowViewModel.cs`, `src/ViewModels/ITerminalHost.cs`, `src/ViewModels/TerminalHost.cs`, and `src/Program.cs`

## Live Baseline

Verified against the current checkout on 2026-07-08:

- `MainWindowViewModel` currently composes `FileTreeViewModel`, `EditorTabViewModel`, `ITerminalHost`, `TownhallViewModel`, and `SourceControlViewModel`, but no agent-panel seam yet.
- `MainWindow` already follows the pattern of keeping long-lived view state in the view layer (`TerminalTabHost`) while composing testable host state through a dedicated host seam (`ITerminalHost` / `TerminalHost`).
- `Program.cs` already registers the terminal host as a dedicated singleton and injects it into `MainWindowViewModel`, which is the clearest live precedent for Phase 5.1.

## Scope

**Goal:** Decide the smallest useful state shape for one agent panel and record the ownership decision for the later multi-panel host seam.

**In scope:**

- Record the host ownership decision for Phase 5.1/5.2
- Define the minimal single-panel state shape
- Introduce only the model/ViewModel types needed for one agent panel
- Add tests for the single-panel state behavior

**Out of scope:**

- Multi-panel host implementation
- Shell composition changes in `MainWindowViewModel`
- Rendering panel UI
- Real provider calls
- Townhall side effects
- Routing semantics

## Ownership Decision

This decision should be treated as settled for the rest of Phase 5 unless live
implementation proves a concrete blocker:

- `MainWindowViewModel` should compose an injected dedicated agent-panel host seam.
- Multi-panel collection/selection/lifecycle state should not live directly in `MainWindowViewModel`.
- View-only retained control state, if Phase 5.2 later needs it, should stay in the view layer rather than the ViewModel layer.

This mirrors the existing terminal pattern and best matches the current codebase.

## Recommended Minimal Panel Shape

The first single-panel state shape should stay narrow:

- `PanelId`
- `AgentId`
- `AgentName`
- `AvatarResourceKey`
- `Status`
- output history collection
- `DraftInput`

Do not add provider-registry concepts, routing metadata, dynamic plugin identity,
or speculative persistence fields in this slice.

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Reconfirm live precedent and record the ownership decision | Repo inspection notes + `dotnet build Zaide.slnx` |
| M1 | Define the minimal single-panel model/ViewModel shape | New single-panel ViewModel tests |
| M2 | Verify the shape stays intentionally narrow and Phase-5-safe | `dotnet test Zaide.slnx --no-build` |

## Limitations (by design)

- No multi-panel behavior yet
- No shell placement commitment yet beyond “host seam will be composed, not embedded in code-behind”
- No provider/model configuration yet
- No Townhall mirroring yet

## Exit Conditions

- [ ] The host ownership decision is recorded here and is concrete enough to guide 5.1.2 and 5.1.3
- [ ] A minimal single-panel state shape exists in code and is covered by tests
- [ ] The single-panel shape does not include routing or provider-platform abstractions
- [ ] `dotnet build Zaide.slnx` passes
- [ ] Focused tests for the new single-panel state pass

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
