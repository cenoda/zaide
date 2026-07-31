# A2 Wiring Audit — `A2_DEBUGGING_AND_OUTPUT`

**Audit name:** `v1-v3-product-reality`
**Slice:** `A2_DEBUGGING_AND_OUTPUT` (thirteenth A2 slice; prior:
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
`A2_BUILD_RUN_AND_TEST`)
**Evidence date:** 2026-07-31
**Baseline:** branch `master`, HEAD
`21a784ab4e2b1838db35c3189f8e7e7eba915834` (matches `origin/master`)
**Method:** read-only source inspection (`rg`, file reads). No app launch,
build, test execution, production-code edits, commits, or pushes.

---

## 1. Audit identity, baseline, and safety boundary

| Check | Result |
|-------|--------|
| Branch | `master` |
| `git rev-parse HEAD` | `21a784ab4e2b1838db35c3189f8e7e7eba915834` |
| `git rev-parse origin/master` | `21a784ab4e2b1838db35c3189f8e7e7eba915834` |
| Working tree at audit start | Clean (`git status --short` empty) |
| Twelve published A2 evidence files | Present (Agent Send, Multi-Agent Routing, Trace/Memory/Usage/Termination, Restart/Recovery/Context, Tools/Permissions, Agent Creation/Backend Onboarding, Townhall/Conversations, First Launch/Settings, Workspace/Project Opening, File Navigation/Editing, Search/Command Discovery, Build/Run/Test) |
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
| NetCoreDbg adapter process executed | No |
| Commit / push | No |

**Safety boundary:** this slice is A2 wiring inspection only. Production
source is verdict authority. Phase 12 milestone evidence and unit/VM tests
are corroboration only. Live Avalonia rendering, real NetCoreDbg DAP
traffic, and disposable-profile debug smoke are not claimed from source
alone. **No real user profile, settings, secrets, or opened workspace path
was accessed.**

**Verdict row (this slice only):** `A1-DB-01`. No new verdicts for AS,
MR, TC, TP, AC, TH, FL, WO, FN, SC, BR, TR, GT, or DB rows. Shared seam
overlap with `A2_BUILD_RUN_AND_TEST` (`IProjectOperationGate`,
`IProjectContextService` consumption, `IProjectWorkflowService.StartBuildForDebugHandoffAsync`)
is called out under the row but is not re-verdicted in this slice.

**Scoped disposition row (this slice only):** `A1-XX-04` (DAP environment
validation constraint). Recorded in §5 as a scoped disposition; **not** a
user-goal verdict, **not** a `Wired` / `Wired-with-gap` / `Missing` /
`Ambiguous` classification.

---

## 2. Sources inspected

### 2.1 Documentation

- [docs-rules.md](../../../../docs-rules.md)
- [AUDIT_PLAN.md](../AUDIT_PLAN.md) (§4 journey 6 Debugging and output; §5
  schema; §6 quality gates; §17.8 A2 progress)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md) (§6 Debugging row `A1-DB-01`; §15
  `A1-XX-04`; §17.8 progress table)
- [A1_ACCEPTANCE.md](../A1_ACCEPTANCE.md)
- V2 roadmap: [V2.md §"Phase 12 — C# Debugging"](../../../roadmap/V2.md#phase-12--c-debugging--complete-m0m7-2026-07-14)
- Phase 12 plan and milestone proofs:
  [IMPLEMENTATION_PLAN.md](../../../phases/v2/phase-12/IMPLEMENTATION_PLAN.md),
  [M0_DAP_ADAPTER_TRANSPORT_PROOF.md](../../../phases/v2/phase-12/M0_DAP_ADAPTER_TRANSPORT_PROOF.md),
  [M1_DAP_SESSION_LIFECYCLE_PROOF.md](../../../phases/v2/phase-12/M1_DAP_SESSION_LIFECYCLE_PROOF.md),
  [M3a_DEBUG_LAUNCH_HANDOFF_PROOF.md](../../../phases/v2/phase-12/M3a_DEBUG_LAUNCH_HANDOFF_PROOF.md),
  [M3b_EDITOR_BREAKPOINT_PROOF.md](../../../phases/v2/phase-12/M3b_EDITOR_BREAKPOINT_PROOF.md),
  [M4_EXECUTION_CONTROLS_DEBUG_CONSOLE_PROOF.md](../../../phases/v2/phase-12/M4_EXECUTION_CONTROLS_DEBUG_CONSOLE_PROOF.md),
  [M5_STACK_VARIABLES_CURRENT_LOCATION_PROOF.md](../../../phases/v2/phase-12/M5_STACK_VARIABLES_CURRENT_LOCATION_PROOF.md),
  [M6_DAP_RECOVERY_PROOF.md](../../../phases/v2/phase-12/M6_DAP_RECOVERY_PROOF.md),
  [M7_MANUAL_EVIDENCE.md](../../../phases/v2/phase-12/M7_MANUAL_EVIDENCE.md),
  [TOFIX.md](../../../phases/v2/phase-12/TOFIX.md)
- V2 Phase 11: [V2.md §"Phase 11 — Project Workflow"](../../../roadmap/V2.md#phase-11--project-workflow--complete-m0m6-2026-07-14)
  (Build/Run/Test Output surface distinction; shared gate)
- Published A2 evidence with shared seam overlap:
  [A2_BUILD_RUN_AND_TEST.md](./A2_BUILD_RUN_AND_TEST.md)
  (`IProjectOperationGate`, `IProjectContextService`, workflow-owned build
  handoff, `BottomPanelMode` exclusivity); [A2_FILE_NAVIGATION_AND_EDITING.md](./A2_FILE_NAVIGATION_AND_EDITING.md)
  (`LspUtf16PositionMapper` reused for current-execution-location mapping);
  [A2_SEARCH_AND_COMMAND_DISCOVERY.md](./A2_SEARCH_AND_COMMAND_DISCOVERY.md)
  (`ICommandRegistry` registration of debug commands with default gestures
  and conflict-free F5/F9/F10/F11/Shift+F5/Shift+F11).

### 2.2 Production source (minimum required + supporting)

**DAP core (M1)**

- [DebugSessionService.cs](../../../../src/Features/Debugging/Application/DebugSessionService.cs)
  (state, generation, snapshot publication, lifecycle, timeouts,
  stderr/diagnostic output, ReplaceBreakpointsBySourceAsync, DAP request
  gating)
- [DebugSessionSnapshot.cs](../../../../src/Features/Debugging/Application/DebugSessionSnapshot.cs),
  [DebugSessionState.cs](../../../../src/Features/Debugging/Application/DebugSessionState.cs),
  [DebugSessionOutcomeKind.cs](../../../../src/Features/Debugging/Application/DebugSessionOutcomeKind.cs),
  [DebugSessionFailure.cs](../../../../src/Features/Debugging/Application/DebugSessionFailure.cs),
  [DebugSessionOperationResult.cs](../../../../src/Features/Debugging/Application/DebugSessionOperationResult.cs),
  [DebugSessionTimeouts.cs](../../../../src/Features/Debugging/Application/DebugSessionTimeouts.cs),
  [DebugSessionTimeoutPolicy.cs](../../../../src/Features/Debugging/Application/DebugSessionTimeoutPolicy.cs)
- [IDebugSessionService.cs](../../../../src/Features/Debugging/Contracts/IDebugSessionService.cs)
- [DebugAdapterLocator.cs](../../../../src/Features/Debugging/Infrastructure/Dap/DebugAdapterLocator.cs),
  [IDebugAdapterLocator.cs](../../../../src/Features/Debugging/Infrastructure/Dap/IDebugAdapterLocator.cs)
- [DebugAdapterSessionFactory.cs](../../../../src/Features/Debugging/Infrastructure/Dap/DebugAdapterSessionFactory.cs),
  [IDebugAdapterSessionFactory.cs](../../../../src/Features/Debugging/Infrastructure/Dap/IDebugAdapterSessionFactory.cs),
  [DebugAdapterStartOptions.cs](../../../../src/Features/Debugging/Infrastructure/Dap/DebugAdapterStartOptions.cs),
  [IDebugAdapterSession.cs](../../../../src/Features/Debugging/Infrastructure/Dap/IDebugAdapterSession.cs)
- [NetCoreDbgAdapterSession.cs](../../../../src/Features/Debugging/Infrastructure/Dap/NetCoreDbgAdapterSession.cs)
  (real NetCoreDbg 3.2.0-1092 wire transport)
- [DapContentLengthTransport.cs](../../../../src/Features/Debugging/Infrastructure/Dap/DapContentLengthTransport.cs)
  (`Content-Length` framed DAP envelopes, distinct from `StreamJsonRpc` LSP
  transport)
- [DapStoppedEvent.cs](../../../../src/Features/Debugging/Infrastructure/Dap/DapStoppedEvent.cs),
  [DapContinuedEvent.cs](../../../../src/Features/Debugging/Infrastructure/Dap/DapContinuedEvent.cs),
  [DapExitedEvent.cs](../../../../src/Features/Debugging/Infrastructure/Dap/DapExitedEvent.cs),
  [DapOutputEvent.cs](../../../../src/Features/Debugging/Infrastructure/Dap/DapOutputEvent.cs),
  [DapInspectionParser.cs](../../../../src/Features/Debugging/Infrastructure/Dap/DapInspectionParser.cs),
  [DapScopeInfo.cs](../../../../src/Features/Debugging/Infrastructure/Dap/DapScopeInfo.cs),
  [DapStackFrameInfo.cs](../../../../src/Features/Debugging/Infrastructure/Dap/DapStackFrameInfo.cs),
  [DapStoppedInfo.cs](../../../../src/Features/Debugging/Infrastructure/Dap/DapStoppedInfo.cs),
  [DapThreadInfo.cs](../../../../src/Features/Debugging/Infrastructure/Dap/DapThreadInfo.cs),
  [DapVariableInfo.cs](../../../../src/Features/Debugging/Infrastructure/Dap/DapVariableInfo.cs),
  [DapBreakpointVerificationParser.cs](../../../../src/Features/Debugging/Infrastructure/Dap/DapBreakpointVerificationParser.cs)

**Breakpoint persistence (M2)**

- [BreakpointService.cs](../../../../src/Features/Debugging/Application/BreakpointService.cs),
  [IBreakpointService.cs](../../../../src/Features/Debugging/Contracts/IBreakpointService.cs),
  [DebugBreakpointRequest.cs](../../../../src/Features/Debugging/Application/DebugBreakpointRequest.cs),
  [BreakpointOperationResult.cs](../../../../src/Features/Debugging/Application/BreakpointOperationResult.cs),
  [BreakpointOutcomeKind.cs](../../../../src/Features/Debugging/Application/BreakpointOutcomeKind.cs),
  [DebugBreakpointVerificationState.cs](../../../../src/Features/Debugging/Application/DebugBreakpointVerificationState.cs)
- Settings: [SettingsModel.cs](../../../../src/Features/Settings/Domain/SettingsModel.cs)
  (`DebugSettings`, `BreakpointsByWorkspaceRoot`, schema v3),
  [SettingsService.cs](../../../../src/Features/Settings/Infrastructure/SettingsService.cs)
  (v2→v3 migration)

**Launch handoff (M3a)**

- [ProjectOperationGate.cs](../../../../src/Features/ProjectSystem/Infrastructure/ProjectOperationGate.cs),
  [IProjectOperationGate.cs](../../../../src/Features/ProjectSystem/Contracts/IProjectOperationGate.cs)
  (shared Build/Run/Test/Debug admission; split admission/critical-section
  mutexes; `IsDebugHandoffActive` predicate)
- [ProjectDebugLaunchService.cs](../../../../src/Features/ProjectSystem/Infrastructure/ProjectDebugLaunchService.cs),
  [IProjectDebugLaunchService.cs](../../../../src/Features/ProjectSystem/Contracts/IProjectDebugLaunchService.cs)
  (build → `TargetPath` resolve → `StartLaunchAsync`; reports pre-launch
  failures to `IDebugSessionService.ReportPreLaunchFailureAsync`; handoff
  lease always disposed in `finally`)
- [ProjectDebugTargetResolver.cs](../../../../src/Features/ProjectSystem/Infrastructure/ProjectDebugTargetResolver.cs),
  [ProjectDebugTargetResolution.cs](../../../../src/Features/ProjectSystem/Domain/ProjectDebugTargetResolution.cs),
  [ProjectDebugTargetResolutionKind.cs](../../../../src/Features/ProjectSystem/Domain/ProjectDebugTargetResolutionKind.cs)
  (MSBuild `-getProperty:TargetPath` only; no `bin/` scanning)
- [ProjectOperationGateMessages.cs](../../../../src/Features/ProjectSystem/Domain/ProjectOperationGateMessages.cs)
  (`WorkflowBusy` / `DebugSessionActive` text)

**Breakpoint editor (M3b)**

- [EditorBreakpointViewModel.cs](../../../../src/Features/Debugging/Presentation/EditorBreakpointViewModel.cs)
  (F9 + margin click share toggle; active-session DAP replacement)
- [EditorBreakpointMarker.cs](../../../../src/Features/Debugging/Presentation/EditorBreakpointMarker.cs),
  [EditorBreakpointProjection.cs](../../../../src/Features/Debugging/Presentation/EditorBreakpointProjection.cs)
- [InstructionPointerMargin.cs](../../../../src/Features/Debugging/Presentation/InstructionPointerMargin.cs),
  [InstructionPointerOperations.cs](../../../../src/Features/Debugging/Presentation/InstructionPointerOperations.cs)
  (leftmost margin, yellow `#FCBB47` arrow)
- [BreakpointMargin.cs](../../../../src/Features/Debugging/Presentation/BreakpointMargin.cs),
  [BreakpointOperations.cs](../../../../src/Features/Debugging/Presentation/BreakpointOperations.cs)

**Execution controls (M4)**

- [DebugSessionViewModel.cs](../../../../src/Features/Debugging/Presentation/DebugSessionViewModel.cs)
  (six `ReactiveCommand`s with state-gated `canExecute`; registers
  `debug.startOrContinue` / `debug.pause` / `debug.stop` / `debug.stepOver`
  / `debug.stepInto` / `debug.stepOut` via `ICommandRegistry`)
- [DebugPanelViewModel.cs](../../../../src/Features/Debugging/Presentation/DebugPanelViewModel.cs)
  (console history, `[error]` annotation, auto-show on `Starting`; raises
  `WhenShowDebugRequested`)
- [DebugConsoleLineViewModel.cs](../../../../src/Features/Debugging/Presentation/DebugConsoleLineViewModel.cs)

**Stack / variables / current location (M5)**

- [DebugStackProjectionViewModel.cs](../../../../src/Features/Debugging/Presentation/DebugStackProjectionViewModel.cs)
  (thread → frame → scope → variable pipeline; selection tokens for
  stale-response rejection; clears on non-`Stopped`)
- [DebugThreadViewModel.cs](../../../../src/Features/Debugging/Presentation/DebugThreadViewModel.cs),
  [DebugStackFrameViewModel.cs](../../../../src/Features/Debugging/Presentation/DebugStackFrameViewModel.cs),
  [DebugScopeViewModel.cs](../../../../src/Features/Debugging/Presentation/DebugScopeViewModel.cs),
  [DebugVariableViewModel.cs](../../../../src/Features/Debugging/Presentation/DebugVariableViewModel.cs)
- [DebugCurrentLocationViewModel.cs](../../../../src/Features/Debugging/Presentation/DebugCurrentLocationViewModel.cs)
  (frame source path → editor open → `LspUtf16PositionMapper` offset →
  `EditorInstructionPointerMarker`)
- [EditorInstructionPointerMarker.cs](../../../../src/Features/Debugging/Presentation/EditorInstructionPointerMarker.cs)
- [DebugProjectionState.cs](../../../../src/Features/Debugging/Application/DebugProjectionState.cs)

**Bottom-panel host and shell integration**

- [DebugPanel.cs](../../../../src/Features/Debugging/Presentation/DebugPanel.cs)
  (three-column Grid: Console 2* | Call Stack 1* | Variables 1*; automation
  names; auto-scroll within 20px of bottom)
- [BottomPanelHost.cs](../../../../src/App/Shell/BottomPanelHost.cs)
  (`BottomPanelMode` 5-value enum: `Terminal` / `Problems` / `Output` /
  `TestResults` / `Debug`; exclusive `IsVisible` per mode; mode-strip
  buttons)
- [MainWindowActivationHost.cs](../../../../src/App/Shell/MainWindowActivationHost.cs)
  (subscribes to `WhenShowDebugRequested` → `BottomPanelMode.Debug` +
  `IsBottomPanelVisible = true`; activates debug session/panel/location
  ViewModels; keeps `SaveAllDirtyTabsAsync` handoff intact)
- [EditorView.cs](../../../../src/Features/Editor/Presentation/EditorView.cs) (M7 closeout cites
  `SyncInstructionPointerMargin` at L452–L468 reactively syncs on
  `ProjectionRevision` or file-path change)
- [MainWindow.axaml.cs](../../../../src/App/Shell/MainWindow.axaml.cs)
  (M7 closeout cites `MaterializeRegistryBindings` at L398–L435 converts
  resolved bindings to Avalonia `KeyBinding`s)

**Composition / DI / startup / shutdown**

- [DebuggingServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/DebuggingServiceCollectionExtensions.cs)
  (singletons: `IDebugAdapterLocator`, `IDebugAdapterSessionFactory`,
  `DebugSessionTimeoutPolicy`, `IDebugSessionService`, `IBreakpointService`,
  `DebugSessionViewModel`, `DebugStackProjectionViewModel`,
  `DebugCurrentLocationViewModel`, `DebugPanelViewModel`,
  `EditorBreakpointViewModel`)
- [ProjectSystemServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/ProjectSystemServiceCollectionExtensions.cs)
  (`IProjectDebugTargetResolver`, `IProjectDebugLaunchService`,
  `IProjectOperationGate`)
- [App.axaml.cs](../../../../src/App/Composition/App.axaml.cs)
  (eager `GetRequiredService<DebugSessionViewModel>` + `EditorBreakpointViewModel` +
  `DebugCurrentLocationViewModel`; constructor `MainWindowViewModel`
  parameter list)
- [ApplicationShutdown.cs](../../../../src/App/Composition/ApplicationShutdown.cs)
  (Phase 12 disposal order: `IDebugSessionService.Dispose` first, then
  resolved `DebugPanelViewModel` / `DebugCurrentLocationViewModel` /
  `EditorBreakpointViewModel` / `DebugSessionViewModel` dispose, then
  workflow, language, project context, terminal, agent session, durable
  store)

### 2.3 Tests (corroboration only — not proof of user wiring)

- [DebugSessionServiceTests.cs](../../../../tests/Zaide.Tests/Features/Debugging/Application/DebugSessionServiceTests.cs)
- [DebugStartOrContinueCommandTests.cs](../../../../tests/Zaide.Tests/Features/Debugging/Application/DebugStartOrContinueCommandTests.cs)
- [DebugToggleBreakpointCommandTests.cs](../../../../tests/Zaide.Tests/Features/Debugging/Application/DebugToggleBreakpointCommandTests.cs)
- [DebugExecutionControlsCommandTests.cs](../../../../tests/Zaide.Tests/Features/Debugging/Application/DebugExecutionControlsCommandTests.cs)
- [DebugPanelViewModelTests.cs](../../../../tests/Zaide.Tests/Features/Debugging/Presentation/DebugPanelViewModelTests.cs)
- [DebugStackProjectionTests.cs](../../../../tests/Zaide.Tests/Features/Debugging/Presentation/DebugStackProjectionTests.cs)
- [DebugCurrentLocationViewModelTests.cs](../../../../tests/Zaide.Tests/Features/Debugging/Presentation/DebugCurrentLocationViewModelTests.cs)
- [EditorBreakpointViewModelTests.cs](../../../../tests/Zaide.Tests/Features/Debugging/Presentation/EditorBreakpointViewModelTests.cs)
- [EditorBreakpointProjectionTests.cs](../../../../tests/Zaide.Tests/Features/Debugging/Presentation/EditorBreakpointProjectionTests.cs)
- [EditorBreakpointRegressionTests.cs](../../../../tests/Zaide.Tests/Features/Debugging/Presentation/EditorBreakpointRegressionTests.cs)
- [ProjectOperationGateTests.cs](../../../../tests/Zaide.Tests/Features/ProjectSystem/Infrastructure/ProjectOperationGateTests.cs)
- [ProjectDebugTargetResolverTests.cs](../../../../tests/Zaide.Tests/Features/ProjectSystem/Infrastructure/ProjectDebugTargetResolverTests.cs)
- [ProjectDebugLaunchServiceTests.cs](../../../../tests/Zaide.Tests/Features/ProjectSystem/Infrastructure/ProjectDebugLaunchServiceTests.cs)
- [NetCoreDbgLifecycleProofTests.cs](../../../../tests/Zaide.Tests/Features/Debugging/Infrastructure/Dap/NetCoreDbgLifecycleProofTests.cs)
- [NetCoreDbgAdapterSessionDirectTests.cs](../../../../tests/Zaide.Tests/Features/Debugging/Infrastructure/Dap/NetCoreDbgAdapterSessionDirectTests.cs)
- [M3aDebugLaunchProofTests.cs](../../../../tests/Zaide.Tests/Features/Debugging/Application/DebugLaunchProofTests.cs)
- [M3bDebugBreakpointProofTests.cs](../../../../tests/Zaide.Tests/Features/Debugging/Application/DebugBreakpointProofTests.cs)
- [M4DebugExecutionProofTests.cs](../../../../tests/Zaide.Tests/Features/Debugging/Application/DebugExecutionProofTests.cs)
- [M5DebugStackProofTests.cs](../../../../tests/Zaide.Tests/Features/Debugging/Application/DebugStackProofTests.cs)
- [M6DebugRecoveryProofTests.cs](../../../../tests/Zaide.Tests/Features/Debugging/Application/DebugRecoveryProofTests.cs)
- [CanonicalCommandRegistrationTests.cs](../../../../tests/Zaide.Tests/App/Composition/CanonicalCommandRegistrationTests.cs)
- [DapBreakpointVerificationParserTests.cs](../../../../tests/Zaide.Tests/Features/Debugging/Infrastructure/Dap/DapBreakpointVerificationParserTests.cs)

The Phase 12 M7 closeout recorded **2053 tests pass** with **8 production
adapter proof tests** against NetCoreDbg 3.2.0-1092 on Linux x64
([M7_MANUAL_EVIDENCE.md §1, §2.1–§2.9](../../../phases/v2/phase-12/M7_MANUAL_EVIDENCE.md)).
These are not executed by this A2 slice; they are inspected as
corroboration of the same source seams cited above.

---

## 3. Verdict table for `A1-DB-01`

| id | a2_wiring_verdict | Summary |
|----|-------------------|---------|
| `A1-DB-01` | **Wired-with-gap** | DAP client (`DebugSessionService` singleton) and `DebugAdapterLocator` + `DebugAdapterSessionFactory` are production-composed; the supported C# launch handoff (`ProjectDebugLaunchService`) acquires the shared `IProjectOperationGate`, calls `IProjectWorkflowService.StartBuildForDebugHandoffAsync`, resolves the `TargetPath` via `IProjectDebugTargetResolver` (`dotnet msbuild -getProperty:TargetPath`), and hands off to `IDebugSessionService.StartLaunchAsync`. Persistent source breakpoints (workspace-root-keyed, schema-v3 `DebugSettings`) flow through `BreakpointService` → `EditorBreakpointViewModel` margin → active-session DAP `setBreakpoints` replacement via `IDebugSessionService.ReplaceBreakpointsBySourceAsync`. Six execution commands (`debug.startOrContinue` / `pause` / `stop` / `stepOver` / `stepInto` / `stepOut`) plus `debug.toggleBreakpoint` register through `ICommandRegistry` with the locked default gestures (`F5` / `Shift+F5` / `F10` / `F11` / `Shift+F11` / `F9`); each `canExecute` is state-gated on the live `DebugSessionSnapshot`. `DebugStackProjectionViewModel` runs the `threads → stackTrace(threadId) → scopes(frameId) → variables(variablesReference)` pipeline with selection-token stale-response rejection; `DebugCurrentLocationViewModel` projects the selected frame to an editor open + `EditorInstructionPointerMarker`. `DebugPanelViewModel` keeps an `ObservableCollection<DebugConsoleLineViewModel>` history (`Info` / `Output` / `Error` kinds; `[error]` annotation), and `DebugPanel` is the fixed three-column `Console 2* | Call Stack 1* | Variables 1*` surface. Failure paths (`AdapterUnavailable` / `BuildFailed` / `UnsupportedLaunchTarget` / `StartupFailed` / `ProtocolFailed` / `AdapterExited` / `Cancelled` / `RejectedContext` / `RejectedConcurrent`) publish terminal snapshots, retain diagnostics, clear live data, release the gate, and remain F5-usable — the M6 recovery contract. **Gaps versus documented goal:** DAP availability is constrained to a host that has NetCoreDbg accessible via `ZAIDE_NETCOREDBG_PATH` or `netcoredbg` on `PATH`; production does **not** bundle, install, auto-download, scan well-known directories, or claim Windows/macOS parity; `BottomPanelMode` Debug hosts Console + Call Stack + Variables only and is not a separate "Debug Console" terminal — debug `output` events are projected into the same `Lines` collection, not a separate scratch buffer; F5 is the only `debug.startOrContinue` command — there is no separate `debug.start` / `debug.continue`; the launch configuration is locked `{ program, cwd, stopAtEntry: true, console: "internalConsole" }` with no `launch.json`, no user-configurable fields, no watch/evaluate, no attach, no remote, no exception configuration; breakpoint payload is `{ line }` only (no condition / hit count / log message / data breakpoint); debug eligibility is `CSharpProject` only — `Solution` / `SolutionX` produce `RejectedContext`; the persisted `DebugSettings` schema requires an open workspace-root, and breakpoints are silently empty when none is open; M2 settings `DebugSettings` schema v3 is the only breakpoint storage; `IProjectContextService.Current` is the sole debug-target owner — `Workspace.WorkspacePath` is not a debug-target fallback. The M7 closeout defers visual gutter paint, three-column panel proportions, console color differentiation, and live Avalonia keyboard delivery to Phase 13 release-hardening manual smoke; A2 confirms functional wiring and the milestone proofs. |

Verdict definitions: [AUDIT_PLAN.md §2](../AUDIT_PLAN.md#2-audit-phase-decomposition).

---

## 4. End-to-end production wiring trace

Legend per seam: **T** = type/contract · **R** = registered in production
DI · **C** = called by a production non-test path · **U** = reachable from
user-visible entry point · **P** = result projected to visible UI.

### 4.1 DAP client and debug-adapter lifecycle (`A1-DB-01` lifecycle)

```text
DebugSessionService (singleton, generation-safe)
  ↳ IDebugAdapterLocator.Resolve()
       ZAIDE_NETCOREDBG_PATH → netcoredbg on PATH
  ↳ IDebugAdapterSessionFactory.StartAsync
       → NetCoreDbgAdapterSession (one per session)
            → DapContentLengthTransport (one per session, framed JSON)
  ↳ events: Stopped, Continued, Output, Terminated, Exited, ProcessExited
  ↳ request ordering: Initialize → Launch → setBreakpoints (per source)
                     → configurationDone → initial stopped (with timeout)
                     → threads / stackTrace / scopes / variables while stopped
                     → continue / next / stepIn / stepOut / pause
                     → disconnect(terminateDebuggee: true)
  ↳ timeouts: Initialize 15s, LaunchConfiguration 15s, OrdinaryRequest 10s,
              Disconnect 5s
  ↳ generation: bumped on every Stop / context change / pre-launch
                failure / adapter-exit recovery
  ↳ diagnostics: _diagnosticOutput (DAP output events + stderr lines +
                              [error] annotations)
  ↳ failure projection: DebugSessionOutcomeKind → DebugSessionFailure
                         → state Failed
  ↳ state: Idle | Starting | Running | Stopped | Stopping | Failed |
            Unavailable
  ↳ dispose: IDebugSessionService.Dispose disconnects (bounded by
              Disconnect timeout), force-kills, then disposes adapter
              session; ApplicationShutdown disposes IDebugSessionService
              first, then the projection ViewModels
```

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `IDebugSessionService` / `DebugSessionService` | ✓ | ✓ | ✓ when launched | ✓ | ✓ | [DebuggingServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/DebuggingServiceCollectionExtensions.cs) L20 |
| `IDebugAdapterLocator` / `DebugAdapterLocator` | ✓ | ✓ | ✓ on start | when user launches | via `AdapterUnavailable` failure path | L16–L17; locator reads `ZAIDE_NETCOREDBG_PATH` |
| `IDebugAdapterSessionFactory` / `DebugAdapterSessionFactory` | ✓ | ✓ | ✓ on start | n/a | n/a | L18 |
| `DebugSessionTimeoutPolicy` | ✓ | ✓ | ✓ on every request | n/a | n/a | L19 |
| `NetCoreDbgAdapterSession` (one per session) | ✓ | factory only | ✓ | when user launches | n/a | [NetCoreDbgAdapterSession.cs](../../../../src/Features/Debugging/Infrastructure/Dap/NetCoreDbgAdapterSession.cs) |
| `DapContentLengthTransport` (one per session) | ✓ | adapter session only | ✓ | n/a | n/a | [DapContentLengthTransport.cs](../../../../src/Features/Debugging/Infrastructure/Dap/DapContentLengthTransport.cs) |
| Stderr capture (distinct from stdin/stdout DAP) | ✓ | ✓ | ✓ | when user launches | appended to `DiagnosticOutput` (`[error] …`) | [DebugSessionService.HandleSessionEndedAsync / HandleStartupFailureAsync](../../../../src/Features/Debugging/Application/DebugSessionService.cs) — `AppendStderrDiagnostics` |
| Generation bump on Stop / context change / failure | ✓ | — | ✓ | n/a | n/a | `StopAsync` L378; `OnProjectContextChanged`/`ReconcileContextAsync` L646; `HandleStartupFailureAsync` / `FailActiveSessionAsync` |
| `IDebugSessionService` dispose ordering | ✓ | — | ✓ | n/a | n/a | [ApplicationShutdown.cs](../../../../src/App/Composition/ApplicationShutdown.cs) L44 disposes `IDebugSessionService` first; then resolved `DebugPanelViewModel` / `DebugCurrentLocationViewModel` / `EditorBreakpointViewModel` / `DebugSessionViewModel` |
| F5-usable after terminal failure | ✓ | — | ✓ | ✓ | ✓ | `IsStartAllowed` permits `Idle`, `Failed`, `Unavailable`, `Stopped` ([DebugSessionService.cs](../../../../src/Features/Debugging/Application/DebugSessionService.cs) L1221–L1222) |
| Stale generation/selection rejection | ✓ | — | ✓ | n/a | ✓ | `RequireStoppedSession` / `PublishIfCurrentGenerationAsync`; selection tokens in `DebugStackProjectionViewModel` and `DebugCurrentLocationViewModel` |

### 4.2 Launch handoff (`A1-DB-01` launch configuration)

```text
User action: F5 (StartOrContinue) while Idle/Failed/Unavailable/Stopped
  → DebugSessionViewModel.ExecuteStartOrContinueAsync
       Stopped branch: IAgentContextSessionPolicyService-style — threadId +
         IDebugSessionService.ContinueAsync(threadId)
       Start branch: SaveAllDirtyTabsAsync → IProjectDebugLaunchService.StartDebuggingAsync
         → IsDebugEligible(ProjectContext): IsEligible + SelectedProject.Kind == CSharpProject
         → IProjectOperationGate.TryAcquireDebugHandoffAsync
              admit if no workflow operation, no handoff, no blocking session
         → IProjectWorkflowService.StartBuildForDebugHandoffAsync(handoffLease)
              Locked MSBuild build; Succeeded required
         → IProjectDebugTargetResolver.ResolveTargetPathAsync
              `dotnet msbuild <csproj> -getProperty:TargetPath`
              exactly one normalized absolute existing .dll required
         → BreakpointService.GetBreakpoints (enabled only)
         → DebugLaunchRequest(resolution.TargetPath, workingDirectory,
                                StopAtEntry: true, breakpoints)
         → IDebugSessionService.StartLaunchAsync
              adapter acquire → Initialize → Launch → setBreakpoints per source
                → configurationDone → initial Stopped (with timeout)
         → finally: handoffLease.Dispose
```

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| F5 dispatch (no separate `debug.start` / `debug.continue`) | ✓ | ✓ | ✓ | ✓ | ✓ | [DebugSessionViewModel.cs](../../../../src/Features/Debugging/Presentation/DebugSessionViewModel.cs) L80–L94, L149–L173 |
| `IProjectOperationGate` shared admission | ✓ | ✓ | ✓ | via F5 | via failure kind | [ProjectOperationGate.cs](../../../../src/Features/ProjectSystem/Infrastructure/ProjectOperationGate.cs) L50–L55 (workflow) and L82–L95 (debug handoff); split admission/critical-section mutexes (M3a `Dispose_WhileFakeRunnerEmitting` regression fix) |
| Mutual `Workflow busy` / `Debug session active` | ✓ | — | ✓ | n/a | n/a | `ProjectOperationGateMessages` constants; `Reject` switch |
| `IProjectDebugTargetResolver` `TargetPath` query | ✓ | ✓ | ✓ | n/a | n/a | [ProjectDebugTargetResolver.cs](../../../../src/Features/ProjectSystem/Infrastructure/ProjectDebugTargetResolver.cs); locked argv; no `bin/` scanning; injectable managed process runner |
| `UnsupportedLaunchTarget` on empty/relative/non-DLL/multi-target | ✓ | — | ✓ | n/a | ✓ | `DebugSessionOutcomeKind.UnsupportedLaunchTarget`; `DebugPanelViewModel.AppendStateTransition` |
| `BuildFailed` on non-success workflow build | ✓ | — | ✓ | n/a | ✓ | `ProjectDebugLaunchService.MapBuildFailureMessage` → `ReportPreLaunchFailureAsync` |
| Handoff lease held across build → resolve → launch | ✓ | — | ✓ | n/a | n/a | `ProjectDebugLaunchService.StartDebuggingAsync` `try/finally handoffLease.Dispose()` L73–L150 |
| `CSharpProject` only (no `Solution` / `SolutionX` debug) | ✓ | — | ✓ | n/a | n/a | `IsDebugEligible` predicate uses `context.SelectedProject!.Kind == ProjectKind.CSharpProject` |
| F5 rejects while workflow is running | ✓ | — | ✓ | ✓ | ✓ | `ProjectOperationGate.IsDebugSessionBlocking` + `WorkflowBusy` |
| Save-before-debug dirty tabs guard | ✓ | — | ✓ | n/a | n/a | `DebugSessionViewModel.EnsureDirtyTabsSavedAsync` + `SaveAllDirtyTabsAsync` set by `MainWindowViewModel` |

### 4.3 Breakpoint persistence and editor margin (`A1-DB-01` breakpoints)

```text
SettingsService.UpdateAsync (v3 DebugSettings, BreakpointsByWorkspaceRoot)
  ↳ BreakpointService (workspace-root-keyed, ordinal-normalized)
  ↳ EditorBreakpointViewModel
       canToggleBreakpoint = (hasWorkspaceRoot) AND (active tab) AND
                              (saved file) AND (valid caret line 1..N)
       ToggleBreakpointCommand (F9) / ToggleAtLineCommand (margin click)
  ↳ SyncDapReplacementAsync: when session is Running/Stopped,
       IBreakpointService.MapToDapReplacementBySource
       IDebugSessionService.ReplaceBreakpointsBySourceAsync
            per source, SetBreakpointsAsync with current line set
            ApplyBreakpointVerificationsAsync updates session snapshot
  ↳ EditorBreakpointMarker (view-model projection)
  ↳ EditorBreakpointProjection.ForSource (enabled state + verification)
  ↳ InstructionPointerMargin (separate from breakpoint margin)
  ↳ DapBreakpointVerificationParser (verified / pending / rejected mapping)
```

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `IBreakpointService` / `BreakpointService` | ✓ | ✓ | ✓ | n/a | n/a | L23 |
| Workspace-root-keyed persistence | ✓ | — | ✓ | n/a | n/a | `BreakpointService.GetBreakpoints` / `AddAsync` / `RemoveAsync` / `ToggleAsync` use `IProjectContextService.Current.WorkspaceRoot` |
| `DebugSettings` schema v3 + v2→v3 migration | ✓ | ✓ | ✓ | n/a | n/a | [SettingsModel.cs](../../../../src/Features/Settings/Domain/SettingsModel.cs) (`DebugSettings`, `BreakpointsByWorkspaceRoot`); [SettingsService.cs](../../../../src/Features/Settings/Infrastructure/SettingsService.cs) V2→V3 |
| `debug.toggleBreakpoint` (F9) registered with no conflict | ✓ | ✓ | ✓ | ✓ | ✓ | [EditorBreakpointViewModel.cs](../../../../src/Features/Debugging/Presentation/EditorBreakpointViewModel.cs) L98–L114 |
| Toggle gating (workspace, saved file, valid caret) | ✓ | — | ✓ | ✓ | n/a | `canToggleBreakpoint` combines `CreateHasToggleContext()` and `EditorBreakpointProjection.IsValidCaretLine` |
| Active-session DAP replacement (no session restart) | ✓ | — | ✓ | when session Running/Stopped | session snapshot | `EditorBreakpointViewModel.SyncDapReplacementAsync` L234–L242; `IDebugSessionService.ReplaceBreakpointsBySourceAsync` per source |
| Per-source `setBreakpoints` (full source replacement) | ✓ | — | ✓ | n/a | n/a | `BreakpointService.MapToDapReplacementBySource`; `DebugSessionService.ReplaceBreakpointsBySourceAsync` |
| Verification projection (Verified / Pending / Rejected) | ✓ | — | ✓ | n/a | ✓ | `DapBreakpointVerificationParser.Parse`; `DebugSessionService.ApplyBreakpointVerificationsAsync`; `EditorBreakpointProjection.ForSource` |
| `InstructionPointerMargin` distinct from breakpoint margin | ✓ | — | ✓ | n/a | ✓ | [InstructionPointerMargin.cs](../../../../src/Features/Debugging/Presentation/InstructionPointerMargin.cs) (leftmost, yellow `#FCBB47` arrow) |
| M3a/M3b M0-fixed dispose race | ✓ | — | ✓ | n/a | n/a | `ProjectOperationGate` admission/critical-section mutex split (M3a acceptance) |

### 4.4 Execution controls (`A1-DB-01` step controls)

| Command ID | Display name | Default gesture(s) | State-gated predicate | Registrar | Evidence |
|------------|--------------|--------------------|-----------------------|-----------|----------|
| `debug.startOrContinue` | Start Debugging / Continue | `F5` | `Idle` / `Failed` / `Unavailable` / `Stopped` | `DebugSessionViewModel` ctor (DI singleton `ICommandRegistry`) | L80–L94 |
| `debug.pause` | Pause | _(none)_ | `Running` | same | L83, L95–L100 |
| `debug.stop` | Stop Debugging | `Shift+F5` | `Starting` / `Running` / `Stopped` | same | L84, L101–L106 |
| `debug.stepOver` | Step Over | `F10` | `Stopped` with `StopInfo.ThreadId` | same | L85, L107–L112 |
| `debug.stepInto` | Step Into | `F11` | `Stopped` with `StopInfo.ThreadId` | same | L86, L113–L118 |
| `debug.stepOut` | Step Out | `Shift+F11` | `Stopped` with `StopInfo.ThreadId` | same | L87, L119–L124 |
| `debug.toggleBreakpoint` | Toggle Breakpoint | `F9` | workspace + saved file + valid caret line | `EditorBreakpointViewModel` ctor | `EditorBreakpointViewModel.cs` L98–L114 |

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `canStartOrContinue` observable | ✓ | — | ✓ | ✓ | — | `DebugSessionViewModel` L62–L66 |
| `canPause` observable | ✓ | — | ✓ | ✓ | — | L68–L69 |
| `canStop` observable | ✓ | — | ✓ | ✓ | — | L71–L74 |
| `canStep` observable | ✓ | — | ✓ | ✓ | — | L76–L78 |
| `ExecuteStartOrContinueAsync` dispatch (Stop vs Start) | ✓ | — | ✓ | ✓ | ✓ (status) | L149–L173 |
| `StatusMessage` projection on failure | ✓ | — | ✓ | ✓ | ✓ | L218–L225; `DebugSessionViewModel.StatusMessage` set when `snapshot.Failure.Message` |
| `debug.startOrContinue` is the only F5 command | ✓ | — | ✓ | ✓ | n/a | `CommandRegistry` materializes a single static gesture→command map; per [Phase 12 plan §"Locked Contracts" §6](../../../phases/v2/phase-12/IMPLEMENTATION_PLAN.md) |
| Gesture conflict test | ✓ | — | — | — | — | `DebugExecutionControlsCommandTests.Registry_DebugGesturesResolveExactlyOnce` (corroboration only) |

### 4.5 Call stack, scopes, variables, current location (`A1-DB-01` projection)

```text
DebugSessionSnapshot.State == Stopped
  → DebugStackProjectionViewModel.ApplySnapshot
       → LoadStoppedStateAsync
            → SetCallStackLoading, ClearThreadFrameScopeCollections
            → IDebugSessionService.RequestThreadsAsync
                 (timeout: DebugSessionTimeoutPolicy.OrdinaryRequest)
            → DapInspectionParser.ParseThreads
            → auto-select first thread → IDebugSessionService.RequestStackTraceAsync
            → auto-select frame 0
            → on SelectScope: IDebugSessionService.RequestScopesAsync
            → IDebugSessionService.RequestVariablesAsync (first level only)
       selection-token stale rejection on each step
  → DebugCurrentLocationViewModel (subscribes to SelectedFrame)
       → resolve frame.SourcePath → LspUtf16PositionMapper.TryGetOffset
       → EditorTabViewModel.OpenFileCommand + RequestNavigate
       → EditorInstructionPointerMarker(Line)
       → clear on non-Stopped / continue / generation change
  → DebugPanelViewModel (subscribes to session state)
       → AppendStateTransition + AppendNewDiagnostics
       → WhenShowDebugRequested on transition Idle→Starting (and on initial Starting)
  → DebugPanel (3-column Grid, FuncDataTemplate per line kind)
```

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `DebugStackProjectionViewModel` thread/frame/scope/variable pipeline | ✓ | ✓ | ✓ | ✓ (selection) | ✓ | [DebugStackProjectionViewModel.cs](../../../../src/Features/Debugging/Presentation/DebugStackProjectionViewModel.cs) L107–L114; `SelectThread` / `SelectFrame` / `SelectScope` |
| `RequestThreadsAsync` / `RequestStackTraceAsync` / `RequestScopesAsync` / `RequestVariablesAsync` DAP requests | ✓ | ✓ | ✓ while Stopped | n/a | n/a | `IDebugSessionService` L493–L536; `RequireStoppedSession` enforces state+generation |
| `DebugProjectionState` (Unavailable / Loading / Ready / Error) | ✓ | — | ✓ | n/a | ✓ | `CallStackState` / `VariablesState` drive thread list / variable list `IsVisible` |
| Stale-response protection | ✓ | — | ✓ | n/a | n/a | `_stoppedLoadToken` / `_threadSelectionToken` / `_frameSelectionToken` / `_scopeSelectionToken` |
| First-thread / first-frame / first-scope auto-selection (M5) | ✓ | — | ✓ | ✓ | ✓ | `LoadStoppedStateAsync` set call-stack-loading, set variables-unavailable, then auto-select first thread on stop ([DebugStackProjectionViewModel.cs](../../../../src/Features/Debugging/Presentation/DebugStackProjectionViewModel.cs) L168–L211) |
| Selected-frame current location projection | ✓ | ✓ | ✓ | ✓ | ✓ | [DebugCurrentLocationViewModel.cs](../../../../src/Features/Debugging/Presentation/DebugCurrentLocationViewModel.cs) L100–L185 |
| `LspUtf16PositionMapper.TryGetOffset` reuse | ✓ | ✓ | ✓ | n/a | n/a | Same seam audited in [A2_FILE_NAVIGATION_AND_EDITING.md](./A2_FILE_NAVIGATION_AND_EDITING.md) (`A1-FN-08` caveats apply: open tracked document only; live `csharp-ls` re-validation not in this slice) |
| Instruction-pointer gutter (distinct from breakpoint margin) | ✓ | — | ✓ | n/a | ✓ | `InstructionPointerMargin`; `InstructionPointerOperations` leftmost; `EditorView.SyncInstructionPointerMargin` reactively syncs on `ProjectionRevision` (per M7 closeout §3.1) |
| Clearing on continue / end / failure / context change / dispose / generation change | ✓ | — | ✓ | n/a | ✓ | `DebugStackProjectionViewModel.ClearProjection`; `DebugCurrentLocationViewModel.ClearProjection` on non-Stopped; `DebugSessionService._breakpointVerifications.Clear()` on Stop / context change / pre-launch failure |

### 4.6 Debug console, output, and panel composition (`A1-DB-01` console + panel)

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `DebugPanelViewModel.Lines` (`ObservableCollection<DebugConsoleLineViewModel>`) | ✓ | ✓ | ✓ | ✓ | ✓ | [DebugPanelViewModel.cs](../../../../src/Features/Debugging/Presentation/DebugPanelViewModel.cs) L40, L155–L161 |
| `DebugConsoleLineKind` (Info / Output / Error) | ✓ | — | ✓ | n/a | ✓ | L155 + View FuncDataTemplate in [DebugPanel.cs](../../../../src/Features/Debugging/Presentation/DebugPanel.cs) L50–L73 |
| `[error]` annotation for adapter / pre-launch / rejected-breakpoint lines | ✓ | — | ✓ | n/a | ✓ | `AppendNewDiagnostics` ([DebugPanelViewModel.cs](../../../../src/Features/Debugging/Presentation/DebugPanelViewModel.cs) L142–L153); `DebugSessionService.AppendDiagnostic($"[error] {message}")` / `AppendDiagnostic(rejection-detail)` |
| Console history preserved after session end | ✓ | — | ✓ | n/a | ✓ | `DebugSessionService` keeps `_diagnosticOutput` until next `Starting` snapshot clears it; `DebugPanelViewModel` does not auto-clear |
| Stderr lines drained independently, never parsed as protocol | ✓ | — | ✓ | n/a | ✓ | `DapContentLengthTransport` is dedicated to framed JSON; `DebugSessionService.AppendStderrDiagnostics` |
| `DebugPanel` 3-column layout (Console 2* | Call Stack 1* | Variables 1*) | ✓ | — | ✓ | n/a | ✓ | L143–L165 (Console / Call Stack / Variables columns with 4px splitter gaps) |
| `BottomPanelMode.Debug` (5-value enum) | ✓ | — | ✓ | ✓ | ✓ | [BottomPanelHost.cs](../../../../src/App/Shell/BottomPanelHost.cs) L193–L203; `Terminal | Problems | Output | TestResults | Debug` |
| Show-on-Starting affordance | ✓ | — | ✓ | ✓ | ✓ | `MainWindowActivationHost` subscribes to `_debugPanelViewModel.WhenShowDebugRequested` and sets `BottomPanelMode.Debug` + `IsBottomPanelVisible = true` ([MainWindowActivationHost.cs](../../../../src/App/Shell/MainWindowActivationHost.cs) L116–L122) |
| Bottom-panel mode exclusivity | ✓ | — | ✓ | ✓ | — | Each child host's `IsVisible` is `mode == <own enum value>`; only one visible at a time |
| Status text (top of panel) | ✓ | — | ✓ | ✓ | ✓ | `_statusText` binds to `ViewModel.StatusMessage`; visible only when non-empty |
| Automation names / help text | ✓ | — | ✓ | n/a | n/a | `AutomationProperties.SetName` / `SetHelpText` on each list ("Debug console lines", "Debug threads", "Call stack frames", "Debug scopes", "Debug variables") |
| Auto-scroll on `Lines.Add` (≤ 20px of `ScrollBarMaximum.Y`) | ✓ | — | ✓ | ✓ | — | [DebugPanel.cs](../../../../src/Features/Debugging/Presentation/DebugPanel.cs) L179–L196 |
| Thread-list visibility (only Ready + >1 thread) | ✓ | — | ✓ | n/a | ✓ | L237–L243 |
| Scope-list visibility (only Ready + >1 scope) | ✓ | — | ✓ | n/a | ✓ | L245–L251 |

### 4.7 Adapter failure / recovery projection (`A1-DB-01` truthful failure handling)

| Outcome | Source layer | Result / projection | Evidence |
|---------|--------------|---------------------|----------|
| `AdapterUnavailable` | Locator returns `null` (no `ZAIDE_NETCOREDBG_PATH` and no `netcoredbg` on `PATH`) | `DebugSessionService.ReportPreLaunchFailureAsync` publishes `Failed` with status text `"NetCoreDbg was not found. Set ZAIDE_NETCOREDBG_PATH or add netcoredbg to PATH."`; F5 remains usable | [DebugSessionService.cs](../../../../src/Features/Debugging/Application/DebugSessionService.cs) L105–L118; [DebugAdapterLocator.cs](../../../../src/Features/Debugging/Infrastructure/Dap/DebugAdapterLocator.cs) L12–L13 |
| `BuildFailed` | Workflow `StartBuildForDebugHandoffAsync` returns non-`Succeeded` | `ProjectDebugLaunchService` maps outcome → message; `ReportPreLaunchFailureAsync` publishes `Failed`; handoff lease released in `finally` | [ProjectDebugLaunchService.cs](../../../../src/Features/ProjectSystem/Infrastructure/ProjectDebugLaunchService.cs) L79–L92 |
| `UnsupportedLaunchTarget` | `IProjectDebugTargetResolver.ResolveTargetPathAsync` returns non-success | `ReportPreLaunchFailureAsync` publishes `Failed` | L100–L113 |
| `StartupFailed` (initialize / launch / configuration / stopped timeouts) | `WithTimeoutAsync` raises `OperationCanceledException` on timeout | `HandleStartupFailureAsync` publishes `Failed`; stderr appended; `_breakpointVerifications` cleared; `PublishLocked` bumped to next generation | [DebugSessionService.cs](../../../../src/Features/Debugging/Application/DebugSessionService.cs) L271–L304, L691–L745 |
| `ProtocolFailed` (ordinary request error or timeout) | `ExecuteSessionRequestAsync` catch | `FailActiveSessionAsync` publishes `Failed`; gate/process cleaned | L1021–L1062 |
| `AdapterExited` (`terminated` / `exited` / `ProcessExited`) | Session event handlers | `HandleSessionEndedAsync` → `FailActiveSessionAsync`; `AppendStderrDiagnostics`; new generation; F5 usable | L910–L986 |
| `Cancelled` (caller token during start) | `OperationCanceledException` with caller token | `HandleStartupFailureAsync` publishes `Failed`; `Cancelled` message | L259–L269 |
| `RejectedContext` (no eligible C# project / `Solution` / `SolutionX`) | `IsDebugEligible` | Returns `RejectedContext` with truthful message; no adapter acquire | L89–L95, [ProjectDebugLaunchService.cs](../../../../src/Features/ProjectSystem/Infrastructure/ProjectDebugLaunchService.cs) L53–L60 |
| `RejectedConcurrent` (workflow busy, debug handoff active, or active debug session) | `IProjectOperationGate` or session state | Returns `RejectedConcurrent` with `WorkflowBusy` / `DebugSessionActive`; no adapter | [ProjectOperationGate.cs](../../../../src/Features/ProjectSystem/Infrastructure/ProjectOperationGate.cs) L50–L95 |
| Rejected / pending breakpoint | `DapBreakpointVerificationParser` | `[error] Breakpoint rejected at <path>:<line>[: message]`; session snapshot `BreakpointVerifications` updated; `EditorBreakpointProjection` overlays verified/pending/rejected state on margin | [DebugSessionService.cs](../../../../src/Features/Debugging/Application/DebugSessionService.cs) L1090–L1138 |
| Context change while active | `IProjectContextService.WhenChanged` | `ReconcileContextAsync` bumps generation, publishes `Stopping`, tears down, publishes new context; gate unchanged | L630–L689 |
| Stop during startup | `StopAsync` / failure handlers | Generation bump, `Stopping` snapshot, teardown, terminal `Idle` | L362–L408 |
| Start after failure | `IsStartAllowed` includes `Failed` | F5 starts a new attempt; diagnostics retained, verifications cleared | L1221–L1222 |

**Recovery contract (per M6_DAP_RECOVERY_PROOF):**

| Requirement | Source-proven location |
|-------------|------------------------|
| Terminal snapshot + diagnostics retained | `DebugSessionFailure(message)` published; `_diagnosticOutput` retained across pre-launch / `Failed` transitions |
| Live inspection data cleared | `StopInfo = null`, `_breakpointVerifications.Clear()`, `DebugStackProjectionViewModel.ClearProjection`, `DebugCurrentLocationViewModel.ClearProjection` on non-`Stopped` |
| Gate release | `ProjectDebugLaunchService` `finally handoffLease.Dispose()`; `StopAsync` tears down adapter |
| Adapter process cleanup | `TearDownActiveSessionAsync` → `DisconnectAsync` (bounded by `Disconnect` timeout) → `ForceKillAsync` → `DisposeAsync` ([DebugSessionService.cs](../../../../src/Features/Debugging/Application/DebugSessionService.cs) L747–L781) |
| F5 usability | `IsStartAllowed` includes `Idle` and `Failed` |
| Stale-generation immunity | `_generation++` on every Stop / context change / pre-launch / failure; `RequireStoppedSession` / `PublishIfCurrentGenerationAsync` / selection tokens reject late replies |

### 4.8 Production DI and user-entry-point reachability

| Service | Registered | Resolved on startup | User-reachable | Evidence |
|---------|------------|---------------------|----------------|----------|
| `IDebugAdapterLocator` | ✓ | yes (singleton) | n/a (used by F5) | [DebuggingServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/DebuggingServiceCollectionExtensions.cs) L16–L17 |
| `IDebugAdapterSessionFactory` | ✓ | yes (singleton) | n/a | L18 |
| `DebugSessionTimeoutPolicy` | ✓ | yes (singleton) | n/a | L19 |
| `IDebugSessionService` | ✓ | yes (singleton) | via F5 | L20 |
| `IBreakpointService` | ✓ | yes (singleton) | via F9 / margin | L23 |
| `IProjectOperationGate` | ✓ | yes (singleton) | via F5 / Build / Run / Test | [ProjectSystemServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/ProjectSystemServiceCollectionExtensions.cs) L18 |
| `IProjectDebugTargetResolver` | ✓ | yes (singleton) | n/a | L20 |
| `IProjectDebugLaunchService` | ✓ | yes (singleton) | via F5 | L21 |
| `DebugSessionViewModel` | ✓ | eager via `App.axaml.cs` | ✓ | [DebuggingServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/DebuggingServiceCollectionExtensions.cs) L26; [App.axaml.cs](../../../../src/App/Composition/App.axaml.cs) L45 |
| `DebugStackProjectionViewModel` | ✓ | yes (singleton) | ✓ | L27 |
| `DebugCurrentLocationViewModel` | ✓ | eager via `App.axaml.cs` | ✓ | L28; [App.axaml.cs](../../../../src/App/Composition/App.axaml.cs) L47 |
| `DebugPanelViewModel` | ✓ | yes (singleton) | ✓ | L29 |
| `EditorBreakpointViewModel` | ✓ | eager via `App.axaml.cs` | ✓ | L30; [App.axaml.cs](../../../../src/App/Composition/App.axaml.cs) L46 |
| `DebugPanel` (View) | constructed in `BottomPanelHost` ctor | n/a (constructed once) | ✓ via `BottomPanelMode.Debug` | [BottomPanelHost.cs](../../../../src/App/Shell/BottomPanelHost.cs) L35 |
| `InstructionPointerMargin` / `InstructionPointerOperations` | installed by `EditorView.SyncInstructionPointerMargin` | n/a | ✓ via `DebugCurrentLocationViewModel.Marker` | [EditorView.cs](../../../../src/Features/Editor/Presentation/EditorView.cs) L452–L468 (per M7 closeout §3.1) |

| User-visible entry point | Debug reachable? | Evidence |
|--------------------------|-------------------|----------|
| **F5** (Start Debugging / Continue) | **Yes** | `DebugSessionViewModel.StartOrContinueCommand` registered with `F5`; one-command F5 dispatch per Phase 12 plan §6 |
| **Shift+F5** (Stop Debugging) | **Yes** | L101–L106 |
| **F10** / **F11** / **Shift+F11** (Step Over / Into / Out) | **Yes** (Stopped + thread) | L107–L124 |
| **Pause** | **Yes** (Running) | L95–L100; no default gesture |
| **F9** (Toggle Breakpoint) | **Yes** (workspace + saved file + valid caret line) | `EditorBreakpointViewModel.ToggleBreakpointCommand` |
| **Margin click** (breakpoint glyph) | **Yes** (calls `ToggleAtLineCommand`) | `EditorBreakpointViewModel.ToggleAtLineCommand` |
| **Command Palette** (`Ctrl+Shift+P`) | **Yes** (lists Debug-category commands) | Per [A2_SEARCH_AND_COMMAND_DISCOVERY.md](./A2_SEARCH_AND_COMMAND_DISCOVERY.md) §4.3 inventory |
| **Settings** | **No** (Phase 12 deliberately has no Settings surface for debug config; persisted only via `DebugSettings` schema v3) | [SettingsModel.cs](../../../../src/Features/Settings/Domain/SettingsModel.cs); Phase 12 plan §5 |
| **Bottom-panel Debug tab** | **Yes** (`BottomPanelMode.Debug` via `SwitchToDebugBottomCommand`; also auto-shown on `Starting` via `WhenShowDebugRequested`) | [BottomPanelHost.cs](../../../../src/App/Shell/BottomPanelHost.cs) L72–L76, L193–L203; [MainWindowActivationHost.cs](../../../../src/App/Shell/MainWindowActivationHost.cs) L116–L122 |
| **Thread picker** (only when `Ready` and `>1` thread) | **Yes** | [DebugPanel.cs](../../../../src/Features/Debugging/Presentation/DebugPanel.cs) L240–L242 |
| **Frame selection** | **Yes** (drives `RequestScopes` / current location) | L266–L277 |
| **Scope selection** | **Yes** (drives `RequestVariables`) | L279–L290 |

---

## 5. `A1-XX-04` scoped disposition (DAP environment validation constraint)

**Label:** scoped disposition only — **not** a user-goal verdict and **not** one
of `Wired` / `Wired-with-gap` / `Missing` / `Ambiguous`.

| Document claim | Production observation |
|----------------|------------------------|
| DAP environment validation is constrained on Linux; DAP is not re-measured when NetCoreDbg is absent; desktop debug UI rows remain not validated. A2 must confirm whether the disposable environment can host NetCoreDbg for a re-measurement, or whether the validation gap is real and blocking. | **Confirmed constrained.** `DebugAdapterLocator.Resolve()` checks `ZAIDE_NETCOREDBG_PATH` (absolute executable) and then `netcoredbg` on `PATH`; no auto-download, no bundling, no well-known-directory scan, no platform-conditional code beyond the `Environment.GetEnvironmentVariable("PATH")` colon-split. Production DI wires the locator with `Environment.GetEnvironmentVariable("ZAIDE_NETCOREDBG_PATH")` ([DebuggingServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/DebuggingServiceCollectionExtensions.cs) L16–L17). The M7 closeout recorded **8 production adapter proof tests** against NetCoreDbg 3.2.0-1092 on Linux x64 ([M7_MANUAL_EVIDENCE.md §1, §2.1–§2.9](../../../phases/v2/phase-12/M7_MANUAL_EVIDENCE.md)) — the proof exists for the documented environment, not for environments where NetCoreDbg is not installed. |
| Adapter unavailability produces truthful `AdapterUnavailable` failure | **Observed.** `DebugSessionService.StartLaunchAsync` returns `DebugSessionOutcomeKind.AdapterUnavailable` and publishes `Failed` with the locator's `UnavailableMessage`; F5 remains usable for retry. |
| Desktop debug UI is the in-shell `DebugPanel` (`BottomPanelMode.Debug`) | **Observed.** Console + Call Stack + Variables; mode-strip button; `MainWindowActivationHost.WhenShowDebugRequested` subscription. The headless M7 closeout marked the gutter paint, three-column proportions, console color differentiation, and live Avalonia keyboard delivery as **visual-only items requiring display** ([M7_MANUAL_EVIDENCE.md §7 "Visual-only items"](../../../phases/v2/phase-12/M7_MANUAL_EVIDENCE.md)). M7 deferred those to **Phase 13 release-hardening manual smoke**. |
| Disposable environment for A3 must host NetCoreDbg or the gap is real and blocking | **Real, blocking for positive-path A3.** The slice charter and [Phase 12 plan §"Phase 12 Limitations"](../../../phases/v2/phase-12/IMPLEMENTATION_PLAN.md) explicitly bound Phase 12 to Linux x64 with C#; no platform parity. The 8 production adapter proof tests pass under a controlled `netcoredbg` at `/tmp/zaide-phase12-m0-netcoredbg/...`. A3 must provision NetCoreDbg at a known path or set `ZAIDE_NETCOREDBG_PATH`; otherwise `AdapterUnavailable` is the only reachable state, and positive-path scenarios cannot complete. Negative-path A3 (locator returns null → truthful `AdapterUnavailable` → F5 retry) is executable on a profile without NetCoreDbg. |

**Relation to user-goal rows:** explains the **gap** half of `A1-DB-01`; does
**not** receive `Wired` / `Wired-with-gap` / `Missing` / `Ambiguous` as a
second user-goal verdict.

**Relation to prior A2 evidence:** the platform-constrained production
adapter is the source-proven reality. No prior A2 evidence re-verdicted
debug rows.

---

## 6. Source-proven wiring vs runtime / A3-unproven behavior

This section separates facts that A2 can prove from `src/` alone
(source-proven) from facts that require a running process and a real
NetCoreDbg adapter (runtime / A3-unproven). The verdict table above
intentionally uses the source-proven facts only.

### 6.1 Source-proven (verdict authority)

1. `IDebugSessionService` and the six debug projection ViewModels are
   registered as singletons and are eagerly resolved at startup.
2. `debug.startOrContinue` / `pause` / `stop` / `stepOver` / `stepInto` /
   `stepOut` / `toggleBreakpoint` are registered via `ICommandRegistry` with
   the locked default gestures (`F5` / _(none)_ / `Shift+F5` / `F10` / `F11`
   / `Shift+F11` / `F9`); `canExecute` predicates are state-gated on the
   live `DebugSessionSnapshot`.
3. `IProjectOperationGate` is the single source of truth for the
   one-at-a-time Build / Run / Test / Debug admission; `DebugSessionState`
   blocking is observed; `ProjectDebugLaunchService` handoff lease is
   disposed in `finally`.
4. `IProjectDebugTargetResolver` resolves the `TargetPath` via
   `dotnet msbuild <csproj> -getProperty:TargetPath` only; non-empty
   normalized absolute existing `.dll` required.
5. Persistent source breakpoints are stored in
   `DebugSettings.BreakpointsByWorkspaceRoot` (schema v3); v2→v3 migration
   registered; path/line normalization; full-source replacement request
   policy.
6. Active-session DAP `setBreakpoints` replacement fires on every persisted
   breakpoint mutation while the session is `Running` or `Stopped`; no
   session restart.
7. `DebugStackProjectionViewModel` runs the
   `threads → stackTrace(threadId) → scopes(frameId) → variables(variablesReference)`
   pipeline with selection-token stale-response rejection; clears on
   non-`Stopped`; first-thread / first-frame / first-scope auto-selection
   on each stop.
8. `DebugCurrentLocationViewModel` resolves the selected frame's source
   path, opens the document via `EditorTabViewModel.OpenFileCommand`,
   navigates to the offset via `LspUtf16PositionMapper.TryGetOffset`, and
   projects an `EditorInstructionPointerMarker` (distinct from the
   breakpoint margin).
9. `DebugPanel` is the fixed three-column `Console 2* | Call Stack 1* |
   Variables 1*` surface; bound to `DebugPanelViewModel` reactive
   `Lines` / status / call-stack / variables collections; thread-list
   visibility gated on `Ready` and `>1` thread; scope-list visibility
   gated on `Ready` and `>1` scope; auto-scroll on `Lines.Add` within
   20px of `ScrollBarMaximum.Y`.
10. `DebugPanelViewModel.AppendStateTransition` + `AppendNewDiagnostics`
    produce `[error]` annotated lines on pre-launch failure, adapter
    error, and rejected-breakpoint events; console history is preserved
    across session end.
11. `DebugSessionService` recovery contract is implemented: terminal
    snapshot + retained diagnostics; cleared live data (`StopInfo`,
    `_breakpointVerifications`); `Disconnect` timeout-bounded then
    `ForceKill`; `F5` usable after `Failed`; `_generation++` on Stop /
    context change / pre-launch / failure; `RequireStoppedSession` /
    `PublishIfCurrentGenerationAsync` / selection tokens reject late
    replies.
12. `ApplicationShutdown` disposes `IDebugSessionService` first, then
    resolved `DebugPanelViewModel` / `DebugCurrentLocationViewModel` /
    `EditorBreakpointViewModel` / `DebugSessionViewModel`, then
    `IProjectWorkflowService`, then language stack, project context, and
    terminal host.
13. `DebugAdapterLocator` reads `ZAIDE_NETCOREDBG_PATH` first, then
    `netcoredbg` on `PATH`; no auto-download, no bundling, no
    well-known-directory scan, no platform-conditional code.
14. The launch payload is locked: `{ program, cwd, stopAtEntry: true,
    console: "internalConsole" }`; no `launch.json`, no user-configurable
    fields.
15. Breakpoint payload sent to DAP is `{ line }` only; no condition, no
    hit count, no log message, no data breakpoint.
16. Bottom-panel `BottomPanelMode` is a 5-value enum; only one of the
    five children is `IsVisible` at a time.

### 6.2 Runtime / A3-unproven behavior (not in this A2 verdict)

1. Whether a real NetCoreDbg on the disposable-profile host completes
   the M0–M6 proof sequence in the expected time and emits the expected
   DAP envelopes the parsers consume.
2. Whether `DebugAdapterLocator` actually resolves `netcoredbg` via
   `PATH` on the disposable-profile host when `ZAIDE_NETCOREDBG_PATH`
   is unset.
3. Whether the M7 visual-only items (gutter paint, three-column
   proportions, console color differentiation, live Avalonia keyboard
   delivery) read as documented under a real desktop session.
4. Whether the thread-list picker surfaces and behaves correctly with a
   multi-threaded debuggee (the `workflow-console` fixture is
   single-thread).
5. Whether `DebugCurrentLocationViewModel` source-file open + navigate
   composes correctly under real `Save → Edit → Rebuild` cycles (the
   `LspUtf16PositionMapper` seam is unit-tested but live race coverage
   belongs to A3).
6. Whether `DebugPanelViewModel` `WhenShowDebugRequested` actually
   triggers the bottom-panel switch on every transition from
   `Idle`/`Failed` to `Starting` under a real `ProjectDebugLaunchService`
   start.
7. Whether the disposes of `IDebugSessionService` and the debug
   ViewModels during a real Phase 13 release-hardening manual smoke
   actually sequence the adapter teardown before the language stack and
   project context (the order is wired; the order under a real
   `dotnet` failure is not re-executed here).
8. Whether the `DebugPanel` `FuncDataTemplate` for `DebugConsoleLineKind.Error`
   actually paints `WarningBrush` on the live render (the seam is wired;
   the live `IBrush` resource resolution is M7 visual-only).

### 6.3 Out of A2 scope for this slice (cross-referenced, not verdicted)

- The shared `IProjectOperationGate` and the `IProjectWorkflowService.StartBuildForDebugHandoffAsync`
  handoff are owned by [A2_BUILD_RUN_AND_TEST.md](./A2_BUILD_RUN_AND_TEST.md)
  (`A1-BR-01`, `A1-BR-04`); this slice reuses the seams but does not
  re-verdict those rows.
- The shared `IProjectContextService` consumption is described in
  [A2_WORKSPACE_AND_PROJECT_OPENING.md](./A2_WORKSPACE_AND_PROJECT_OPENING.md)
  (`A1-WO-02`); the `A1-WO-02` "ambiguous multi-project picker is
  absent" gap means that no `ProjectContext` state beyond
  `SingleProject` / `Selected` can be reached through any user entry
  point, and the debug eligibility gate therefore returns `RejectedContext`
  for all other states. Phase 12 M1 already records this; A2 confirms
  the wiring is consistent.
- The `LspUtf16PositionMapper` reuse for the current-execution-location
  offset mapping shares the same seam audited in
  [A2_FILE_NAVIGATION_AND_EDITING.md](./A2_FILE_NAVIGATION_AND_EDITING.md)
  (`A1-FN-08`); the open-tracked-documents-only caveat carries forward.
- The `ICommandRegistry` registration of debug commands with default
  gestures and conflict-free F-key uniqueness is owned by
  [A2_SEARCH_AND_COMMAND_DISCOVERY.md](./A2_SEARCH_AND_COMMAND_DISCOVERY.md)
  (`A1-SC-01`); this slice does not re-verdict the registry.

---

## 7. Contradiction / attribution corrections

| Document claim | Live production reading |
|----------------|------------------------|
| Phase 12 plan "F5 is not claimed as Debug until M3 registers and proves it" | `DebugSessionViewModel` registers `debug.startOrContinue` with default `F5`; `DebugExecutionControlsCommandTests.Registry_DebugGesturesResolveExactlyOnce` and the M7 closeout confirm no F-key conflict. The one-command F5 dispatch contract is preserved in production. |
| Phase 12 plan "no `launch.json`" and "no user-configurable launch fields" | The launch payload is locked `{ program, cwd, stopAtEntry: true, console: "internalConsole" }`; `DebugLaunchRequest` carries `ProgramPath`, `WorkingDirectory`, `StopAtEntry`, and `Breakpoints` only. |
| Phase 12 plan "Phase 12 does not introduce a second project-discovery model" | `ProjectDebugLaunchService` uses `IProjectContextService.Current.SelectedProject` only; `Workspace.WorkspacePath` is not a debug-target fallback. `ProjectDebugTargetResolver` is the only MSBuild property query. |
| Phase 12 plan "Phase 12 makes no Windows/macOS parity promise" | `DebugAdapterLocator.FindOnPath` splits `PATH` on `:` (Unix-style); no platform-conditional code in production. Headless M7 evidence is Linux x64 only. |
| Phase 12 limitations "Multi-target projects are supported only when that one default evaluation yields exactly one valid target path" | `IProjectDebugTargetResolver` requires exactly one non-empty normalized absolute existing `.dll`; multi-valued results are `UnsupportedLaunchTarget`. |
| Phase 12 plan "M1 registers `stopped`, `continued`, `output`, `terminated`, and `exited` handlers before the DAP transport starts its receive loop" | `DebugSessionService.AttachSessionHandlers` runs before the launch sequence begins (`session.InitializeAsync` / `LaunchAsync` / `SetBreakpointsAsync` / `ConfigurationDoneAsync` follow handler attachment in `StartLaunchAsync`). |
| Phase 12 plan "Only one debug session may be active" | `IDebugSessionService` is a singleton; `DebugSessionService.IsStartAllowed` permits only `Idle` / `Failed`; `IsActiveState` is the gating set for handoff and stop. |
| Phase 12 plan "App disposal order is: debug session … then existing Phase 11 workflow, language stack, project context, and terminal host." | `ApplicationShutdown.cs` L44 disposes `IDebugSessionService` first, then the resolved debug ViewModels (L48–L51), then `IProjectWorkflowService` (L54), then `IProjectContextService` (L71), then `ITerminalHost` (L75). |
| M7 "All Phase 12 limitations remain truthful" | Verified for local launch-debug only; no attach/remote/test debugging; no watch/evaluate; no nested variables; no arbitrary launch config; no conditional/data/log breakpoints; no platform-parity claim; breakpoints address on-disk normalized path and one-based line only; no auto-save of dirty buffers before debug start; class library / non-runnable project → structured failure; no second project-discovery model. |
| "F5 is one state-dispatching command" (Phase 12 plan §6) | Production has one `debug.startOrContinue` command and a Stopped-vs-Start branch in `ExecuteStartOrContinueAsync`. There is no separate `debug.start` / `debug.continue`. |

No issue / deferred / prior evidence files were edited.

---

## 8. A3 clean-profile smoke constraints (described only — not started)

A3 for `A1-DB-01` must respect the A0–A4 disposable-profile rules and the
additional constraints inherited from
[A2_BUILD_RUN_AND_TEST.md §5.2](./A2_BUILD_RUN_AND_TEST.md#52-runtime--a3-unproven-behavior-not-in-this-a2-verdict)
(`IProjectOperationGate` / `IProjectContextService` / `BottomPanelMode`
exclusivity).

1. **Disposable isolated profile only** — temporary `XDG_CONFIG_HOME`
   (or equivalent); never the real user profile, settings, conversation
   store, or debug breakpoint store
   ([AUDIT_PLAN.md §3](../AUDIT_PLAN.md#3-safety-and-isolation-rules-mandatory-for-a0a4)).
2. **Disposable workspace only** — harmless C# `dotnet build` target with
   a real `.csproj` whose `TargetPath` resolves to one absolute `.dll`
   (the `tests/fixtures/workflow-console/WorkflowConsole.csproj`
   fixture used in M0–M7 is a known candidate; never mutate it during
   A3).
3. **NetCoreDbg must be available on the disposable host.** Either set
   `ZAIDE_NETCOREDBG_PATH` to a known absolute executable or place
   `netcoredbg` on `PATH`. The M7 closeout recorded the production
   adapter version as **NetCoreDbg 3.2.0-1092**. Without an adapter,
   `AdapterUnavailable` is the only reachable state and positive-path
   scenarios cannot complete. Do **not** attempt to install
   `netcoredbg` from a non-authoritative source or to fall back to a
   different debugger.
4. **Backend bind prerequisite does not apply** — debug has its own
   contract; F5 is the user-reachable entry, not a backend bind.
5. **Cases to cover when NetCoreDbg + eligible C# project + workspace
   are available:**
   - F5 starts a session and the bottom panel switches to `Debug`.
   - F9 toggles a breakpoint; margin projection shows enabled/disabled
     visual state; `DebugSettings.BreakpointsByWorkspaceRoot` contains
     the entry.
   - F5 launches, sends all `setBreakpoints`, `configurationDone` reaches
     `Stopped` at entry; bottom-panel Console records state transitions.
   - F10 / F11 / Shift+F11 step through stopped frames; call-stack and
     variables populate; current location opens the source and shows the
     instruction-pointer marker.
   - Shift+F5 returns to `Idle`; console history preserved; process
     tree cleaned.
6. **Failure cases to cover (always executable):**
   - Missing adapter: `ZAIDE_NETCOREDBG_PATH` unset and no
     `netcoredbg` on `PATH` → `AdapterUnavailable` with locator's
     `UnavailableMessage`; F5 retry remains usable.
   - Build failure: introduce a compile error in the disposable
     workspace → `BuildFailed`; F5 retry after fix.
   - `UnsupportedLaunchTarget`: point the resolver at a non-`.dll`
     target path (or multi-valued) → `UnsupportedLaunchTarget`; F5
     retry after a real `.dll` is present.
   - Context change while active: switch the project context
     (eligibility flips) → session torn down to a new `Idle` /
     `Unavailable` snapshot; live data cleared.
   - Rapid F5 / Shift+F5: at least one F5-then-Shift+F5 cycle returns
     to `Idle`; F5 remains usable.
   - `Cancelled`: pre-launch cancel → `Cancelled` with `Failed`
     snapshot; F5 retry remains usable.
7. **Visibility distinction** — record both bottom-panel Debug Console
   lines (`[error]` annotated) and the `DebugSessionSnapshot.Failure.Message`
   surfaced by the `DebugSessionViewModel.StatusMessage`; they are not
   the same surface.
8. **Adapter verification projection** — observe `Verified` /
   `Pending` / `Rejected` overlays on the breakpoint margin when the
   adapter reports mixed verification; do **not** assert Verified
   until the adapter's `setBreakpoints` response is parsed and
   projected.
9. **Rejection projection** — type a deleted line number (e.g. line
   beyond file length) → `Rejected` with `[error] Breakpoint rejected
   at <path>:<line>` line; persisted `PersistedBreakpoint` intent is
   unchanged; the rejected overlay is session-only.
10. **Limitation gap** — do **not** expect a separate Debug Console
    terminal; debug `output` events are projected into
    `DebugPanelViewModel.Lines`. Do **not** expect a Settings entry for
    debug configuration; persisted config is in `DebugSettings` only.
    Do **not** expect watch/evaluate / attach / remote / data
    breakpoints / conditional breakpoints; Phase 12 explicitly bounds
    Phase 12 to local launch debugging with line-only breakpoints.
11. **Clean-up** — remove disposable profile and workspace; never touch
    real user profile, real settings, or the production breakpoint
    store.
12. Do not treat Phase 12 unit/integration green as A3 substitutes; the
    M7 closeout itself defers visual-only items to Phase 13
    release-hardening manual smoke.

A3 is **not** begun in this session. Positive-path scenarios remain
blocked on a disposable host that can install or supply NetCoreDbg;
negative-path scenarios are executable on any disposable host.

---

## 9. Corroborating tests (non-proof)

The tests below corroborate individual contracts but use test doubles or
a real adapter under controlled conditions; A2 does not promote them to
production reachability proof.

| Area | Representative tests | Prove | Do **not** prove |
|------|----------------------|-------|-------------------|
| Adapter / transport / session lifecycle | `DebugSessionServiceTests`, `NetCoreDbgLifecycleProofTests`, `NetCoreDbgAdapterSessionDirectTests` | Initialize / launch / breakpoints / stopped / continue / disconnect against real NetCoreDbg 3.2.0-1092; fake-session ordering / generation / disposal | Disposable-profile A3; multi-thread picker under a real Avalonia render; live keyboard delivery |
| Launch handoff | `ProjectOperationGateTests`, `ProjectDebugTargetResolverTests`, `ProjectDebugLaunchServiceTests`, `DebugStartOrContinueCommandTests`, `M3aDebugLaunchProofTests` | Gate, resolver, launch service, F5 dispatch; real NetCoreDbg build → resolve → launch → stop | Real Avalonia F5 keyboard delivery; F5 retry after a controlled production pre-launch failure |
| Breakpoint persistence + editor margin | `BreakpointServiceTests`, `SettingsServiceTests` v2→v3 / round-trip / unknown-v4, `DebugToggleBreakpointCommandTests`, `EditorBreakpointViewModelTests`, `EditorBreakpointProjectionTests`, `EditorBreakpointRegressionTests`, `DapBreakpointVerificationParserTests`, `M3bDebugBreakpointProofTests` | F9 gating, persistence, projection, DAP replacement, regression, verification parsing; production breakpoint hit after continue | Live margin render; dirty-buffer save on F5 in a real session |
| Execution controls | `DebugExecutionControlsCommandTests` (state-gated predicates, registry uniqueness, dispatch), `DebugPanelViewModelTests` (console history, isolation, error projection, call-stack shell), `M4DebugExecutionProofTests` | Pause / stop / step gating; registry gestures; console isolation; production launch → breakpoint stop → step → stop | Live keyboard delivery; F5 retry after a controlled production request failure |
| Stack / variables / current location | `DebugStackProjectionTests`, `DebugCurrentLocationViewModelTests`, `M5DebugStackProofTests` | Stopped load / selection / empty / error / stale-generation / clearing; production stack + scope + variable + continue | Three-column panel render; multi-thread picker render; live navigate under real `Save → Edit → Rebuild` |
| Recovery | `M6DebugRecoveryProofTests` (real NetCoreDbg: stop → restart → stop; missing adapter → `Failed`), `DebugSessionServiceTests.AdapterProcessExit_*`, `DebugSessionServiceTests.StopDuringStartup_*`, `DebugSessionServiceTests.StartAfterFailure_*` | Production recovery contract; missing-adapter `Failed`; stop / restart / context change; `DebugExecutionControlsCommandTests.CanExecute` after failure | Live Avalonia retry UX; visual indicator that F5 remains usable after failure |
| Architectural boundaries | `CanonicalCommandRegistrationTests` (18 canonical commands incl. all 7 debug commands), `Registry_DebugGesturesResolveExactlyOnce` | Command registry invariants; F-key uniqueness | Discoverability of debug UX under real Avalonia focus / input |

**Distinction from production composition:** the tests do not require
the production `ICommandRegistry` to be wired at construction time
(DebugSessionViewModel and EditorBreakpointViewModel accept
`ICommandRegistry? commandRegistry = null`); they construct the ViewModels
with a test registry. Production reachability is established separately
by the source-trace in §4 and the DI registration in §4.8.

---

## 10. Issue / deferred-finding relationships (read-only)

No issue or deferred-finding files were edited. The following artifacts
bear on this slice without creating new commitments.

| Artifact | Relationship to this slice |
|----------|----------------------------|
| [A2_BUILD_RUN_AND_TEST.md](./A2_BUILD_RUN_AND_TEST.md) | Shared `IProjectOperationGate` and workflow-owned build handoff re-used for the debug launch path; `A1-BR-01` / `A1-BR-04` verdicts unchanged; `BottomPanelMode` exclusivity source-proven |
| [A2_FILE_NAVIGATION_AND_EDITING.md](./A2_FILE_NAVIGATION_AND_EDITING.md) | `LspUtf16PositionMapper` reused for current-execution-location offset mapping; `A1-FN-08` open-tracked-documents-only caveat carries forward to current-location navigation |
| [A2_SEARCH_AND_COMMAND_DISCOVERY.md](./A2_SEARCH_AND_COMMAND_DISCOVERY.md) | `ICommandRegistry` registers the seven debug commands with default gestures; `A1-SC-01` registry + `Registry_DebugGesturesResolveExactlyOnce` invariants cover debug F-key uniqueness |
| [A2_WORKSPACE_AND_PROJECT_OPENING.md](./A2_WORKSPACE_AND_PROJECT_OPENING.md) | `IProjectContextService` consumer for debug eligibility; `A1-WO-02` "ambiguous multi-project picker is absent" gap means only `SingleProject` / `Selected` reaches the debug eligibility gate; `A1-WO-03` folder change emits `WorkspaceFolderChanged` consumed by debug session reconciliation |
| [A2_RESTART_RECOVERY_AND_CONTEXT.md](./A2_RESTART_RECOVERY_AND_CONTEXT.md) | Persistence schema does **not** include debug breakpoints, sessions, or runs; `DebugSettings` is the only debug persistence. Binding store / project-context seams re-used. |
| [Phase 12 plan §"Phase 12 Limitations"](../../../phases/v2/phase-12/IMPLEMENTATION_PLAN.md) | Source-proven truths behind the M7 limitations table |
| [M7_MANUAL_EVIDENCE.md §7 "Visual-only items requiring display (honest)"](../../../phases/v2/phase-12/M7_MANUAL_EVIDENCE.md) | M7 itself defers gutter paint, three-column proportions, console color differentiation, and live Avalonia keyboard delivery to Phase 13 release-hardening manual smoke |

---

## 11. Exact next recommended A2 slice (explicitly not started)

**Next recommended A2 slice (not begun in this session):**
`A2_TERMINAL`.

**Suggested goal rows:**

- `A1-TR-01` — embedded terminal in bottom panel; `Ctrl+`` toggle; PTY-backed
  process execution; ANSI/CSI parser, screen buffer; alternate screen;
  selection, scrollback, search
- `A1-TR-02` — terminal tabs (per-tab sessions, session host/factory seam,
  view-layer panel caching, tab strip UI)

**Why this next:** the Debug slice leaves a documented output / terminal
separation (`BottomPanelMode.Output` for Phase 11 Build/Run/Test vs
`BottomPanelMode.Terminal` for the embedded terminal) that belongs to
the Terminal journey, not the Debug journey. Verdict dispositioning the
debugger's relationship to the bottom-panel output stream
([A2_BUILD_RUN_AND_TEST.md `A1-BR-04`](./A2_BUILD_RUN_AND_TEST.md)) is
already complete; the remaining unverified journeys in the IDE / shell
layer are the Terminal journey and the Git workflow journey. A3
disposable-profile preconditions for the Terminal journey are lighter
(no NetCoreDbg, no eligible C# project, no Phase 11 workflow) so a
Terminal slice is a more natural next step than re-entering the agent
or audit layers.

**Explicitly not started here:** that slice, A3, A4, stabilization, V4,
corrective implementation, or any other A2 evidence file.

---

## 12. Verification and working-tree closeout

### Pre-closeout checks (to be re-run after writing)

| Check | Expected |
|-------|----------|
| Exactly one new untracked file | `docs/audits/v1-v3-product-reality/evidence/A2_DEBUGGING_AND_OUTPUT.md` |
| No tracked files modified | Clean aside from that untracked evidence file |
| Whitespace | `git diff --no-index --check /dev/null <evidence-file>` (exit 1 from diff vs `/dev/null` is expected; no whitespace diagnostics) |
| Relative Markdown links | Repository-relative paths under `docs/` and `src/` resolve |
| Fragment links | Headings in this file and cited docs resolve |
| Primary verdict table | Exactly one verdict for `A1-DB-01` |
| `A1-XX-04` | Scoped disposition only (§5); not a user-goal verdict |
| Next slice | Named and **not** begun |
| Commit / push | Not performed |

### Closeout verdicts (repeat)

| id | verdict |
|----|---------|
| `A1-DB-01` | **Wired-with-gap** |
| `A1-XX-04` | Scoped disposition only — DAP environment validation is constrained to a disposable host that can supply NetCoreDbg via `ZAIDE_NETCOREDBG_PATH` or `netcoredbg` on `PATH`; production does not bundle, install, auto-download, or scan well-known directories; Phase 12 bounds to Linux x64 with C#; M7 closeout defers visual-only items to Phase 13 release-hardening manual smoke. Positive-path A3 remains blocked on NetCoreDbg availability; negative-path A3 (missing-adapter `Failed`, F5 retry) is executable on any disposable host. |

**Stop for re-audit.** No next slice started. No commit or push.

---

*A2_DEBUGGING_AND_OUTPUT complete. Read-only audit; no fixes, A3 work,
commits, or pushes.*
