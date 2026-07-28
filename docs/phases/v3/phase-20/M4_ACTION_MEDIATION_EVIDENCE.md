# Phase 20 M4 — Action Mediation Evidence

## Publication

| Item | Value |
|------|-------|
| Milestone | M4 — Zaide-mediated client filesystem actions and separate ACP permission boundary |
| Published commit | `63880c53c2317a4e4d85ade2088c96764c510b6f` |
| Depends on | M3 at `2d90604991dd9b87cb6e22a2c8c9a7b771504de6` with publication-record correction `04831f1c` |
| Production surfaces | `src/Features/Agents/Application/Acp/` action bridge, minimal Infrastructure/Contracts wiring for inbound handler and capability advertisement |
| Test surfaces | `tests/Zaide.Tests/Features/Agents/Acp/Actions/`, Phase 17 proposal/permission regression tests, architecture inventory ratchets |

## ACP-to-broker action mapping

| ACP method | Broker payload | Notes |
|------------|----------------|-------|
| `fs/read_text_file` | `AgentReadFileActionPayload` | Absolute path converted to workspace-relative; bounded full-file read only (no line/limit slicing in M4) |
| `fs/write_text_file` (absent target) | `AgentCreateFileActionPayload` | Preceded by authoritative broker read that confirms absence |
| `fs/write_text_file` (existing target) | `AgentReplaceFileActionPayload` | Base revision taken from authoritative broker read immediately before compose |
| `session/request_permission` | _none_ | Handled by `AcpClientPermissionBridge`; never consumes `AgentPermissionDecision` |
| `terminal/*` | _rejected_ | `terminal: false`; fallback router returns method-not-found |

## Capability advertisement before and after bridge availability

| State | `fs.readTextFile` | `fs.writeTextFile` | `terminal` |
|-------|-------------------|--------------------|------------|
| M1–M3 / bridge unavailable | `false` | `false` | `false` |
| M4 bridge active (`AcpActionCapableAgentBackend` + real broker) | `true` | `true` | `false` |

`AcpAgentBackend` (without action bridge) continues to advertise the M1 profile. `AcpActionCapableAgentBackend` implements `IAgentActionRequestCapableBackend` and enables filesystem advertisement only when a real run-scoped broker is supplied.

## Permission-boundary distinction

- ACP `session/request_permission` returns an ACP option outcome (`selected` / `cancelled`) through `IAcpPermissionChoiceSource`.
- Phase 17 `AgentPermissionDecision.TryConsume()` is never invoked by the ACP permission bridge.
- Broker-mediated filesystem mutations still use the existing Phase 17 permission lifecycle when policy requires user decision.
- ACP permission choice and Zaide broker authorization are independently testable and independently fail-closed.

## Stale-base and final `TryConsume()` ordering

M4 preserves Phase 17 ordering without modification:

1. Proposal composed at admission.
2. Permission decision published when required.
3. Authoritative target/base re-read immediately before `AgentPermissionDecision.TryConsume()`.
4. Stale target/base returns `Revoked/StaleBaseRevision` without consuming a `Published` decision.

`Phase20Permission_StaleBaseThroughBridge_DoesNotConsumePublishedDecision` proves a stale create through the ACP write bridge leaves the published decision unconsumed.

## Denial, revocation, cancellation, and late-completion outcomes

| Scenario | ACP JSON-RPC mapping | Broker result preserved |
|----------|----------------------|-------------------------|
| Path outside workspace / traversal | `InvalidParams` | not reached |
| Malformed params | `InvalidParams` | not reached |
| Read not found | `ResourceNotFound` | `Failed/ExecutionFailed` |
| Permission denied | `InternalError` (bounded summary) | `Denied/PermissionDenied` |
| Stale base / revoked | `InternalError` (bounded summary) | `Revoked/StaleBaseRevision` |
| Caller cancellation | `RequestCancelled` | `Cancelled` |
| Broker revoked | `InternalError` | `Denied/BrokerRevoked` |

Late ACP inbound completion after prompt cancellation is owned by the transport read loop; M4 bridge handlers honor the linked cancellation token and do not perform direct I/O.

## Evidence-level mapping

| Activity source | Evidence level |
|-----------------|----------------|
| ACP `tool_call` / `tool_call_update` session updates | `BackendExecutedAndReported` |
| ACP `plan` / `usage_update` session updates | `BackendExecutedAndReported` |
| Broker-successful `fs/read_text_file` / `fs/write_text_file` | `ZaideMediated` / `ZaideExecuted` via Phase 17 action facts |
| Direct external-agent process filesystem/process work | `BackendExecutedAndReported`, `ExternallyObserved`, or `Unobservable` (not claimed as Zaide-mediated) |

## Test fixtures and commands

Production bridge types:

- `AcpClientActionBridge`
- `AcpClientPermissionBridge`
- `AcpActionCapableAgentBackend`
- `AcpWorkspaceAbsolutePathConverter`
- `AcpClientCapabilityProfiles`

Test fixtures:

- `tests/Zaide.Tests/Features/Agents/Acp/Actions/Phase20ActionBridgeTests.cs`
- `tests/Zaide.Tests/Features/Agents/Acp/Actions/Phase20PermissionTests.cs`
- `tests/Zaide.Tests/Features/Agents/Acp/Actions/Phase20ActionBridgeBypassTests.cs`
- `tests/Zaide.Tests/Features/Agents/Acp/Backend/AcpFakeSessionClient.cs` (`AcpFakeInboundRequest` scripted inbound calls)

Verification commands:

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase20ActionBridge|FullyQualifiedName~Phase20Permission"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase17ProposalBroker|FullyQualifiedName~Phase17PermissionLifecycle"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
git diff --check
```

## Residual limitations carried to M5

- No production DI registration or coordinator backend selection.
- No explicit per-Actor identity/backend binding UI.
- No Townhall projection changes for mediated filesystem actions.
- No authentication UI or real ACP candidate execution.
- `IAcpPermissionChoiceSource` uses deterministic fail-closed test/default behavior until M5 user-facing permission UI exists.
- Terminal capability remains unadvertised and unsupported.
- No persistent resume/reconnect, raw trace, or Phase 21 continuity behavior.
