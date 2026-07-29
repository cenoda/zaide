# Phase 21 M4 — Recovery State Machine

**Milestone:** M4 — session continuity and explicit termination
**Depends on:** M1 published at `4db83202`; M2/M3 record contracts where recovery evidence references trace or usage
**Status:** Complete pending publication gate

M4 reconciles interrupted sessions after restart and offers explicit recovery
only when current evidence and capabilities make it safe; otherwise it offers
explicit termination with truthful acknowledgement state.

---

## 1. Classification states

| Classification | Meaning |
|----------------|---------|
| `Recoverable` | Durable checkpoint exists; Actor/workspace/backend binding fingerprint matches; session is not ended; interrupted run evidence supports user-initiated resume preparation |
| `Terminal` | Session or run reached a terminal lifecycle state, or the user issued terminate/abandon/archive |
| `Indeterminate` | Workspace mismatch, binding mismatch, missing binding, revoked capability, disconnect without sufficient evidence, or incompatible schema |

Absence of evidence is never success. Missing checkpoints do not imply recoverability.

---

## 2. Explicit operations (distinct intents)

| Operation | M4 behavior |
|-----------|-------------|
| `Reconcile` | Startup or manual reload of durable `SessionRecovery` records; reclassifies without creating a live session or resuming work |
| `Resume` | Explicit user action; revalidates identity/workspace/binding/capability; records a resume checkpoint; admits resumed `AgentSessionId` for later `SendAsync`; does **not** auto-execute prior runs or replay permissions |
| `Terminate` | Records local termination intent and best-effort acknowledgement; classification becomes `Terminal` |
| `Abandon` | Terminal intent distinct from terminate; same acknowledgement path, separate operation kind |
| `Archive` | Terminal intent distinct from abandon; same acknowledgement path, separate operation kind |
| `Reconnect` | Reserved in taxonomy; not auto-invoked in M4 |
| `Retry` | Reserved; not collapsed into resume |
| `Replay` | Reserved; permission decisions are never replayed |
| `NewSession` | Unaffected; `AgentSessionId.New()` remains the default when no resume admission exists |
| `Checkpoint` | System-written durable record on material lifecycle transitions |

---

## 3. Checkpoint phases

| Phase | When written |
|-------|----------------|
| `BeforeSessionStart` | New live session created in `AgentSessionService` |
| `AfterSessionReady` | Session ready lifecycle observed |
| `BeforeRunStart` | Run accepted / running |
| `AfterRunTerminal` | Terminal run or ended session |
| `BeforeApplicationShutdown` | `ApplicationShutdown` before `IAgentSessionService` disposal |
| `AfterStartupReconcile` | Startup or manual reconcile pass |

Checkpoints persist through M1 `AgentDurableRecordClass.SessionRecovery` only.

---

## 4. Acknowledgement states

Termination intent and backend/process acknowledgement are separate facts:

| State | Meaning |
|-------|---------|
| `None` | No termination recorded |
| `LocalIntentRecorded` | User/system termination intent persisted locally |
| `LocalProcessAcknowledged` | Local process teardown recorded |
| `BackendAcknowledged` | Backend adapter reported verifiable acknowledgement (none in accepted Phase 20 ACP profile) |
| `BackendAcknowledgementUnavailable` | Backend cannot confirm; no provider deletion claim is made |
| `ProviderDeletionUnverified` | Terminate requested but provider-side deletion cannot be verified |

---

## 5. Backend capability matrix (Native Harness and ACP)

| Backend | Checkpoints | Resume currently usable | Terminate ack | Reconnect |
|---------|-------------|-------------------------|---------------|-----------|
| `backend:zaide-native-harness` | Yes | No | No | No |
| `backend:acp` | Yes | No | No | No |

Both backends remain independent siblings. M4 does not invoke ACP `session/resume`,
`session/load`, or provider deletion APIs.

---

## 6. Permission and side-effect rules

1. Startup reconcile never resumes side-effecting work.
2. Resume never replays prior `AgentPermissionDecision` values; resumed work must obtain new decisions through the Phase 17 broker.
3. `AgentPermissionDecision.TryConsume()` remains the final authorization step.
4. `AgentSessionId` may be preserved only after explicit resume admission; each resumed send creates a new `ExecutionRunId` and run-scoped broker.

---

## 7. Idempotency

- Checkpoint appends use class-scoped idempotency keys.
- Resume and terminate operations honor caller-supplied idempotency keys and return `DuplicateIgnored` for repeats within the coordinator lifetime and durable store duplicates.

---

## 8. M4 exclusions preserved

- No M5 memory retrieval/injection
- No M6 integrated management UI redesign
- No automatic backend session resume or silent Native Harness/ACP fallback
- No provider deletion claims without evidence
