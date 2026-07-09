# Phase 7.3: Basic Diff View — Implementation Plan

## Pre-Implementation Verification

- [x] Phase 7.2 live Source Control wiring is in place: `SourceControlViewModel` loads
      live snapshots from `ISourceControlSnapshotOrchestrator`, `MainWindowViewModel.OpenFolderCommand`
      invokes `RefreshCommand` on workspace-open, and `SourceControlPanel` exposes a
      header refresh button bound to `RefreshCommand`. M1/M2 are complete; M3 manual
      walkthrough is deferred.
- [ ] Re-check the current Source Control panel/view-model surfaces to determine the smallest place a diff view can live without reopening shell layout
- [ ] Verify the chosen diff library/API against the current target framework with a minimal proof-of-concept

## Planning Status

**Planned (2026-07-09, post-7.2 baseline).**

This sub-phase adds the first intentionally small diff surface after repository
truth and live Source Control status are already stable. The 7.2 baseline is:

- `LibGit2Sharp` is already referenced in `src/Zaide.csproj` and powers the
  read seam (`IGitRepositoryService` → `SourceControlSnapshotOrchestrator`).
- `DiffPlex` is documented in `docs/LIBRARIES.md` as the intended diff library
  but is **not yet added** as a package reference; diff-library proof is a real
  pre-implementation task.
- The Source Control change rows (`UnstagedChanges` / `StagedChanges`) are rendered
  by `SourceControlPanel.CreateChangeItemTemplate` but are **not yet selectable**
  and carry no diff-trigger model. M0 must explicitly lock the selection/trigger
  design before diff retrieval work begins.

## Goal

Allow the user to inspect a basic diff for a selected changed file from the
existing Source Control workflow.

## Boundaries

Phase 7.3 adds minimal diff retrieval and rendering only. It does **not** add
inline hunk actions, history compare, side-by-side editor integration, or broad
review tooling.

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Lock the 7.3 diff shape: decide where the diff is shown, what file-selection flow triggers it, and how binary/unsupported files fail visibly. | Plan re-read + API proof-of-concept result recorded |
| M1 | Add a narrow diff-retrieval seam for one selected file based on the live repository snapshot. Keep the output shape intentionally small. | Service-level tests for modified/added/deleted text files and unsupported/binary fallback |
| M2 | Expose selected-file diff state through the Source Control view-model layer without mixing diff generation into the view. | ViewModel tests for file selection, diff load success, empty diff, and visible failure state |
| M3 | Render the first basic diff surface in the existing shell and verify it does not destabilize the rest of the Source Control workflow. | Build + tests; focused manual verification for changed-file selection and diff display |

## Likely Implementation Shape

- Add one narrow diff service or extend the git seam in `src/Services/`
- Add minimal diff-view models/state projection in `src/ViewModels/`
- Prefer a small embedded surface in the existing Source Control flow over a new top-level shell region

## Out of Scope

- Side-by-side compare editor
- Inline hunk staging
- Commit history / previous revisions
- Syntax-aware or semantic diffing
- Review comments / annotations

## Limitations (by design)

- The first diff surface may support text files only
- Large/binary files may show a summary or unsupported message instead of full diff content
- Diff loading may be selection-driven and not precomputed for all files

## Exit Conditions

- [ ] A selected changed file can surface a basic diff
- [ ] Unsupported/binary/empty cases are handled truthfully
- [ ] Diff generation remains behind a narrow seam, not inside the view
- [ ] Build succeeds: `dotnet build Zaide.slnx --no-restore`
- [ ] Tests pass: `dotnet test Zaide.slnx --no-build`

## Exact Next Step

After 7.3 is complete, move to `docs/phases/phase-7.4/IMPLEMENTATION_PLAN.md`
for stage/unstage and local commit flow.

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
