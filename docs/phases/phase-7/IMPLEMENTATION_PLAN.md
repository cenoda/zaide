# Phase 7: Git Integration — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm Phase 6 is complete in live code/docs, not just roadmap wording
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx --no-restore`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx --no-build`
- [ ] Re-check `src/ViewModels/SourceControlViewModel.cs`, `src/Models/SourceControlState.cs`, `src/Views/SourceControlPanel.cs`, `src/Views/StatusBar.cs`, `src/ViewModels/MainWindowViewModel.cs`, and `src/Program.cs`
- [ ] Confirm the current Source Control panel remains demo-only and must be replaced by a real repo-backed seam
- [ ] Confirm the current status bar branch text is still driven by demo Source Control state
- [ ] Verify the chosen git library/API against the actual target framework before implementation begins
- [ ] Confirm Phase 7 root docs remain aligned with the Phase 7 umbrella split

## Planning Status

**Planned (2026-07-08).**

Phase 7 should stay one product phase, but it should not be implemented as one
large uninterrupted bucket. The live roadmap already defines a coherent narrow
Phase 7 boundary:

- git status in the left sidebar
- basic diff view
- commit from the IDE
- branch display

That boundary is good, but it still spans four different concerns:

- discovering and reading real repository state
- wiring that state into the existing UI truthfully
- rendering a minimal diff surface
- mutating repository state through staging/commit actions

This document is the umbrella only. It defines the phase boundary, the required
decisions, and the sub-phase order. The implementation details belong in:

- `docs/phases/phase-7.1/IMPLEMENTATION_PLAN.md`
- `docs/phases/phase-7.2/IMPLEMENTATION_PLAN.md`
- `docs/phases/phase-7.3/IMPLEMENTATION_PLAN.md`
- `docs/phases/phase-7.4/IMPLEMENTATION_PLAN.md`

## Live Baseline

Verified against the current checkout on 2026-07-08:

- `docs/roadmap/PHASES.md` defines Phase 7 as Git Integration and lists four
  outcomes only: git status in the left sidebar, basic diff view, commit from
  the IDE, and branch display.
- `docs/architecture/OVERVIEW.md` still describes Git Integration as a planned
  Phase 7 layer, not yet implemented.
- `src/ViewModels/SourceControlViewModel.cs` explicitly says the Source Control
  panel uses static/demo data only; commands update UI state but do not execute
  real git operations.
- `src/Models/SourceControlState.cs` seeds fake branches and file changes.
- `src/Views/SourceControlPanel.cs` already provides the shell surface for a
  branch selector, unstaged/staged sections, and commit input.
- `src/Views/StatusBar.cs` already exposes a branch slot, but that slot is fed
  by `SourceControlViewModel.CurrentBranchName`, which is currently demo-backed.
- `docs/LIBRARIES.md` already records `LibGit2Sharp` and `DiffPlex` as the
  intended Git/ diff libraries for Phase 7, but their actual integration is not
  present in live code yet.

## Scope

**Goal:** Replace the demo-only Source Control state with a real repository-backed
git integration seam that truthfully powers branch display, working-tree status,
a basic diff surface, and local staging/commit actions inside the existing shell.

**Boundaries:** Phase 7 covers local repository discovery, branch/status read
behavior, minimal diff viewing, staging/unstaging, and local commit execution.
It does **not** cover remotes, push/pull/sync, merge/rebase workflows, stash,
history/log browsing, conflict resolution UX, blame, GitHub/GitLab features, or
a broad whole-project modular split.

## Phase-Level Decisions

### 1. Phase 7 is repo-truth-first, not UI-first

The existing Source Control shell is already good enough to host the first real
git slice. Phase 7 should prioritize making the current UI truthful before it
considers redesigning that UI.

### 2. Git operations belong behind a narrow service seam

The current demo state should not slowly accrete repository logic. Phase 7
should introduce one narrow git seam that owns:

- repository discovery
- branch/status reads
- diff retrieval
- stage/unstage actions
- commit execution

UI view-models should consume that seam; they should not parse CLI output or
touch repository internals directly.

### 3. Status must land before diff and commit

Phase 7 should not begin with mutation features. The first truthful milestone is
reading repository state correctly:

- detect whether the opened workspace is a repository
- resolve current branch/HEAD state
- surface working-tree changes accurately

Only after that seam is stable should diff and commit work begin.

### 4. The first diff surface stays intentionally minimal

The roadmap says "basic diff view," not a full source-control workbench. The
first Phase 7 diff slice should:

- show one selected file's diff
- prefer text diffs first
- document binary/large-file limitations explicitly

Do not widen into side-by-side editor integration, inline hunk actions, or
history browsing unless the minimal view proves impossible.

### 5. Branch display is required; branch management is not

Phase 7 must truthfully show the current branch and detached-HEAD-like states
as needed. It does not need broad branch management UX in the first pass.

If branch switching falls out cheaply from the chosen seam, it can be considered
later, but it is not required for the first truthful Phase 7 closeout.

### 6. Commit flow stays local and narrow

Phase 7 should support local commit creation only:

- staged changes only
- commit message validation
- clear user-visible failure/success reporting

No push, sync, remote auth, PR creation, or hosted-platform flows belong here.

## Proposed Phase Split

Phase 7 is intentionally split into narrow slices:

| Sub-phase | Scope | Status |
|-----------|-------|--------|
| 7.1 | Repository discovery + branch/status read seam | Planned |
| 7.2 | Live Source Control panel/status-bar wiring | Planned |
| 7.3 | Basic diff view | Planned |
| 7.4 | Stage/unstage + local commit flow | Planned |

## Phase Map

Treat the sub-phases as a dependency chain, not as parallel implementation work:

| Order | Sub-phase | Primary outcome |
|------:|-----------|-----------------|
| 1 | 7.1 | Real repo-backed git read seam exists and is testable |
| 2 | 7.2 | Existing Source Control panel and status bar become truthful |
| 3 | 7.3 | User can inspect a minimal diff for a selected changed file |
| 4 | 7.4 | User can stage/unstage and create a local commit from the IDE |

## Phase-Level Risks To Watch

- Letting Source Control view-models become the git service layer
- Starting with commit mutations before read-state truth is stable
- Expanding into remote workflows or hosted-platform features
- Turning "basic diff view" into a full editor-integrated compare system
- Introducing a broad architecture split when a narrow git seam would do
- Forgetting to keep `README.md`, `docs/roadmap/PHASES.md`, and
  `docs/architecture/OVERVIEW.md` aligned as Phase 7 progresses

## Phase-Level Test Budget

At minimum, the whole phase should budget explicit tests for:

- repository discovery and "not a repo" handling
- current branch / detached-HEAD-like display behavior
- working-tree status mapping into the app's file-change model
- Source Control panel refresh and binding behavior
- status-bar branch updates from real repo state
- diff retrieval/rendering for simple text files
- stage/unstage behavior
- commit validation and error handling

Likely files to extend or add across the phase:

- `tests/Zaide.Tests/ViewModels/SourceControlViewModelTests.cs`
- `tests/Zaide.Tests/MainWindowViewModelTests.cs`
- `tests/Zaide.Tests/Services/` new git service test files
- `tests/Zaide.Tests/Views/` focused Source Control view tests if the UI shape widens

## Out of Scope

- Push/pull/fetch/sync/remotes
- Merge, rebase, cherry-pick, stash, tags, or conflict resolution UX
- Commit history/log browsing
- Git blame / inline annotations
- GitHub/GitLab integration
- Code-review/PR workflows
- Whole-project modularization beyond a narrow Phase 7 seam
- Phase 6 routed-visibility follow-up work

## Limitations (by design)

- Phase 7 may support only the currently opened workspace/repository at first
- The first diff surface may handle only text files and may summarize binary files
- Live refresh may begin as explicit refresh / event-driven via app seams rather
  than full filesystem/git watcher automation
- Branch display may be read-only in the first pass
- Commit flow may require staged changes only and may not support amend

## Exit Conditions

The phase is complete only when all sub-phases are complete and these conditions
are true in live code:

- [ ] A real repository-backed git seam exists and is covered by tests
- [ ] The Source Control panel no longer depends on seeded demo branch/change data
- [ ] The status bar branch text reflects live repository state
- [ ] The user can view working-tree changes in the left Source Control panel
- [ ] The user can inspect a basic diff for a selected changed file
- [ ] The user can stage and unstage files from the Source Control surface
- [ ] The user can create a local commit from staged changes with truthful error handling
- [ ] `docs/roadmap/PHASES.md`, `docs/architecture/OVERVIEW.md`, and `README.md` match the implemented Phase 7 state
- [ ] Build succeeds: `dotnet build Zaide.slnx --no-restore`
- [ ] Tests pass: `dotnet test Zaide.slnx --no-build`
- [ ] Manual verification covers repo discovery, non-repo behavior, branch display, status list, diff view, stage/unstage, and local commit

## Exact Next Step

Start with `docs/phases/phase-7.1/IMPLEMENTATION_PLAN.md`. The first job is not
UI polish or commit flow; it is replacing demo Source Control truth with a real
repository-backed read seam.

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
