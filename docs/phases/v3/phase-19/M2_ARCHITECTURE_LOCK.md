# Phase 19 M2 — Architecture Lock

**Milestone:** M2 — harness contracts and architecture lock  
**Lock date:** 2026-07-27  
**Status:** Complete (read-only audit gate; M3 not started)  
**Depends on:** M1 research/provenance complete with limitation (full-corpus benchmark
gate retired; no architecture winner selected)

This document locks the Native Harness internal contracts, resolved M2-owned open
decisions, six-fact capability rows, prior-conversation history seam, event-surface
decision, and provider/protocol boundary. M2 introduces contract/domain types and
tests only. No backend implementation, production DI registration, or M3 work is
authorized by this lock.

---

## 1. Trust boundary (unchanged ownership)

```text
Phase 15 session/run/event surface
  -> run-scoped IAgentActionBroker (Phase 17) for all file/command operations
  -> AgentContextManifest (Phase 18) consumed into system prompt / tool context
  -> Native Harness private turn loop (in-run history, model turns, tool parsing)
  -> AgentBackendEvent (MessageCompleted / FailureObserved only)
  -> broker action facts -> AgentEvent -> AgentConversationEventProjection
  -> truthful AgentCapabilitySnapshot (six-fact rows)
```

**Hard rules preserved from Phase 15/17/18:**

| Boundary | Rule |
|----------|------|
| File/command operations | All five `AgentActionKind` values flow through `IAgentActionBroker.RequestAsync`; no direct workspace IO or process execution in the harness |
| Model/provider transport | Backend-owned HTTP; does not flow through the action broker (P19-D16) |
| Backend observation | `AgentBackendEvent` carries text completion and failure only |
| Broker facts | Normalized `AgentEvent` with `AgentActionFactPayload` carries tool/permission activity |
| Legacy backend | Remains context-inert and action-inert; ratchet-enforced |
| Persistence | No run-scoped loop history, replay selection, or raw traces are persisted (P19-D11) |

---

## 2. Resolved M2-owned open decisions

| Decision | M2 lock |
|----------|---------|
| Backend selection model | Phase 19 M4 registers **one** production `IAgentBackend`: Native Harness replaces legacy in `AddZaideAgents`. Legacy code remains for tests/reference but is not production-registered. No backend-selection UI in Phase 19. |
| Model provider and protocol | **OpenAI-compatible** `/chat/completions` with **tools/function-calling** over HTTPS. Provider endpoint, API key, and model name reuse `AgentExecutionOptions` configuration surface. Multi-provider abstraction is deferred; transport is namespaced under Native Harness infrastructure. |
| Streaming | **Implemented and honestly reported.** Internal SSE consumption for responsiveness; terminal completion remains a single `AgentBackendEvent.MessageCompleted` (Phase 15 contract preserved). No new `AgentBackendEventKind`. |
| Model-client library (P19-D13) | **No new NuGet dependency.** M3 implements streaming and function-calling with the existing `HttpClient` registration extended for SSE parsing. A future library adoption requires focused proof, license/provenance check, and plan amendment. |
| Tool-calling protocol format | **OpenAI tools/function-calling JSON** mapped to Phase 17 `AgentActionPayload` subtypes. Tool names are stable harness-defined identifiers (`read_file`, `create_file`, `replace_file`, `delete_file`, `execute_command`). No neutral abstraction that would force ACP into a dishonest lowest common denominator (P19-D03). |
| Turn budget and termination | Default **25 model turns** per run (`NativeHarnessProviderProtocol.DefaultMaxTurns`, informed by M1 research ceilings). One model round (request + optional tool calls) consumes one turn. Broker tool execution does not consume turns. Exhaustion yields `NativeHarnessRunTerminationKind.TurnBudgetExceeded` → `AgentBackendEvent.FailureObserved`. Model stop with final text yields `Completed`. Unrecoverable transport/parse errors yield `Failed`. |
| Cancellation and late completion | Cooperative via run `CancellationToken`. On cancellation: stop issuing new model requests; call `IAgentActionBroker.Revoke()` through session revocation path; allow in-flight broker work to reach terminal `AgentActionResult`. Provider or broker work completing after cancellation is recorded as `NativeHarnessLateCompletionDisposition` and may surface run `Indeterminate` when outcome cannot be classified safely. |
| Prior conversation replay (P19-D10 concern 2) | **Bounded read-only replay** of prior `UserChat` and `AssistantResponse` entries from `IConversationStore` through `INativeHarnessPriorConversationReader`. Current admitted message is excluded. Failures, routing events, channel events, and system notifications are excluded. Token budget uses Phase 18 heuristic (`ceil(chars/4)`), capped by `NativeHarnessPriorConversationReplayPolicy` (default 4,000 tokens, 50 entries, recency-first from newest backward). No replay state is persisted. |
| Townhall event surface (P19-D02) | **Reuse existing broker-event path.** No new `AgentEventKind` or `AgentEventPayload` subtype in M2. `ActionResultReported` → `ProjectActionResultReported` remains the Townhall tool-activity surface for M5. Richer rendering, if needed, is a bounded M5 extension only. |

---

## 3. Native Harness identity

| Constant | Value |
|----------|-------|
| `AgentBackendIds.NativeHarnessValue` | `backend:zaide-native-harness` |
| Planned backend version (M4) | `zaide-native-harness/1` |
| Marker interface | `IAgentActionRequestCapableBackend` |

---

## 4. Internal contract types (M2)

All types live under `src/Features/Agents/Contracts/` and `Domain/`. M3 consumes
them; M2 does not register implementations.

### 4.1 In-run model/tool loop history (P19-D10 concern 1)

Private, in-memory, run-scoped records **distinct** from `AgentEvent`,
`ConversationEntry`, and `IConversationStore`:

| Type | Role |
|------|------|
| `NativeHarnessLoopHistory` | Immutable append-only collection |
| `NativeHarnessLoopHistoryRecord` | Abstract base with `TurnIndex`, `RecordedAtUtc` |
| `NativeHarnessSystemPromptRecord` | Phase 18 manifest + instructions (per run) |
| `NativeHarnessUserTurnRecord` | Admitted or replayed user text |
| `NativeHarnessAssistantTurnRecord` | Model assistant text |
| `NativeHarnessToolCallRecord` | Model tool call before broker dispatch |
| `NativeHarnessToolResultRecord` | Broker result summary bound to tool call id |

Validation rules locked in M2:

- Turn index must not decrease across append.
- `NativeHarnessToolResultRecord` requires a preceding `NativeHarnessToolCallRecord` with the same `NativeHarnessToolCallId`.
- History is never written to conversation store or event stream.

### 4.2 Tool-call representation

| Type | Role |
|------|------|
| `NativeHarnessToolCallId` | Run-scoped model tool-call identity |
| `NativeHarnessToolCallDescriptor` | Validated descriptor mapped to `AgentActionKind` |
| `NativeHarnessToolCallRecord` | History record with `ModelToolName`, `ArgumentsJson` |

M3 maps OpenAI tool arguments JSON to `AgentReadFileActionPayload`,
`AgentCreateFileActionPayload`, `AgentReplaceFileActionPayload`,
`AgentDeleteFileActionPayload`, and `AgentExecuteCommandActionPayload` before
`IAgentActionBroker.RequestAsync`.

### 4.3 Turn loop state and termination

| Type | Role |
|------|------|
| `NativeHarnessTurnPhase` | `AwaitingModel`, `ExecutingTools`, `Terminal` |
| `NativeHarnessTurnBudget` | Max/consumed/remaining turns |
| `NativeHarnessCancellationState` | Cancellation requested + late-completion disposition |
| `NativeHarnessRunTerminationKind` | `Completed`, `Failed`, `Cancelled`, `Indeterminate`, `TurnBudgetExceeded` |
| `NativeHarnessRunOutcome` | Terminal harness outcome before `AgentBackendEvent` emission |
| `NativeHarnessLateCompletionDisposition` | Late work handling after cancellation |

Mapping to Phase 15 backend events (M3):

| `NativeHarnessRunTerminationKind` | `AgentBackendEvent` |
|-----------------------------------|---------------------|
| `Completed` | `MessageCompleted` |
| `Failed`, `TurnBudgetExceeded` | `FailureObserved` (`AgentFailureKind.Execution` or `Timeout` as appropriate) |
| `Cancelled` | `FailureObserved` (`AgentFailureKind.Cancellation`) |
| `Indeterminate` | `FailureObserved` (`AgentFailureKind.Indeterminate`) |

### 4.4 Prior-conversation replay seam (P19-D10 concern 2)

| Type | Role |
|------|------|
| `INativeHarnessPriorConversationReader` | Read-only selection contract |
| `NativeHarnessPriorConversationReplayRequest` | Conversation id, current entry id, policy |
| `NativeHarnessPriorConversationReplayPolicy` | Token/entry limits and included kinds |
| `NativeHarnessPriorConversationReplayEntry` | Filtered replay row for prompt assembly |

Implementation notes for M3:

- Reader implementation queries `IConversationStore.TryGet` only; no `AppendEntry`.
- Selection walks entries in reverse chronological order until budget exhausted.
- Redaction: replay text is taken from store content as-is; Phase 18 manifest
  redaction applies to IDE context only. Secrets in prior chat are a threat-model
  concern (M2_THREAT_MODEL.md T-02).
- Current admitted `messageEntryId` and all entries after it are excluded.

### 4.5 Provider/protocol constants

`NativeHarnessProviderProtocol` locks:

| Constant | Value |
|----------|-------|
| `ChatCompletionsPath` | `/chat/completions` |
| `FunctionCallingFormat` | `openai-tools` |
| `StreamingTransport` | `sse` |
| `DefaultMaxTurns` | `25` |
| `DefaultProviderTimeoutSeconds` | `120` |

---

## 5. Six-fact capability rows (P19-D06)

`NativeHarnessCapabilityRows` defines initial rows for:

| `AgentCapabilityId` | Advertised | Available when | Configured when | Permitted when | Degraded when | CurrentlyUsable when |
|---------------------|------------|----------------|-----------------|----------------|---------------|----------------------|
| `MessageCompletion` | Always `Supported` | Provider configured | Provider configured | Unknown (M4 refines) | `NotSupported` unless transport retry | Provider configured |
| `Tools` | Always `Supported` | Provider + workspace captured | Both true | Unknown until permission resolved | `NotSupported` unless scope invalidation | Unknown until permission resolved; never `Supported` without workspace |
| `Permissions` | Always `Supported` | Provider + workspace captured | Both true | Unknown until review | `NotSupported` | Unknown until review |
| `IdeContext` | Always `Supported` | Provider configured | Manifest present | Unknown | `NotSupported` | Manifest present and provider configured |
| `Streaming` | Always `Supported` | Provider SSE support | Provider configured | Unknown | `NotSupported` | Provider supports SSE |
| `Cancellation` | Always `Supported` | Provider configured | Provider configured | Unknown | `NotSupported` | Provider configured |

Snapshot transitions use `AgentCapabilitySnapshot.WithRow` with strictly
increasing `version`. M4 updates rows as permission, degradation, and transport
state change during a run.

Out-of-scope capabilities (`Attachments`, `Resume`, `Reconnect`, `UsageReporting`,
`RawTrace`) remain `Unavailable` for Native Harness in Phase 19.

---

## 6. Event-surface decision (Townhall)

**Decision:** Reuse the existing broker-event path without M2 contract extension.

| Activity | Surface | Phase |
|----------|---------|-------|
| Tool permission/execution facts | `AgentEvent` / `AgentActionFactPayload` | Phase 17 (existing) |
| Tool result in conversation | `ActionResultReported` → `ProjectActionResultReported` | Phase 17 (existing) |
| Final assistant text | `AgentBackendEvent.MessageCompleted` → session projection | Phase 15 (existing) |
| Richer tool rendering (optional) | Bounded additive `AgentEventKind`/payload if M5 proves insufficient | M5 only |

No Phase 15/17/18 kind or payload semantics are altered at M2.

---

## 7. Explicit exclusions (unchanged)

- ACP (Phase 20), persistence/memory/resume (Phase 21), public agent API,
  dedicated agent-requested network tools, backend-selection UI.
- Raw trace storage and cross-session memory.
- Production DI registration of Native Harness (M4).
- Tool-loop implementation (M3).

---

## 8. M1 comparative-execution limitation (retained)

M1 did not select an architecture winner. The full-corpus benchmark gate was
retired by plan amendment. M2 decisions are Zaide-native and informed by M1
observations only; they do not adopt external harness code (see `M1_PROVENANCE.md`).

---

## 9. M3 entry criteria

M3 may begin when:

1. This architecture lock is accepted.
2. `M2_THREAT_MODEL.md` is accepted.
3. `dotnet test --filter 'FullyQualifiedName~Phase19Contracts'` passes.
4. Architecture inventory ratchets reflect M2 types.

M3 implements `INativeHarnessPriorConversationReader`, the turn loop, OpenAI tool
parsing, broker dispatch, and provider transport using the contracts locked here.
