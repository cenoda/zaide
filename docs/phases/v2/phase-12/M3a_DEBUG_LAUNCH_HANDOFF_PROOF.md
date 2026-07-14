# Phase 12 M3a: Debug Launch Handoff Proof

## Purpose and result

This records the production Linux proof for Phase 12 M3a: the shared
`IProjectOperationGate`, workflow-owned build-before-debug handoff, MSBuild
`TargetPath` resolution, and F5-equivalent launch through
`ProjectDebugLaunchService` and `debug.startOrContinue`.

**Result: PASS.** A fresh workflow-console build completed through
`IProjectWorkflowService`, `TargetPath` resolved to one absolute existing
`.dll`, NetCoreDbg launched via `IDebugSessionService`, the session reached
`Stopped` at entry, and `StopAsync` returned to `Idle`.

## Environment and adapter provenance

| Item | Observed value |
|---|---|
| Date | 2026-07-14 |
| Host | Linux `arch`, x64 |
| SDK | .NET SDK 10.0.109 |
| Adapter | NetCoreDbg 3.2.0-1092 release archive, reported `NET Core debugger 3.2.0-1 (9744e1f, Release)` |
| Adapter path | `/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg` |
| Adapter argv | `netcoredbg --interpreter=vscode` |
| Fixture project | `tests/fixtures/workflow-console/WorkflowConsole.csproj` |
| Fixture working directory | `tests/fixtures/workflow-console/` |
| Build command | `dotnet build tests/fixtures/workflow-console/WorkflowConsole.csproj` |
| TargetPath query | `dotnet msbuild tests/fixtures/workflow-console/WorkflowConsole.csproj -getProperty:TargetPath -nologo` |
| Resolved TargetPath | `tests/fixtures/workflow-console/bin/Debug/net10.0/WorkflowConsole.dll` |

The adapter remains external proof material. M3a does not bundle, install, or
auto-download it. Locator precedence is `ZAIDE_NETCOREDBG_PATH`, then
`netcoredbg` on `PATH`.

## Automated proof gate

```bash
dotnet test Zaide.slnx --no-build \
  --filter "FullyQualifiedName~M3aDebugLaunchProofTests|FullyQualifiedName~ProjectOperationGateTests|FullyQualifiedName~ProjectDebugTargetResolverTests|FullyQualifiedName~ProjectDebugLaunchServiceTests|FullyQualifiedName~DebugStartOrContinueCommandTests"
```

Observed on 2026-07-14:

- `M3aDebugLaunchProofTests.ProductionHandoff_BuildResolveTargetPathAndLaunch_ThenStop` — PASS (915 ms)
- Focused M3a unit tests — PASS

## Observed handoff evidence

| Step | Observed result | Meaning |
|---|---|---|
| Gate acquire (`DebugStart`) | success | Debug handoff lease held across build → resolve → launch. |
| Workflow build | exit code `0` | Selected `.csproj` built through existing workflow ownership. |
| `TargetPath` query | one absolute `.dll` | No `bin/` scanning; MSBuild property query only. |
| DAP `launch` | successful | Resolved DLL and project directory accepted. |
| `configurationDone` | successful | Launch sequence completed. |
| `stopped` at entry | `DebugSessionState.Stopped` | F5-equivalent start reached a truthful stopped snapshot. |
| Handoff lease release | `IsDebugHandoffActive == false` | No admission gap after launch. |
| `StopAsync` | `DebugSessionState.Idle` | Stop completed cleanly. |

## M3a acceptance checklist

- [x] `IProjectOperationGate` shared by workflow and debug start
- [x] Mutual `Workflow busy` / `Debug session active` rejection
- [x] Debug handoff lease held across build → `TargetPath` → adapter launch
- [x] `IProjectDebugTargetResolver` uses `dotnet msbuild -getProperty:TargetPath` only
- [x] `debug.startOrContinue` registered with default `F5`; no gesture conflict
- [x] Deterministic fake/seam tests for gate, resolver, launch service, and F5 dispatch
- [x] Production Linux handoff proof recorded
- [x] `Dispose_WhileFakeRunnerEmitting` deadlock regression fixed (split admission/critical-section mutexes)