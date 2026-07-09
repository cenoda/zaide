# Phase 7.4: Stage and Commit Flow — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm Phase 7.3's diff flow exists and passes its tests
- [ ] Re-read `src/ViewModels/SourceControlViewModel.cs`, `src/Views/SourceControlPanel.cs`,
      `IGitRepositoryService`, and the unified-diff seam (`IFileDiffService`)
      to verify the narrowest mutation path before M1 coding
- [ ] Confirm LibGit2Sharp `Repository.Index.Stage()` / `.Unstage()` / `repo.Commit()`
      and `repo.Config.BuildSignature()` work against a local repo with a minimal
      proof-of-concept (file create → stage → modify → unstage → restage → commit)
- [ ] Verify the planned mutation seam name is not already defined anywhere (grep
      for `IGitMutationService` and `GitMutationService` across the whole tree)

## Planning Status

**Revised 2026-07-09 — audit findings applied.**

This revision was written against the live codebase on 2026-07-09.
The previous plan was aspirational in several areas; this one is concretely
grounded in the facts of the repo.

### Live Baseline (verified 2026-07-09)

| File | Phase 7.4-relevant facts |
|------|--------------------------|
| `src/Services/IGitRepositoryService.cs` | **Read-only by contract.** XML doc explicitly says "No mutation operations (stage, unstage, commit) are exposed; this is read-only for all of Phase 7.1." Phase 7.3 respected this and did not add mutation. This service must **not** be extended with mutation methods — that would violate its documented contract. |
| `src/Services/GitRepositoryService.cs` | `ToChanges()` splits `StatusEntry` into staged + unstaged `FileChange` objects. **Bug/limitation:** when a file has both staged and unstaged changes with the same `ChangeType` (e.g. `ModifiedInIndex` + `ModifiedInWorkdir`), the unstaged entry is suppressed (line 65: `if (unstaged != null && unstaged.ChangeType != staged?.ChangeType)`). This means the file appears only in the staged list today. Phase 7.4 must decide whether to fix this or accept it. |
| `src/Services/ISourceControlSnapshotOrchestrator.cs` | Refresh-only. Must remain unchanged — it is the app's single refresh seam, not a mutation seam. |
| `src/Services/SourceControlSnapshotOrchestrator.cs` | Same as above. |
| `src/Services/IFileDiffService.cs` + impl | Diff-only. Unchanged. |
| `src/ViewModels/SourceControlViewModel.cs` | `StageFileCommand`, `UnstageFileCommand`, and `CommitCommand` are **pure UI-placeholder mutations** — they shuffle `FileChange` objects between `ObservableCollection`s and flip the `IsStaged` boolean. They never touch a git repository. `DiffCommand` (selection) and `RefreshCommand` are real. Three existing tests assert this placeholder behavior (see below). **Stale doc comment:** class XML doc says "Commands update UI state but do not execute real git operations (those are later milestones)" — must be updated when commands become real. |
| `src/Views/SourceControlPanel.cs` | Each `ListBox` row has stage/unstage `Button` instances (via `CreateChangeItemTemplate`). The commit input (`TextBox`) and commit button (`Button`) are wired. No mutation eventing exists yet. |
| `tests/Zaide.Tests/ViewModels/SourceControlViewModelTests.cs` | `StageFile_MovesFromUnstagedToStaged` [L118], `UnstageFile_MovesFromStagedToUnstaged` [L135], and `CommitCommand_ClearsStagedAndMessage` [L152] assert visual-only behavior. **These three tests must be removed/rewritten** when the real commands replace the placeholders. The tests that assert selection, diff, and refresh behavior (e.g. `SelectFileCommand_*`, `Refresh_*`) must remain. |
| `src/Models/FileChange.cs` | `IsStaged` has a public `set` accessor (placeholder commands toggle it). **Stale doc comment:** class XML doc says "Used for static/demo data — no real git operations" — must be updated when real mutation is introduced. |
| `src/Models/SourceControlState.cs` | Passive container with `Snapshot` and `CommitMessageDraft`. Not used by the ViewModel today. Phase 7.4 does **not** wire it in; it remains dead code. If it still has zero consumers after Phase 7.4, remove it in a follow-up cleanup. |
| `src/Program.cs` (DI) | `IGitRepositoryService`, `ISourceControlSnapshotOrchestrator`, `IFileDiffService` are registered as singletons. Any new mutation seam must be registered here. |

### Truthfulness Gaps Discovered

1. **Same file, both states**: `GitRepositoryService.ToChanges()` currently suppresses
   a file's unstaged entry when its `ChangeType` matches the staged entry. This means
   if you modify a file, stage it, then modify it again, it only shows in the staged
   list. LibGit2Sharp's `StatusEntry` represents both states correctly; the suppression
   is a 7.1 design choice that 7.4 must revisit.
2. **`FileChange.IsStaged` is mutable**: the model exposes a `set` accessor so the
   placeholder commands can toggle it. Real mutation must go through the git seam,
   not through object property mutation.
3. **No signature-config path exists**: `repo.Commit()` requires a `Signature`.
   LibGit2Sharp `repo.Config.BuildSignature()` returns `null` when `user.name` or
   `user.email` are unset. The plan must handle this as a specific failure mode.

## Goal

Allow the user to stage and unstage files and create a local commit from the
existing Source Control panel with truthful validation and error reporting.

## Boundaries

Phase 7.4 covers local stage/unstage and local commit creation only. It does
**not** cover push/pull, amend, interactive patch staging, stash, branch
creation/switching, or hosted-platform flows.

## Mutation-Seam Decision (M0)

**`IGitRepositoryService` stays read-only.** Extending it with mutation methods
would violate its XML-documented contract and the SRP boundary established in 7.1.
Instead, Phase 7.4 introduces a **new dedicated mutation seam**:

```csharp
// New file: src/Services/IGitMutationService.cs
// New file: src/Services/GitMutationService.cs (implementation)
/// <summary>
/// Narrow mutation seam for git stage, unstage, and commit operations.
/// Separate from the read-only IGitRepositoryService and the refresh-only
/// ISourceControlSnapshotOrchestrator. Uses LibGit2Sharp directly.
/// </summary>
```

The mutation service:
- Receives the repository root path (already discovered by the read seam)
- Exposes `Stage(repoRoot, filePath)` / `Unstage(repoRoot, filePath)` returning
  a `StageResult` that projects success or failure (file removed externally,
  repo error, IO error). True no-op for already-staged / already-unstaged files.
- Exposes `Commit(repoRoot, message)` returning a `CommitResult` that projects
  success, validation failure (empty message, nothing staged), or service
  failure (signature missing, repo error, IO error)
- Does **not** call `Refresh()` or update ViewModel state — it is a pure
  operation seam, not an orchestration seam
- **Async convention note:** LibGit2Sharp's API is synchronous. The service
  methods are intentionally synchronous to match the library. The ViewModel
  wraps them in `Task.Run` via `ReactiveCommand.CreateFromTask` for
  off-thread execution. This is a documented exception to the CONVENTIONS.md
  rule that I/O-bound methods should be async. The code snippets below
  use `ReactiveCommand.CreateFromTask` with `await Task.Run(...)` to
  match this convention.

### Return Types

```csharp
public sealed class StageResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public static StageResult Success() => new() { IsSuccess = true };
    public static StageResult Failure(string message) => new() { IsSuccess = false, ErrorMessage = message };
}

public sealed class CommitResult
{
    public bool IsSuccess { get; init; }
    public string? CommitSha { get; init; }  // Set only on success
    public string? ErrorMessage { get; init; }
    public static CommitResult Success(string sha) => new() { IsSuccess = true, CommitSha = sha };
    public static CommitResult Failure(string message) => new() { IsSuccess = false, ErrorMessage = message };
}
```

### Interface Shape

```csharp
public interface IGitMutationService
{
    StageResult Stage(string repositoryRoot, string filePath);
    StageResult Unstage(string repositoryRoot, string filePath);
    CommitResult Commit(string repositoryRoot, string message);
}
```

### Decision Record

| Alternative | Rejected Because |
|-------------|------------------|
| Extend `IGitRepositoryService` with stage/unstage/commit | Violates the read-only contract documented in XML doc + Phase 7.1 decision record. Would require renaming/recontracting a stable seam. |
| Put mutation methods on the orchestrator | Breaks SRP: orchestrator's job is refresh, not mutation. Would make unit testing harder (one seam for two concerns). |
| Put mutation in the ViewModel | Already rejected by Phase 7 conventions — MVVM rules forbid repo logic in ViewModels. |
| *Chosen: New `IGitMutationService` seam* | Consistent with how `IFileDiffService` got its own seam in 7.3. Keeps read, diff, refresh, and mutation each behind their own interface. |

## Representation Decision for Same-File Staged+Unstaged Changes (M0)

A single file can simultaneously have staged and unstaged changes (e.g. edit →
stage → edit again). LibGit2Sharp's `Repository.Status` correctly reports
`FileStatus.ModifiedInIndex | FileStatus.ModifiedInWorkdir` for this case.
`FileChange` currently maps to a single file path + a single `IsStaged` bool,
which is insufficient to represent both states simultaneously.

**Decision for Phase 7.4:** Accept the current suppression behavior where a file
with identical ChangeType in both index and workdir appears only in the staged
list. This matches VS Code's behavior (only the staged state is shown; the
unstaged overlay is hidden). The user can observe the full combined diff by
selecting the staged entry. A future phase can introduce a richer
`FileChange` model (e.g. split into `StagedChange` / `UnstagedChange` with the
same `FilePath`) when there is real demand.

- If the staged and unstaged ChangeTypes **differ** (e.g. `Modified` staged +
  `Deleted` workdir), both entries already appear, and mutation actions operate
  independently on each.
- If they match, the unstaged entry is suppressed. Staging, unstaging, and
  refresh will reflect the combined state correctly because LibGit2Sharp's
  `Stage()`/`Unstage()` wrap both layers.
- If this decision proves confusing in practice, it can be revisited in a
  follow-up without breaking the mutation seam.

## Live Constraints To Respect

1. `StageFileCommand`, `UnstageFileCommand`, and `CommitCommand` in
   `SourceControlViewModel` are **visual-only placeholder commands**. Their
   implementation mutates `ObservableCollection` objects directly and never
   touches git. 7.4 replaces these three command bodies with calls through
   the new mutation seam — it does not layer more demo state on top.
2. The three tests that assert the current placeholder behavior
   (`StageFile_MovesFromUnstagedToStaged`, `UnstageFile_MovesFromStagedToUnstaged`,
   `CommitCommand_ClearsStagedAndMessage`) must be replaced, not augmented.
   New tests must assert seam-backed behavior and truthful state projection.
3. `IGitRepositoryService`, `ISourceControlSnapshotOrchestrator`, and
   `IFileDiffService` must remain unchanged by Phase 7.4.

## Commit Validation Rules (M0)

| Rule | Behavior |
|------|----------|
| Empty commit message | Commit is rejected before any git call. `CommitResult` returns validation failure with message: "Commit message cannot be empty." No repository operation is attempted. |
| Nothing staged | Commit is rejected before any git call. `CommitResult` returns validation failure with message: "Nothing staged to commit." No repository operation is attempted. The ViewModel checks `StagedCount == 0` or the service checks `retrieveStatus().Staged.Count() == 0` — either is acceptable as long as the gate is truthful. |
| Signature missing (no `user.name`/`user.email`) | LibGit2Sharp `repo.Config.BuildSignature(DateTimeOffset.Now)` returns `null`. The service catches this and returns service-failure: "Git user identity is not configured. Set user.name and user.email in your git config." |
| Service failure (IO, repo corruption, etc.) | Caught by the service, returned as service-failure with the exception message. |
| Success | `CommitResult` returns success with the new commit SHA. The ViewModel calls `RefreshCommand` to reload the truth. |

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| **M0** | Lock all decisions in this plan. Complete a LibGit2Sharp proof-of-concept covering 7 scenarios: init a repo, write a file, stage it, unstage it, modify + stage + modify again to confirm dual-state behavior, commit with signature, commit without signature → failure path. Verify the same-file dual-state suppression decision against real LibGit2Sharp output. | Proof-of-concept tests pass; this plan is reviewed and approved before any implementation code is written. The `IGitMutationService` / `GitMutationService` names are grep-verified as unused. |
| **M1** | Implement `IGitMutationService` with `Stage`, `Unstage`, and `Commit` (the latter with `CommitResult`). Register in DI. Replace the body of `SourceControlViewModel.StageFileCommand` / `UnstageFileCommand` to call the mutation seam. After the seam call succeeds, call `RefreshCommand` to reload truth from the orchestrator. The existing placeholder tests are rewritten as seam-backed tests at both service and ViewModel level. **Update stale XML doc comments:** `FileChange.cs` class doc (currently says "Used for static/demo data — no real git operations") and `SourceControlViewModel.cs` class doc (currently says "Commands update UI state but do not execute real git operations"). | Service tests: stage an untracked file, stage a modified file, unstage a file, no-op stage of already-staged, no-op unstage of already-unstaged. ViewModel tests: stage → unstaged count decreases / staged count increases; unstage → reverse; post-mutation refresh is called (verify via mock). The old placeholder tests are removed. |
| **M2** | Wire `CommitCommand` to call `IGitMutationService.Commit` with `CommitMessage`. Implement validation gates (empty message, nothing staged) returning truthful `CommitResult`. On success, call `RefreshCommand` and clear `CommitMessage`. On failure, surface the error via the new `CommitError` property (not `LastRefreshError`/`LastRefreshStatus` — those are reserved for refresh failures). Handle missing-signature failure specially. | Service tests: commit with empty message fails (no git call), commit with nothing staged fails (no git call), commit with staged files and valid message succeeds, commit with missing signature returns failure, commit with IO/repo error returns failure. ViewModel tests: commit clears message on success, commit with nothing staged keeps message, commit failure surfaces error in `CommitError`. |
| **M3** | Ensure the Source Control panel and status surfaces refresh correctly after mutation actions. This covers: selection persistence across post-mutation refresh, diff surface staying visible or updating correctly, the status bar branch name staying truthful, and the stage/unstage/commit loop not degrading over repeated operations. Manual smoke test required: create a new file, stage it, modify it, view its diff, commit it, verify the file disappears from both lists. | Build + tests; focused manual verification for: file creation → stage → diff → commit → refresh loop; staged file unstage → unstaged file restage → commit; empty commit message rejection in UI (button does nothing / shows notice). Exit conditions below must all be checked off. |

## Likely Implementation Shape

### New Files

- `src/Services/IGitMutationService.cs` — interface with `Stage`, `Unstage`, `Commit`
- `src/Services/GitMutationService.cs` — LibGit2Sharp-backed implementation

### Changes to `SourceControlViewModel`

- Inject `IGitMutationService` **and `IGitRepositoryService`** in constructor (the latter for
  repository-root discovery; it is the existing read-only seam, not a new dependency)
- Add a new property: `string? CommitError` — set on commit failure, cleared on successful commit or refresh. This is distinct from `StatusMessage` (which covers refresh-state and stage/unstage notices); `CommitError` is commit-specific so the view can style it differently (e.g. red error text).
- Constructor now has 5 parameters: `ISourceControlSnapshotOrchestrator`, `Workspace`,
  `IFileDiffService`, `IGitMutationService`, `IGitRepositoryService`
  (existing test helpers that mock `ISourceControlSnapshotOrchestrator` and `IFileDiffService`
  must also provide mocks for the two new services)
- The constructor performs initial discovery via `_gitRepositoryService.Discover(...)`
  and caches the result in `_repositoryRoot`. This is done after the initial
  `ApplyResult` call so the repository state is already loaded.
- Replace `StageFileCommand` body:
  ```csharp
  StageFileCommand = ReactiveCommand.CreateFromTask(async (FileChange file) =>
  {
      var result = await Task.Run(() => _mutationService.Stage(_repositoryRoot, file.FilePath));
      if (result.IsSuccess)
          RefreshCommand.Execute().Subscribe();
      else
          StatusMessage = result.ErrorMessage;
      // On failure, refresh is intentionally NOT called — the snapshot is still
      // truthful (the operation simply did nothing). StatusMessage shows why.
      // Mutation errors use StatusMessage because it already has a visible
      // binding in the panel. CommitError is reserved for commit failures only.
  });
  ```
- Replace `UnstageFileCommand` body analogously (failure → `StatusMessage`)
- Replace `CommitCommand` body:
  ```csharp
  CommitCommand = ReactiveCommand.CreateFromTask(async () =>
  {
      var result = await Task.Run(() => _mutationService.Commit(_repositoryRoot, CommitMessage));
      if (result.IsSuccess)
      {
          CommitMessage = string.Empty;
          CommitError = null;
          RefreshCommand.Execute().Subscribe();
      }
      else
      {
          CommitError = result.ErrorMessage;
          // Do NOT set StatusMessage here — StatusMessage is reserved for
          // refresh-state notices (non-repo, failure). CommitError has its own
          // dedicated TextBlock bound in SourceControlPanel, styled as a
          // red error text. Setting StatusMessage would collide with
          // refresh-produced messages and confuse the user.
          // Do NOT set LastRefreshError/LastRefreshStatus — those are for
          // refresh failures only.
      }
  });
  ```
- The `repositoryRoot` is obtained by calling `IGitRepositoryService.Discover(_workspace.WorkspacePath)`
  and reading `RepositoryDiscoveryResult.RepositoryRoot` from the result. This reuses the existing
  read-only seam without modifying it. The root is cached in a `_repositoryRoot` field.
  **Note:** Neither `SnapshotRefreshResult` nor `RepositoryStatusSnapshot` exposes `RepositoryRoot`
  — it lives only on `RepositoryDiscoveryResult`, which is why the ViewModel calls `Discover()`
  directly rather than reading it from the snapshot.

- **How `_repositoryRoot` is kept up to date:** The ViewModel injects `IGitRepositoryService`
  alongside the existing dependencies. Initial discovery happens once in the constructor
  (after the initial `ApplyResult` call). On each subsequent `ApplyResult` call — which is
  only reached on `SnapshotRefreshStatus.Success` — the cached `_repositoryRoot` is already
  valid and does not need rediscovery (the repository root path does not change while the
  workspace is open). If a fresh discover is ever needed (e.g. the user opens a different
  workspace), `ApplyResult` can rediscover: `var d = _gitRepositoryService.Discover(...)`.
  The caching strategy avoids redundant `Discover()` calls on every mutation.
- The `RefreshCommand.Execute().Subscribe()` call after mutation is intentionally fire-and-forget.
  The subscription is not disposed because the command completes synchronously (it is not a long-lived
  observable). This is a pragmatic exception to the CONVENTIONS.md disposal rule.
- **`CommitError` lifecycle:** Cleared on any successful commit (before refresh). Also cleared
  by `ApplyResult` when a fresh snapshot loads, so stale commit errors disappear when the user
  refreshes or makes other changes. `ApplyResult` sets `CommitError = null` at the top of its
  success path.
- **`StatusMessage` lifecycle for mutation errors:** Stage/unstage failures set `StatusMessage`.
  It is cleared on the next successful refresh (existing `ApplyResult` success path sets
  `StatusMessage = null`). This means a stage failure message persists only until the next
  refresh — sufficient for the user to see why the button did nothing.

### Changes to `SourceControlPanel`

- The existing `CreateChangeItemTemplate` already renders stage/unstage buttons
  on each row. Verify they bind correctly to the new commands. No layout redesign is needed.
- **Add a `TextBlock` bound to `CommitError`** below the commit button so that
  commit failures are visible to the user. Style it as an error notice (red text,
  small font, visible only when `CommitError` is non-null). The binding:
  ```csharp
  // In SourceControlPanel constructor, after _commitButton:
  var commitErrorText = new TextBlock
  {
      FontSize = 12,
      Foreground = new SolidColorBrush(Color.Parse("#E05555")),
      TextWrapping = TextWrapping.Wrap,
      Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, 0, LayoutTokens.SpacingMd, LayoutTokens.SpacingSm),
      IsVisible = false
  };

  // In WhenActivated:
  d.Add(this.WhenAnyValue(x => x.ViewModel!.CommitError)
      .Subscribe(err =>
      {
          commitErrorText.Text = err ?? string.Empty;
          commitErrorText.IsVisible = !string.IsNullOrEmpty(err);
      }));
  ```
  Add `commitErrorText` to the layout StackPanel after `_commitButton`.

### Changes to Tests

- `tests/Zaide.Tests/ViewModels/SourceControlViewModelTests.cs`:
  - **Update test helper methods** that construct `SourceControlViewModel` to provide
    mocks for the two new constructor parameters (`IGitMutationService` and
    `IGitRepositoryService`). The existing `CreateOrchestrator()` helpers return
    `ISourceControlSnapshotOrchestrator`; add a companion helper or update the
    `SourceControlViewModel` construction call sites to supply:
    - `Mock.Of<IGitMutationService>()` (default no-op mock for tests that don't
      exercise mutation)
    - `Mock.Of<IGitRepositoryService>()` or a mock that returns a valid
      `RepositoryDiscoveryResult` for tests that need `_repositoryRoot` to be set
  - Remove: `StageFile_MovesFromUnstagedToStaged`, `UnstageFile_MovesFromStagedToUnstaged`,
    `CommitCommand_ClearsStagedAndMessage`
  - Add: stage calls mutation seam then refresh; unstage calls mutation seam then refresh;
    commit with valid message clears input and refreshes; commit with nothing staged does
    not call git mutation seam; commit failure surfaces error in `CommitError`;
    stage/unstage failure surfaces error in `StatusMessage`
- New file: `tests/Zaide.Tests/Services/GitMutationServiceTests.cs`

### DI Registration (`src/Program.cs`)

```csharp
services.AddSingleton<IGitMutationService, GitMutationService>();
```

## Out of Scope

- Push/pull/fetch
- Commit amend (`--amend`)
- Partial-hunk staging (interactive add)
- Branch creation/switching
- History/log UI
- Conflict resolution
- Rich commit author/date editing (uses default signature from git config)

## Limitations (by design)

- Whole-file stage/unstage only — no per-hunk or per-line granularity
- Commit requires at least one staged file and a non-empty message
- Post-commit UX is a full refresh + cleared message; no commit SHA shown
  (can be added if there is demand)
- Same-file staged+unstaged dual state is suppressed when ChangeTypes match
  (see representation decision above)
- Signature is read from git config; there is no UI to set it in-app

## Risk Summary

| Risk | Mitigation |
|------|------------|
| Same file can have both staged and unstaged changes → UI shows only one entry | Accept current suppression; document the decision. If confusing, revisit in a follow-up. |
| `Stage()`/`Unstage()` on an already-clean file throws | Wrap in try/catch; treat as no-op success (LibGit2Sharp generally no-ops gracefully). Verify via proof-of-concept. |
| `BuildSignature()` returns null for unconfigured git identity | Specific error message shown to user; no attempt to commit. |
| Post-mutation refresh clears selection/diff | Already handled by `ApplyResult` selection-recovery (re-selects by path). Verify in M3 manual smoke test. |
| Stage/Unstage on a file that was removed externally between UI read and mutation | `StageResult` returns failure; ViewModel calls refresh to restore truth. Acceptable for M1. |

## Exit Conditions

- [ ] The user can stage files from the Source Control surface (M1)
- [ ] The user can unstage files from the Source Control surface (M1)
- [ ] The user can create a local commit with truthful validation and failure behavior (M2)
- [ ] Post-mutation UI refresh keeps branch/status/change data truthful (M3)
- [ ] `IGitRepositoryService`, `ISourceControlSnapshotOrchestrator`, and `IFileDiffService`
      are unchanged (grep-verified)
- [ ] The three old placeholder tests are gone; new seam-backed tests exist for
      stage, unstage, and commit at both the service and ViewModel levels
- [ ] Stale XML doc comments updated: `FileChange.cs` no longer says "Used for static/demo
      data — no real git operations"; `SourceControlViewModel.cs` no longer says "Commands
      update UI state but do not execute real git operations"
- [ ] Build succeeds: `dotnet build Zaide.slnx --no-restore`
- [ ] Tests pass: `dotnet test Zaide.slnx --no-build`

## Exact Next Step

Begin M0: complete the LibGit2Sharp proof-of-concept (7 scenarios against a
local repo), verify the same-file dual-state suppression decision, and confirm
the `IGitMutationService` / `GitMutationService` names are unused. Only then
proceed to M1 implementation.

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins