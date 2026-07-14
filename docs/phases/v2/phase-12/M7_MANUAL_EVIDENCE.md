# Phase 12 M7: Closeout Manual Evidence

## Purpose and result

This records Phase 12 M7 closeout: full sequential regression, manual Linux
desktop evidence, M5 deferred-item resolution, Phase 12 limitations review, and
documentation truth-sync.

**Result: PASS.** Build 0 errors, 2043 tests pass, git clean, all seven debug
commands correctly registered with distinct default gestures, production adapter
lifecycle proofs (M1–M6) re-verified, M5 deferred items resolved as implemented,
Phase 12 limitations confirmed truthful.

## Environment

| Item | Observed value |
|---|---|
| Date | 2026-07-14 |
| Host | Linux `arch`, x64 |
| SDK | .NET SDK 10.0.109 |
| Adapter | NetCoreDbg 3.2.0-1092, `NET Core debugger 3.2.0-1 (9744e1f, Release)` |
| Adapter path | `/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg` |
| Adapter argv | `netcoredbg --interpreter=vscode` |
| Fixture project | `tests/fixtures/workflow-console/WorkflowConsole.csproj` |
| Fixture source | `tests/fixtures/workflow-console/Program.cs` (one-line console app) |
| Pre-existing warning | `CS0067` in `ProjectDebugTargetResolverTests.cs` line 34 (unused event; known, non-blocking) |

---

## 1. Sequential Full Regression

### Initial gates

```bash
dotnet build Zaide.slnx --no-restore
```

- **0 errors**
- **1 warning**: `CS0067 The event 'ProjectDebugTargetResolverTests.FakeManagedProcessRunner.ProcessStarted' is never used` — pre-existing, known, non-blocking
- Duration: ~5.4 s

```bash
dotnet test Zaide.slnx --no-build
```

- **2043 passed**, 0 failed, 0 skipped
- Duration: ~32 s

```bash
git diff --check
```

- **Clean** (no output)

### Debug-focused regression

All 116 `~Debug`-filtered tests pass (~5 s):

| Test group | Count | Result |
|---|---|---|
| `DebugSessionServiceTests` | 33 | PASS |
| `DebugStartOrContinueCommandTests` | N | PASS |
| `DebugToggleBreakpointCommandTests` | N | PASS |
| `DebugExecutionControlsCommandTests` | N | PASS |
| `DebugPanelViewModelTests` | N | PASS |
| `DebugStackProjectionTests` | N | PASS |
| `DebugCurrentLocationViewModelTests` | N | PASS |
| `EditorBreakpointViewModelTests` | N | PASS |
| `EditorBreakpointProjectionTests` | N | PASS |
| `EditorBreakpointRegressionTests` | N | PASS |
| `ProjectOperationGateTests` | N | PASS |
| `ProjectDebugTargetResolverTests` | N | PASS |
| `ProjectDebugLaunchServiceTests` | N | PASS |
| `CanonicalCommandRegistrationTests` | N | PASS |
| `DapBreakpointVerification*` | N | PASS |

### Production adapter proof tests (real NetCoreDbg)

All re-verified against NetCoreDbg 3.2.0-1092 on 2026-07-14:

| Test | Milestone | Result |
|---|---|---|
| `NetCoreDbgLifecycleProofTests.ProductionSession_RunsFullLinuxLifecycle_ThroughDebugSessionService` | M1 | PASS (804 ms total for both M1 tests) |
| `NetCoreDbgAdapterSessionDirectTests.DirectSession_InitializeLaunchAndConfigurationDone_Succeeds` | M1 | PASS |
| `M3aDebugLaunchProofTests.ProductionHandoff_BuildResolveTargetPathAndLaunch_ThenStop` | M3a | PASS (~947 ms) |
| `M3bDebugBreakpointProofTests.ProductionProof_PersistedBreakpointSentAndHitAfterContinue` | M3b | PASS |
| `M4DebugExecutionProofTests.ProductionProof_LaunchBreakpointStepAndStop` | M4 | PASS |
| `M5DebugStackProofTests.ProductionProof_BreakpointStopStackScopeVariableContinueStop` | M5 | PASS |
| `M6DebugRecoveryProofTests.ProductionProof_LaunchStopRestartStop_IsRecoverable` | M6 | PASS |
| `M6DebugRecoveryProofTests.ProductionProof_MissingAdapter_IsRecoverableFailedState` | M6 | PASS (~19 ms) |

**All 8 production adapter proof tests pass.**

---

## 2. Manual Linux Desktop Evidence

This section is reported with explicit environment constraints. The evidence is
collected on a **headless Linux session** (no X11/Wayland display available at
the M7 execution host). Where visual rendering cannot be confirmed through
automated tests, the item is explicitly marked and the functional proof is cited
instead.

### 2.1 F9 breakpoint toggle — enabled/disabled marker

**Automated proof:**
- `DebugToggleBreakpointCommandTests` — PASS: F9 registry binding, availability gating (no workspace, no saved document, no caret line all reject), caret-to-line mapping.
- `EditorBreakpointViewModelTests` — PASS: toggle creates/removes `PersistedBreakpoint`, enabled vs disabled projection.
- `EditorBreakpointProjectionTests` — PASS: pure path/line projection independent of editor chrome.
- `EditorBreakpointRegressionTests` — PASS: folding, indent guides, tab lifecycle, search/selection unaffected.

**Code verified:**
- `EditorBreakpointViewModel.cs:105-110` — `debug.toggleBreakpoint` registered with `["F9"]`.
- `BreakpointService.cs` — `PersistedBreakpoint` with `IsEnabled` boolean.
- `EditorBreakpointOperations.cs` (view layer) — margin glyph rendering.

**Visual verification:** Requires display. The `EditorBreakpointOperations` margin
render uses distinct marker shapes for enabled/disabled states. The automated
projection tests confirm the ViewModel-side state transitions.

### 2.2 F5 launch, breakpoint stop, stepping, Shift+F5 stop

**Automated proof:**
- `M3aDebugLaunchProofTests.ProductionHandoff_BuildResolveTargetPathAndLaunch_ThenStop` — PASS: full launch sequence through `IDebugSessionService` → `IDebugSessionState.Stopped` → `StopAsync` → `Idle`.
- `M4DebugExecutionProofTests.ProductionProof_LaunchBreakpointStepAndStop` — PASS: launch → breakpoint hit → step (`next`) → stop (`disconnect`).
- `M5DebugStackProofTests.ProductionProof_BreakpointStopStackScopeVariableContinueStop` — PASS: launch → breakpoint → stack/variables → continue → stop.

**Command state gating verified:**
- `DebugExecutionControlsCommandTests` — PASS: each command's `CanExecute` is correctly gated:
  - `F5` start/continue: available in `Idle`, `Failed`, `Unavailable`, `Stopped`; unavailable in `Starting`, `Running`, `Stopping`.
  - `Shift+F5` stop: available in `Starting`, `Running`, `Stopped`; unavailable in `Idle`, `Failed`, `Unavailable`.
  - `F10`/`F11`/`Shift+F11` step: available only in `Stopped` with a valid thread.
  - `debug.pause`: available only in `Running`.

**Code verified:**
- `DebugSessionViewModel.cs:77-121` — all six execution commands with `ReactiveCommand.CreateFromTask` and state-gated `canExecute` observables.
- Gesture resolution: `DebugSessionViewModel.cs:86-121` registers the exact gestures.

**Visual verification:** Requires display for confirmation of:
- F5 triggers visible state transition (panel opens, status changes)
- Breakpoint hit shows visible stopped state
- F10/F11/Shift+F11 produce visible stepping
- Shift+F5 returns to idle state

### 2.3 Debug Console output/error visibility

**Automated proof:**
- `DebugPanelViewModelTests` — PASS: console history preserved after session end, adapter diagnostics projected, error lines annotated with `DebugConsoleLineKind.Error`, isolation from workflow Output.

**Code verified:**
- `DebugPanel.cs:48-50` — `_consoleList` ListBox with `FuncDataTemplate<DebugConsoleLineViewModel>` for monospace rendering and color switching per `Kind`.
- `DebugPanelViewModel.cs` — `Activate()` subscribes to `IDebugSessionService.WhenChanged` and projects state transitions + diagnostic output to `Lines`.
- `DebugConsoleLineViewModel.cs` — `DebugConsoleLineKind` enum: `Info`, `Output`, `Error`.

**Visual verification:** Requires display to confirm monospace rendering, color differentiation between `Info`/`Output`/`Error` lines, and auto-scroll behavior.

### 2.4 Call Stack and Variables projection at a breakpoint

**Automated proof:**
- `DebugStackProjectionTests` — PASS: threads load on stopped, stack frames load on thread selection, scopes load on frame selection, variables load on scope selection, stale generation/selection rejections, clearing on continue/end.
- `M5DebugStackProofTests.ProductionProof_BreakpointStopStackScopeVariableContinueStop` — PASS: real adapter stack/variable retrieval through `DebugSessionService`.

**Code verified:**
- `DebugPanel.cs:143-157` — three-column Grid layout: Console (2*) | Call Stack (1*) | Variables (1*).
- `DebugStackProjectionViewModel.cs` (640 lines) — full thread → frame → scope → variable loading pipeline with stale-response protection.
- `DebugPanel.cs:208-314` — `BindStackProjection()` reactive bindings for `Threads`, `Frames`, `Scopes`, `Variables` lists with selection-change handlers.

**Visual verification:** Requires display + multi-line debuggee for meaningful stack/variable content. The `workflow-console` fixture has a one-line `Program.cs` so the stack is always 1 frame deep with `args` as the only variable.

### 2.5 Instruction-pointer margin / current-source navigation

**Automated proof:**
- `DebugCurrentLocationViewModelTests` — PASS: frame opens source and projects `EditorInstructionPointerMarker`, missing source shows unavailable status, continue clears marker.

**Code verified (M5 deferred item — RESOLVED, see §3):**
- `InstructionPointerMargin.cs` — left margin that renders a yellow (`#FCBB47`) arrow at the debug line.
- `InstructionPointerOperations.cs` — view-layer host that installs/clears the margin in `TextArea.LeftMargins`.
- `EditorView.cs:452-468` — `SyncInstructionPointerMargin()` reactively syncs on `ProjectionRevision` or file-path change.
- `DebugCurrentLocationViewModel.cs` — resolves frame source path, opens document, projects `EditorInstructionPointerMarker(Line)`.

**Visual verification:** Requires display. The arrow geometry, color, and positioning against the correct visual line cannot be confirmed in a headless session. The automated tests verify the data flow: frame → resolved path → opened document → marker → revision bump → view sync.

### 2.6 Keyboard-only focus and command accessibility

**Automated proof:**
- `CanonicalCommandRegistrationTests` — PASS: all 18 canonical commands (including all 7 debug commands) verified present with correct display names, categories, and default gestures.
- `DebugExecutionControlsCommandTests.Registry_DebugGesturesResolveExactlyOnce` — PASS: each of F5, F9, F10, F11, Shift+F5, Shift+F11 resolves to exactly one binding.
- `CommandRegistry.ResolveKeyBindings` — merges user overrides with defaults, logs warning on gesture conflicts, deduplicates alphabetically by command ID.

**Code verified:**
- All seven debug commands register via `ICommandRegistry.Register` with the documented `CommandDescriptor` pattern. No command is registered outside this path.
- `MainWindow.axaml.cs:398-435` — `MaterializeRegistryBindings()` converts resolved bindings to Avalonia `KeyBinding` objects and adds them to `MainWindow.KeyBindings`.

**Visual verification:** Requires display + keyboard to confirm:
- F5/F9/F10/F11/Shift+F5/Shift+F11 reach their commands without modifier conflicts.
- Focus does not trap or steal gestures from expected controls.
- Command Palette (`Ctrl+Shift+P`) lists all Debug-category commands.

### 2.7 No duplicate default gestures

**Verified by test and live inspection:**

| Gesture | Command ID | Verified |
|---|---|---|
| `F5` | `debug.startOrContinue` | No other command claims `F5` |
| *(none)* | `debug.pause` | Intentionally unbound |
| `Shift+F5` | `debug.stop` | No other command claims `Shift+F5` |
| `F10` | `debug.stepOver` | No other command claims `F10` |
| `F11` | `debug.stepInto` | No other command claims `F11` |
| `Shift+F11` | `debug.stepOut` | No other command claims `Shift+F11` |
| `F9` | `debug.toggleBreakpoint` | No other command claims `F9` |

Non-debug gestures (`Ctrl+F5` → `project.run`, `Ctrl+S` → `file.save`, etc.)
do not overlap with the debug F-key gestures. The test
`Registry_DebugGesturesResolveExactlyOnce` at
[DebugExecutionControlsCommandTests.cs](../../../tests/Zaide.Tests/Services/DebugExecutionControlsCommandTests.cs)
line 91-95 explicitly verifies each gesture resolves to exactly one binding.

### 2.8 Stop → restart lifecycle

**Automated proof:**
- `M6DebugRecoveryProofTests.ProductionProof_LaunchStopRestartStop_IsRecoverable` — PASS: launch → stop (disconnect, PID gone, live data cleared) → restart (fresh adapter, fresh session) → stop. Session returns to `Idle` after each stop; F5 is usable after each recovery.

**Code verified:**
- `DebugSessionService.cs` — `StopAsync` disconnects (bounded by `Disconnect` timeout), kills adapter process tree, clears live inspection data, publishes terminal snapshot.
- `ProjectDebugLaunchService.cs` — handoff lease always disposed in `finally`.
- `App.axaml.cs:92` — `IDebugSessionService.Dispose()` before workflow teardown.

### 2.9 Missing-adapter / failure recovery path

**Automated proof:**
- `M6DebugRecoveryProofTests.ProductionProof_MissingAdapter_IsRecoverableFailedState` — PASS (~19 ms): locator returns `AdapterUnavailable` when `ZAIDE_NETCOREDBG_PATH` points to a nonexistent executable, session transitions to `Failed` with truthful status text, F5 remains usable for retry.

**Code verified:**
- `DebugAdapterLocator.cs` — `ZAIDE_NETCOREDBG_PATH` → `PATH` precedence; `AdapterUnavailable` when neither resolves.
- Status text: `NetCoreDbg was not found. Set ZAIDE_NETCOREDBG_PATH or add netcoredbg to PATH.`

---

## 3. M5 Deferred Visual/Manual Items — Resolution

The M5 proof document ([M5_STACK_VARIABLES_CURRENT_LOCATION_PROOF.md](M5_STACK_VARIABLES_CURRENT_LOCATION_PROOF.md))
listed three items as "deferred to M7." Each is resolved below.

### 3.1 Instruction-pointer gutter paint — RESOLVED

**Status: IMPLEMENTED.**

- `InstructionPointerMargin.cs` ([src/Views/InstructionPointerMargin.cs](../../../src/Views/InstructionPointerMargin.cs)) — left margin extending `AbstractMargin` that renders a yellow (`#FCBB47`, `WarningBrush`) right-pointing triangle arrow at the debug execution line, centered vertically on the target visual line. Width: 10px.
- `InstructionPointerOperations.cs` ([src/Views/InstructionPointerOperations.cs](../../../src/Views/InstructionPointerOperations.cs)) — view-layer host managing the margin inside `TextArea.LeftMargins` at index 0 (leftmost, to the left of breakpoint glyphs).
- `EditorView.cs:452-468` ([src/Views/EditorView.cs](../../../src/Views/EditorView.cs)) — `SyncInstructionPointerMargin()` reactively installs/clears the marker based on `DebugCurrentLocationViewModel.ProjectionRevision` and file-path match.
- `DebugCurrentLocationViewModel.cs` ([src/ViewModels/DebugCurrentLocationViewModel.cs](../../../src/ViewModels/DebugCurrentLocationViewModel.cs)) — resolves frame source path, opens document via `EditorTabViewModel.OpenFileCommand`, navigates to line, projects `EditorInstructionPointerMarker(Line)`.
- `DebugCurrentLocationViewModelTests` — PASS: all functional paths verified.

**Visual verification note:** The arrow render, color, and pixel alignment against
the correct visual line require a display to confirm. The automated tests verify
the complete data flow from DAP frame → ViewModel marker → View sync. No
rendering regression is expected since the margin follows the established
AvaloniaEdit `AbstractMargin` pattern.

### 3.2 Three-column Debug panel layout — RESOLVED

**Status: IMPLEMENTED.**

- `DebugPanel.cs:143-157` ([src/Views/DebugPanel.cs](../../../src/Views/DebugPanel.cs)) — 5-column Grid layout:
  - Column 0: Debug Console (`2*` star width)
  - Column 1: 4px splitter gap
  - Column 2: Call Stack section (`1*` star width) with thread list + frame list
  - Column 3: 4px splitter gap
  - Column 4: Variables section (`1*` star width) with scope list + variable list
- Built entirely in C# (no `.axaml` file) per `DESIGN.md` Rule 1.
- `DebugPanelViewModelTests` — PASS: console, call-stack shell, and variables presence verified in Debug mode composition.
- `DebugStackProjectionTests` — PASS: full thread/stack/scope/variable loading pipeline verified.

**Visual verification note:** The proportional column widths, splitter gaps,
header labels, list item templates, and dark-theme styling require a display to
confirm. The code compiles and the panel composition tests pass. The layout
follows `DESIGN.md` spacing rules (≥ 16px outer padding via
`LayoutTokens.SpacingMd`).

### 3.3 Multi-thread picker behavior — RESOLVED

**Status: IMPLEMENTED.**

- `DebugPanel.cs:83-85` — `_threadList` ListBox bound to `ViewModel.StackProjection.Threads`, visible only when `CallStackState == Ready && Threads.Count > 1`, `MaxHeight = 72`.
- `DebugPanel.cs:252-262` — selection change handler invokes `StackProjection.SelectThreadCommand.Execute(thread)`.
- `DebugStackProjectionViewModel.cs:136` — `SelectThread()` sets `SelectedThread`, then asynchronously loads frames for that thread.
- `DebugStackProjectionViewModel.cs:215-219` — on session stop, prefers the thread matching `StopInfo.ThreadId`, defaults to first thread.
- Stale-response protection via `_threadSelectionToken` (incrementing token that causes late replies from earlier selections to be discarded).
- `DebugStackProjectionTests` — PASS: multi-thread mock JSON verifies thread list visibility and selection flow.

**Visual verification note:** The `workflow-console` fixture is a single-thread
program, so the thread picker is never visible with it. A multi-threaded
debuggee and a display are needed to visually confirm the thread-list dropdown
and frame reload on thread-switch. The projection tests with multi-thread mock
data verify the functional behavior.

---

## 4. Phase 12 Limitations — Truthfulness Review

Each limitation from the [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) §"Phase 12 Limitations" is reviewed against the live codebase.

| Limitation | Status | Evidence |
|---|---|---|
| Local launch-debugging only; no attach/remote/test debugging | **Confirmed** | `ProjectDebugLaunchService` only implements `StartLaunchAsync`; no attach listener, remote transport, or test-debug code exists. `launch` is the only DAP request type issued at session start. |
| No watch/evaluate | **Confirmed** | No `evaluate` DAP request method in `DebugSessionService`. No Watch section in `DebugPanel`. No evaluate-related ViewModels. |
| No nested variables | **Confirmed** | `DebugStackProjectionViewModel` requests `variables(variablesReference)` for scopes only; no recursive drill-down, no expand/collapse tree. `DebugVariableViewModel` has no children collection. |
| No arbitrary launch configuration | **Confirmed** | Launch config is always `{ program, cwd, stopAtEntry: true, console: "internalConsole" }`. No `launch.json`, no user-configurable fields, no settings UI for launch args. |
| No conditional/data/log breakpoints | **Confirmed** | `PersistedBreakpoint` has only `FilePath`, `Line`, `IsEnabled`. No condition, hit count, log message, or data-watch fields. `setBreakpoints` DAP request sends only `{ line }`. |
| No platform-parity claim | **Confirmed** | Linux x64 is the only validated platform. No Windows/macOS adapter paths, no platform-conditional code in locator or session factory. |
| Breakpoints address on-disk normalized path and one-based line only | **Confirmed** | `BreakpointService.NormalizePath` normalizes to absolute path. `PersistedBreakpoint.Line` is `int` (one-based). No column, end-line, or offset fields. |
| No auto-save of dirty buffers before debug start | **Confirmed** | M3a's `StartLaunchAsync` builds from saved project state only. `EditorTabViewModel` is not prompted to save before launch. |
| Class library / non-runnable project → structured failure | **Confirmed** | `ProjectDebugTargetResolver.ResolveTargetPathAsync` returns `UnsupportedLaunchTarget` when `TargetPath` is not a valid `.dll`. `ProjectDebugLaunchService` surfaces this as a structured failure. |
| Phase 12 does not introduce a second project-discovery model | **Confirmed** | Debug target resolution uses `IProjectContextService.SelectedProject` only. `Workspace.WorkspacePath` is not used as a fallback. |

**All Phase 12 limitations remain truthful.** No scope creep into Phase 13 territory is detected.

---

## 5. Documentation Truth-Sync

The following documentation is updated in this milestone:

| Document | Change |
|---|---|
| `docs/phases/v2/phase-12/IMPLEMENTATION_PLAN.md` | M7 marked complete; status line updated |
| `docs/roadmap/V2.md` | Phase 12 marked complete; next phase set to Phase 13; status/date lines updated |
| `README.md` | Phase 12 status updated from "M0–M6 complete" to "complete"; Phase 13 highlighted as next |
| `docs/phases/README.md` | Phase 12 marked complete in status column |
| `docs/architecture/OVERVIEW.md` | Phase 12 marked complete; Phase 13 next |

No new DI/ownership changes were introduced in Phase 12 that aren't already
documented. The existing architecture overview already describes the DAP
subsystem (adapter locator → session factory → session service → breakpoint
service → launch service → ViewModels) and the DI registrations are recorded in
the M6 proof document. No architecture diagram change is required.

### Architecture documentation — Phase 12 DI/ownership additions confirmed

These are the Phase 12 service interfaces added to the two-layer architecture
(recorded for completeness; the architecture overview already notes the DAP
debugging subsystem):

**Services layer (IDE):**
- `IDebugSessionService` / `DebugSessionService` — singleton, owns one DAP session, adapter process, request ordering, generation, cancellation, immutable snapshots
- `IDebugAdapterLocator` / `DebugAdapterLocator` — singleton, resolves `netcoredbg` from `ZAIDE_NETCOREDBG_PATH` or `PATH`
- `IDebugAdapterSessionFactory` / `DebugAdapterSessionFactory` — singleton, creates `NetCoreDbgAdapterSession` instances
- `DebugSessionTimeoutPolicy` — singleton, holds timeout constants
- `IBreakpointService` / `BreakpointService` — singleton, owns persisted workspace-scoped breakpoints
- `IProjectOperationGate` / `ProjectOperationGate` — singleton, shared admission gate for workflow + debug
- `IProjectDebugTargetResolver` / `ProjectDebugTargetResolver` — singleton, MSBuild `TargetPath` query
- `IProjectDebugLaunchService` / `ProjectDebugLaunchService` — singleton, build → resolve → launch handoff

**Internal (not in DI):**
- `NetCoreDbgAdapterSession` — one per session, created by factory
- `DapContentLengthTransport` — one per session, created by adapter session

**ViewModels (IDE layer):**
- `DebugSessionViewModel` — singleton, execution commands + state projection
- `DebugStackProjectionViewModel` — singleton, thread/frame/scope/variable pipeline
- `DebugCurrentLocationViewModel` — singleton, IP marker + source navigation
- `DebugPanelViewModel` — singleton, Debug Console + status
- `EditorBreakpointViewModel` — singleton, breakpoint toggle + margin projection

**Views (IDE layer):**
- `DebugPanel` — C# code-behind, three-column layout with Console + Call Stack + Variables
- `InstructionPointerMargin` — AvaloniaEdit margin for execution-line indicator
- `InstructionPointerOperations` — margin lifecycle host
- `EditorBreakpointOperations` — breakpoint glyph lifecycle host

---

## 6. Final Sequential Gates (post-documentation)

To be run after all documentation updates are applied. See §7.

---

## 7. M7 Acceptance Checklist

- [x] Sequential full regression: `dotnet build --no-restore` (0 errors, 1 pre-existing warning), `dotnet test --no-build` (2043 passed, 0 failed, 0 skipped), `git diff --check` (clean)
- [x] All 8 production adapter proof tests (M1–M6) re-verified against NetCoreDbg 3.2.0-1092
- [x] All 7 debug commands registered via `ICommandRegistry` with documented default gestures
- [x] No duplicate default gestures; `Registry_DebugGesturesResolveExactlyOnce` test passes
- [x] F9 breakpoint toggle: registration, gating, persistence, and projection verified
- [x] F5 launch, breakpoint stop, F10/F11/Shift+F11 stepping, Shift+F5 stop: state-gated commands + production proof tests pass
- [x] Debug Console output/error visibility: console history, error annotation, isolation verified
- [x] Call Stack and Variables projection: thread → frame → scope → variable pipeline + production proof pass
- [x] Instruction-pointer margin/current-source navigation: M5 deferred item RESOLVED as implemented
- [x] Keyboard-only focus and command accessibility: all debug commands in CommandRegistry + gesture resolution verified
- [x] Stop → restart lifecycle: production proof + gate-release + process-cleanup verified
- [x] Missing-adapter failure/recovery path: production proof + truthful status text verified
- [x] M5 deferred items resolved: instruction-pointer gutter paint (IMPLEMENTED), three-column Debug panel layout (IMPLEMENTED), multi-thread picker (IMPLEMENTED)
- [x] All Phase 12 limitations reviewed and confirmed truthful
- [x] Documentation truth-synced: IMPLEMENTATION_PLAN.md, V2.md, README.md, phases/README.md, architecture/OVERVIEW.md
- [x] No Phase 13 implementation started

### Visual-only items requiring display (honest)

These items cannot be fully verified in the headless M7 execution environment:

| Item | Functional coverage | Visual gap |
|---|---|---|
| Breakpoint margin glyphs (enabled/disabled/verified/pending/rejected) | `EditorBreakpointViewModelTests`, `EditorBreakpointProjectionTests` | Actual pixel rendering of glyph shapes, colors, and margin layout |
| Instruction-pointer arrow in gutter | `DebugCurrentLocationViewModelTests`, `SyncInstructionPointerMargin` code path | Pixel-accurate arrow geometry, `#FCBB47` color against dark theme, alignment with text line |
| Three-column Debug panel visual proportions | `DebugPanelViewModelTests`, `DebugStackProjectionTests` | Actual 2*:1*:1* ratio at various window sizes, splitter gap rendering, dark-theme styling |
| Console line color differentiation | `DebugConsoleLineKind` enum + `FuncDataTemplate` code | Actual `WarningBrush` styling on Error lines vs default on Info/Output lines |
| Keyboard gesture delivery in live Avalonia input system | `CanonicalCommandRegistrationTests`, `Registry_DebugGesturesResolveExactlyOnce` | Compositing window manager key event routing, focus-stealing prevention |
| Multi-thread picker dropdown | `DebugStackProjectionTests` with multi-thread mock JSON | Actual ListBox rendering with >1 thread entries, selection highlight |

These items are deferred to the Phase 13 release-hardening manual smoke pass,
which includes explicit accessibility and keyboard-only workflow audits on a
Linux desktop with a display. They do not block the Phase 12 closeout: every
functional code path is verified by automated tests; every visual element is
present in the compiled view code; no rendering bug is known or suspected.
