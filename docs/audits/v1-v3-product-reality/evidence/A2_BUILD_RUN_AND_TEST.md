# A2 Wiring Audit — `A2_BUILD_RUN_AND_TEST`

**Audit name:** `v1-v3-product-reality`
**Slice:** `A2_BUILD_RUN_AND_TEST` (twelfth A2 slice; prior:
`A2_AGENT_SEND`, `A2_MULTI_AGENT_ROUTING`,
`A2_TRACE_MEMORY_USAGE_TERMINATION`, `A2_RESTART_RECOVERY_AND_CONTEXT`,
`A2_TOOLS_PERMISSIONS`, `A2_AGENT_CREATION_AND_BACKEND_ONBOARDING`,
`A2_TOWNHALL_AND_CONVERSATIONS`, `A2_FIRST_LAUNCH_AND_SETTINGS`,
`A2_WORKSPACE_AND_PROJECT_OPENING`, `A2_FILE_NAVIGATION_AND_EDITING`,
`A2_SEARCH_AND_COMMAND_DISCOVERY`)
**Evidence date:** 2026-07-31
**Baseline:** branch `master`, HEAD
`a154a91f9fc8fb996e1acdd4849bdb59482ad268` (matches `origin/master`)
**Method:** read-only source inspection (`rg`, file reads). No app launch,
build, test execution, production-code edits, commits, or pushes.

---

## 1. Audit identity, baseline, and safety boundary

| Check | Result |
|-------|--------|
| Branch | `master` |
| `git rev-parse HEAD` | `a154a91f9fc8fb996e1acdd4849bdb59482ad268` |
| `git rev-parse origin/master` | `a154a91f9fc8fb996e1acdd4849bdb59482ad268` |
| Working tree at audit start | Clean (`git status --short` empty) |
| Eleven published A2 evidence files | Present (Agent Send, Multi-Agent Routing, Trace/Memory/Usage/Termination, Restart/Recovery/Context, Tools/Permissions, Agent Creation/Backend Onboarding, Townhall/Conversations, First Launch/Settings, Workspace/Project Opening, File Navigation/Editing, Search/Command Discovery) |
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
source is verdict authority. Phase 11 milestone evidence and unit/VM tests
are corroboration only. Live command-palette / keybinding delivery, real
`dotnet build` / `dotnet test` invocation against a real fixture tree,
`FileSystemWatcher` / `Process.Start` event delivery, and live bottom-panel
visibility are not claimed from source alone. **No real user profile,
settings, secrets, or opened workspace path was accessed.**

**Verdict rows (this slice only):** `A1-BR-01` … `A1-BR-04`. No new
verdicts for AS, MR, TC, TP, AC, TH, FL, WO, FN, SC, DB, TR, GT, or XX
rows. Shared seam overlap with prior slices is called out under each row
but the row verdicts remain scoped to Phase 11 / `IProjectWorkflowService`.

---

## 2. Sources inspected

### 2.1 Documentation

- [docs-rules.md](../../../../docs-rules.md)
- [AUDIT_PLAN.md](../AUDIT_PLAN.md) (§4 journey 5; §5 schema; §6 quality
  gates; §17.8 A2 progress)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md) (§5 Build / run / test rows
  `A1-BR-01`…`A1-BR-04`)
- [A1_ACCEPTANCE.md](../A1_ACCEPTANCE.md)
- [V2.md §"Phase 11 — Project Workflow"](../../../roadmap/V2.md#phase-11--project-workflow--complete-m0m6-2026-07-14)
- [Phase 11 plan](../../../phases/v2/phase-11/IMPLEMENTATION_PLAN.md)
  (Locked contracts 1–8; Milestones M0–M6; Limitations F3, F7–F11;
  Resolved decisions U1–U7)
- Phase 11 milestone evidence (corroboration only — not verdict authority):
  [M2_MANUAL_EVIDENCE.md](../../../phases/v2/phase-11/M2_MANUAL_EVIDENCE.md),
  [M3_MANUAL_EVIDENCE.md](../../../phases/v2/phase-11/M3_MANUAL_EVIDENCE.md),
  [M4_MANUAL_EVIDENCE.md](../../../phases/v2/phase-11/M4_MANUAL_EVIDENCE.md),
  [M5_MANUAL_EVIDENCE.md](../../../phases/v2/phase-11/M5_MANUAL_EVIDENCE.md),
  [M6_MANUAL_EVIDENCE.md](../../../phases/v2/phase-11/M6_MANUAL_EVIDENCE.md),
  [M0_DISCOVERY_PROOF.md](../../../phases/v2/phase-11/M0_DISCOVERY_PROOF.md)
- Published A2 evidence with shared seam overlap:
  [A2_FILE_NAVIGATION_AND_EDITING.md](./A2_FILE_NAVIGATION_AND_EDITING.md)
  (Problems navigation seam and LspUtf16PositionMapper caveats);
  [A2_SEARCH_AND_COMMAND_DISCOVERY.md](./A2_SEARCH_AND_COMMAND_DISCOVERY.md)
  (`project.build` / `run` / `test` / `cancel` registry row, §4.3
  command-registration inventory; binding-materialization context);
  [A2_WORKSPACE_AND_PROJECT_OPENING.md](./A2_WORKSPACE_AND_PROJECT_OPENING.md)
  (single shared `IProjectContextService` consumers include
  `ProjectWorkflowService`; ambiguous-picker gap context for `WO-02`)

### 2.2 Production source (minimum required + supporting)

**Workflow core (M1)**

- [IProjectWorkflowService.cs](../../../../src/Features/ProjectSystem/Contracts/IProjectWorkflowService.cs)
  (target resolution, one-at-a-time, cancel, generation contract)
- [IManagedProcessRunner.cs](../../../../src/Features/ProjectSystem/Contracts/IManagedProcessRunner.cs)
  (redirected child process; not PTY)
- [IProjectOperationGate.cs](../../../../src/Features/ProjectSystem/Contracts/IProjectOperationGate.cs)
  (shared Build/Run/Test admission slot)
- [ProjectWorkflowService.cs](../../../../src/Features/ProjectSystem/Infrastructure/ProjectWorkflowService.cs)
  (generation, cancel, `RejectedConcurrent` / `RejectedContext`,
  context-change cancel, dispose-before-language)
- [ProjectOperationGate.cs](../../../../src/Features/ProjectSystem/Infrastructure/ProjectOperationGate.cs)
  (admission mutex; debug-block check; `WorkflowBusy` /
  `DebugSessionActive` rejection reasons)
- [ManagedProcessRunner.cs](../../../../src/Features/ProjectSystem/Infrastructure/ManagedProcessRunner.cs)
  (`System.Diagnostics.Process` redirected stdout/stderr, kill tree)
- [ProjectTargetResolver.cs](../../../../src/Features/ProjectSystem/Infrastructure/ProjectTargetResolver.cs)
  (eligibility + per-operation gate; Run refuses `Solution` / `SolutionX`)
- [ProjectExecutionProfileResolver.cs](../../../../src/Features/ProjectSystem/Infrastructure/ProjectExecutionProfileResolver.cs)
  (locked argv: `dotnet build <file>`,
  `dotnet test <file>`, `dotnet run --project <file>`)
- [ProjectContext.cs](../../../../src/Features/ProjectSystem/Domain/ProjectContext.cs)
  and
  [ProjectContextState.cs](../../../../src/Features/ProjectSystem/Domain/ProjectContextState.cs)
  (authoritative context that drives target resolution)
- [ProjectWorkflowSnapshot.cs](../../../../src/Features/ProjectSystem/Domain/ProjectWorkflowSnapshot.cs)
  (immutable workflow state; `LastOperation`; outcome kinds)
- [ProjectWorkflowOperationState.cs](../../../../src/Features/ProjectSystem/Domain/ProjectWorkflowOperationState.cs)

**Build / Run / Test command surface (M2 + M4 + M5)**

- [ProjectWorkflowViewModel.cs](../../../../src/Features/ProjectSystem/Presentation/ProjectWorkflowViewModel.cs)
  (`BuildCommand` / `RunCommand` / `TestCommand` / `CancelCommand`;
  `CommandDescriptor` registrations for `project.build` / `run` /
  `test` / `cancel`; `WhenShowOutputRequested`; `WhenShowTestResultsRequested`;
  F9 save-before-workflow guard; `SaveAllDirtyTabsAsync`)
- [MainWindowViewModel.cs](../../../../src/App/Shell/MainWindowViewModel.cs)
  (DI singleton; wires `ProjectWorkflowViewModel.SaveAllDirtyTabsAsync`;
  bottom-panel mode and `BottomPanelMode` enum)

**Output panel (M2)**

- [IProjectOutputService.cs](../../../../src/Features/ProjectSystem/Contracts/IProjectOutputService.cs)
  (`Current` / `WhenChanged` / `WhenLineReceived`)
- [ProjectOutputService.cs](../../../../src/Features/ProjectSystem/Infrastructure/ProjectOutputService.cs)
  (maps workflow snapshots; subscribes to
  `IProjectWorkflowService.WhenOutputReceived`)
- [OutputPanel.cs](../../../../src/Features/ProjectSystem/Presentation/OutputPanel.cs)
  (structured line list; status text; cancel button via shared
  `ProjectWorkflowViewModel.CancelCommand`; F11 scroll-follow)
- [OutputLineViewModel.cs](../../../../src/Features/ProjectSystem/Presentation/OutputLineViewModel.cs)
  (`[HH:mm:ss.fff] [stdout|stderr] <line>` formatting)

**Problems projection from build (M3)**

- [IBuildDiagnosticsService.cs](../../../../src/Features/ProjectSystem/Contracts/IBuildDiagnosticsService.cs)
- [BuildDiagnosticsService.cs](../../../../src/Features/ProjectSystem/Infrastructure/BuildDiagnosticsService.cs)
  (subscribes to workflow; clears on build start, parses at build end)
- [BuildDiagnosticParser.cs](../../../../src/Features/ProjectSystem/Domain/BuildDiagnosticParser.cs)
  (English MSBuild CLI form: `path(line,col): error|warning|done|message [CODE:] message`; severity → LSP levels; relative paths → target parent)
- [BuildDiagnosticSources.cs](../../../../src/Features/ProjectSystem/Domain/BuildDiagnosticSources.cs)
  (`public const string Build = "build";` — Problems `[build]` source label)
- [ProblemsViewModel.cs](../../../../src/Features/ProjectSystem/Presentation/ProblemsViewModel.cs)
  (`_languageProblems` + `_buildProblems` merge; `RebuildProblemsList`
  keeps language items before build items; `NavigateToBuildProblemAsync`
  re-validates live generation + file; LSP items never cleared on build)
- [ProblemItemViewModel.cs](../../../../src/Features/ProjectSystem/Presentation/ProblemItemViewModel.cs)
  (two constructors: `LanguageDiagnostic` vs
  `BuildDiagnostic` + `buildGeneration`; `Kind` =
  `ProblemKind.Language` / `ProblemKind.Build`; `Source = "build"`
  for build items)
- [ProblemsPanel.cs](../../../../src/Features/ProjectSystem/Presentation/ProblemsPanel.cs)
  (status text, count, list; double-click / Enter
  invokes `NavigateToProblemCommand`)

**Test Results surface (M5)**

- [ITestResultsService.cs](../../../../src/Features/ProjectSystem/Contracts/ITestResultsService.cs)
- [TestResultsService.cs](../../../../src/Features/ProjectSystem/Infrastructure/TestResultsService.cs)
  (clears on test start; parses at test end; `IsStructurallyComplete` for fail summary)
- [TestResultsParser.cs](../../../../src/Features/ProjectSystem/Domain/TestResultsParser.cs)
  (console-first; `Passed!` / `Failed!` banner + VSTest + xUnit variants;
  fail-open; never invents passes)
- [TestResultsSummary.cs](../../../../src/Features/ProjectSystem/Domain/TestResultsSummary.cs),
  [TestCaseResult.cs](../../../../src/Features/ProjectSystem/Domain/TestCaseResult.cs)
- [TestResultsViewModel.cs](../../../../src/Features/ProjectSystem/Presentation/TestResultsViewModel.cs)
  (`SummaryText` / `StatusMessage`; `NavigateToCaseCommand` via
  `LspUtf16PositionMapper.TryGetOffset` + `EditorTabViewModel.OpenFileCommand`;
  exposes `Workflow` for shared Cancel button)
- [TestResultsPanel.cs](../../../../src/Features/ProjectSystem/Presentation/TestResultsPanel.cs)
  (summary + status + case list; double-click / Enter navigation;
  cancel button shares `ProjectWorkflowViewModel.CancelCommand`)
- [TestCaseItemViewModel.cs](../../../../src/Features/ProjectSystem/Presentation/TestCaseItemViewModel.cs)
  (`CanNavigate` = non-empty `FilePath` + positive `Line`)

**Bottom-panel host (Output / Test Results / Terminal separation)**

- [BottomPanelHost.cs](../../../../src/App/Shell/BottomPanelHost.cs)
  (`ApplyBottomPanelMode` toggles `IsVisible` for each child host
  independently; `TerminalTabHost` vs `OutputPanel` vs
  `TestResultsPanel` vs `ProblemsPanel` vs `DebugPanel` are separate
  `UserControl` children of the same `Grid` row)
- [ShellPanelNavigation.cs](../../../../src/App/Shell/ShellPanelNavigation.cs)
  (per-mode `SwitchTo*BottomCommand`; each sets
  `BottomPanelMode` + `IsBottomPanelVisible = true`)
- [MainWindowActivationHost.cs](../../../../src/App/Shell/MainWindowActivationHost.cs)
  (`_projectWorkflowViewModel.WhenShowOutputRequested` →
  `BottomPanelMode.Output`; `WhenShowTestResultsRequested` →
  `BottomPanelMode.TestResults`; `Activate` also projects problems +
  test results)
- [MainWindow.axaml.cs](../../../../src/App/Shell/MainWindow.axaml.cs)
  (`_outputPanel.ViewModel = ViewModel.ProjectWorkflowViewModel`;
  `_testResultsPanel.ViewModel = ViewModel.TestResultsViewModel`;
  `_terminalTabHost.SetHost(ViewModel!.TerminalHost)`)

**Terminal seam (non-merge check for `A1-BR-04`)**

- [ITerminalHost.cs](../../../../src/Features/Terminal/Presentation/ITerminalHost.cs)
- [TerminalTabHost.cs](../../../../src/Features/Terminal/Presentation/TerminalTabHost.cs)
  (PTY-backed; not used by `IProjectWorkflowService`; confirmed by
  no caller wiring from `IProjectOutputService` /
  `IBuildDiagnosticsService` / `ITestResultsService` to
  `ITerminalHost`)

**Composition / DI / shutdown**

- [ProjectSystemServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/ProjectSystemServiceCollectionExtensions.cs)
  (singletons: `IProjectContextService`, `IProjectOperationGate`,
  `IManagedProcessRunner`, `IProjectWorkflowService`,
  `IProjectOutputService`, `ProjectWorkflowViewModel`,
  `IBuildDiagnosticsService`, `ITestResultsService`,
  `TestResultsViewModel`, `ProblemsViewModel`)
- [AppCoreServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AppCoreServiceCollectionExtensions.cs)
  (`ICommandRegistry` singleton so DI resolves
  `ProjectWorkflowViewModel(ICommandRegistry? commandRegistry = null)`
  with the registry instance, not null)
- [App.axaml.cs](../../../../src/App/Composition/App.axaml.cs)
  (eager `GetRequiredService<MainWindowViewModel>()` + `ICommandRegistry`
  resolution; `desktop.Exit` → `ApplicationShutdown.Run`)
- [ApplicationShutdown.cs](../../../../src/App/Composition/ApplicationShutdown.cs)
  (F10 dispose order: workflow first, then Output /
  `IBuildDiagnosticsService` / `ITestResultsService`, then language
  stack, then `IProjectContextService`, then `ITerminalHost`)

### 2.3 Tests (corroboration only; not verdict authority)

- `tests/Zaide.Tests/Features/ProjectSystem/Infrastructure/ProjectBuildCommandTests.cs`
  (`project.build` / `project.cancel` registration, CanExecute matrix,
  show-on-build, `RejectedContext`, `RejectedConcurrent`)
- `tests/Zaide.Tests/Features/ProjectSystem/Infrastructure/ProjectTestCommandTests.cs`
  (`project.test` registration + CanExecute matrix)
- `tests/Zaide.Tests/Features/ProjectSystem/Infrastructure/BuildDiagnosticsServiceTests.cs`
  (clear-on-build, parse-on-terminal, generation retention, partial on
  `Cancelled`)
- `tests/Zaide.Tests/Features/ProjectSystem/Infrastructure/ProblemsBuildProjectionTests.cs`
  (LSP retention across build lifecycle; navigation re-validation)
- `tests/Zaide.Tests/Features/ProjectSystem/Infrastructure/ProjectOutputServiceTests.cs`
  (per-line `WhenLineReceived`; clear-on-start; cancel terminal)
- `tests/Zaide.Tests/Features/ProjectSystem/Infrastructure/TestResultsServiceTests.cs`
  (U4 fail-open; partial summary)
- `tests/Zaide.Tests/Features/ProjectSystem/DI/ProjectWorkflowServiceDiTests.cs`
  (DI singleton resolution + dispose-order)
- `tests/Zaide.Tests/Features/ProjectSystem/Infrastructure/ProjectWorkflowServiceTests.cs`
  (target resolution, one-at-a-time, `RejectedConcurrent`, generation,
  cancel, context-change cancel, dispose kill)
- `tests/Zaide.Tests/Features/ProjectSystem/Infrastructure/ProjectTargetResolutionTests.cs`,
  `ManagedProcessRunnerTests.cs` (per milestone)
- All tests above are **not** executed by this A2 slice; they are
  inspected for the same source seams already cited above and to
  confirm test-side mirrors of the documented contracts.

---

## 3. Four-row verdict table

| id | a2_wiring_verdict | Summary |
|----|-------------------|---------|
| `A1-BR-01` | **Wired** | `IProjectWorkflowService` (singleton) and `ProjectWorkflowViewModel` (singleton, registered with `ICommandRegistry`) compose the full Phase 11 surface: target resolution reads **only** `IProjectContextService.Current` via `ProjectTargetResolver`; `ProjectExecutionProfileResolver` produces locked argv (`dotnet build <file>` / `dotnet run --project <file>` / `dotnet test <file>`); `BuildCommand` / `RunCommand` / `TestCommand` / `CancelCommand` are `ReactiveCommand`s with `canBuild` / `canRun` / `canTest` / `canCancel` predicates that combine context eligibility, workflow state, debug state, and `IProjectOperationGate.IsDebugHandoffActive`; `IProjectOperationGate` enforces one-at-a-time Build/Run/Test admission (`WorkflowBusy` / `DebugSessionActive` reasons) and the workflow service **also** re-checks `_current.State` so UI gating and programmatic `Start*Async` calls cannot diverge; `CancelAsync` cancels the operation `CancellationTokenSource` and `_runner.KillAsync()` (process tree); `WhenOutputReceived` streams redirected stdout/stderr lines and `WhenChanged` publishes state snapshots; the **structured Output** projection `IProjectOutputService → OutputPanel` (with `BottomPanelMode.Output`) is reached via `MainWindowActivationHost` `_projectWorkflowViewModel.WhenShowOutputRequested`; the show-on-start affordance fires on `Build` / `Run` / `Test` `Starting`, never on `RejectedContext`; the Test Results projection `ITestResultsService → TestResultsViewModel → TestResultsPanel` (with `BottomPanelMode.TestResults`) is reached via `_projectWorkflowViewModel.WhenShowTestResultsRequested`. App dispose order (`ApplicationShutdown.Run`) is workflow → Output / BuildDiags / TestResults → language stack → context → terminal. |
| `A1-BR-02` | **Wired** | `BuildDiagnosticsService` subscribes to `IProjectWorkflowService.WhenChanged`, clears on `Build` `Starting` (records `_pendingBuildGeneration`), and on the build terminal `Idle` snapshot invokes `BuildDiagnosticParser.Parse` over `snapshot.OutputLines` against the target parent directory; severity maps `error → Error`, `warning → Warning`, `done → Information`, `message → Hint`; optional `CODE` and `[project.csproj]` suffix are stripped; duplicate keys are deduplicated; results are sorted by `(FilePath, Line, Column, Severity, Message)`. `ProblemsViewModel` holds `_languageProblems` and `_buildProblems` as separate lists and `RebuildProblemsList` keeps **language items before build items** on every update — `ApplyLanguageSnapshot` clears only `_languageProblems`, `ApplyBuildSnapshot` clears only `_buildProblems`, so **LSP items are never cleared by build start or finish** (per Phase 11 M3 acceptance). `ProblemItemViewModel` for build carries `Kind = ProblemKind.Build`, `Source = BuildDiagnosticSources.Build` (`"build"`), the originating `BuildGeneration`, and no `DocumentUri` / LSP offsets. `NavigateToBuildProblemAsync` re-validates `snapshot.BuildGeneration` against the live `BuildDiagnosticsService.Current`, locates the live `BuildDiagnostic` by `(FilePath, Line, Column, Severity, Code, Message)`, opens via `EditorTabViewModel.OpenFileCommand.Execute(live.FilePath)`, re-validates `ActiveTab.FilePath`, then maps `(line, column)` through `LspUtf16PositionMapper.TryGetOffset` and calls `tab.RequestNavigate(startOffset, 0)`. Stale generation, missing file, empty `FilePath`, or `null` live match no-op safely. |
| `A1-BR-03` | **Wired** | `TestResultsService` clears on `Test` `Starting` (records `_pendingTestGeneration`) and parses at test terminal `Idle`: `TestResultsParser.Parse` returns `(Cases, Summary?, ParseComplete)`; the parser handles the `Passed!` / `Failed!` summary banner regex, per-case `Passed|Failed|Skipped <Name> [duration]` lines, VSTest `Total tests:` count lines, and xUnit `[xUnit.net …] Name [FAIL]` + `(line,col): at <stack>` stack variants; `IsStructurallyComplete(summary, cases)` requires `parsedFailed >= summary.Failed` when failures were reported; `IsPartial = (LastOutcome == Cancelled) || !structurallyComplete` per U4 (F7). `TestResultsViewModel` projects `Cases` and `SummaryText` (`Passed: …  Failed: …  Skipped: …  Total: …`) and `StatusMessage` (`"All tests passed."` / `"One or more tests failed."` / `"Tests cancelled."` / `"Tests could not start."` / partial banners / `"No test results yet."` when no run yet). `TestResultsPanel` is a distinct bottom surface (`BottomPanelMode.TestResults`) — header with title + cancel button; summary + status + case list; Enter / double-click invokes `NavigateToCaseCommand`; the cancel button is shared with `OutputPanel` and both invoke `ProjectWorkflowViewModel.CancelCommand` (which in turn calls `ProjectWorkflowService.CancelAsync`). Fail-open behavior is structured: when the parser cannot produce a complete summary, `LastOutcome` and raw Output lines are still surfaced (per U4; F7). |
| `A1-BR-04` | **Wired** | `IProjectWorkflowService` is composed separately from the terminal stack. `IManagedProcessRunner` is documented and named "Starts one redirected child process at a time … **Not a PTY terminal host.**" and the production implementation starts `dotnet` via `System.Diagnostics.Process` with redirected stdout/stderr (no `ITerminalHost`, no `ITerminalService` injection on any workflow type). `IProjectOutputService` exposes only the workflow's captured `ManagedProcessOutputLine` stream and the workflow state snapshot — there is no producer path that writes to or reads from `ITerminalHost`. `OutputPanel : ReactiveUserControl<ProjectWorkflowViewModel>` binds to `vm.Lines` (the workflow VM's `ObservableCollection<OutputLineViewModel>`) which is fed only by `IProjectOutputService.WhenLineReceived`. `BottomPanelMode` is a 5-value enum (`Terminal`, `Problems`, `Output`, `TestResults`, `Debug`) and `BottomPanelHost.ApplyBottomPanelMode` sets `TerminalTabHost.IsVisible = mode == BottomPanelMode.Terminal`, `OutputPanel.IsVisible = mode == BottomPanelMode.Output`, `TestResultsPanel.IsVisible = mode == BottomPanelMode.TestResults` (and likewise for the other two) — only one of the five children is `IsVisible` at a time. The `project.build` command descriptor is registered in `ProjectWorkflowViewModel` constructor with the workflow VM's own `BuildCommand` (default gesture `Ctrl+Shift+B`); on `Build` `Starting`, `MainWindowActivationHost` switches the bottom panel to `BottomPanelMode.Output` and makes it visible, so the user sees the Output panel rather than the PTY terminal that holds any unrelated interactive shells. Likewise `project.test` and `project.run` switch the bottom panel to their dedicated surfaces (`TestResults` for Test, `Output` for Run). |

Verdict definitions: [AUDIT_PLAN.md §2](../AUDIT_PLAN.md#2-audit-phase-decomposition).

---

## 4. End-to-end production wiring trace

Legend per seam: **T** = type/method exists · **R** = registered in
production DI · **C** = called by production path · **U** = reachable
from user-visible entry point · **P** = result projected back to UI · **A3** =
clean-profile smoke evidence.

### 4.1 Build / Run / Test target selection (`A1-BR-01`)

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `IProjectContextService` authoritative source | ✓ | ✓ | ✓ | — | ✓ (`MainWindowViewModel.CurrentProjectContext`) | [ProjectContextService.cs](../../../../src/Features/ProjectSystem/Infrastructure/ProjectContextService.cs); [MainWindowViewModel.cs](../../../../src/App/Shell/MainWindowViewModel.cs) `CurrentProjectContext` / `Activate` |
| `ProjectTargetResolver.IsEligible` gate | ✓ | — | ✓ | — | — | `context.SelectedProject is not null && State is SingleProject or Selected` |
| `ProjectTargetResolver.Resolve` (Run) | ✓ | — | ✓ | — | — | Rejects `Solution` / `SolutionX`; returns `RejectedContext` otherwise |
| `ProjectExecutionProfileResolver.Resolve` | ✓ | — | ✓ | — | — | Locked argv `dotnet build <file>` / `dotnet test <file>` / `dotnet run --project <file>` |
| `IProjectOperationGate.TryAcquireWorkflowOperationAsync` | ✓ | ✓ | ✓ | — | — | Returns `WorkflowBusy` / `DebugSessionActive` |
| Workflow re-check inside `StartOperationAsync` | ✓ | — | ✓ | — | — | Defends API vs UI path divergence; returns `RejectedConcurrent` if state `Starting` / `Running` |
| Generation increment on accept | ✓ | — | ✓ | — | — | `_operationGeneration++`; late lines filtered by `line.Generation != _operationGeneration` |
| Context-change cancel | ✓ | — | ✓ | — | — | `HandleContextChangeAsync` revalidates normalized `SelectedProject.FilePath`; cancels + kills runner |
| `ManagedProcessRunner` redirected stdio | ✓ | ✓ | ✓ | — | — | `IProjectWorkflowService.WhenOutputReceived` (per-line) + `WhenChanged` (state) |
| `ProjectWorkflowStatusPolicy` mapping | ✓ | — | ✓ | — | ✓ | `OutputPanel._statusText` binds `vm.StatusMessage`; `MapCancelAutomationName` |

### 4.2 Command registration and gesture / palette reach (`A1-BR-01`, `A1-BR-04`)

| Command ID | Display name | Category | Default gesture(s) | Registrar | `ICommandRegistry` source |
|------------|--------------|----------|--------------------|-----------|----------------------------|
| `project.build` | Build | Project | `Ctrl+Shift+B` | `ProjectWorkflowViewModel` ctor | DI singleton `ICommandRegistry` → resolves `ProjectWorkflowViewModel(ICommandRegistry? commandRegistry = null)` non-null |
| `project.run` | Run | Project | `Ctrl+F5` | `ProjectWorkflowViewModel` ctor | as above |
| `project.test` | Run Tests | Project | *(unbound)* | `ProjectWorkflowViewModel` ctor | as above; reachable via Command Palette when `CanExecute` true |
| `project.cancel` | Cancel Build/Run/Test | Project | `Ctrl+F2` | `ProjectWorkflowViewModel` ctor | as above |

Note: `ICommandRegistry` is a singleton registered in
`AppCoreServiceCollectionExtensions.AddZaideAppCore`. The
`ProjectWorkflowViewModel` constructor parameter is `ICommandRegistry?
commandRegistry = null`; with `ICommandRegistry` registered, DI resolves
the parameter to the singleton instance. The published
[A2_SEARCH_AND_COMMAND_DISCOVERY.md §4.3](./A2_SEARCH_AND_COMMAND_DISCOVERY.md)
already lists these IDs as production-registered, default-bound (Build
/ Run / Cancel) or palette-only (Test).

### 4.3 `canBuild` / `canRun` / `canTest` / `canCancel` matrix

| Command | Predicate (source) | False in |
|---------|--------------------|----------|
| `project.build` | `ProjectTargetResolver.IsEligible(context) && snapshot.State is not Starting/Running && !IsWorkflowBlockedByDebug(debug) && !_operationGate.IsDebugHandoffActive` | NoProject / Unsupported / Ambiguous / Failed / Loading / Unloaded; an active Build/Run/Test; an active debug session; an active debug handoff lease |
| `project.run` | above **plus** `context.SelectedProject!.Kind == ProjectKind.CSharpProject` | as Build, plus `Solution` / `SolutionX` candidates |
| `project.test` | as Build (no `CSharpProject`-only restriction) | as Build |
| `project.cancel` | `snapshot.State is Starting or Running` | Idle |

These match the matrix in the M2 / M4 / M5 evidence files (corroboration
only — not verdict authority).

### 4.4 Cancellation and one-at-a-time policy (`A1-BR-01`)

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `ProjectWorkflowService.CancelAsync` | ✓ | ✓ | ✓ | ✓ (Command + buttons) | ✓ | Calls `_operationCts.CancelAsync()` then `_runner.KillAsync()` (process tree) |
| Cancel button — Output panel | ✓ | — | ✓ | ✓ | — | `OutputPanel._cancelButton.Click` → `ViewModel!.CancelCommand.Execute().Subscribe()` |
| Cancel button — Test Results panel | ✓ | — | ✓ | ✓ | — | `TestResultsPanel._cancelButton.Click` → `workflow.CancelCommand.Execute().Subscribe()` |
| `project.cancel` command (Ctrl+F2) | ✓ | ✓ | ✓ | ✓ | — | `ProjectWorkflowViewModel.CancelCommand` registered |
| Cancel automation name | ✓ | — | ✓ | — | ✓ | `ProjectWorkflowStatusPolicy.MapCancelAutomationName`; set on both panel cancel buttons |
| Cancellation outcome | — | — | — | — | ✓ | `ProjectWorkflowOutcomeKind.Cancelled` (never `Failed`); build parser keeps partial set with `IsPartial = true` per U3 |
| Generation guard | ✓ | — | ✓ | — | — | Old generation lines / completion paths exit early |
| `IProjectOperationGate` slot release | ✓ | ✓ | ✓ | — | — | `WorkflowOperationLease.Dispose` → `ReleaseWorkflowOperation` |
| Debug-handoff interaction | ✓ | ✓ | ✓ | — | — | `StartBuildForDebugHandoffAsync`; `IsWorkflowBlockedByDebug` predicate |

### 4.5 Structured Output projection (`A1-BR-01`, `A1-BR-04`)

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `IProjectOutputService.WhenLineReceived` | ✓ | ✓ | ✓ | — | ✓ | `ProjectOutputService` forwards `IProjectWorkflowService.WhenOutputReceived` |
| `OutputPanel` line list | ✓ | ✓ (via `MainWindow`) | ✓ | ✓ | ✓ | `_list.ItemsSource = vm!.Lines`; `OutputLineViewModel.DisplayText = "[ts] [stdout|stderr] <line>"` |
| `OutputPanel` status text | ✓ | — | ✓ | ✓ | ✓ | `_statusText.Text = vm.StatusMessage` from `ProjectWorkflowStatusPolicy.MapOutputStatusMessage` |
| Show-on-build affordance | ✓ | — | ✓ | ✓ | ✓ | `WhenShowOutputRequested` → `MainWindowActivationHost` sets `BottomPanelMode.Output` + visible |
| `BottomPanelMode.Output` | ✓ | — | ✓ | ✓ | ✓ | Enum; `BottomPanelHost.ApplyBottomPanelMode` toggles `OutputPanel.IsVisible` only |
| F11 scroll-follow (≤ 20px of `ScrollBarMaximum.Y`) | ✓ | — | ✓ | ✓ | — | `OutputPanel.WhenActivated` subscription |
| Distinction from PTY terminal | ✓ | — | ✓ | ✓ | ✓ | `IManagedProcessRunner` redirected stdio only; `ITerminalHost` is a separate `ReactiveObject` wired in `MainWindow._terminalTabHost.SetHost(ViewModel!.TerminalHost)` |
| Save-before-workflow (F9) | ✓ | — | ✓ | — | — | `ProjectWorkflowViewModel.SaveAllDirtyTabsAsync` set by `MainWindowViewModel` ctor; `EnsureDirtyTabsSavedAsync` blocks Build/Run/Test on save failure |

### 4.6 Build diagnostics → Problems merge (`A1-BR-02`)

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `IBuildDiagnosticsService` clear-on-Build-start | ✓ | ✓ | ✓ | — | — | `BuildDiagnosticsService.OnWorkflowChanged` zeroes diagnostics on `Build Starting` and bumps `_pendingBuildGeneration` |
| Parse-on-Build-terminal | ✓ | — | ✓ | — | — | Idle + non-null `LastOutcome` + matching generation → `BuildDiagnosticParser.Parse(lines, target.Parent)` |
| Severity mapping | ✓ | — | ✓ | — | ✓ | `error`→Error, `warning`→Warning, `done`→Information, `message`→Hint |
| Code-less diagnostics supported | ✓ | — | ✓ | — | ✓ | Parser sets `code = null` when group missing; U3 invariant |
| Relative path resolution | ✓ | — | ✓ | — | — | `Path.GetFullPath(workingDirectory)` + `NormalizePath` |
| Problems merge (LSP + build by source) | ✓ | ✓ | ✓ | — | ✓ | `_languageProblems` + `_buildProblems` lists; `RebuildProblemsList` keeps language before build; **no path mutates `_languageProblems` on build events** |
| `[build]` source label | ✓ | — | ✓ | — | ✓ | `BuildDiagnosticSources.Build` constant; `ProblemItemViewModel(diagnostic, generation)` for build |
| Generation-keyed re-validation on navigate | ✓ | — | ✓ | ✓ | — | `NavigateToBuildProblemAsync` checks `BuildGeneration == item.BuildGeneration` and live `(FilePath, Line, Column, Severity, Code, Message)` match |
| Navigation seam (no second host) | ✓ | — | ✓ | ✓ | ✓ | `EditorTabViewModel.OpenFileCommand.Execute(live.FilePath)` → `tab.RequestNavigate(offset, 0)` via `LspUtf16PositionMapper.TryGetOffset(content, line-1, col-1)` |
| Stale / missing file no-op | ✓ | — | ✓ | — | — | Empty `FilePath`, missing live match, mapping failure, or `ActiveTab` mismatch all return `false`; no exception |
| U3 partial on cancel | ✓ | — | ✓ | — | ✓ | `isPartial = LastOutcome == Cancelled` |

### 4.7 Test Results surface (`A1-BR-03`)

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `ITestResultsService` clear-on-Test-start | ✓ | ✓ | ✓ | — | — | `TestResultsService.OnWorkflowChanged` zeroes cases + summary on `Test Starting` |
| Console-first parse (U4) | ✓ | — | ✓ | — | ✓ | `TestResultsParser.Parse` handles banner + VSTest + xUnit; never invents passes |
| Fail-open structured fallback | ✓ | — | ✓ | — | ✓ | `IsStructurallyComplete(summary, cases)`; `IsPartial = Cancelled || !structurallyComplete`; `LastOutcome` + raw Output remain |
| `TestResultsViewModel.SummaryText` | ✓ | ✓ | ✓ | ✓ | ✓ | `Passed: ?  Failed: ?  Skipped: ?  Total: ?` |
| `TestResultsViewModel.StatusMessage` | ✓ | — | ✓ | ✓ | ✓ | `"No test results yet."` / `"Running tests…"` / outcome-specific / partial |
| Case list + outcome coloring | ✓ | — | ✓ | ✓ | ✓ | `TestResultsPanel` `FuncDataTemplate<TestCaseItemViewModel>` red for `Failed` |
| `NavigateToCaseCommand` | ✓ | — | ✓ | ✓ | — | Opens via `EditorTabViewModel.OpenFileCommand`; re-validates `ActiveTab.FilePath`; maps `line-1, 0` via `LspUtf16PositionMapper`; calls `tab.RequestNavigate` |
| Show-on-test-start | ✓ | — | ✓ | ✓ | ✓ | `WhenShowTestResultsRequested` → `MainWindowActivationHost` sets `BottomPanelMode.TestResults` + visible |
| Cancel shared with Output | ✓ | — | ✓ | ✓ | ✓ | `TestResultsPanel._cancelButton.Click` → `workflow.CancelCommand.Execute()`; `TestResultsViewModel.Workflow` property exposes the shared `ProjectWorkflowViewModel` |
| `TestResultsViewModel.NavigateToCaseCommand` `CanExecute` | ✓ | — | ✓ | — | — | `item is not null && item.CanNavigate` (non-empty `FilePath`, `Line > 0`) |

### 4.8 Output / Test Results / Terminal separation (`A1-BR-04`)

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `IManagedProcessRunner` not a PTY | ✓ | ✓ | ✓ | — | — | Contract comment: "Not a PTY terminal host." |
| `IProjectOutputService` only emits captured lines | ✓ | ✓ | ✓ | — | ✓ | `WhenLineReceived = _workflow.WhenOutputReceived`; no terminal producer |
| `BottomPanelMode` enum (5 values) | ✓ | — | ✓ | ✓ | ✓ | `Terminal | Problems | Output | TestResults | Debug` |
| `BottomPanelHost.ApplyBottomPanelMode` exclusivity | ✓ | — | ✓ | ✓ | — | Each child host's `IsVisible` is set to `mode == <own enum value>` — only one of five visible |
| Mode-strip buttons | ✓ | — | ✓ | ✓ | — | `BottomPanelHost.CreateModeButton`; five buttons; click → `ShellPanelNavigation.SwitchTo*BottomCommand` |
| `project.build` → `BottomPanelMode.Output` | ✓ | — | ✓ | ✓ | ✓ | `MainWindowActivationHost` subscribes to `WhenShowOutputRequested` |
| `project.test` → `BottomPanelMode.TestResults` | ✓ | — | ✓ | ✓ | ✓ | `MainWindowActivationHost` subscribes to `WhenShowTestResultsRequested` |
| F10 dispose order | ✓ | — | ✓ | — | — | `ApplicationShutdown.Run`: workflow → Output / BuildDiags / TestResults → language → context → terminal |

### 4.9 Application lifecycle / persistence

The Phase 11 surface is **non-persistent** in the strict sense: the
`IProjectWorkflowService` does not expose a save/restore contract, and
no production code reads or writes workflow snapshots, output lines,
build diagnostics, or test results to a store. This matches the
documented Phase 11 scope: workflow is an in-process orchestration
seam, not a durable user surface. F10's `ApplicationShutdown.Run`
teardown order is the only persistence-adjacent concern and is wired
as documented. A2 does not verdict the non-persistence — that is the
expected state for this journey.

---

## 5. Source-proven wiring vs runtime / A3-unproven

This section separates facts that A2 can prove from `src/` alone
(source-proven) from facts that require a running process and a real
`dotnet` invocation (runtime / A3-unproven). The verdict table above
intentionally uses the source-proven facts only.

### 5.1 Source-proven wiring (verdict authority)

- All `project.*` command descriptors are registered, with the
  documented IDs, display names, categories, and default gestures.
- All four `can*` predicates and the `canCancel` predicate match the
  documented matrices; the `IProjectOperationGate` is the single source
  of truth for the one-at-a-time policy.
- Target resolution is from `IProjectContextService.Current` only;
  `ProjectTargetResolver` rejects `Solution` / `SolutionX` for Run per
  U1a and any ineligible context per contract 1.
- Locked default argv is `dotnet build <file>`, `dotnet run --project
  <file>`, `dotnet test <file>` from `ProjectExecutionProfileResolver`.
- The Output panel (`BottomPanelMode.Output`) and Test Results panel
  (`BottomPanelMode.TestResults`) are separate bottom-panel surfaces
  that are not PTY terminals; the production `IManagedProcessRunner`
  uses redirected stdio.
- `IBuildDiagnosticsService` clears on `Build` `Starting` and parses at
  the build terminal `Idle` snapshot; LSP diagnostics are never cleared
  by build events; build items are tagged `Source = "build"` and keyed
  by `BuildGeneration`; navigation re-validates live generation + file
  path.
- `ITestResultsService` clears on `Test` `Starting` and parses at the
  test terminal `Idle` snapshot; parser handles the documented banner
  / VSTest / xUnit forms; never invents passes; `IsStructurallyComplete`
  enforces `parsedFailed >= summary.Failed`; `IsPartial` for cancel or
  incomplete parse.
- Cancel is wired: `ProjectWorkflowService.CancelAsync` cancels the
  operation `CancellationTokenSource` and calls `_runner.KillAsync()`
  (process tree). Cancel buttons exist on both Output and Test Results
  panels; the `project.cancel` command is palette-reachable.
- F9 save-before-workflow: `ProjectWorkflowViewModel.SaveAllDirtyTabsAsync`
  blocks Build / Run / Test when any dirty tab fails to save.
- F10 dispose order is wired in `ApplicationShutdown.Run`; F11
  scroll-follow is wired in `OutputPanel`; F3 cancel discoverability
  is wired via the panel buttons plus the Ctrl+F2 gesture.

### 5.2 Runtime / A3-unproven behavior (not in this A2 verdict)

- Whether a real `dotnet build` against a real fixture tree completes
  in the expected time and emits the exact `path(line,col): error|warning|done|message [CODE:] message` lines the parser consumes.
  A2 does not claim pass/fail on the Linux smoke gate recorded in
  [Phase 11 M2](../../../phases/v2/phase-11/M2_MANUAL_EVIDENCE.md).
- Whether the F11 scroll-follow heuristic (≤ 20px from
  `ScrollBarMaximum.Y`) reads as "follow at bottom" in a real user
  scroll gesture on a real display.
- Whether the save-before-workflow F9 implementation correctly reports
  `LastSaveError` to the status bar when an untitled (no-file-path)
  dirty tab blocks the workflow (per Phase 11 F9 wording). The source
  shows `EditorViewModel.SaveCommand` returns `false` for empty
  `FilePath`; the actual status-bar subscription is outside this slice.
- Whether the `OutputPanel` show-on-start affordance visibly raises
  the bottom panel from a hidden state (versus already-visible state)
  on a real Avalonia render.
- Whether `ProblemsViewModel.NavigateToBuildProblemAsync` re-validates
  live generation / live file / live offsets under real
  `Save → Edit → Rebuild` cycles. The unit test
  `ProblemsBuildProjectionTests` corroborates the contract; A3
  disposable-profile smoke is the runtime gate.
- Whether the `DOTNET_CLI_UI_LANGUAGE=en` invariant is actually
  enforced on child processes. The Phase 11 plan calls this out as
  "not yet enforced in runner" — A2 confirms the parser is
  English-only and the runner has no documented `DOTNET_CLI_UI_LANGUAGE`
  setting.
- Whether `TestResultsViewModel.FormatStatus` is reachable for
  `ProjectWorkflowOutcomeKind.StartupFailed` on a real test start
  failure (e.g. `dotnet` missing on `PATH`). Source confirms the
  branch exists; A3 will exercise it.

### 5.3 Out of A2 scope for this slice (cross-referenced, not verdicted)

- The `workspace.openFolder` / `editor.find` / `file.save` command
  reachability is owned by
  [A2_SEARCH_AND_COMMAND_DISCOVERY.md](./A2_SEARCH_AND_COMMAND_DISCOVERY.md)
  and
  [A2_WORKSPACE_AND_PROJECT_OPENING.md](./A2_WORKSPACE_AND_PROJECT_OPENING.md).
  This slice does not re-verdict those rows.
- The Problems navigation seam shares `LspUtf16PositionMapper` with
  [A2_FILE_NAVIGATION_AND_EDITING.md](./A2_FILE_NAVIGATION_AND_EDITING.md)
  (`A1-FN-08`). The same `LspUtf16PositionMapper.TryGetOffset` /
  `EditorTabViewModel.OpenFileCommand` calls are present here, so the
  `A1-FN-08` caveats (open tracked documents only; eligible project
  context + external `csharp-ls` binary required for live LSP
  re-validation) carry forward to build navigation as well. A2 does
  not re-verdict `A1-FN-08`; the wording on this slice says "uses the
  same seam" only.
- The shared `IProjectContextService` consumption is described in
  [A2_WORKSPACE_AND_PROJECT_OPENING.md](./A2_WORKSPACE_AND_PROJECT_OPENING.md)
  (`A1-WO-02`); the `A1-WO-02` "ambiguous multi-project picker is
  absent" gap means that no `ProjectContext` state beyond
  `SingleProject` / `Selected` can be reached through any user entry
  point, and the workflow service's `IsEligible` gate therefore
  returns `false` for all other states. The Phase 11 M1 contract table
  already records this; A2 confirms the wiring is consistent.
- The agent-context composer reads `IProjectWorkflowService.Current`
  + `IBuildDiagnosticsService` + `ITestResultsService` for trace
  payload assembly. That seam is
  [A2_TRACE_MEMORY_USAGE_TERMINATION.md](./A2_TRACE_MEMORY_USAGE_TERMINATION.md)
  territory; A2 does not verdict it here.

---

## 6. Next slice declaration

The next recommended A2 slice is **`A2_DEBUGGING_AND_OUTPUT`**,
**explicitly not begun** (no evidence file; no verdict assigned;
no production work or A3 execution). Scope per
[AUDIT_PLAN.md §4 journey 6](../AUDIT_PLAN.md#4-inventory-scope--user-journeys):
DAP client and debug-adapter lifecycle, launch configuration, breakpoints,
step controls, call stack / variables / debug console, adapter failure
recovery; goal rows `A1-DB-01`. A2 for the `A1-DB-*` family remains
deferred until `A2_BUILD_RUN_AND_TEST` is re-audited and accepted.

A3 (clean-profile smoke), A4 (gap report and V4 proceed decision),
stabilization, and V4 / successor-roadmap planning are **not begun**.
No production code, tests, or prior evidence were modified by this
slice. No commit or push was performed; the new evidence file is the
single untracked change under
`docs/audits/v1-v3-product-reality/evidence/`.

---

*Author: A2 wiring audit (read-only), 2026-07-31. Slice stops here for
re-audit. Authoritative next action: user-driven re-audit of
`A2_BUILD_RUN_AND_TEST` per
[AUDIT_PLAN.md §3 safety rules](../AUDIT_PLAN.md#3-safety-and-isolation-rules-mandatory-for-a0a4)
and the published A1 acceptance.*
