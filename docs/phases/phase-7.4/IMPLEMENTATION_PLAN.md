# Phase 7.4: Stage and Commit Flow — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm Phase 7.3's diff flow exists and passes its tests
- [ ] Re-check `src/ViewModels/SourceControlViewModel.cs`, `src/Views/SourceControlPanel.cs`, and the Phase 7 git seam to identify the narrowest mutation path
- [ ] Confirm the intended commit library/API path works against a local repository with a minimal proof-of-concept

## Planning Status

**Planned (2026-07-08).**

This sub-phase adds the first local mutation features only after the read,
status, and diff seams are already grounded in live repository truth.

## Goal

Allow the user to stage and unstage files and create a local commit from the
existing Source Control panel with truthful validation and error reporting.

## Boundaries

Phase 7.4 covers local stage/unstage and local commit creation only. It does
**not** cover push/pull, amend, interactive patch staging, stash, branch
creation/switching, or hosted-platform flows.

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Lock the mutation policy: define the first supported stage/unstage granularity, commit validation rules, post-commit refresh behavior, and user-visible failure states. | Plan re-read against the live Source Control panel and git seam |
| M1 | Add narrow stage/unstage operations behind the git seam and expose them through the Source Control view-model without embedding repository logic there. | Service + ViewModel tests for stage, unstage, no-op, and failure cases |
| M2 | Add local commit execution with message validation and truthful success/failure projection. | Service + ViewModel tests for empty message, nothing staged, commit success, and commit failure |
| M3 | Ensure the Source Control panel and status surfaces refresh correctly after mutation actions and remain truthful. | Build + tests; focused manual verification for stage/unstage/commit loop |

## Likely Implementation Shape

- Extend the git seam with narrow mutation methods
- Keep `SourceControlViewModel` orchestration-focused
- Reuse the existing Source Control panel controls where possible rather than redesigning the flow

## Out of Scope

- Push/pull/fetch
- Commit amend
- Partial-hunk staging
- Branch creation/switching
- History/log UI
- Conflict resolution

## Limitations (by design)

- The first pass may support whole-file stage/unstage only
- Commit may require at least one staged file and a non-empty message
- Post-commit UX may begin as a simple refresh + cleared message flow

## Exit Conditions

- [ ] The user can stage files from the Source Control surface
- [ ] The user can unstage files from the Source Control surface
- [ ] The user can create a local commit with truthful validation and failure behavior
- [ ] Post-mutation UI refresh keeps branch/status/change data truthful
- [ ] Build succeeds: `dotnet build Zaide.slnx --no-restore`
- [ ] Tests pass: `dotnet test Zaide.slnx --no-build`

## Exact Next Step

After 7.4 is complete, Phase 7 can close and the repo can reassess whether the
next need is a small git follow-up, a Phase 6.1 routing-visibility follow-up, or
a later structural refactor based on real pressure rather than anticipation.

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
