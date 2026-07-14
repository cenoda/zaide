# Phase 12 M1: DAP Session Lifecycle Proof

## Purpose and result

This records the production Linux proof for Phase 12 M1: the UI-independent
DAP adapter locator, Content-Length transport, session factory/service, DI, and
shutdown ordering. The proof uses the production `NetCoreDbgAdapterSession` and
`DebugSessionService`, not the M0 disposable frame client.

**Result: PASS.** NetCoreDbg 3.2.0-1092 accepted VS Code DAP envelopes over
Content-Length stdio, completed `initialize → launch → setBreakpoints →
configurationDone`, emitted `stopped`, answered `threads`, `stackTrace`, and
`scopes`, accepted `continue`, and completed terminating `disconnect`.

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
| Fixture source | `tests/fixtures/workflow-console/Program.cs`, line 1 |
| Target program | `tests/fixtures/workflow-console/bin/Debug/net10.0/WorkflowConsole.dll` |
| Target working directory | `tests/fixtures/workflow-console/` |

The adapter remains external proof material. M1 does not bundle, install, or
auto-download it. Locator precedence is `ZAIDE_NETCOREDBG_PATH`, then
`netcoredbg` on `PATH`.

## Transport note

NetCoreDbg's `--interpreter=vscode` mode requires VS Code DAP JSON envelopes
(`seq`, `type`, `command`/`event`, `arguments`/`body`) over Content-Length
stdio. JSON-RPC 2.0 envelopes are rejected. M1 therefore uses
`DapContentLengthTransport`, which preserves the same Content-Length framing
semantics exercised by M0 and by Phase 10's `HeaderDelimitedMessageHandler`,
while speaking the DAP wire format NetCoreDbg accepts.

## Automated proof gate

```bash
dotnet test Zaide.slnx --no-build \
  --filter "FullyQualifiedName~NetCoreDbgLifecycleProofTests|FullyQualifiedName~NetCoreDbgAdapterSessionDirectTests"
```

Observed on 2026-07-14:

- `NetCoreDbgAdapterSessionDirectTests.DirectSession_InitializeLaunchAndConfigurationDone_Succeeds` — PASS
- `NetCoreDbgLifecycleProofTests.ProductionSession_RunsFullLinuxLifecycle_ThroughDebugSessionService` — PASS

## Observed lifecycle evidence

| Exchange | Observed result | Meaning |
|---|---|---|
| `initialize` | successful body with capabilities | Adapter accepted the client. |
| `launch` | successful | Absolute DLL and fixture working directory accepted. |
| `setBreakpoints` | successful | Initial source breakpoint submitted before configuration. |
| `configurationDone` | successful | Configuration boundary completed. |
| `stopped` event | `reason=entry` | Initial asynchronous stop reached `DebugSessionState.Stopped`. |
| `threads` | count `1` | Stopped-state thread request succeeded. |
| `stackTrace` | frames `1` | Stack retrieval succeeded for returned thread. |
| `scopes` | count `1` | Scope retrieval succeeded for top frame. |
| `continue` | successful + `continued` event | Execution-control request succeeded. |
| `disconnect` | successful; session returned to `Idle` | Terminating disconnect completed. |

## M1 acceptance checklist

- [x] Locator precedence (`ZAIDE_NETCOREDBG_PATH`, then PATH) implemented and tested
- [x] Production Content-Length DAP transport with pre-listening event registration
- [x] Locked timeouts: initialize 15s; launch/configuration 15s; ordinary 10s; disconnect 5s
- [x] One-session state/generation ownership with structured failures and disposal
- [x] DI registrations and debug teardown before workflow teardown on app exit
- [x] Deterministic fake adapter/session tests for ordering, events, errors, timeouts, generation, locator, DI, and disposal
- [x] Production Linux lifecycle proof recorded
