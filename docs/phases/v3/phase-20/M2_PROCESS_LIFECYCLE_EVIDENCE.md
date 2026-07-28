# Phase 20 M2 — Process Lifecycle Evidence

## Publication

| Item | Value |
|------|-------|
| Milestone | M2 — bounded stdio process and JSON-RPC lifecycle |
| Depends on | M1 at `314076ebc8dcf2c9910baecc5ef96c461910cb1b` |
| Production surfaces | `src/Features/Agents/Infrastructure/Acp/` process host, bounded stdio, lifecycle exceptions; `src/Features/Agents/Contracts/` launch abstractions; `src/App/Composition/ApplicationShutdown.cs` shutdown hook |
| Test surfaces | `tests/Zaide.Tests/Features/Agents/Acp/Transport/` |
| Fake fixture | `tests/fixtures/acp-fake-agent/` |

## Process ownership and disposal

- `AcpStdioProcessHost` owns exactly one `IAcpChildProcess`, one `AcpProtocolSession`, and one `AcpBoundedStderrReader`.
- `AcpProcessHostShutdownRegistry` registers active hosts and is invoked from `ApplicationShutdown.Run` for exactly-once teardown.
- `DisposeAsync` disposes stderr reader, protocol session, and terminates the owned process tree through `AcpProcessTreeTerminator`.
- `Phase20ProcessLifecycleOwnershipTests` proves disposal and shutdown-registry cleanup terminate the owned root PID.

## Stdin/stdout/stderr boundaries

- Stdout is protocol-only input to `AcpProtocolConnection` via `AcpNewlineFrameReader` with M1 frame limits preserved.
- Stdin is protocol-only output from `AcpNewlineFrameWriter`.
- Stderr is diagnostic-only via `AcpBoundedStderrReader`; it is never parsed as JSON-RPC input.
- `Phase20TransportStderrBoundaryTests` proves stderr redaction and byte caps.

## Frame and output limits

| Limit | Value | Enforcement |
|-------|-------|-------------|
| Max stdout frame bytes | 4 MiB (`AcpProtocolLimits.MaxFrameBytes`) | M1 reader/writer |
| Max stderr bytes | 64 KiB (`AcpProcessLifecycleLimits.MaxStderrBytes`) | `AcpBoundedStderrReader` |
| Max stderr line bytes | 16 KiB (`AcpProcessLifecycleLimits.MaxStderrLineBytes`) | `AcpBoundedStderrReader` |
| Initialize timeout | 30 s default (`AcpProcessLifecycleLimits.InitializeTimeout`) | `AcpStdioProcessHost` |
| Session operation timeout | 5 min default (`AcpProcessLifecycleLimits.SessionOperationTimeout`) | `AcpStdioProcessHost` |
| Process-tree cleanup timeout | 5 s (`AcpProcessLifecycleLimits.ProcessTreeCleanupTimeout`) | `AcpProcessTreeTerminator` |

Malformed stdout frames remain fail-closed at the codec/framing layer without unbounded buffering.

## Request correlation and duplicate/late responses

- M1 `AcpProtocolConnection` pending map correlates JSON-RPC responses by request id.
- M2 increments `LateResponseCount` when a response arrives after its pending entry is gone.
- `Phase20TransportTimeoutCancellationTests.DuplicateResponse_IsCountedAsLateCompletion` uses fake-agent mode `duplicate-response`.

## Timeout and cancellation behavior

| Failure | Type | Proof |
|---------|------|-------|
| Caller cancellation | `AcpProcessLifecycleFailureKind.Cancellation` | `CancelledInitialize_SurfacesCancellationFailure` |
| Operation timeout | `AcpProcessLifecycleFailureKind.Timeout` | `SlowInitialize_SurfacesTimeoutFailure` |
| Protocol parse/contract failure | `AcpProcessLifecycleFailureKind.ProtocolFailure` | environment policy rejection test |
| Child exit before completion | `AcpProcessLifecycleFailureKind.ProcessExit` | `ExitImmediateFixture_SurfacesProcessExitFailure` |
| Late duplicate response | `LateResponseCount` increment | duplicate-response fixture |

## Process exit and malformed-frame behavior

- Immediate child exit (`exit-immediate` fixture) surfaces `ProcessExit` on the next host operation.
- Malformed stdout (`malformed-stdout` fixture) is ignored by the read loop per M1 fail-closed framing rules; the host does not retry or restart automatically.
- No automatic restart or silent fallback occurs on any terminal path.

## Process-tree cleanup evidence

- `AcpProcessTreeTerminator` calls `Process.Kill(entireProcessTree: true)` with bounded wait.
- Fake-agent mode `spawn-child` starts a `sleep` descendant; `Host_DisposeAsync_TerminatesOwnedProcessTree` verifies the owned tree is gone after disposal.

## Fake-process commands and fixture provenance

Repository-owned fixture: `tests/fixtures/acp-fake-agent/Program.cs`

| Mode | Behavior |
|------|----------|
| `healthy` | Responds to `initialize`, `session/new`, `session/prompt` |
| `slow-init` | Delays initialize response for timeout proof |
| `slow-request` | Delays all request responses |
| `exit-immediate` | Exits before protocol handshake |
| `malformed-stdout` | Writes non-JSON stdout |
| `duplicate-response` | Emits each JSON-RPC response twice |
| `spawn-child` | Spawns `sleep 600` descendant |
| `stderr-secret` | Writes `api_key=...` to stderr |
| `hang` | Never responds |
| `oversized-line` | Writes a >4 MiB stdout line |

Tests build the fixture with `dotnet build tests/fixtures/acp-fake-agent/AcpFakeAgent.csproj -o tests/fixtures/acp-fake-agent/bin/TransportFixture/net10.0` via `AcpFakeAgentFixture`.

Launch uses absolute `dotnet` path plus DLL argument vector; no shell interpolation. Environment is explicit allowlist only (`AcpProcessEnvironmentPolicy`).

## Residual limitations carried to M3+

- No `AcpAgentBackend`, session adapter, context mapping, capability mapping, or event normalization.
- No production DI registration or per-Actor backend binding.
- No client filesystem/terminal advertisement, broker bridge, Townhall projection, or authentication UI.
- No real ACP candidate execution, credentials, network transport, or automatic restart.
- `AcpProcessLifecycleFailureKind.IndeterminateLateCompletion` is recorded in the failure taxonomy; duplicate/late responses are counted but not yet projected into backend session outcomes (M3).

## Verification gates

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase20Transport|FullyQualifiedName~Phase20ProcessLifecycle"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
git diff --check
```
