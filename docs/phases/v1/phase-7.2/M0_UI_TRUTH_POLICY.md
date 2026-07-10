# Phase 7.2 M0: UI Truth Policy (Locked)

**Status: LOCKED (2026-07-09).** Pre-implementation surfaces re-read against live
code (`SourceControlPanel.cs`, `SourceControlViewModel.cs`, `StatusBar.cs`,
`MainWindowViewModel.cs`). No implementation started. Later-milestone behavior
excluded.

This document is the contract for M1–M3. It does not widen scope into diff
rendering, stage/unstage, commit, or branch switching.

## Live State Inventory (verified)

| State | Source | StatusBar branch text | Panel change lists |
|-------|--------|----------------------|--------------------|
| Success, dirty/clean | `SnapshotRefreshStatus.Success` + snapshot | real branch (`CurrentBranchName`) | populated / empty with `(N)` counts |
| Non-repo | `NotARepository` | **`"master"` (BUG — demo value)** | empty, no notice |
| Error | `Failed` | **`"master"` (BUG)** | empty, no notice |
| Loading | **does not exist** — seam is synchronous | n/a | n/a |

`LastRefreshStatus` / `LastRefreshError` exist on the ViewModel but are bound
nowhere in `StatusBar` or `SourceControlPanel`.

## Locked Truth Policy

### 1. Non-repo label
- **StatusBar** branch segment shows the literal string `"no repo"` when
  `LastRefreshStatus == NotARepository`. Replaces the hardcoded `"master"`.
- **SourceControlPanel** shows a single non-repo notice in place of the change
  lists (narrow, no layout change):
  `"No repository — open a folder inside a git repository"`.
- Branch selector is empty/disabled; headers show `Changes (0)` / `Staged (0)`.

### 2. Loading presentation
- **None.** The refresh seam is synchronous and runs inline. M0 locks the
  decision that there is **no loading state, no spinner, no progress surface**.
  If an async refresh is introduced later (not 7.2), a `Loading` state becomes a
  separate milestone decision — out of scope here.

### 3. Empty-clean presentation
- **No special banner.** Success with zero changes stays truthful and minimal:
  real branch name in the status bar; headers `Changes (0)` / `Staged (0)`;
  empty lists. A dedicated "clean" message is explicitly **not** added to keep
  the layout narrow. Empty-clean is therefore already truthful (only the
  non-repo/error `"master"` fallback needs fixing).

### 4. Error presentation
- **StatusBar** branch segment shows `"—"` when `LastRefreshStatus == Failed`
  (neutral, narrow; detailed text lives in the panel).
- **SourceControlPanel** surfaces the error in place of the change lists:
  `"Source Control unavailable: {LastRefreshError}"`.
- Empty lists; branch selector disabled.

### 5. `SourceControlState` decision
- **REMOVED from the `SourceControlViewModel` constructor path entirely.**
- Rationale (verified): it is a write-only passive mirror today. `Snapshot` is
  written in `ApplyResult` but never read by any live consumer; `CommitMessageDraft`
  is read once at construction and is always `""` (no live producer populates it).
  The orchestrator seam is the sole source of truth. Carrying a dead singleton
  perpetuates the "truth might live here" ambiguity.
- Consequence for M1: drop the `SourceControlState` parameter from the
  `SourceControlViewModel` constructor, remove the `Program.cs` singleton
  registration, and update the existing tests that construct
  `new SourceControlState()`. If a later phase (e.g. 7.4) needs a commit-draft
  cache, it must be reintroduced deliberately — not retained as dead state now.

## Already Truthful (no M1 work needed)
- Branch name projection on `Success` (status bar + `SelectBranchCommand`).
- Change-list population and count headers on `Success`.
- `RefreshCommand` semantics (re-runs the orchestrator).

## Still Needs Work (M1+)
- Replace `"master"` fallback with the locked non-repo/error labels above.
- Bind `LastRefreshStatus`/`LastRefreshError` into the panel surface.
- Remove `SourceControlState` from the constructor (decision above).
- Add refresh triggers (M2) — out of M0 scope.

## Acceptance (M0)
- [x] UI truth policy locked for non-repo / loading / empty-clean / error.
- [x] `SourceControlState` future decided: removed from constructor path.
- [x] Live surfaces re-read against repo.
- [x] Next milestone (M1) unambiguous.
