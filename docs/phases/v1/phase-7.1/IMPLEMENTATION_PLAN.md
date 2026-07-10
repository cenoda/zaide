# Phase 7.1: Repository Discovery and Status Seam — Implementation Plan

## Pre-Implementation Verification

- [x] Re-check `src/ViewModels/SourceControlViewModel.cs`, `src/Models/SourceControlState.cs`, `src/Models/FileChange.cs`, `src/Models/GitBranch.cs`, and `src/Program.cs`
- [x] Verify the chosen git library/API against the current target framework with a minimal proof-of-concept
- [x] Re-check `src/Models/Workspace.cs` and confirm how repository discovery will obtain the real workspace/root path
- [x] Confirm no real git service seam exists in live code yet

> M0 result is locked in `docs/phases/v1/phase-7.1/M0_SEAM_DECISION.md`.
> Compatibility proof: LibGit2Sharp 0.30.0 builds and runs on `net10.0`
> (SDK 10.0.108). No later-milestone work started.

## Planning Status

**Planned (2026-07-08).**

This sub-phase exists to replace fake Source Control truth with a narrow,
testable repository-read seam before any UI or mutation work widens.

## Goal

Introduce the smallest useful real git read seam so the app can discover whether
the active workspace is inside a repository, resolve branch/HEAD state, and
enumerate working-tree changes truthfully.

## Boundaries

Phase 7.1 does **not** wire the final UI, render diffs, or perform stage/commit
mutations. It only establishes truthful repository reads and a shape the rest of
Phase 7 can safely consume.

## Live Constraints To Resolve In M0

- `LibGit2Sharp` and `DiffPlex` are documented in `docs/LIBRARIES.md` but are
  not installed in `Directory.Packages.props` / `src/Zaide.csproj` yet.
- `Workspace` currently exposes only `ProjectName`, not the full opened folder
  path, so repository discovery cannot assume an existing `WorkspacePath` seam.
- `SourceControlViewModel` still consumes seeded `SourceControlState` demo data,
  so 7.1 must define what transitional truth shape later phases will consume.

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Lock the Phase 7.1 seam: decide the git-service interface, the repository-root discovery rule, how the app exposes the real workspace path, the "not a repo" result shape, and the minimal branch/status models needed by later phases. Also verify package/version compatibility before implementation starts. Do not start UI rewiring yet. | **Done (2026-07-09)** — see `M0_SEAM_DECISION.md` |
| M1 | Add the repository discovery + branch/status read service seam and register it in DI. Keep it read-only. | Service-level tests for repo found / repo missing / current branch / detached-like state / file-status mapping |
| M2 | Replace or narrow the demo `SourceControlState` dependency so live Source Control consumers can request a truthful snapshot instead of seeded fake data. If `SourceControlState` remains temporarily, it must become a passive snapshot container rather than the source of truth. | Build + tests; no seeded branch/change data required for the read path |
| M3 | Add a focused refresh/app orchestration seam that can request a fresh git snapshot for the current workspace without yet finalizing all Source Control UI behavior. | **Done (2026-07-09)** — `ISourceControlSnapshotOrchestrator` + `SourceControlSnapshotOrchestrator` in `src/Services/`; `SnapshotRefreshResult`/`SnapshotRefreshStatus` project success/non-repo/failure truthfully. `SourceControlViewModel` now depends on the seam for both initial load and `RefreshCommand`, exposing `LastRefreshStatus`/`LastRefreshError`. Tests: `SourceControlSnapshotOrchestratorTests` + refresh cases in `SourceControlViewModelTests`. |

## Likely Implementation Shape

- Add one narrow git read service in `src/Services/`
- Add or expose one truthful workspace-path seam before repository discovery depends on it
- Introduce or adapt minimal repo-status models in `src/Models/`
- Update DI registration in `src/Program.cs`
- Keep `SourceControlViewModel` consuming snapshot-style data rather than owning repository logic

## Out of Scope

- Source Control panel UI reshaping
- Diff view
- Stage/unstage
- Commit execution
- Branch switching
- Remotes or history

## Limitations (by design)

- The first read seam may target one active workspace path only
- Non-repo behavior may surface as a simple empty/disabled state first
- Refresh may initially be explicit rather than watcher-driven

## Exit Conditions

- [x] A real repository discovery + branch/status read seam exists
- [x] The seam is registered in DI and covered by focused tests
- [x] The read path no longer depends on seeded fake branches or fake file changes
- [x] Non-repo workspaces are handled truthfully
- [x] Build succeeds: `dotnet build Zaide.slnx --no-restore`
- [x] Tests pass: `dotnet test Zaide.slnx --no-build`

## M3 Implementation Notes

- `src/Services/ISourceControlSnapshotOrchestrator.cs` — narrow refresh seam:
  `SnapshotRefreshResult Refresh(string? workspacePath)`. Never throws on non-repo
  or transient failure; those are projected as `NotARepository` / `Failed`.
- `src/Services/SourceControlSnapshotOrchestrator.cs` — wraps `IGitRepositoryService`
  (discover → read status) and catches exceptions into `Failed`.
- `src/Services/SnapshotRefreshResult.cs` — `SnapshotRefreshStatus` enum
  (Success / NotARepository / Failed) + passive result container with optional
  `Snapshot` / `ErrorMessage`. No demo data is ever produced.
- `src/ViewModels/SourceControlViewModel.cs` — depends on the orchestrator (not the
  raw git seam) for both initial load and `RefreshCommand`; exposes
  `LastRefreshStatus` / `LastRefreshError`. Non-success clears projections (truthful
  empty state), no fake data.
- `src/Program.cs` — DI registers `ISourceControlSnapshotOrchestrator`.
- Tests: `tests/Zaide.Tests/Services/SourceControlSnapshotOrchestratorTests.cs`
  (success / non-repo / failure paths) and refresh cases in
  `tests/Zaide.Tests/ViewModels/SourceControlViewModelTests.cs`.
- Scope respected: no diff rendering, stage/unstage, commit, or branch switching;
  later-milestone behavior was not started.

## Exact Next Step

After 7.1 is complete, move to `docs/phases/v1/phase-7.2/IMPLEMENTATION_PLAN.md`
to wire the existing Source Control surfaces to this live seam.

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
