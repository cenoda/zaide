# Phase 7.1: Repository Discovery and Status Seam — Implementation Plan

## Pre-Implementation Verification

- [ ] Re-check `src/ViewModels/SourceControlViewModel.cs`, `src/Models/SourceControlState.cs`, `src/Models/FileChange.cs`, `src/Models/GitBranch.cs`, and `src/Program.cs`
- [ ] Verify the chosen git library/API against the current target framework with a minimal proof-of-concept
- [ ] Confirm the app already has a reliable workspace/root path seam to anchor repository discovery
- [ ] Confirm no real git service seam exists in live code yet

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

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Lock the Phase 7.1 seam: decide the git-service interface, the repository-root discovery rule, the "not a repo" result shape, and the minimal branch/status models needed by later phases. Do not start UI rewiring yet. | Plan re-read + API proof-of-concept result recorded |
| M1 | Add the repository discovery + branch/status read service seam and register it in DI. Keep it read-only. | Service-level tests for repo found / repo missing / current branch / detached-like state / file-status mapping |
| M2 | Replace or narrow the demo `SourceControlState` dependency so live Source Control consumers can request a truthful snapshot instead of seeded fake data. | Build + tests; no seeded branch/change data required for the read path |
| M3 | Add a focused refresh/app orchestration seam that can request a fresh git snapshot for the current workspace without yet finalizing all Source Control UI behavior. | ViewModel-level tests covering refresh success, non-repo state, and failure projection |

## Likely Implementation Shape

- Add one narrow git read service in `src/Services/`
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

- [ ] A real repository discovery + branch/status read seam exists
- [ ] The seam is registered in DI and covered by focused tests
- [ ] The read path no longer depends on seeded fake branches or fake file changes
- [ ] Non-repo workspaces are handled truthfully
- [ ] Build succeeds: `dotnet build Zaide.slnx --no-restore`
- [ ] Tests pass: `dotnet test Zaide.slnx --no-build`

## Exact Next Step

After 7.1 is complete, move to `docs/phases/phase-7.2/IMPLEMENTATION_PLAN.md`
to wire the existing Source Control surfaces to this live seam.

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
