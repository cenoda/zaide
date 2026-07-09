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

- The binding surfaces from ViewModel → StatusBar and ViewModel → SourceControlPanel
  already exist; 7.2 should correct state projection and add refresh behavior, not
  redesign the binding layer.
- `SourceControlViewModel.ApplyResult` resets `CurrentBranchName` to `"master"`
  on non-repo/failure (`src/ViewModels/SourceControlViewModel.cs` line 142). This
  is a demo-ism — the status bar should project a truthful non-repo label instead.
- `MainWindowViewModel.OpenFolderCommand` updates `Workspace` and opens the file
  tree but does **not** refresh Source Control
  (`src/ViewModels/MainWindowViewModel.cs` lines 122–128). This is the primary
  refresh gap.
- `RefreshCommand` exists on the ViewModel but has no UI trigger in
  `SourceControlPanel`.

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | ~~Lock the 7.2 UI truth policy: define the label shown for non-repo, loading, empty-clean, and error states, and decide `SourceControlState`'s fate.~~ **LOCKED** — see `M0_UI_TRUTH_POLICY.md`. Decisions: non-repo label `"no repo"` (status bar) + panel notice; no loading state (seam is synchronous); no clean banner; error label `"—"` (status bar) + panel `LastRefreshError`; `SourceControlState` **removed** from the constructor path. | Plan re-read against live `SourceControlPanel` and `StatusBar` surfaces |
| M1 | ~~Fix truthful state projection: replace the `"master"` fallback with a truthful non-repo label, surface refresh/error state in the panel, and remove `SourceControlState` from the constructor path.~~ **DONE.** `ApplyResult` now projects `"no repo"` / `"—"` and a `StatusMessage` (panel) instead of `"master"`; `SourceControlState` removed from the VM constructor and DI; 751 tests pass. | ViewModel tests for non-repo/error branch + `StatusMessage`; panel binds `StatusMessage` |
| M2 | Connect refresh triggers: refresh Source Control after workspace-open in `MainWindowViewModel.OpenFolderCommand`, add a UI-accessible refresh action in `SourceControlPanel` (button or toolbar icon), and ensure both paths reuse `RefreshCommand`. | Main-window / integration-style tests for refresh after workspace change; manual verification of explicit refresh button |
| M3 | End-to-end verification: open a real repository, switch to a non-repo folder, and trigger refresh — confirm the status bar branch text and panel change lists stay truthful across all transitions. | Build + tests; manual walkthrough of repo → non-repo → repo cycle |

## Likely Implementation Shape

- Update `SourceControlViewModel.ApplyResult` to project a truthful non-repo label
  instead of `"master"`, and expose that label as a new property
  (e.g. `NonRepoLabel`) the status bar binds to when no repo is active
- Add `SourceControlViewModel.RefreshCommand.Execute()` in
  `MainWindowViewModel.OpenFolderCommand` after workspace path is set
- Add a small refresh control (button or icon) in `SourceControlPanel` bound to
  `RefreshCommand`
- Keep `SourceControlPanel` and `StatusBar` mostly intact; prefer binding changes
- Update existing tests for the new non-repo branch label

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
- [ ] Source Control refreshes automatically after opening a workspace folder (M2)
- [ ] A user-accessible refresh action exists in the Source Control panel (M2)
- [x] Non-repo and error states are surfaced truthfully (panel `StatusMessage`); clean/dirty already truthful
- [x] Build succeeds: `dotnet build Zaide.slnx --no-restore`
- [x] Tests pass: `dotnet test Zaide.slnx --no-build`

## Exact Next Step

After 7.2 is complete, move to `docs/phases/phase-7.3/IMPLEMENTATION_PLAN.md`
for the first minimal diff surface.

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
