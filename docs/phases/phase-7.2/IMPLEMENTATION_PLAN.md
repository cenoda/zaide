# Phase 7.2: Live Source Control Wiring — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm Phase 7.1's repo-backed read seam exists and passes its tests
- [ ] Re-check `src/Views/SourceControlPanel.cs`, `src/ViewModels/SourceControlViewModel.cs`, `src/Views/StatusBar.cs`, and `src/ViewModels/MainWindowViewModel.cs`
- [ ] Confirm the existing shell still provides the correct surface for branch display and change lists without a layout redesign

## Planning Status

**Planned (2026-07-08).**

This sub-phase turns the existing Source Control shell from visual scaffolding
into a truthful live view without widening into diff or commit behavior yet.

## Goal

Wire the existing Source Control panel and status bar to live repository status
so the branch display and change lists reflect the real opened workspace.

## Boundaries

Phase 7.2 consumes the repo-read seam from 7.1 and drives the existing UI from
that truth. It does **not** introduce diff rendering or commit mutations.

## Live Constraints To Respect

- `SourceControlViewModel` currently copies seeded `SourceControlState` demo data
  in its constructor; 7.2 must not leave that seeded object as the lasting truth
  source for the live Source Control UI.
- `StatusBar` already listens to `SourceControlViewModel.CurrentBranchName`, so
  7.2 should prefer truthful view-model rewiring over a status-bar redesign.

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Lock the 7.2 UI truth policy: define how non-repo, loading, empty-clean, and error states appear in the current Source Control surfaces, and decide whether `SourceControlState` survives only as a passive snapshot model or is removed from the view-model constructor path entirely. Keep the layout narrow. | Plan re-read against live `SourceControlPanel` and `StatusBar` surfaces |
| M1 | Update `SourceControlViewModel` and any orchestration seams so branch text and change collections are populated from the real git snapshot instead of demo state. | ViewModel tests for branch name, clean repo, dirty repo, non-repo, and error state |
| M2 | Wire the live branch state into `StatusBar` and verify the Source Control panel list headers/counts stay truthful under refresh. | Build + tests; focused view or binding verification as needed |
| M3 | Connect refresh triggers to the smallest reliable app seams (for example workspace-open and explicit reload) without adding broad watcher complexity yet. | Main-window / integration-style tests for refresh after workspace change |

## Likely Implementation Shape

- Narrow `SourceControlViewModel` so it projects live snapshot data rather than seeded state
- Update `MainWindowViewModel` composition/refresh seams as needed
- Keep `SourceControlPanel` mostly intact; prefer binding/presentation changes over redesign
- Keep `StatusBar` as a passive consumer of live branch state
- Update existing tests that currently assert exact demo branch/count values

## Out of Scope

- Diff view UI
- Stage/unstage
- Commit execution
- Branch switching UX
- Remote sync indicators

## Limitations (by design)

- Refresh may initially occur only on explicit app seams rather than automatic git watcher events
- Clean/non-repo states may be simple text states first
- The branch selector may remain display-oriented until a later sub-phase explicitly widens it

## Exit Conditions

- [ ] The Source Control panel branch/change data is live, not seeded demo data
- [ ] The status bar branch text reflects live repository state
- [ ] Non-repo, clean, dirty, and error states are surfaced truthfully
- [ ] Build succeeds: `dotnet build Zaide.slnx --no-restore`
- [ ] Tests pass: `dotnet test Zaide.slnx --no-build`

## Exact Next Step

After 7.2 is complete, move to `docs/phases/phase-7.3/IMPLEMENTATION_PLAN.md`
for the first minimal diff surface.

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
