# A2 Wiring Audit — `A2_GIT`

**Audit name:** `v1-v3-product-reality`
**Slice:** `A2_GIT` (fifteenth A2 slice; prior:
`A2_AGENT_SEND`, `A2_MULTI_AGENT_ROUTING`,
`A2_TRACE_MEMORY_USAGE_TERMINATION`,
`A2_RESTART_RECOVERY_AND_CONTEXT`,
`A2_TOOLS_PERMISSIONS`,
`A2_AGENT_CREATION_AND_BACKEND_ONBOARDING`,
`A2_TOWNHALL_AND_CONVERSATIONS`,
`A2_FIRST_LAUNCH_AND_SETTINGS`,
`A2_WORKSPACE_AND_PROJECT_OPENING`,
`A2_FILE_NAVIGATION_AND_EDITING`,
`A2_SEARCH_AND_COMMAND_DISCOVERY`,
`A2_BUILD_RUN_AND_TEST`,
`A2_DEBUGGING_AND_OUTPUT`,
`A2_TERMINAL`)
**Evidence date:** 2026-07-31
**Baseline:** branch `master`, HEAD
`dd71cad4b098cb6c81ff77d668425bd015c48234` (matches `origin/master`)
**Method:** read-only source inspection (`rg`, file reads). No app launch,
build, test execution, production-code edits, commits, or pushes.

---

## 1. Audit identity, baseline, and safety boundary

| Check | Result |
|-------|--------|
| Branch | `master` |
| `git rev-parse HEAD` | `dd71cad4b098cb6c81ff77d668425bd015c48234` |
| `git rev-parse origin/master` | `dd71cad4b098cb6c81ff77d668425bd015c48234` |
| Working tree at audit start | Clean (`git status --short` empty) |
| Fourteen published A2 evidence files | Present (Agent Send through Terminal) |
| This slice evidence file before write | Absent |
| A1 acceptance authority | [A1_ACCEPTANCE.md](../A1_ACCEPTANCE.md) (2026-07-30) |
| Production code modified | No |
| Tests modified | No |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` edited | No |
| Earlier evidence edited | No |
| Issues / deferred findings edited | No |
| Real user profile / settings / secrets / workspace read or written | No |
| App launched | No |
| Build or tests run | No |
| A3 executed | No |
| Commit / push | No |

**Safety boundary:** this slice is A2 wiring inspection only. Production
source is verdict authority. Phase 7 / 7.1 / 7.2 / 7.3 / 7.4 plans and
closeout materials, unit tests, and proof-of-concept tests are corroboration
only. Live Avalonia rendering, LibGit2Sharp execution against arbitrary
disk repositories, system `git` CLI environment interaction, credential
helper interaction, and clean-profile Git smoke are not claimed from source
alone. **No real user profile, settings, secrets, or opened workspace path
was accessed.**

**Verdict rows (this slice only):** `A1-GT-01`, `A1-GT-02`, `A1-GT-03`, and
`A1-GT-04` (each exactly once in §3). No new verdicts for AS, MR, TC, TP, AC,
TH, FL, WO, FN, SC, BR, DB, TR, or XX rows.

---

## 2. Sources inspected

### 2.1 Documentation

- [docs-rules.md](../../../../docs-rules.md)
- [AUDIT_PLAN.md](../AUDIT_PLAN.md) (§4 journey 8 Git workflow; §5 schema;
  §17.8 A2 progress)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md) (§8 Git workflow rows `A1-GT-01`
  through `A1-GT-04`; §17.8 progress table)
- [A1_ACCEPTANCE.md](../A1_ACCEPTANCE.md)
- V1 roadmap: [PHASES.md §"Phase 7: Git Integration"](../../../roadmap/PHASES.md#phase-7-git-integration)
- Phase 7 family plans and closeout materials:
  - [phase-7/IMPLEMENTATION_PLAN.md](../../../phases/v1/phase-7/IMPLEMENTATION_PLAN.md)
  - [phase-7.1/IMPLEMENTATION_PLAN.md](../../../phases/v1/phase-7.1/IMPLEMENTATION_PLAN.md)
  - [phase-7.1/M0_SEAM_DECISION.md](../../../phases/v1/phase-7.1/M0_SEAM_DECISION.md)
  - [phase-7.2/IMPLEMENTATION_PLAN.md](../../../phases/v1/phase-7.2/IMPLEMENTATION_PLAN.md)
  - [phase-7.2/M0_UI_TRUTH_POLICY.md](../../../phases/v1/phase-7.2/M0_UI_TRUTH_POLICY.md)
  - [phase-7.3/IMPLEMENTATION_PLAN.md](../../../phases/v1/phase-7.3/IMPLEMENTATION_PLAN.md)
  - [phase-7.3/TOFIX.md](../../../phases/v1/phase-7.3/TOFIX.md)
  - [phase-7.4/IMPLEMENTATION_PLAN.md](../../../phases/v1/phase-7.4/IMPLEMENTATION_PLAN.md)
- Published A2 evidence with shared seam overlap:
  - [A2_WORKSPACE_AND_PROJECT_OPENING.md](./A2_WORKSPACE_AND_PROJECT_OPENING.md)
    (`A1-WO-03` folder open triggering Source Control refresh)
  - [A2_FIRST_LAUNCH_AND_SETTINGS.md](./A2_FIRST_LAUNCH_AND_SETTINGS.md)
    (`A1-FL-01` layout, status bar branch segment)
  - [A2_FILE_NAVIGATION_AND_EDITING.md](./A2_FILE_NAVIGATION_AND_EDITING.md)
    (`A1-FN-01` read-only diff tabs in main editor tab strip)
  - [A2_SEARCH_AND_COMMAND_DISCOVERY.md](./A2_SEARCH_AND_COMMAND_DISCOVERY.md)
    (`A1-SC-01` command registry for `sourcecontrol.commit` and `sourcecontrol.refresh`)

### 2.2 Production source (minimum required + supporting)

**Repository discovery and read status seam**

- [IGitRepositoryService.cs](../../../../src/Features/SourceControl/Contracts/IGitRepositoryService.cs)
- [GitRepositoryService.cs](../../../../src/Features/SourceControl/Infrastructure/GitRepositoryService.cs)
- [GitBranch.cs](../../../../src/Features/SourceControl/Domain/GitBranch.cs)
- [FileChange.cs](../../../../src/Features/SourceControl/Domain/FileChange.cs)
- [FileChangeEvent.cs](../../../../src/Features/SourceControl/Domain/FileChangeEvent.cs)
- [RepositoryDiscoveryResult.cs](../../../../src/Features/SourceControl/Application/RepositoryDiscoveryResult.cs)
- [RepositoryStatusSnapshot.cs](../../../../src/Features/SourceControl/Application/RepositoryStatusSnapshot.cs)

**Snapshot orchestration & context publishing**

- [ISourceControlSnapshotOrchestrator.cs](../../../../src/Features/SourceControl/Contracts/ISourceControlSnapshotOrchestrator.cs)
- [SourceControlSnapshotOrchestrator.cs](../../../../src/Features/SourceControl/Application/SourceControlSnapshotOrchestrator.cs)
- [SnapshotRefreshResult.cs](../../../../src/Features/SourceControl/Application/SnapshotRefreshResult.cs)
- [SnapshotRefreshStatus.cs](../../../../src/Features/SourceControl/Application/SnapshotRefreshResult.cs)
- [SourceControlSnapshotAvailability.cs](../../../../src/Features/SourceControl/Application/SourceControlSnapshotAvailability.cs)
- [SourceControlStatusSnapshot.cs](../../../../src/Features/SourceControl/Application/SourceControlStatusSnapshot.cs)
- [SourceControlSnapshotMapper.cs](../../../../src/Features/SourceControl/Application/SourceControlSnapshotMapper.cs)
- [ISourceControlSnapshotService.cs](../../../../src/Features/SourceControl/Contracts/ISourceControlSnapshotService.cs)
- [ISourceControlSnapshotPublisher.cs](../../../../src/Features/SourceControl/Contracts/ISourceControlSnapshotPublisher.cs)
- [SourceControlSnapshotService.cs](../../../../src/Features/SourceControl/Application/SourceControlSnapshotService.cs)
- [AgentContextSnapshotSources.cs](../../../../src/Features/Agents/Application/AgentContextSnapshotSources.cs)
- [AgentContextContentComposer.cs](../../../../src/Features/Agents/Application/AgentContextContentComposer.cs)

**Unified diff & read-only editor tabs**

- [IFileDiffService.cs](../../../../src/Features/SourceControl/Contracts/IFileDiffService.cs)
- [FileDiffService.cs](../../../../src/Features/SourceControl/Infrastructure/FileDiffService.cs)
- [FileDiffResult.cs](../../../../src/Features/SourceControl/Application/FileDiffResult.cs)
- [ISourceControlDiffTabService.cs](../../../../src/Features/SourceControl/Contracts/ISourceControlDiffTabService.cs)
- [NullSourceControlDiffTabService.cs](../../../../src/Features/SourceControl/Application/NullSourceControlDiffTabService.cs)
- [SourceControlDiffTabService.cs](../../../../src/Features/SourceControl/Application/SourceControlDiffTabService.cs)
- [SourceControlDiffTabKey.cs](../../../../src/Features/SourceControl/Application/SourceControlDiffTabKey.cs)
- [SourceControlDiffContent.cs](../../../../src/Features/SourceControl/Application/SourceControlDiffContent.cs)
- [EditorViewModel.cs](../../../../src/Features/Editor/Presentation/EditorViewModel.cs)
- [EditorView.cs](../../../../src/Features/Editor/Presentation/EditorView.cs)
- [EditorTabViewModel.cs](../../../../src/Features/Editor/Presentation/EditorTabViewModel.cs)

**Mutation seam & action derivation**

- [IGitMutationService.cs](../../../../src/Features/SourceControl/Contracts/IGitMutationService.cs)
- [GitMutationService.cs](../../../../src/Features/SourceControl/Infrastructure/GitMutationService.cs)
- [StageResult.cs](../../../../src/Features/SourceControl/Application/StageResult.cs)
- [CommitResult.cs](../../../../src/Features/SourceControl/Application/CommitResult.cs)
- [PushResult.cs](../../../../src/Features/SourceControl/Application/PushResult.cs)
- [SourceControlPrimaryAction.cs](../../../../src/Features/SourceControl/Domain/SourceControlPrimaryAction.cs)
- [SourceControlActionDeriver.cs](../../../../src/Features/SourceControl/Application/SourceControlActionDeriver.cs)

**Presentation & shell integration**

- [SourceControlViewModel.cs](../../../../src/Features/SourceControl/Presentation/SourceControlViewModel.cs)
- [SourceControlPanel.cs](../../../../src/Features/SourceControl/Presentation/SourceControlPanel.cs)
- [StatusBarViewModel.cs](../../../../src/App/Shell/StatusBarViewModel.cs)
- [StatusBar.cs](../../../../src/App/Shell/StatusBar.cs)
- [MainWindowViewModel.cs](../../../../src/App/Shell/MainWindowViewModel.cs)
- [MainWindow.axaml.cs](../../../../src/App/Shell/MainWindow.axaml.cs)
- [MainLayoutBuilder.cs](../../../../src/App/Shell/MainLayoutBuilder.cs)
- [NavBar.cs](../../../../src/App/Shell/NavBar.cs)
- [ShellPanelNavigation.cs](../../../../src/App/Shell/ShellPanelNavigation.cs)
- [SourceControlServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/SourceControlServiceCollectionExtensions.cs)

### 2.3 Tests inspected

- [GitRepositoryServiceTests.cs](../../../../tests/Zaide.Tests/Features/SourceControl/Infrastructure/GitRepositoryServiceTests.cs)
- [SourceControlSnapshotOrchestratorTests.cs](../../../../tests/Zaide.Tests/Features/SourceControl/Application/SourceControlSnapshotOrchestratorTests.cs)
- [SourceControlSnapshotServiceTests.cs](../../../../tests/Zaide.Tests/Features/SourceControl/Application/SourceControlSnapshotServiceTests.cs)
- [FileDiffServiceTests.cs](../../../../tests/Zaide.Tests/Features/SourceControl/Infrastructure/FileDiffServiceTests.cs)
- [SourceControlDiffTabServiceTests.cs](../../../../tests/Zaide.Tests/Features/SourceControl/Application/SourceControlDiffTabServiceTests.cs)
- [GitMutationServiceTests.cs](../../../../tests/Zaide.Tests/Features/SourceControl/Infrastructure/GitMutationServiceTests.cs)
- [SourceControlActionDeriverTests.cs](../../../../tests/Zaide.Tests/Features/SourceControl/Application/SourceControlActionDeriverTests.cs)
- [SourceControlViewModelTests.cs](../../../../tests/Zaide.Tests/Features/SourceControl/Presentation/SourceControlViewModelTests.cs)
- [SourceControlPanelCommandWiringTests.cs](../../../../tests/Zaide.Tests/Features/SourceControl/Presentation/SourceControlPanelCommandWiringTests.cs)
- [SourceControlMutationFlowTests.cs](../../../../tests/Zaide.Tests/Features/SourceControl/Integration/SourceControlMutationFlowTests.cs)
- [SourceControlRegistrationModuleTests.cs](../../../../tests/Zaide.Tests/App/Composition/SourceControlRegistrationModuleTests.cs)
- [GitBranchTests.cs](../../../../tests/Zaide.Tests/Features/SourceControl/Domain/GitBranchTests.cs)
- [LibGit2SharpDiffProofOfConceptTests.cs](../../../../tests/Zaide.Tests/Features/SourceControl/Infrastructure/LibGit2SharpDiffProofOfConceptTests.cs)
- [LibGit2SharpMutationProofOfConceptTests.cs](../../../../tests/Zaide.Tests/Features/SourceControl/Infrastructure/LibGit2SharpMutationProofOfConceptTests.cs)
- [MainWindowViewModelTests.cs](../../../../tests/Zaide.Tests/App/Shell/MainWindowViewModelTests.cs)

---

## 3. Primary verdicts for Git workflow (`A1-GT-01`–`A1-GT-04`)

| ID | Journey | Roadmap | Phase | Promised Outcome | Primary Verdict | Key Finding / Evidence |
|----|---------|---------|-------|------------------|-----------------|------------------------|
| `A1-GT-01` | git | V1 | Phase 7 / 7.1 | Real repository-backed git seam; Source Control panel and status bar reflect live state; "no repo", "—" labels. | **Wired** | `IGitRepositoryService` / `GitRepositoryService` discovers repo root via `LibGit2Sharp.Repository.Discover` and reads branch/HEAD and working-tree status via LibGit2Sharp `Repository.RetrieveStatus`. `SourceControlSnapshotOrchestrator` projects `Success`, `NotARepository` ("no repo"), or `Failed` ("—"). Folder open and refresh button execute `SourceControlViewModel.RefreshCommand`. Non-repo and failure notes surface via `StatusMessage` in panel. |
| `A1-GT-02` | git | V1 | Phase 7.3 | Basic diff view (unified diff via `Diff.Compare<Patch>()`); binary file notice; refresh-safe selection. | **Wired** | `IFileDiffService` / `FileDiffService` compares `HEAD:index` (staged) or `HEAD:workdir` (unstaged) via LibGit2Sharp `Diff.Compare<Patch>()`. `SourceControlDiffTabService` formats content and opens/updates read-only diff tabs (`diff://...`) in the main editor tab strip. Binary files show `"Binary file — diff not available"`. `SourceControlViewModel.ApplyResult` restores selection by path across refresh. |
| `A1-GT-03` | git | V1 | Phase 7.4 | Stage/unstage files; local commit; commit message validation; truthful error handling. | **Wired** | `IGitMutationService` / `GitMutationService` performs `Commands.Stage`, `Commands.Unstage`, `Commands.StageAll`, and `repo.Commit`. Empty message is guarded before git call ("Commit message cannot be empty."). Nothing staged is guarded before git call ("Nothing staged to commit."). Missing git identity (`user.name`/`user.email`) returns truthful error. `CommitError` displays in red text in panel. Stage/unstage/commit run on `Task.Run` and trigger unconditional `RefreshCommand`. |
| `A1-GT-04` | git | V1 | Phase 7 | Branch display is truthful (current branch). | **Wired** | `GitRepositoryService.ReadStatus` detects detached HEAD (returns commit SHA) or attached branch name (`repo.Head.FriendlyName`). `SourceControlViewModel.CurrentBranchName` updates `StatusBarViewModel.BranchText` via ReactiveUI binding. Status bar displays branch name / SHA with `Icon.GitBranch` icon button, `"no repo"` for non-repos, or `"—"` for refresh errors. |

---

## 4. Cross-cutting topics traced across the Git workflow

### 4.1 Repository discovery and workspace path handling (`A1-GT-01`)

```
Workspace.WorkspacePath
  ↓ (MainWindowViewModel.Activate RootPath subscription / RefreshCommand)
IGitRepositoryService.Discover(workspacePath)
  ↓ (LibGit2Sharp Repository.Discover)
RepositoryDiscoveryResult { IsRepository, RepositoryRoot }
  ↓
IGitRepositoryService.ReadStatus(repositoryRoot)
```

- **Discovery mechanism:** `GitRepositoryService.Discover` calls `LibGit2Sharp.Repository.Discover(startingPath)`. It walks upward from `startingPath` looking for a `.git` folder or git repository marker. Returns `RepositoryDiscoveryResult.Found(startingPath, path)` or `RepositoryDiscoveryResult.NotFound(startingPath)`.
- **Workspace binding:** `SourceControlViewModel` references `global::Zaide.Features.Workspace.Domain.Workspace`. When no workspace folder is open or `WorkspacePath` is null/empty, `SourceControlSnapshotOrchestrator.Refresh` returns `SnapshotRefreshResult.NotARepository(workspacePath, "No workspace is open.")`.
- **Per-operation root resolution:** To prevent stale path bugs when switching workspaces, `SourceControlViewModel` does not cache a static `_repositoryRoot` string in memory. Each mutation command (`StageFileCommand`, `UnstageFileCommand`, `StageAllCommand`, `CommitCommand`, `PushCommand`) calls `_gitRepositoryService.Discover(_workspace.WorkspacePath ?? string.Empty)` inline at execution time to obtain the current `RepositoryRoot`.

### 4.2 Status refresh, explicit triggers, and absence of filesystem watchers (`A1-GT-01`)

- **Refresh triggers:**
  1. **Workspace open:** `MainWindowViewModel.Activate()` subscribes to `FileTreeViewModel.WhenAnyValue(x => x.RootPath)` and invokes `SourceControlViewModel.RefreshCommand` whenever the opened folder changes.
  2. **Panel switch:** `MainWindow.axaml.cs` subscribes to `LeftPanelMode` changes and invokes `SourceControlViewModel.RefreshCommand` when switching to `LeftPanelMode.SourceControl`.
  3. **Explicit button:** `SourceControlPanel` contains a header refresh button (`Icon.ArrowClockwise`) bound to `RefreshCommand` (with `Unit.Default` event-stream projection).
  4. **Post-mutation auto-refresh:** Every stage, unstage, stage-all, commit, and push action calls `RefreshCommand.Execute().Subscribe()` unconditionally upon completion (both success and failure) to restore repository truth.
  5. **Palette command:** `sourcecontrol.refresh` is registered in `CommandRegistry` and invokable via Command Palette (`Ctrl+Shift+P`).
- **Absence of automatic filesystem watcher:** Production code does not run a background `FileSystemWatcher` or git polling loop for Source Control status changes. External filesystem modifications (e.g. CLI git operations or external text editor saves) become visible in Zaide's UI upon explicit user refresh, panel switch, or post-mutation refresh. This is an intentional Phase 7 design limitation documented in [phase-7/IMPLEMENTATION_PLAN.md](../../../phases/v1/phase-7/IMPLEMENTATION_PLAN.md).

### 4.3 Unified diff generation, binary file handling, and read-only diff tabs (`A1-GT-02`)

```
User selects file row in Source Control ListBox
  ↓ (SelectFileCommand)
IFileDiffService.GetDiff(repoRoot, change)
  ↓ (LibGit2Sharp Diff.Compare<TreeChanges> + Diff.Compare<Patch>)
FileDiffResult { FilePath, IsBinary, DiffText, AddedLines, DeletedLines }
  ↓
ISourceControlDiffTabService.OpenOrUpdateDiff(change)
  ↓ (SourceControlDiffContent.Format)
IEditorReadOnlyTabService.OpenOrUpdate(EditorReadOnlyTabRequest)
  ↓
EditorViewModel { IsSourceControlDiff = true, SourceControlDiffKey = "diff://...", Content = diffText }
```

- **Diff targets:** `FileDiffService.GetDiff` selects diff targets based on `change.IsStaged`:
  - `change.IsStaged == true` → `DiffTargets.Index` (compares `HEAD` commit tree against index / staged changes).
  - `change.IsStaged == false` → `DiffTargets.WorkingDirectory` (compares `HEAD` commit tree against working directory).
- **TreeChanges & Patch pipeline:**
  1. `repo.Diff.Compare<TreeChanges>(repo.Head.Tip.Tree, diffTargets, filePaths)` verifies path existence in the diff set. If path is absent, returns `null`.
  2. `repo.Diff.Compare<Patch>(repo.Head.Tip.Tree, diffTargets, filePaths)` generates the patch entry for `change.FilePath`.
- **Binary file detection:** If `patchEntry.IsBinaryComparison` is true, `FileDiffService` returns `FileDiffResult` with `IsBinary = true` and `DiffText = null`. `SourceControlDiffContent.Format` formats this into `"Binary file — diff not available"`.
- **Read-only editor tab integration:** Diffs are displayed as read-only document tabs in the main editor tab strip rather than inline accordion rows.
  - Virtual path: `<FilePath> (Diff)`.
  - Reuse key: `diff://<FilePath>`.
  - Comparison state label: `"Staged Changes"` (if staged) or `"Changes"` (if unstaged).
  - Tab content: formatted header with file path, change type, and staged/unstaged classification, followed by full unified diff text.
- **Diff text rendering:** Rendered in AvaloniaEdit via `EditorView` with `IsSourceControlDiff = true`. Rendered as plain monospace document text without line-level addition/deletion background coloring.

### 4.4 Selection persistence across snapshot refresh (`A1-GT-02`)

- **Path-based re-selection:** When `SourceControlViewModel.ApplyResult` processes a new snapshot, it captures `previouslySelectedPath = _selectedFilePath`.
- After clearing and repopulating `UnstagedChanges` and `StagedChanges`, it searches both collections for a `FileChange` with matching `FilePath`.
- **Match found:** Restores `SelectedFileChange` and calls `_diffTabService.RefreshOpenDiff(match.FilePath, match)`, updating the open diff tab with fresh diff text.
- **Match not found (file committed, reverted, or deleted):** Sets `SelectedFileChange = null`, `SelectedFilePath = null`, and calls `_diffTabService.RefreshOpenDiff(previouslySelectedPath, change: null)`. `SourceControlDiffContent.FormatUnavailable` updates the open diff tab with `"Diff unavailable for <FilePath>"` instead of closing the tab unexpectedly.

### 4.5 Staging and unstaging operations (`A1-GT-03`)

- **Single file stage/unstage:**
  - Stage button (`+`) and Unstage button (`−`) on each list row in `SourceControlPanel` execute `StageFileCommand` / `UnstageFileCommand`.
  - `GitMutationService.Stage` executes `LibGit2Sharp.Commands.Stage(repo, filePath)`.
  - `GitMutationService.Unstage` executes `LibGit2Sharp.Commands.Unstage(repo, filePath)`.
  - Both operations are whole-file (no partial hunk or line staging).
- **Stage All:**
  - "Stage All" button in `SourceControlPanel` executes `StageAllCommand`.
  - Guarded by `canStageAll = UnstagedCount > 0`.
  - Snapshots unstaged file paths before async execution and calls `GitMutationService.StageAll(repoRoot, filePaths)` which executes `LibGit2Sharp.Commands.Stage(repo, filePaths)` in a single repository open.
- **Off-thread execution:** All mutation methods are executed off the UI thread via `Task.Run` inside `ReactiveCommand.CreateFromTask`.
- **Unconditional refresh:** `RefreshCommand.Execute().Subscribe()` is invoked after every stage/unstage operation to synchronize UI collections with LibGit2Sharp repository index truth.

### 4.6 Local commit execution and multi-layer validation (`A1-GT-03`)

- **Validation layers:**
  1. **Empty message guard (UI & Service):** `SourceControlViewModel.ExecuteCommitAsync` checks `string.IsNullOrWhiteSpace(CommitMessage)`. If empty, sets `CommitError = "Commit message cannot be empty."` and aborts before any git call or repository open. `GitMutationService.Commit` also enforces `string.IsNullOrWhiteSpace(message)` as a service-level guard.
  2. **Nothing staged guard (UI & Service):** `SourceControlViewModel.ExecuteCommitAsync` checks `StagedChanges.Count == 0`. If zero, sets `CommitError = "Nothing staged to commit."` and aborts before calling the mutation service. `GitMutationService.Commit` also inspects `repo.RetrieveStatus()` via `HasStagedChanges()` and returns `CommitResult.Failure("Nothing staged to commit.")` if no index changes exist.
  3. **Git user identity guard (Service):** `GitMutationService.Commit` calls `repo.Config.BuildSignature(DateTimeOffset.Now)`. If `user.name` or `user.email` is not configured in git config, `BuildSignature` returns `null`. The service catches this and returns `CommitResult.Failure("Git user identity is not configured. Set user.name and user.email in your git config.")`.
- **Commit creation:** Calls `repo.Commit(message, signature, signature)`. Returns `CommitResult.Success(commit.Sha)`.
- **Error surface:** On commit failure, `SourceControlViewModel.CommitError` is populated. `SourceControlPanel` renders `CommitError` in a dedicated red text block (`#E05555`) below the commit button. `CommitError` is distinct from `StatusMessage` (used for refresh/non-repo notices) and `LastRefreshError`.
- **Success cleanup:** On successful commit, `CommitMessage` is cleared to `string.Empty`, `CommitError` is set to `null`, and `RefreshCommand` reloads repository status.

### 4.7 Branch display, detached-HEAD behavior, and branch selector ComboBox (`A1-GT-04`, `A1-GT-01`)

- **Branch detection:** `GitRepositoryService.ReadStatus` inspects `repo.Info.IsHeadDetached`.
  - **Attached HEAD:** `currentBranchName = repo.Head.FriendlyName ?? string.Empty`.
  - **Detached HEAD:** `currentBranchName = repo.Head.Tip?.Sha ?? string.Empty` (full 40-character commit SHA).
  - **Branches collection:** Enumerates local non-remote branches: `repo.Branches.Where(b => !b.IsRemote).Select(b => new GitBranch(b.FriendlyName, b.IsCurrentRepositoryHead))`.
- **Status bar branch display:**
  - `StatusBarViewModel` subscribes to `MainWindowViewModel.SourceControlViewModel.CurrentBranchName` and sets `BranchText = value ?? ""`.
  - `StatusBar` view binds `BranchText` and renders an icon button (`Icon.GitBranch` + `_branchText`).
  - **Normal branch:** Displays branch name (e.g. `master`, `main`, `feature/foo`).
  - **Detached HEAD:** Displays commit SHA.
  - **Non-repo workspace:** Displays `"no repo"`.
  - **Refresh error:** Displays `"—"`.
- **Branch selector ComboBox in panel:**
  - `SourceControlPanel` includes a `ComboBox` (`_branchSelector`) bound to `SourceControlViewModel.Branches`.
  - `SelectedItem` is bound to `SourceControlViewModel.SelectedBranch`.
  - Selecting a branch in the ComboBox executes `SelectBranchCommand`, updating `SelectedBranch` and `CurrentBranchName` in ViewModel memory.
  - **Checkout non-mutation:** The ComboBox is display/selection-oriented; `SelectBranchCommand` does **not** call `git checkout` or mutate repository HEAD on disk. On the next status refresh, `CurrentBranchName` is restored to repository truth (`repo.Head.FriendlyName`). Broad branch checkout/creation UX was explicitly excluded from V1 Phase 7 scope ([phase-7/IMPLEMENTATION_PLAN.md §"Phase-Level Decisions" #5](../../../phases/v1/phase-7/IMPLEMENTATION_PLAN.md)).

### 4.8 Remote actions, push behavior, and out-of-scope boundaries (`A1-GT-03`, `A1-GT-01`)

- **Primary action button derivation:** `SourceControlActionDeriver.Derive` calculates `PrimaryAction` (`Commit` or `Push`):
  - If working tree has any unstaged or staged changes → `SourceControlPrimaryAction.Commit`.
  - If working tree is clean, branch has configured upstream (`HasUpstream == true`), and local branch is ahead of remote (`AheadBy > 0`) → `SourceControlPrimaryAction.Push`.
  - Otherwise → `SourceControlPrimaryAction.Commit`.
- **Push execution seam:** When `PrimaryAction` is `Push` and the button is clicked, `SourceControlViewModel.ExecutePushAsync` calls `IGitMutationService.Push`.
  - `GitMutationService.Push` validates: non-detached HEAD, clean working tree, configured tracking branch (`TrackedBranch != null`), and `AheadBy > 0`.
  - Delegates to `PushViaGitCli(repositoryRoot)`: spawns `Process.Start("git", "push")` in the repository working directory so system SSH agents, HTTPS credential helpers, and local SSH configs function natively.
  - On exit code 0: returns `PushResult.Success()`, ViewModel sets `ActionNotice = "Pushed <branch>."` and refreshes. On error: returns `PushResult.Failure(stderr)` and ViewModel populates `PushError`.
- **Out-of-scope remote operations:** As defined in [AUDIT_PLAN.md §4 journey 8](../AUDIT_PLAN.md), `git pull`, `git fetch`, `git merge`, `git rebase`, `git stash`, remote creation, and PR workflows are out of V1–V3 scope and remain un-wired in production.

### 4.9 Agent context snapshot publishing (`A1-GT-01`)

- **Context integration:** `SourceControlViewModel.PublishSnapshot` passes `SourceControlStatusSnapshot` to `ISourceControlSnapshotPublisher` (`SourceControlSnapshotService`).
- `AgentContextSnapshotSources` exposes `SourceControl => _sourceControlSnapshotService.Current`.
- `AgentContextContentComposer.ComposeSourceControlSummary` reads the snapshot to include `branch=<branchName>`, staged count, and unstaged count in agent context prompt assemblies.

---

## 5. Summary of verdicts, gaps, and hand-off to A3 / re-audit

### 5.1 Verdict Summary

| ID | Journey | Verdict | Summary |
|----|---------|---------|---------|
| `A1-GT-01` | git | **Wired** | Real LibGit2Sharp repository discovery and read status seam (`IGitRepositoryService` / `SourceControlSnapshotOrchestrator`). Truthful panel change lists, status message notes, and status bar `"no repo"` / `"—"` labels. |
| `A1-GT-02` | git | **Wired** | Unified diff generation via LibGit2Sharp `Diff.Compare<Patch>()` (`IFileDiffService`). Diffs displayed as read-only editor tabs (`ISourceControlDiffTabService`). Binary files show inline notice. Selection survives refresh by file path. |
| `A1-GT-03` | git | **Wired** | Local staging, unstaging, stage-all, and commit execution via LibGit2Sharp (`IGitMutationService`). Empty commit message, nothing staged, and unconfigured git identity guarded with truthful error feedback in red text block (`CommitError`). Off-thread execution with unconditional refresh. |
| `A1-GT-04` | git | **Wired** | Status bar branch text reflects current branch name (`repo.Head.FriendlyName`), commit SHA on detached HEAD, `"no repo"` for non-repositories, or `"—"` on refresh failure. |

### 5.2 Identified Gaps & Limitations (by design)

1. **No background filesystem watcher for Git status:** Status refresh occurs on explicit user triggers (workspace open, panel switch, header refresh button, post-mutation). Changes made externally in CLI or other editors require manual refresh or panel re-entry to update Zaide's UI.
2. **Same-file dual-state (staged + unstaged) representation:** When a file has identical `ChangeType` in both index and workdir (e.g. modified, staged, then modified again), the unstaged entry is suppressed in `GitRepositoryService.ToChanges` so only the staged entry appears in the list.
3. **Plaintext diff tab rendering:** Diffs are rendered in AvaloniaEdit as plain monospace document text without line-level background colorization for additions/deletions.
4. **Branch ComboBox is selection-only:** Selecting a branch in the panel ComboBox updates memory selection state but does not invoke `git checkout` on disk.
5. **Whole-file staging only:** Staging and unstaging operate on entire files; per-hunk or per-line interactive staging is absent.

### 5.3 Hand-off to A3 / Re-Audit

- **A2 Wiring Audit for `A2_GIT` is complete.**
- **A3 Clean-Profile Smoke Test:** A3 scenario for `git` can verify:
  1. Opening a git repository vs non-repo directory on clean disposable profile → status bar branch label `"no repo"` vs live branch name.
  2. Selecting a modified text file → read-only diff tab opens with unified patch content.
  3. Staging a file (`+` button) → moves from Changes to Staged.
  4. Attempting commit with empty message → red validation error `"Commit message cannot be empty."`.
  5. Committing staged change with message → commit succeeds, change list clears.
- **Next A2 slice:** All 15 A2 audit slices (`A2_AGENT_SEND` through `A2_GIT`) are now complete and published. The next audit phase is A3 or cross-slice A2 consolidation as governed by `AUDIT_PLAN.md`. The next A2 slice is explicitly **not started** in this file.
