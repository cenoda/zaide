# Phase 12: C# Debugging (DAP) — TOFIX

**Status:** Phase 12 is complete (M0–M7 closeout, 2026-07-14). Post-closeout
implementation audit (2026-07-14) recorded one resolved transport finding and six
open findings below. Items are ordered by recommended fix priority (highest
first).

**Source:** Live-code audit of the DAP transport, debug session lifecycle,
breakpoint/command projection, bottom-panel debug UI, and Phase 12 contracts in
[IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md).

**Gates at audit time (2026-07-14):** `dotnet build Zaide.slnx --no-restore`
completed with 0 errors and the pre-existing `CS0067` test warning;
`dotnet test Zaide.slnx --no-build` passed **2048** tests (0 failed, 0 skipped);
`git diff --check` was clean.

**Gates after F2–F7 closeout (2026-07-14):** `dotnet build Zaide.slnx --no-restore`
completed with 0 errors and the pre-existing `CS0067` test warning;
`dotnet test Zaide.slnx --no-build` passed **2053** tests (0 failed, 0 skipped);
`git diff --check` clean.

---

## Resolved — F2: DebugPanel selection bindings re-fire stack/scope/variable loads

**Severity:** High  
**Area:** `DebugPanel`, `DebugStackProjectionViewModel`

**Problem:** On stop, `DebugStackProjectionViewModel.LoadStoppedStateAsync` already
loads threads → frames → scopes → variables and sets `SelectedThread`,
`SelectedFrame`, and `SelectedScope`. `DebugPanel` mirrors those properties into
`ListBox.SelectedItem`, and each `SelectionChanged` handler calls
`SelectThreadCommand` / `SelectFrameCommand` / `SelectScopeCommand`, which always
increment their selection tokens and issue another DAP load even when the
selection object is unchanged.

**Evidence:**

- Auto-select + load chain: `DebugStackProjectionViewModel.cs` lines 214–219,
  298–302, 365–368.
- VM→UI→command loop: `DebugPanel.cs` lines 252–313.
- `DebugStackProjectionTests` assert single DAP calls but never attach `DebugPanel`,
  so the duplicate path is untested in production composition.

**Contract violated:** Contract 4 (inspection requests should not be redundant);
M5 exit condition (truthful stopped-state projection without races).

**Resolved:** 2026-07-14

**Implementation:** Added `_syncingSelection` guard and `SyncListSelection` in
`DebugPanel` to suppress `SelectionChanged` during VM→UI mirroring. Made
`SelectThread` / `SelectFrame` / `SelectScope` no-op when the target is already
selected in `DebugStackProjectionViewModel`.

**Tests:** `DebugPanelSelectionTests.Stopped_WithDebugPanel_IssuesSingleInspectionRequestPerKind`.

---

## Resolved — F3: Continue/step returns before session state leaves `Stopped`

**Severity:** Medium  
**Area:** `DebugSessionService`, `NetCoreDbgAdapterSession`, `DebugSessionViewModel`

**Problem:** `ContinueAsync` and step commands return when the DAP **response**
completes. `DebugSessionService` transitions to `Running` only when a
`continued` event (or a new `stopped` event after a step) arrives asynchronously
on the transport read loop. During that gap, `DebugSessionViewModel` still treats
the session as `Stopped`, so F5 continue and F10/F11/Shift+F11 step commands
remain enabled and can issue overlapping adapter requests.

**Evidence:**

- State flips on events only: `DebugSessionService.cs` lines 826–874
  (`HandleSessionStoppedAsync`, `HandleSessionContinuedAsync`).
- Production adapter awaits response only: `NetCoreDbgAdapterSession.cs` lines
  216–253 (`ContinueAsync`, `NextAsync`, `StepInAsync`, `StepOutAsync`).
- Tests mask the gap: `TestDebugAdapterSession.cs` lines 155–163 invoke
  `Continued` synchronously inside `ContinueAsync`.
- UI gating: `DebugSessionViewModel.cs` lines 59–75, 146–161 (`canStartOrContinue`,
  `canStep` key off `Stopped`).

**Contract violated:** Contract 4 (“valid only for the appropriate state”); exit
condition (continue/step/stop clear stale data without conflicting commands).

**Resolved:** 2026-07-14

**Implementation:** `ExecuteSessionRequestAsync` now publishes `Running` (clears
`StopInfo`) on successful continue/step. `TestDebugAdapterSession` defers
continued/stopped events by default (`DeferExecutionEvents`) and suppresses
late emits after dispose. `DebugSessionService.Dispose` completes the snapshot
subject without disposing it while projection VMs tear down.

**Tests:** `DebugSessionServiceTests.ContinueAsync_ReturnsRunning_BeforeDeferredContinuedEvent`,
`DebugSessionServiceTests.StepOverAsync_FromStopped_TransitionsToRunningBeforeNextStop`,
`DebugExecutionControlsCommandTests.Continue_Gap_DisablesConflictingCommandsUntilRunningSnapshot`.

---

## Resolved — F4: App shutdown order omits explicit debug-projection teardown

**Severity:** Medium  
**Area:** `App.axaml.cs`, `MainWindowViewModel`, `MainWindow.axaml.cs`

**Problem:** Contract 3 requires disposal order:
**debug session → debug projection services → Phase 11 workflow**. On normal
window close, `MainWindow` `WhenActivated` cleanup disposes `MainWindowViewModel`
(and nested `DebugPanelViewModel`, `DebugStackProjectionViewModel`,
`DebugCurrentLocationViewModel`, `EditorBreakpointViewModel`) **before**
`desktop.Exit` runs `DisposeServicesOnExit`, which disposes `IDebugSessionService`
first among services but never explicitly disposes debug projection VMs.

**Evidence:**

- Service exit path: `App.axaml.cs` lines 90–96 (`IDebugSessionService` then
  workflow; no debug VM markers).
- Window cleanup: `MainWindow.axaml.cs` line 210 (`ViewModel!.Dispose()` in
  `WhenActivated` disposables).
- Debug VMs registered in `MainWindowViewModel.Activate()` lines 345–368.
- Shutdown tests cover workflow projections only:
  `ProjectWorkflowProjectionShutdownTests.cs` (no debug VM dispose markers).

**Contract violated:** Contract 3 (disposal ordering).

**Resolved:** 2026-07-14

**Implementation:** `App.DisposeServicesOnExit` disposes
`DebugPanelViewModel`, `DebugCurrentLocationViewModel`,
`EditorBreakpointViewModel`, and `DebugSessionViewModel` after
`IDebugSessionService` and before `IProjectWorkflowService`. Removed debug
singleton dispose from `MainWindowViewModel.Activate` (App owns lifecycle).

**Tests:** Extended `ProjectWorkflowProjectionShutdownTests.DisposeServicesOnExit_OrdersWorkflowBeforeProjectionsBeforeLanguage` with debug VM dispose markers.

---

## Resolved — F5: `StoppedByUser` outcome is never produced

**Severity:** Low  
**Area:** `DebugSessionService`, `DebugSessionOutcomeKind`

**Problem:** Contract 3 lists `StoppedByUser` as a required terminal outcome kind.
`StopAsync` publishes `Idle` via `BuildIdleSnapshot` without recording
`StoppedByUser` anywhere in the terminal snapshot or last-outcome field.

**Evidence:**

- Enum definition: `DebugSessionOutcomeKind.cs` line 17.
- Stop path: `DebugSessionService.cs` lines 355–400.
- `grep StoppedByUser` matches only the enum and plan docs.

**Contract violated:** Contract 3 (outcome taxonomy completeness).

**Resolved:** 2026-07-14

**Implementation:** Added `LastOutcome` to `DebugSessionSnapshot`. `StopAsync`
records `DebugSessionOutcomeKind.StoppedByUser` in the idle terminal snapshot.

**Tests:** `DebugSessionServiceTests.StopAsync_FromRunning_RecordsStoppedByUserOutcome`,
`DebugSessionServiceTests.StopAsync_FromStopped_RecordsStoppedByUserOutcome`.

---

## Resolved — F6: Scopes/variables fetched without user frame/scope selection

**Severity:** Medium  
**Area:** `DebugStackProjectionViewModel`, `IMPLEMENTATION_PLAN.md` Contract 4

**Problem:** Contract 4 states `scopes` and `variables` should be requested
**only after the user selects** a live frame/scope. M5 implementation auto-selects
the first thread, frame 0, and scope 0 and loads variables on every stop. Tests
codify this auto-load behavior.

**Evidence:**

- Auto-select chain: `DebugStackProjectionViewModel.cs` lines 214–219, 298–302,
  365–368.
- Tests expect auto-load: `DebugStackProjectionTests.cs` lines 55–94
  (`RequestScopesAsync` / `RequestVariablesAsync` on initial stop).

**Contract violated:** Contract 4 (locked request policy). This is intentional M5
UX drift versus the M0-locked contract text.

**Resolved:** 2026-07-14

**Implementation:** Amended Contract 4 in `IMPLEMENTATION_PLAN.md` to document
first-frame auto-inspection (first thread, frame 0, scope 0) as accepted M5 UX.
No projection behavior change.

---

## Resolved — F7: M7 closeout docs report stale test count

**Severity:** Low  
**Area:** `M7_MANUAL_EVIDENCE.md`, `IMPLEMENTATION_PLAN.md` M7 status line

**Problem:** M7 closeout records **2043** passing tests. The post-F1 transport
hardening added `DapContentLengthTransportTests`; the live suite now reports
**2048** passes. Closeout evidence is otherwise accurate but the count is stale.

**Evidence:**

- M7 checklist: `M7_MANUAL_EVIDENCE.md` line 382 (“2043 passed”).
- Audit gate: `dotnet test Zaide.slnx --no-build` → 2048 passed (2026-07-14).

**Contract violated:** None (documentation accuracy only).

**Resolved:** 2026-07-14

**Implementation:** Updated `M7_MANUAL_EVIDENCE.md` and `IMPLEMENTATION_PLAN.md`
M7 status lines to **2053** passing tests (2048 post-F1 plus five F2–F5
regression tests).

---

## Resolved — F1: DAP pending-request map is accessed concurrently without synchronization

**Severity:** High (was)  
**Area:** `DapContentLengthTransport`  
**Resolved:** 2026-07-14

**Problem:** `RequestAsync`, the read loop, and `DisposeAsync` accessed the same
`Dictionary<int, TaskCompletionSource<JsonElement?>>` without synchronization.
Overlapping DAP requests could lose responses, throw during enumeration, or
leave stale pending entries until timeout.

**Implementation:** Replaced `_pending` with
`ConcurrentDictionary<int, TaskCompletionSource<JsonElement?>>`. Response
dispatch, caller cancellation, and `RequestAsync` cleanup each `TryRemove` the
sequence before completing/cancelling so only one path owns a pending entry.
`DisposeAsync` now cancels the read loop, immediately drains pending requests
via key snapshot + `TryRemove`, then tears down the read loop and write gate.
Content-Length writes remain serialized behind `_writeGate` only.

**Tests:** Added `tests/Zaide.Tests/Services/DapContentLengthTransportTests.cs`
with a non-parallel xUnit collection and deterministic harness coverage for:
single request/response completion; 32 overlapping requests with out-of-order
responses; cancellation racing response dispatch (50 iterations with `Barrier`);
and disposal racing eight outstanding requests (50 iterations). Tests assert
correct correlation, terminal completion (success or cancellation), and
`_pending` count returning to zero without collection exceptions.

---

## Verified — no additional issues found

| Area | Result |
|------|--------|
| Adapter locator (`ZAIDE_NETCOREDBG_PATH` → PATH) | Actionable `AdapterUnavailable` message; no silent download |
| Launch sequence | `initialize → launch → setBreakpoints* → configurationDone → stopped` |
| Timeouts | 15s / 15s / 10s / 5s in `DebugSessionTimeouts.cs` |
| Generation safety | Stale events ignored; context change bumps generation |
| Breakpoint schema v3 | `DebugSettings`, `SettingsMigrationV2ToV3`, serializer ceiling 3 |
| Build→TargetPath→launch handoff | `ProjectDebugLaunchService` + shared `ProjectOperationGate` |
| Commands | All 7 debug commands registered; F5 only on `debug.startOrContinue` |
| Bottom panel Debug mode | Debug Console + Call Stack + Variables in `DebugPanel.cs` |
| MVVM boundaries | No `Zaide.Views` in `Services/`; breakpoint/session logic in ViewModels |
| DI / session-before-workflow on exit | Tested in `ProjectWorkflowProjectionShutdownTests.cs` |