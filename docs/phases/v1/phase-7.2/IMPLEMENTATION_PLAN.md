# Phase 7.2: Live Source Control Wiring — Implementation Plan

## Pre-Implementation Verification

- [x] Confirm Phase 7.1's repo-backed read seam exists and passes its tests
- [x] Re-check `src/Views/SourceControlPanel.cs`, `src/ViewModels/SourceControlViewModel.cs`, `src/Views/StatusBar.cs`, and `src/ViewModels/MainWindowViewModel.cs`
- [x] Confirm the existing shell still provides the correct surface for branch display and change lists without a layout redesign

## Planning Status

**Revised (2026-07-09).** Original plan (2026-07-08) was written before Phase 7.1
M3 delivered snapshot loading into `SourceControlViewModel`; this revision removes
work already completed and centers the plan on the remaining gaps.

## Goal

Make the existing Source Control panel and status bar truthful end-to-end:
refresh on workspace-open, an explicit user-accessible refresh action, and
correct non-repo / error state projection in the status bar.

## Post-7.1 Baseline (what M3 already delivered)

Phase 7.1 M3 already wired `SourceControlViewModel` to the live orchestrator seam.
These are **not** 7.2 work items:

- `SourceControlViewModel` loads `Branches`, `UnstagedChanges`, and `StagedChanges`
  from `ISourceControlSnapshotOrchestrator` on construction and on `RefreshCommand`.
  It never seeds demo data.
- `SourceControlState` is a passive snapshot container only; it is not a source of
  truth.
- ViewModel tests already cover branch name, clean repo, dirty repo, non-repo, and
  error state (`tests/Zaide.Tests/ViewModels/SourceControlViewModelTests.cs`).
- `StatusBar` already binds `SourceControlViewModel.CurrentBranchName`
  (`src/Views/StatusBar.cs` line 131).
- `SourceControlPanel` already binds `UnstagedChanges` / `StagedChanges` and
  updates count headers (`src/Views/SourceControlPanel.cs` lines 165–171).

## Boundaries

Phase 7.2 closes the remaining truthfulness and refresh gaps on top of the 7.1
baseline. It does **not** introduce diff rendering or commit mutations.

## Live Constraints To Respect

- `SourceControlViewModel.ApplyResult` now projects truthful `"no repo"` / `"—"`
  labels on non-repo/failure and surfaces a `StatusMessage` so the panel and
  status bar stay truthful.
- `MainWindowViewModel.OpenFolderCommand` invokes `SourceControlViewModel.RefreshCommand`
  after setting the workspace path, so the Source Control panel reflects the newly
  opened repository.
- `SourceControlPanel` exposes an explicit refresh button in the header bound to
  `RefreshCommand`.
- The current Source Control change rows are not yet selectable; any diff trigger
  or selection model is deferred to Phase 7.3 M0.

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | ~~Lock the 7.2 UI truth policy: define the label shown for non-repo, loading, empty-clean, and error states, and decide `SourceControlState`'s fate.~~ **LOCKED** — see `M0_UI_TRUTH_POLICY.md`. Decisions: non-repo label `"no repo"` (status bar) + panel notice; no loading state (seam is synchronous); no clean banner; error label `"—"` (status bar) + panel `LastRefreshError`; `SourceControlState` **removed** from the constructor path. | Plan re-read against live `SourceControlPanel` and `StatusBar` surfaces |
| M1 | ~~Fix truthful state projection: replace the `"master"` fallback with a truthful non-repo label, surface refresh/error state in the panel, and remove `SourceControlState` from the constructor path.~~ **DONE.** `ApplyResult` now projects `"no repo"` / `"—"` and a `StatusMessage` (panel) instead of `"master"`; `SourceControlState` removed from the VM constructor and DI; 751 tests pass. | ViewModel tests for non-repo/error branch + `StatusMessage`; panel binds `StatusMessage` |
| M2 | ~~Connect refresh triggers: refresh after workspace-open in `MainWindowViewModel.OpenFolderCommand`, add a UI-accessible refresh action in `SourceControlPanel`, both reusing `RefreshCommand`.~~ **DONE.** `OpenFolderCommand` now invokes `SourceControlViewModel.RefreshCommand` after the workspace path is set; `SourceControlPanel` header gained a `Icon.ArrowClockwise` refresh button bound to `RefreshCommand`. New test `OpenFolderCommand_RefreshesSourceControlForNewWorkspace` covers the workspace-open path; 752 tests pass. | Test for workspace-open refresh; both triggers reuse `RefreshCommand` |
| M3 | End-to-end verification: open a real repository, switch to a non-repo folder, and trigger refresh — confirm the status bar branch text and panel change lists stay truthful across all transitions. | Build + tests; manual walkthrough of repo → non-repo → repo cycle | ⏸ Deferred |

## Likely Implementation Shape

- `SourceControlViewModel.ApplyResult` already projects truthful `"no repo"` / `"—"`
  labels and surfaces a `StatusMessage` for the panel and status bar.
- `MainWindowViewModel.OpenFolderCommand` already invokes `SourceControlViewModel.RefreshCommand`
  after the workspace path is set.
- `SourceControlPanel` already exposes a refresh button in the header bound to `RefreshCommand`.

## Out of Scope

- Diff view UI
- Stage/unstage (the existing `StageFileCommand` / `UnstageFileCommand` are still
  UI-only placeholder commands from pre-7.0 — they move collections but do not
  call the git seam; replacing them is Phase 7.4 work)
- Commit execution
- Branch switching UX
- Remote sync indicators

## Limitations (by design)

- Refresh occurs only on workspace-open and explicit user action; no automatic
  git watcher events
- Clean/non-repo states are simple text labels
- The branch selector remains display-oriented until a later sub-phase explicitly
  widens it

## Exit Conditions

- [x] The Source Control panel branch/change data is live, not seeded demo data
- [x] The status bar shows a truthful non-repo label (`"no repo"`) and error label (`"—"`) instead of `"master"`
- [x] Source Control refreshes automatically after opening a workspace folder (M2)
- [x] A user-accessible refresh action exists in the Source Control panel (M2)
- [x] Non-repo and error states are surfaced truthfully (panel `StatusMessage`); clean/dirty already truthful
- [x] Build succeeds: `dotnet build Zaide.slnx --no-restore`
- [x] Tests pass: `dotnet test Zaide.slnx --no-build`
- [ ] End-to-end repo → non-repo → repo cycle verified truthful (M3) — deferred; build/tests pass

## Exact Next Step

After 7.2 is complete, move to `docs/phases/v1/phase-7.3/IMPLEMENTATION_PLAN.md`
for the first minimal diff surface.

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
