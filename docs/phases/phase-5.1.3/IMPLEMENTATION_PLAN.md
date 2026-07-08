# Phase 5.1.3: MainWindow Composition Seam and 5.1 Exit Audit — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm Phase 5.1.1 and 5.1.2 are complete
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx --no-build`
- [ ] Re-check `src/MainWindow.axaml.cs`, `src/ViewModels/MainWindowViewModel.cs`, and `src/Program.cs`

## Scope

**Goal:** Expose the new agent-panel host seam through application composition without prematurely building the full Phase 5.2 UI.

**In scope:**

- Inject host seam into `MainWindowViewModel`
- Register the new seam in `Program.cs` if not already registered
- Make the shell exposure point explicit enough that Phase 5.2 can build on it cleanly inside the existing right-side shell column
- Close the Phase 5.1 docs and exit audit

**Out of scope:**

- Final panel rendering
- Full shell placement implementation details that belong to Phase 5.2
- Provider execution
- Townhall mirroring
- Routing behavior

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Decide the narrowest composition exposure point for the host seam | Repo review + build |
| M1 | Wire the host seam through DI and `MainWindowViewModel` | Build + focused composition tests |
| M2 | Close the Phase 5.1 exit audit and sync the umbrella docs | `dotnet test Zaide.slnx --no-build` + doc review |

## Limitations (by design)

- This slice may expose the seam without any finished visual agent-panel surface yet
- Shell placement may remain recorded as a composition decision rather than a final rendered layout until Phase 5.2, but the composition target is already fixed to the existing right-side shell column
- No direct-agent execution path yet

## Exit Conditions

- [ ] `MainWindowViewModel` composes the agent-panel host seam
- [ ] `Program.cs` registers the new seam correctly
- [ ] Phase 5.1 umbrella docs accurately reflect the resulting shape
- [ ] `dotnet build Zaide.slnx` passes
- [ ] Focused composition tests pass

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
