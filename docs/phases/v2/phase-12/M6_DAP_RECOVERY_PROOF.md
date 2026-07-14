# Phase 12 M6: DAP Error and Recovery Hardening Proof

## Purpose and result

This records Linux validation for Phase 12 M6: truthful recovery for DAP and
pre-launch failure paths, consistent terminal recovery contract, session-only
breakpoint verification projection, gate/process cleanup, and stale-generation
immunity.

**Result: PASS.** Focused M6 unit/regression tests pass, production NetCoreDbg
recovery proof passes where safely reproducible, and full regression gates are
green.

## Recovery contract (locked)

When a debug session reaches a terminal recovery state (`Failed`, `Idle`, or
`Unavailable` as appropriate):

| Requirement | Behavior |
|---|---|
| Terminal snapshot + diagnostics | Failure kind/message published; Debug Console retains prior diagnostics and appends `[error]` lines |
| Live inspection data | StopInfo, thread/frame/scope/variable projection, and instruction pointer clear immediately (ViewModels react to non-`Stopped` snapshots) |
| Operation-gate leases | `ProjectDebugLaunchService` always disposes the handoff lease in `finally` |
| Adapter process | Disconnect (bounded by `DebugSessionTimeoutPolicy.Disconnect`) then force-kill; no orphan process remains |
| F5 usability | `IsStartAllowed` includes `Idle` and `Failed`; pre-launch failures publish `Failed` without leaving an active generation |
| Stale generation | `Stop`, context change, and session-end failures bump generation; late events/replies from disposed sessions are ignored |

## Failure-path matrix

| Failure path | Evidence | Adapter |
|---|---|---|
| Missing / unexecutable adapter | `StartLaunchAsync_WhenAdapterMissing_ReturnsAdapterUnavailable`; `ProductionProof_MissingAdapter_IsRecoverableFailedState` | Production locator + missing path |
| Build failure | `StartDebuggingAsync_BuildFailure_ReturnsBuildFailedAndReleasesHandoff` + `ReportPreLaunchFailure` | Fake workflow |
| Target-resolution failure | `StartDebuggingAsync_TargetResolutionFailure_ReturnsUnsupportedLaunchTarget` | Fake resolver |
| Launch / configuration failure or timeout | `StartLaunchAsync_LaunchException_*`; `StartLaunchAsync_WhenInitializeTimesOut_*`; `StartLaunchAsync_WhenStoppedNeverArrives_*` | Fake adapter |
| Rejected / unverified breakpoint response | `SetBreakpoints_ProjectsVerifiedPendingRejectedOutcomes`; `DapBreakpointVerificationParserTests`; projection overlay tests | Fake adapter (+ parser) |
| Malformed / failed ordinary request or timeout | `ContinueAsync_RequestTimeout_*`; `StepOverAsync_ProtocolError_*` | Fake adapter |
| Adapter crash / disconnect / terminated / exited | `AdapterProcessExit_*`; `TerminatedEvent_*`; `AdapterExited_ClearsLiveData_*` | Fake adapter |
| Project-context change while active | `ContextChangeWhileActive_*`; late-generation ignore tests | Fake adapter |
| Rapid start/stop, stop during startup, start after failure | `StopDuringStartup_*`; `StartAfterFailure_*`; `LateEventAfterRecovery_*` | Fake adapter |
| Gate release + process cleanup | Launch handoff tests; production stop/restart proof | Both |
| Clear live projections, retain diagnostics | Service recovery tests + existing M5 stack/location clear tests on non-`Stopped` | Both |

### Fake-adapter-only (not forced on NetCoreDbg)

These paths remain deterministic fake-adapter coverage only:

- Ordinary-request timeout while stopped (`Continue` delayed beyond policy).
- Protocol error on step (`next` throws).
- Explicit rejected breakpoint body with a non-pending adapter message.
- Malformed protocol envelopes (covered by transport/service failure mapping to
  `StartupFailed` / `ProtocolFailed` via thrown exceptions).
- Adapter process crash mid-session without a graceful `terminated` event
  (simulated `ProcessExited`).

NetCoreDbg production proof covers: successful launch → breakpoint verification
row (Pending or Verified) → Stop cleanup (PID gone, live data cleared) →
restart → Stop; and missing-adapter `Failed` recovery.

## Environment and adapter provenance

| Item | Observed value |
|---|---|
| Date | 2026-07-14 |
| Host | Linux x64 |
| SDK | .NET SDK 10.0.x |
| Adapter | NetCoreDbg 3.2.0-1092 (`NET Core debugger 3.2.0-1`) |
| Adapter path | `/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg` |
| Adapter argv | `netcoredbg --interpreter=vscode` |
| Fixture project | `tests/fixtures/workflow-console/WorkflowConsole.csproj` |
| Fixture source | `tests/fixtures/workflow-console/Program.cs` |
| Breakpoint line | 1 (one-based) |

## Automated proof gates

```bash
dotnet test Zaide.slnx --no-build \
  --filter "FullyQualifiedName~DebugSessionServiceTests|FullyQualifiedName~M6Debug|FullyQualifiedName~DapBreakpoint|FullyQualifiedName~EditorBreakpointProjection|FullyQualifiedName~ProjectDebugLaunch"
```

Full regression (same session):

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build
git diff --check
```

Observed on 2026-07-14:

- Build: 0 errors, 0 warnings (incremental clean rebuild)
- Tests: **2043 passed**, 0 failed, 0 skipped (~32 s)
- `git diff --check`: clean

## Breakpoint verification projection

- Session-only rows on `DebugSessionSnapshot.BreakpointVerifications`.
- Mapping: `verified=true` → **Verified**; `verified=false` without message or
  with a message containing `"pending"` → **Pending**; other messages →
  **Rejected**.
- Editor margin colors distinguish verified (red fill), pending (amber),
  rejected (muted fill + red stroke). Disabled intent remains hollow/muted.
- Persisted `PersistedBreakpoint` intent is unchanged (no settings schema
  change).

## M6 acceptance checklist

- [x] Missing adapter, build failure, target failure, launch/config timeout,
      rejected breakpoints, protocol/request failures, crash/disconnect,
      context change, rapid start/stop, start-after-failure are truthful and
      recoverable
- [x] Recovery contract: diagnostics retained, live data cleared, gate released,
      process cleaned, F5 usable, stale generation immune
- [x] Breakpoint verification outcomes distinguishable without new breakpoint
      types or settings schema
- [x] Timeouts bounded by `DebugSessionTimeoutPolicy`
- [x] M1–M5 ownership preserved (services own DAP/processes; VMs project; views
      render)
- [x] Focused regression tests + production NetCoreDbg recovery proof
- [x] No M7 closeout / Phase 13 claim

## Known limitations

- NetCoreDbg may report initial `setBreakpoints` as pending until the debuggee
  is configured; that is projected as **Pending**, not Verified.
- Ordinary-request timeout / step protocol failure and forced rejected
  breakpoint bodies are fake-adapter-only.
- Full keyboard/accessibility and broad manual GUI evidence remain M7.
