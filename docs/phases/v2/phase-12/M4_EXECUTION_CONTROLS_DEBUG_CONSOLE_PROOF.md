# Phase 12 M4: Execution Controls and Debug Console Proof

## Purpose and result

This records Linux validation for Phase 12 M4: registry execution controls
(pause/stop/step), DAP session gating, Debug bottom-panel composition (Debug
Console + Call Stack shell), and adapter error projection.

**Result: PASS.** Focused M4 unit tests pass, production proof shows launch →
breakpoint stop → step over → stop through the production
`DebugSessionService`, and full regression gates are green.

## Environment and adapter provenance

| Item | Observed value |
|---|---|
| Date | 2026-07-14 |
| Host | Linux x64 |
| SDK | .NET SDK 10.0.109 |
| Adapter | NetCoreDbg 3.2.0-1092 (`NET Core debugger 3.2.0-1`) |
| Adapter path | `/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg` |
| Adapter argv | `netcoredbg --interpreter=vscode` |
| Fixture project | `tests/fixtures/workflow-console/WorkflowConsole.csproj` |
| Fixture source | `tests/fixtures/workflow-console/Program.cs` |
| Breakpoint line | 1 (one-based) |

## Automated proof gates

```bash
dotnet test Zaide.slnx --no-build \
  --filter "FullyQualifiedName~DebugExecution|FullyQualifiedName~DebugPanel|FullyQualifiedName~M4Debug|FullyQualifiedName~PauseAsync|FullyQualifiedName~StepOver"
```

Observed on 2026-07-14:

- `DebugExecutionControlsCommandTests` — PASS (registry gestures, gating, dispatch, uniqueness)
- `DebugPanelViewModelTests` — PASS (console history, isolation, error projection, call-stack shell)
- `DebugSessionServiceTests.PauseAsync_*` / `StepOverAsync_*` — PASS
- `MainWindowViewModelBottomPanelModeTests.BottomPanelMode_Debug_*` — PASS
- `M4DebugExecutionProofTests.ProductionProof_LaunchBreakpointStepAndStop` — PASS (~0.4 s)

Full regression (same session):

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build
git diff --check
```

- Build: 0 errors, 1 pre-existing warning (`ProjectDebugTargetResolverTests`)
- Tests: **2017 passed**, 0 failed, 0 skipped (~31 s)
- `git diff --check`: clean

## Operator smoke checklist (workflow-console)

| Step | Gesture / action | Expected visual/behavior | Result |
|---|---|---|---|
| Start debugging | `F5` | Session starts; Debug panel opens | PASS (M3a/M4 panel auto-show tests) |
| Stop at breakpoint | persisted `Program.cs:1` | Stopped state with breakpoint reason | PASS (production proof) |
| Step over | `F10` | DAP `next` issued while stopped | PASS (production + service tests) |
| Step into / out | `F11` / `Shift+F11` | Registered; stopped-only gating | PASS (command tests) |
| Pause | _(no default gesture)_ | Running-only gating | PASS (command + service tests) |
| Stop debugging | `Shift+F5` | Disconnect → Idle; console history preserved | PASS (production proof) |

## Unautomatable GUI evidence (honest)

| Item | Why | Workaround / evidence |
|---|---|---|
| Pause on `workflow-console` | Fixture is a one-line program that exits before a stable `Running` window is observable in automation | `PauseAsync_FromRunning_TransitionsToStopped` uses the fake adapter; manual pause smoke deferred to M7 |
| Call Stack frame list | M4 shell only — frame retrieval/selection is M5 | `DebugPanelViewModelTests.CallStack_ShowsDeferredM5ShellState` |
| Bottom-panel visual layout | No headless Avalonia composition assertion in M4 | `DebugPanel` + `MainWindow` wiring compile; manual layout check in M7 |

## M4 acceptance checklist

- [x] `debug.pause` registered with no default gesture
- [x] `debug.stop` / `Shift+F5`, `debug.stepOver` / `F10`, `debug.stepInto` / `F11`, `debug.stepOut` / `Shift+F11`
- [x] `F5` start/continue and `F9` breakpoint gestures unchanged; each debug gesture resolves exactly once
- [x] Pause: Running only; Stop: Starting/Running/Stopped; Step: Stopped with thread only
- [x] Commands unavailable during Stopping/Failed/Idle/Unavailable and after terminal session end
- [x] DAP `pause`, `next`, `stepIn`, `stepOut`, and `disconnect` seam extended; protocol errors projected to console without UI crash
- [x] `BottomPanelMode.Debug` with Debug Console + Call Stack sections; console isolated from Terminal/Output
- [x] Console history preserved after session end; startup/adapter/request/disconnect failures surfaced
- [x] Call Stack shows truthful deferred M5 state (no fake frames)
- [x] MVVM boundaries preserved; no settings/breakpoint/M5 scope changes