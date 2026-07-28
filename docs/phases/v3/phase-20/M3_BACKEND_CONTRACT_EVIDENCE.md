# Phase 20 M3 — Backend Contract Evidence

## Publication

| Item | Value |
|------|-------|
| Milestone | M3 — ACP backend/session adapter, context mapping, event normalization, and six-fact capability mapping |
| Published commit | `2d90604991dd9b87cb6e22a2c8c9a7b771504de6` |
| Depends on | M2 at `880b4524c9c53190687aee0cc10843900191b8ce` |
| Production surfaces | `src/Features/Agents/Application/Acp/`, `src/Features/Agents/Infrastructure/Acp/AcpAgentBackend.cs`, additive Agents Domain activity/capability types, narrowly additive `AgentSessionService` backend-activity normalization |
| Test surfaces | `tests/Zaide.Tests/Features/Agents/Acp/Backend/`, Phase 20 context/capability/backend tests, Phase 18 context-bypass ratchet extension |

## ACP backend/session ownership

- `AcpAgentBackend` implements `IAgentBackend` under `AgentBackendIds.Acp` (`backend:acp`).
- `AcpAgentSessionAdapter` owns Zaide-session to ACP-session binding via `AcpAgentSessionBinding`.
- Zaide-owned identifiers remain authoritative: `AgentSessionId`, `ExecutionRunId`, `ConversationId`, and `ActorId`.
- ACP `sessionId`, tool-call IDs, and `agentInfo` are adapter correlation/evidence only.
- Rebinding a different `agentInfo` to an existing Zaide session fails closed with transport failure.

## Fake transport contract and provenance

- M3 tests use `AcpFakeSessionClient` / `AcpFakeSessionScript` in `tests/Zaide.Tests/Features/Agents/Acp/Backend/`.
- The fake client implements `IAcpSessionClient` without process launch, credentials, network, or real ACP candidates.
- Production transport remains M2 `AcpProtocolSession` / `AcpStdioProcessHost`; M3 wires it only through `AcpProtocolSessionClient` for future milestones.

## Request/response and session correlation

- One admitted Zaide run maps to one ACP prompt turn through `AcpAgentSessionAdapter`.
- First run for a Zaide session records expected `agentInfo` and creates the ACP `session/new` mapping.
- Later runs on the same Zaide session reuse the bound ACP session id when the adapter instance persists.
- Prompt content is built from the user message plus optional Phase 18 manifest text blocks.

## ACP `session/update` to Zaide response/event mapping

| ACP update | Zaide treatment |
|------------|-----------------|
| `agent_message_chunk` | Buffered only; assistant completion emitted after terminal prompt response |
| `agent_thought_chunk` | Bounded validation only; never projected as assistant answer |
| `user_message_chunk` | Ignored for projection in M3 profile |
| `tool_call` / `tool_call_update` | `AgentBackendEventKind.ActivityReported` → `AgentEventKind.BackendActivityReported` with `BackendExecutedAndReported` evidence |
| `plan` / `usage_update` | Separate backend-reported activity events |
| session control updates | Parsed as bounded `SessionControlUpdate` activity |
| terminal prompt stop reason | `MessageCompleted` or mapped `FailureObserved` |

## Completion, cancellation, failure, timeout, and indeterminate outcomes

| Outcome | Mapping |
|---------|---------|
| `end_turn` with assistant text | `AgentBackendEventKind.MessageCompleted` |
| `cancelled` stop reason | `AgentFailureKind.Cancellation` |
| `max_tokens` / `max_turn_requests` / `refusal` | `AgentFailureKind.Execution` |
| caller cancellation | `AgentFailureKind.Cancellation` after best-effort `session/cancel` |
| ACP lifecycle timeout / process exit / protocol failure | `Timeout`, `Transport`, or `Indeterminate` via `AcpProcessLifecycleException` / `AcpProtocolException` |
| empty assistant completion | `AgentFailureKind.Execution` |
| unsupported stop reason | `AgentFailureKind.Indeterminate` |

## Phase 18 manifest consumption and exclusion/redaction behavior

- Context is consumed only through `AcpContextManifestEncoder` from `AgentBackendExecutionContext.ContextManifest`.
- `ProcessingFailed` items are excluded from prompt encoding.
- Hard and soft exclusion decisions are recorded in the encoded context text.
- No direct editor, workspace, terminal, or cross-feature infrastructure reads occur in ACP application code.

## Six-fact capability snapshots and version changes

`AcpCapabilityRows` defines truthful rows for:

- `MessageCompletion`
- `Cancellation`
- `Tools`
- `Permissions`
- `IdeContext`
- `Streaming`
- `Resume`
- `UsageReporting`
- `RawTrace`

Unsupported capabilities remain `NotSupported` or unavailable rather than advertised as Zaide-mediated. Observed `usage_update` increments the capability snapshot version through `AgentBackendEventKind.CapabilitySnapshotChanged`.

## Evidence levels for backend-reported versus Zaide-mediated activity

- Assistant completion after protocol termination: `BackendExecutedAndReported`.
- Tool/plan/usage activity: `BackendExecutedAndReported` via `BackendActivityReported` events.
- Phase 17 broker-mediated work remains unavailable for ACP in M3; permissions capability is explicitly not currently usable.

## Identity and no-fallback behavior

- ACP is an independent sibling backend id (`backend:acp`).
- No Native Harness reference, fallback, or implicit backend selection exists in ACP code.
- Actor/backend/runtime identity separation is preserved; mismatched ACP `agentInfo` fails closed.

## Residual limitations carried to M4/M5

- No production DI registration or coordinator backend selection.
- No client filesystem/terminal advertisement or Phase 17 broker bridge.
- No Townhall projection changes beyond existing normalized event stream consumption.
- No authentication UI, explicit per-Actor binding product surface, or real ACP candidate execution.
- No persistent resume/reconnect, raw trace, or Phase 21 continuity behavior.

## Verification gates

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase20Backend|FullyQualifiedName~Phase20Context|FullyQualifiedName~Phase20Capabilities"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
git diff --check
```
