# Phase 12 M5: Stack, Variables, and Current Location Proof

## Purpose and result

This records Linux validation for Phase 12 M5: stopped-state DAP inspection
(threads → stackTrace → scopes → variables), Debug panel Variables section,
selected-frame current-execution-location projection, and lifecycle clearing on
continue/end.

**Result: PASS.** Focused M5 unit tests pass, production proof shows breakpoint
stop → stack/frame → scope → variable → continue through the production
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
  --filter "FullyQualifiedName~DebugStackProjection|FullyQualifiedName~DebugCurrentLocation|FullyQualifiedName~M5Debug|FullyQualifiedName~DebugPanel"
```

Observed on 2026-07-14:

- `DebugStackProjectionTests` — PASS (stopped load, selection flow, empty/error, stale generation/selection rejection, clearing, panel composition)
- `DebugCurrentLocationViewModelTests` — PASS (source open/marker, unavailable source, continue clear)
- `DebugPanelViewModelTests` — PASS (console history preserved; stack/variables projection in Debug mode)
- `M5DebugStackProofTests.ProductionProof_BreakpointStopStackScopeVariableContinueStop` — PASS (~0.5 s)

Full regression (same session):

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build
git diff --check
```

- Build: 0 errors, 1 pre-existing warning (`ProjectDebugTargetResolverTests`)
- Tests: **2029 passed**, 0 failed, 0 skipped (~32 s)
- `git diff --check`: clean

## Operator smoke checklist (workflow-console)

| Step | Gesture / action | Expected visual/behavior | Result |
|---|---|---|---|
| Stop at breakpoint | persisted `Program.cs:1` | Debug panel shows call stack frames | PASS (production + projection tests) |
| Inspect variables | select frame/scope | First-level variables list in Variables section | PASS (production + projection tests) |
| Current location | select top frame | Editor opens `Program.cs` with instruction-pointer marker | PASS (location VM tests; marker wiring in `EditorView`) |
| Continue | `F5` while stopped | Stack/variables/current location clear | PASS (production proof + projection clear tests) |
| Stop debugging | `Shift+F5` while stopped | Session ends; console history preserved | PASS (M4 production proof retained) |

## Unautomatable GUI evidence (honest)

| Item | Why | Workaround / evidence |
|---|---|---|
| Instruction-pointer margin paint | No headless Avalonia margin render assertion | `InstructionPointerMargin` + `EditorView` wiring compile; manual gutter check deferred to M7 |
| Three-column Debug panel layout | No composition pixel test in M5 | `DebugPanel` C# layout builds Console + Call Stack + Variables; manual layout check in M7 |
| Multi-thread picker | `workflow-console` exposes one thread in automation | Thread list visibility covered by projection tests with multi-thread mock JSON |

## M5 acceptance checklist

- [x] On every valid `Stopped` snapshot: `threads` → `stackTrace(selectedThreadId)` with deterministic initial thread/frame selection and user override
- [x] Selected frame requests `scopes`; selected scope requests first-level `variables` only
- [x] `BottomPanelMode.Debug` retains Debug Console + Call Stack and adds Variables with loading/empty/unavailable/error states
- [x] No seeded fake stack/frame/scope/variable data
- [x] Selected-frame source path + one-based line opens on-disk document and projects distinct instruction-pointer marker separate from breakpoints
- [x] Missing/non-local/unavailable source reported without crash or invented navigation
- [x] Stack/scope/variable/current-location data clears on continue, session end, failure, disconnect, context change, disposal, and generation/selection change; stale replies ignored
- [x] DAP requests remain state-gated with established timeout policy; no DAP logic in views
- [x] MVVM boundaries preserved; no settings/breakpoint/watch/evaluate/M6 scope creep