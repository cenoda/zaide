# Phase 5.1.3: MainWindow Composition Seam and 5.1 Exit Audit — Implementation Plan

## Pre-Implementation Verification

- [x] Confirm Phase 5.1.1 and 5.1.2 are complete
- [x] Verify current build succeeds: `dotnet build Zaide.slnx`
- [x] Verify current tests pass: `dotnet test Zaide.slnx --no-build`
- [x] Re-check `src/MainWindow.axaml.cs`, `src/ViewModels/MainWindowViewModel.cs`, and `src/Program.cs`

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
| M0b | Record the M0 decision explicitly in this plan doc | Doc review |
| M1 | Wire the host seam through DI and `MainWindowViewModel` | Build + focused composition tests |
| M2 | Close the Phase 5.1 exit audit and sync the umbrella docs | `dotnet test Zaide.slnx --no-build` + doc review |

## M0 Decision Recorded

The following composition decisions are now locked for Phase 5.1.3 and following
Phase 5 sub-phases:

1. **AgentPanelHost is registered as singleton in DI and injected into
   MainWindowViewModel.**
   Mirroring the existing `ITerminalHost`/`TerminalHost` registration at
   `src/Program.cs` line 28, `IAgentPanelHost`/`AgentPanelHost` will be
   registered as a singleton in the same DI container. `MainWindowViewModel`
   receives it via constructor injection, exactly like `ITerminalHost`.

2. **MainWindowViewModel composes the injected host seam only.**
   `MainWindowViewModel` exposes the host as a public property but does not
   re-own the panel collection, active selection, or lifecycle state. The host
   seam maintains ownership — identical to how `TerminalHost` owns
   `Tabs`/`ActiveTab` and `MainWindowViewModel` merely exposes `TerminalHost`.

3. **Any retained view-only per-panel state belongs in the view layer if needed
   later.**
   If Phase 5.2 determines that the view needs to retain per-panel visual state
   (e.g. scroll position, expanded/collapsed sections, active sub-tab), that
   state stays in a dedicated view-layer host (matching the
   `TerminalTabHost` pattern) rather than polluting the ViewModel seam.

4. **The seam stays narrow — no UI, execution, Townhall, or routing concerns.**
   `IAgentPanelHost` and `AgentPanelHost` remain exactly as implemented in
   Phase 5.1.2: panel collection and active selection only. No reference to
   rendering, provider calls, Townhall mirroring, or agent-to-agent routing.

## Limitations (by design)

- This slice may expose the seam without any finished visual agent-panel surface yet
- Shell placement may remain recorded as a composition decision rather than a final rendered layout until Phase 5.2, but the composition target is already fixed to the existing right-side shell column
- No direct-agent execution path yet

## Exit Conditions

- [x] `MainWindowViewModel` composes the agent-panel host seam (M1)
- [x] `Program.cs` registers the new seam correctly (M1)
- [ ] Phase 5.1 umbrella docs accurately reflect the resulting shape (M2)
- [x] `dotnet build Zaide.slnx` passes
- [x] Focused composition tests pass (M1)
- [x] M0 decision recorded and confirmed against live precedent

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
