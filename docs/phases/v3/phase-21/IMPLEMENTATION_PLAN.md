# Phase 21: Agent Transparency, Continuity, and Memory — Implementation Plan

## Status and authorization

**Status:** M1, M2, M3, M4, M5, M6, and M7 are complete and published. M6 is published at `928a17c801f664bd43896d10cff2cde2ed968934` with publication-record correction `85af80d3f89fa25288f5282654da6267bdba9e3a`. M7 is published at `4ec4f31febfb963e5373d72b749519c788d319cf` (`docs(phase-21): establish M7 adversarial and release closeout`).

**Authorized work through M7:** Session continuity, explicit termination, durable scoped memory records, budgeted memory retrieval/influence attribution, integrated management, and adversarial closeout are complete; M7 added no new product behavior. Final human acceptance remains a separate gate.

**Production authorization:** extends through M7 only.

**Next gate:** Phase 21 final human acceptance remains a separate gate. Phase 22 remains not started and not authorized.

### M0 repository baseline

| Check | Verified result |
|-------|-----------------|
| Branch | `master` |
| Planning-base `HEAD` | `597b7b5151de8ab8f6091adb69809a531dd49357` |
| Planning-base `origin/master` | `597b7b5151de8ab8f6091adb69809a531dd49357` |
| Phase 20 accepted baseline | `597b7b5151de8ab8f6091adb69809a531dd49357` (`docs(phase-20): accept final closeout`) |
| Working tree before planning | Clean (`git status --short --branch` reported only `## master...origin/master`) |
| Phase 20 relationship | Complete, published, accepted, and preserved as an independent sibling backend |
| Existing Phase 21 plan or implementation | None under `docs/phases/v3/phase-21/`, `src/`, `tests/`, or `tools/` |
| Verification date | 2026-07-29 |

Phase 20 history and acceptance wording are immutable inputs to this plan.
Phase 21 neither reopens nor reinterprets Phase 20.

---

## Phase outcome

Phase 21 delivers the outcome locked by `docs/roadmap/V3.md`:

> For a production backend that supports them, users can inspect redacted
> traces, understand usage and cost, recover or clearly terminate interrupted
> sessions, and manage durable agent or project memory.

Phase 21 is a backend-neutral product layer over accepted session, event,
action, context, conversation, capability, and backend boundaries. It must not
move transparency, continuity, or memory truth into the Native Harness or ACP
adapter merely because one backend exposes richer evidence.

```text
Townhall / user management surfaces
  -> backend-neutral Phase 21 application owners
      -> conversations and visible activity
      -> trace evidence
      -> usage/cost evidence
      -> session recovery or explicit termination
      -> durable memory
          -> Native Harness capability input (independent backend)
          -> ACP capability input (independent backend)
```

Phase 20 remains a peer backend choice. Phase 21 may consume ACP evidence and
continuity capabilities only after the accepted six-fact capability boundary
reports them truthfully. It must not require ACP to wrap, reuse, or fall back
to Native Harness behavior.

---

## Verified live baseline

### Session, run, runtime, and backend seams

| Seam | Live truth at M0 | Phase 21 boundary |
|------|------------------|-------------------|
| `AgentSessionService` | Application-lifetime in-memory owner; one live session per `ConversationId`; creates Zaide-owned session/run IDs; serializes per-session event sequence; clears all sessions on disposal | Preserve as lifecycle authority or replace it only through an explicit compatible owner; durable continuity must not make a backend adapter authoritative for Zaide session truth |
| `AgentSessionSnapshot` / `AgentRunSnapshot` | Read-only current observations only | Durable records need explicit versioned representations; snapshots are not persistence DTOs by assumption |
| `AgentSessionService.EndAsync` | Cancels an active run, emits terminal session events, and removes live ownership | Explicit termination remains distinct from deletion, archive, disconnect, and recovery |
| Restart behavior | No Agent Session, active run, session policy override, backend binding, or event-stream state is restored | Startup must classify interrupted state; it must never infer success or silently resume side-effecting work |
| `ApplicationShutdown` | Disposes session ownership and terminates owned ACP process trees | Clean shutdown and crash recovery need different evidence; process termination does not by itself create a durable interruption record |
| `AgentActorBackendBindingStore` | In-memory explicit Actor/backend/runtime binding | Persistence and restart validity are open; any restored binding must revalidate Actor, workspace, backend, executable/runtime identity, and capability state |
| Native Harness | Accepted independent production backend with no Phase 21 persistence, raw trace, usage/cost product, or memory | Supplies only evidence/capabilities it can truthfully expose; Phase 21 does not adopt backend-private loop history as the neutral store by assumption |
| ACP | Accepted independent production backend; client boundary supports initialize/new/prompt/cancel/auth; continuity methods remain uninvoked | `session/load`, `session/resume`, `session/close`, `session/delete`, and usage/trace payload support require new capability-gated Phase 21 work, not optimistic invocation |

The accepted backend-neutral IDs remain separate:

- `ActorId` identifies a durable logical actor;
- `AgentSessionId` identifies a Zaide session;
- `ExecutionRunId` identifies one bounded attempt;
- `ConversationId` identifies the communication space;
- `AgentBackendId` identifies the selected backend implementation;
- ACP session IDs, process IDs, provider request IDs, model IDs, and runtime
  executable identities are backend evidence, not Zaide identity substitutes.

### Event and audit seams

| Seam | Live truth at M0 | Phase 21 boundary |
|------|------------------|-------------------|
| `AgentEvent` | Schema version 1; contains event/session/run/conversation/backend IDs, per-session sequence, occurred/received timestamps, optional causation, evidence level, kind, and typed payload | This is the normalized input boundary; a durable envelope, replay contract, upgrade policy, and unknown-version behavior remain open |
| `AgentEventStream` | Serialized in-memory publish/subscribe queue; no persistence, replay cursor, backpressure store, or restart reconstruction | A durable event owner must not turn the UI subscriber into the source of truth |
| `AgentConversationEventProjection` | Sole normalized agent-event writer into the conversation store; deduplicates within its application lifetime | Remains the sole conversation projection path; durable replay needs stable idempotency beyond in-memory hash sets |
| `AgentActionAuditStore` | Bounded in-memory current-lifetime store; retains at most 256 records; not durable | Audit evidence needs its own durable retention and deletion policy and must remain separate from optional raw trace |
| `AgentActionAuditSummary` | Bounded summary with narrow key-name redaction | This is not a complete Phase 21 secret-redaction boundary and must not be reused as proof that arbitrary trace payloads are safe |

Conversation projection, audit evidence, and raw diagnostics are three
different records. One must not silently substitute for another.

### Conversation and Townhall seams

| Seam | Live truth at M0 | Phase 21 boundary |
|------|------------------|-------------------|
| `ConversationStore` | Authoritative in-memory owner for conversations and ordered typed entries | Remains conversation truth; it must not become the owner of sessions, traces, usage ledgers, audit logs, or memory merely because it has persistence |
| `ConversationPersistenceService` | Schema-v1 JSON snapshot under the application config directory; debounced save; temp-file replace; last-known-good fallback | Existing conversation recovery is an input, not a general Phase 21 store; workspace keying, retention, export, delete, backup, multi-window coordination, and migration are not solved globally |
| Conversation snapshot | Persists channels, conversations/entries, active selection, drafts, and read cursors | Does not persist sessions, runs, backend bindings, capabilities, normalized events, action audit, usage/cost, traces, or memory |
| Corrupt/future schema behavior | Corrupt main file may load last-known-good; unsupported future schema disables writes | Phase 21 stores need explicit per-record compatibility, downgrade, quarantine, backup, and migration rules rather than copying this behavior by assumption |
| Townhall | Observes authoritative conversation entries and renders backend/action activity through `TownhallEntryProjection` | Phase 21 UI belongs in the existing conversation experience or a clearly linked management surface; no backend-specific chat silo |

### Capability, usage, trace, and memory seams

| Concern | Live truth at M0 |
|---------|------------------|
| Capability model | Versioned snapshot separates `Advertised`, `Available`, `Configured`, `Permitted`, `Degraded`, and `CurrentlyUsable` |
| ACP usage | A valid `usage_update` only marks usage reporting as observed and emits a generic backend-reported activity summary; token values, units, pricing source, currency, billing period, and cost are not retained |
| ACP resume | `Resume` is explicitly not supported/currently usable in the accepted Phase 20 profile |
| ACP raw trace | `RawTrace` is explicitly not supported in the accepted Phase 20 profile |
| Native Harness trace/usage | No Phase 21 product contract or durable store exists |
| Trace storage | Absent |
| Durable memory | Absent |
| Memory retrieval/injection | Absent; existing Phase 18 context manifests do not imply memory |
| Prior conversation use | Native Harness may read prior conversation entries under its accepted bounded replay policy; conversation replay is not durable memory |

Missing evidence is unavailable, not zero. A backend-reported number is not a
Zaide-verified billing fact. A protocol frame is not proof of hidden model
input/output. A persisted conversation is not memory merely because it can be
read during a later run.

### Verified tests and architecture protection

Live tests cover:

- session identity, lifecycle state transitions, exact event order, monotonic
  sequence, cancellation races, late completion, terminal/indeterminate
  outcomes, and new-session-after-end behavior without resume;
- conversation persistence round-trip, corrupt-file fallback, future-schema
  refusal, interrupted write, atomic replacement, and entry-ID stability;
- ACP capability truthfulness, generic usage observation, raw-trace/resume
  unsupported state, identity mismatch, action mediation, and Townhall
  projection;
- architecture inventory, ownership, projection, context, broker, and backend
  bypass ratchets.

No test currently proves Phase 21 trace redaction, usage/cost evidence,
session recovery, durable termination, memory, retention, export, deletion,
backup, migration, or cross-restart event replay.

---

## Ownership model

Phase 21 defines backend-neutral product ownership before choosing storage or
UI implementation details.

| Record or responsibility | Authoritative owner | Non-owner rules |
|--------------------------|---------------------|-----------------|
| Actor / Agent Identity | Existing Conversations actor catalog and typed `ActorId` | Runtime, session, provider, model, display name, and memory record never become identity |
| Conversation and visible entries | Conversations feature through `IConversationStore` | Trace, usage, audit, session, and memory stores may reference `ConversationId` but do not rewrite conversation history |
| Live session and run lifecycle | Agents application session owner | Backend adapters report capability and outcomes; they do not own Zaide session truth |
| Durable session/recovery record | Backend-neutral Agents application contract with an Agents-owned persistence adapter unless M1 evidence proves a genuinely shared owner | ACP/Native Harness private state is opaque evidence attached to the neutral record, not the record itself |
| Runtime binding | Existing explicit Actor/backend/runtime binding boundary | Restored state must be revalidated; runtime/process/provider identity cannot silently inherit Actor identity |
| Normalized agent events | Agents application event owner | Townhall is a projection; raw transport capture is a separate record |
| Action audit events | Backend-neutral Agents audit owner | Optional trace capture cannot be required for security audit continuity |
| Raw/redacted trace evidence | Backend-neutral Agents trace owner with mandatory pre-admission redaction | A backend supplies only its exposed layer; UI/logging/export/indexing/telemetry never receives an unredacted retained payload |
| Usage and cost evidence | Backend-neutral Agents usage ledger owner | Backend reports, local token accounting, price catalogs, and invoice facts remain separately attributed evidence |
| Durable memory | Backend-neutral Agents memory owner keyed to explicit scope and workspace | Conversation store remains history; context assembly remains per-run selection; backend-private memory cannot silently become shared Zaide memory |
| Projection and management UI | Existing Townhall/conversation experience plus narrowly justified Agents presentation surfaces | No Native-Harness-only or ACP-only transparency, recovery, usage, or memory silo |
| Storage engine and physical files | **Resolved at M1** — Agents-owned JSON partitions under `{config}/agents-durable/`; no database or new package |

Cross-feature access follows existing architecture rules: another feature may
consume only a minimal contract or approved application façade. Infrastructure
implementations never depend on Presentation. App composition alone registers
concrete persistence and UI implementations.

---

## Scope model

These scopes must remain explicit in contracts, storage keys, policy
evaluation, UI labels, export/delete requests, and tests.

| Scope | Meaning | Required key or evidence |
|-------|---------|--------------------------|
| Identity scope | Durable logical human, agent, or system actor | `ActorId`; profile/display revisions are separate |
| Workspace/project scope | One opened repository/project trust and storage boundary | Stable workspace identity plus generation/fingerprint; exact durable key is open |
| Conversation scope | One ordered communication space | `ConversationId`; conversation deletion does not imply backend/provider deletion |
| Session scope | One Zaide conversational/runtime context | `AgentSessionId`, owning Actor, conversation, workspace, backend binding, schema version |
| Run scope | One bounded task attempt | `ExecutionRunId`, session, context manifest, terminal/recovery classification |
| Runtime scope | One concrete backend binding/process/transport identity | `AgentBackendId` plus verified backend-specific runtime evidence |
| Trace scope | Captured evidence for a run/session/transport span | Trace record ID, source/evidence level, capture/redaction state, size/ordering metadata |
| Usage/cost scope | Meter evidence for a run/session/backend/account period | Usage record ID, run/session, metric/unit, evidence origin, price source/version, currency/time |
| Memory scope | Durable derived knowledge at Session, Agent, Conversation, or Project/Shared scope | Memory record ID, explicit scope owner, workspace, provenance, version, validation/supersession state |

Application/global policy may provide defaults, but it is not a memory scope
unless a later approved decision proves an application-wide memory product.

Storage and prompt injection are separate. Retaining a trace, conversation, or
memory record never authorizes automatic inclusion in a future model request.

---

## Mandatory invariants

### Transparency and secret safety

1. “Raw trace” means the deepest backend-exposed payload after mandatory
   safety processing. It does not promise hidden reasoning, chain-of-thought,
   or data a backend does not expose.
2. Secret redaction occurs before persistence, indexing, rendering, export,
   logging, backup, telemetry, or cross-process transfer to a Phase 21 store.
3. If redaction fails, the payload is rejected or replaced by a bounded
   failure marker. Unredacted fallback storage is forbidden.
4. Capture state is explicit: disabled, unavailable, captured, redacted,
   sampled, truncated, summarized, or failed.
5. Capture/retention policy is separate from display verbosity.
6. Trace processing cannot block or create an unbounded queue in the agent
   event pipeline.
7. Durable security audit does not depend on optional trace capture.

### Usage and cost truthfulness

1. Usage facts retain metric name, unit, value, source, backend, model where
   reported, run/session attribution, occurred/received time, and evidence
   level.
2. Reported, locally measured, calculated, estimated, invoiced, unavailable,
   and disputed values remain distinct.
3. Cost never defaults to zero when price, currency, model, provider, account,
   discounts, taxes, cache treatment, or billing evidence is missing.
4. Any calculation retains the pricing source, version/effective time,
   currency, formula, rounding rule, and source usage records.
5. Backend or provider claims are labeled as claims unless Zaide can verify
   them independently.

### Recovery and termination

1. Restart never silently resumes side-effecting work.
2. An interrupted run is classified as recoverable, terminal, or indeterminate
   from durable evidence; absence of evidence is never success.
3. Resume requires an explicit user action, a currently usable backend
   capability, matching Actor/workspace/conversation/session/runtime identity,
   compatible schema, and a verified checkpoint or backend session token.
4. Before resume, any previously proposed or approved side effect is invalid.
   The resumed run must re-propose and reauthorize each material action through
   the current Phase 17 broker.
5. `AgentPermissionDecision.TryConsume()` remains the final authorization
   step. Recovery cannot replay a consumed decision or consume a stale
   published decision.
6. Explicit termination records intent and best available backend/process
   acknowledgement separately. Local termination cannot claim provider-side
   deletion or completion without evidence.
7. Reconnect, resume, retry, replay, and new session are different operations.
8. Late completion after cancellation/termination remains representable and
   cannot overwrite a previously recorded outcome silently.

### Memory integrity

1. Memory records are derived, editable knowledge, not conversation or audit
   history.
2. Every memory has provenance, author/source, workspace, explicit scope,
   created/updated/last-validated time, schema version, and status.
3. Users can inspect, correct, disable, supersede, and delete durable memory.
4. Correction or deletion of memory does not rewrite conversation history or
   audit evidence.
5. Retrieval is budgeted and attributable. A run records which memory
   revisions influenced it where practical.
6. Cross-workspace memory access fails closed unless a future separately
   approved sharing model defines an explicit boundary.
7. Backend-private memory is not imported, trusted, shared, or deleted by
   implication.
8. Prompt injection, memory poisoning, stale facts, conflicting records, and
   supersession are tested threat cases.

### Durable record lifecycle

All Phase 21 durable record classes must define:

- retention defaults, overrides, expiry, rotation, quota, and failure behavior;
- bounded export with schema version, provenance, redaction state, and
  partial/unavailable markers;
- deletion scope, cascade boundaries, tombstone/audit behavior, provider-state
  disclaimer, and retry/verification;
- backup consistency point, included/excluded records, encryption decision,
  restore validation, and corrupt/partial backup behavior;
- forward migration, unknown version, downgrade, rollback, backup-before-write,
  and interrupted migration behavior;
- ordering key, gap behavior, duplicate handling, idempotency key, replay
  cursor, terminal-transition validation, and unknown extension policy;
- workspace isolation, multi-window/write coordination, path/symlink rules,
  and workspace close/reopen behavior;
- restart state and whether it is reconstructed, quarantined, terminalized,
  or explicitly recoverable.

No record class may inherit another class's retention or deletion semantics
merely because both use the same physical store.

---

## Open decisions for M1+

M0 deliberately leaves these decisions open:

1. Physical storage engine, schema technology, serialization format, file
   layout, transaction model, and whether record classes share one engine.
2. Encryption-at-rest requirements, key ownership, platform support, and
   behavior when a key is unavailable.
3. Canonical durable workspace/project identity and behavior for clones,
   moves, renames, worktrees, symlinks, and multi-root workspaces.
4. Multi-window/process writer coordination and corruption recovery.
5. Default retention duration, trace capture default, size quotas, rotation,
   sampling, and user policy precedence.
6. Redaction detector inputs, configured sensitive-path policy, structured
   payload handling, false-positive/false-negative policy, and redaction-rule
   versioning.
7. Trace granularity for Native Harness and ACP and which payloads each
   accepted backend can expose without overstating visibility.
8. Usage metric taxonomy, pricing catalog source/update policy, offline
   behavior, currencies, cache/tool accounting, and invoice reconciliation.
9. Durable session/checkpoint schema and exact recovery state machine.
10. ACP continuity subset (`load`, `resume`, `close`, `delete`, or none) and
    Native Harness checkpoint capabilities after live proofs.
11. Whether backend binding and context-session policy become durable, and
    which fields require restart revalidation.
12. Memory representation, record granularity, creation/edit workflow,
    conflict/supersession model, retrieval/index strategy, and ranking.
13. Whether embeddings or another index are necessary. A new dependency,
    model, network service, or paid service is not assumed.
14. Memory influence evidence format and how derived memory links back to
    source conversation/event/trace revisions.
15. Exact export formats, deletion confirmation UX, backup packaging, restore
    workflow, and migration rollback UX.
16. Whether existing conversation schema v1 changes. Any change must preserve
    conversation ownership and existing recovery behavior.
17. Exact UI composition and accessibility behavior under `docs/DESIGN.md`.

An open decision that materially changes architecture, dependency, privacy,
cost, migration, or data-loss risk is a stop condition, not an invitation to
choose by implementation convenience.

---

## Scope

### In scope after M0 and separate milestone authorization

- Backend-neutral durable record envelopes and storage lifecycle contracts.
- Redacted backend-exposed trace capture, inspection, export, and deletion.
- Truthful usage and cost evidence with provenance and unavailable states.
- Durable interruption classification, explicit recovery where supported, and
  explicit termination where recovery is unavailable or declined.
- Durable Session, Agent, Conversation, and Project/Shared memory records.
- Memory inspection, correction, disablement, supersession, deletion,
  retrieval budgeting, and influence attribution.
- Retention, export, deletion, backup, restore, migration, ordering,
  idempotency, replay, schema versioning, workspace isolation, and restart
  behavior for every admitted durable record.
- Existing Townhall/equal-backend presentation and narrowly justified Agents
  management surfaces.
- Focused security, restart, migration, corruption, replay, and adversarial
  tests.

### Phase 21 exclusions

- Human-to-Human messaging.
- A public Zaide agent API.
- Unredacted secret retention.
- Silent resumption of side-effecting work.
- Phase 16 evaluation campaigns, candidate runners, corpora, or benchmark
  authorization.
- Native Harness or ACP fallback, wrapping, or shared backend-private
  internals.
- Claiming hidden reasoning or provider data that a backend does not expose.
- Automatic provider/account deletion claims.
- Cross-device/cloud synchronization unless separately planned and authorized.
- Credentials, authentication, provider execution, candidate acquisition,
  network services, paid services, or external activity without exact
  activity-specific authorization.
- New dependencies, databases, embeddings, vector stores, hosted memory, or
  telemetry selected by assumption.
- Unrelated refactoring, cleanup, visual redesign, or test weakening.

---

## Milestones

| Milestone | Outcome | Depends on |
|-----------|---------|------------|
| M0 | Documentation-only live-seam audit, ownership/scope model, open decisions, M1+ gates, rollback, and stop boundary | Phase 20 accepted baseline |
| M1 | Backend-neutral durable record, storage, workspace-isolation, migration, replay, and threat-model foundation | M0 accepted and M1 explicitly authorized |
| M2 | Mandatory-redaction trace capture and bounded inspection lifecycle for truthful backend-exposed evidence | M1 |
| M3 | Usage and cost evidence ledger with provenance, calculation truthfulness, and user understanding | M1 |
| M4 | Durable interruption classification, explicit recovery where supported, and explicit termination otherwise | M1; M2/M3 record contracts where recovery evidence references them |
| M5 | Durable scoped memory records with inspect/correct/disable/supersede/delete controls | M1 |
| M6 | Budgeted memory retrieval/influence attribution plus cross-record retention/export/backup/migration and Townhall management integration | M2–M5 |
| M7 | Adversarial, restart, corruption, migration, privacy, and release closeout | M6 |

M2 and M3 may be implemented in either order after M1 if their accepted plan
surfaces remain independent. M4 must not start until M1 has locked restart,
schema, and idempotency semantics. M6 is the first milestone allowed to claim
integrated Phase 21 product behavior. M7 is verification/closeout, not a place
to add unplanned product behavior.

### Common milestone gate rules

For M1–M7:

- every filtered command must discover at least one test and pass with zero
  failures; `No test matches` is failure regardless of process exit code;
- run fast tests in an interactive terminal;
- if a fast filtered or full run fails or hangs, reproduce with the serial
  settings before classifying a regression;
- no baseline, allowlist, or test weakening is permitted to obtain a pass;
- stage the exact milestone files before final verification;
- use one reviewable commit per coherent milestone outcome;
- stop before push if any required gate fails.

### M0 — Planning gate

**Allowed surfaces:**

- `docs/phases/v3/phase-21/IMPLEMENTATION_PLAN.md`
- `docs/phases/v3/phase-21/TOFIX.md`
- status surfaces only if `docs-rules.md` requires them to truthfully record
  M0 planning

`docs-rules.md` assigns normal current work to the phase-local `TOFIX.md`.
This M0 does not change the Phase 21 roadmap outcome/order/dependencies or
introduce an architecture subsystem, so no status-surface update is required.

**Required artifacts:**

- this implementation plan;
- `TOFIX.md`.

**Exact verification:**

```bash
git diff --cached --check
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
git diff --cached --name-only -- src tests tools
```

The Architecture command must discover at least one test and pass with zero
failures. The final command must produce no paths. Do not run the slow or full
suite for documentation-only M0.

**Exit conditions:**

- exact staged scope contains only the two Phase 21 planning documents;
- all required gates pass;
- one reviewable commit `docs(phase-21): establish M0 plan` is pushed to
  `origin/master`;
- post-push working tree is clean and synchronized;
- no Phase 21 implementation exists;
- M1+ remain not started and not authorized;
- Phase 20 remains accepted and unchanged.

**Rollback point:** `597b7b5151de8ab8f6091adb69809a531dd49357`.
If M0 is rejected, revert the one documentation commit. No source, test, tool,
dependency, UI, persistence, memory, trace, recovery, or backend rollback is
needed.

### M1 — Durable record and storage foundation

**Goal:** Resolve M0 storage/ownership decisions with evidence and implement
only the backend-neutral versioned record/storage primitives required by later
milestones.

**Allowed production surfaces:**

- minimal internal contracts/domain/application types under
  `src/Features/Agents/{Contracts,Domain,Application}/`;
- an Agents-owned persistence adapter under
  `src/Features/Agents/Infrastructure/` only after the M1 ownership decision;
- `src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs` and
  `ApplicationShutdown.cs` only for proven composition/flush ownership;
- existing Workspace identity contracts only as consumers;
- existing Conversations contracts only as references, with no conversation
  ownership transfer.

**Allowed test surfaces:**

- `tests/Zaide.Tests/Features/Agents/Transparency/Storage/`;
- architecture inventory/ownership ratchets;
- deterministic temp-directory fixtures under tests only.

**Required artifacts:**

- `M1_STORAGE_AND_RECORD_CONTRACT.md`;
- `M1_THREAT_MODEL.md`;
- `M1_MIGRATION_AND_ROLLBACK_MATRIX.md`;
- proof or rejection of any proposed dependency/storage engine.

**Required behavior:**

- versioned durable envelopes with workspace/session/run/conversation/backend
  references where applicable;
- explicit ordering, idempotency, replay, unknown-version, migration,
  interrupted-write, backup-before-migration, and quarantine behavior;
- workspace isolation and multi-writer decision;
- separate record classes/policies for trace, usage, session/recovery, audit,
  and memory;
- no product trace capture, usage UI, resume, memory retrieval, or prompt
  injection yet.

**Exact gates:**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase21RecordContract|FullyQualifiedName~Phase21Storage|FullyQualifiedName~Phase21WorkspaceIsolation|FullyQualifiedName~Phase21Migration"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
git diff --check
```

**Exit:** Later milestones have a tested neutral durable foundation without
claiming Phase 21 user behavior.

**Rollback:** Revert the single M1 commit. If a test store was migrated,
restore the M1 pre-migration backup and verify its digest. M1 must not migrate
real user data without a separately accepted migration/backup decision.

### M2 — Redacted trace evidence

**Goal:** Capture and inspect the deepest truthful backend-exposed trace layer
only after mandatory redaction and bounded admission.

**Allowed production surfaces:**

- `src/Features/Agents/{Contracts,Domain,Application}/Transparency/` or the
  M1-approved equivalent;
- M1-approved Agents persistence adapter;
- narrow Native Harness and ACP evidence adapters that produce neutral trace
  inputs without sharing backend internals;
- `AgentEventStream`/`AgentSessionService` only for nonblocking correlation;
- Agents presentation and Townhall projection only for trace availability,
  redaction state, and an inspection entry point.

**Allowed test surfaces:**

- `tests/Zaide.Tests/Features/Agents/Transparency/Trace/`;
- backend-specific adapter tests;
- architecture/bypass ratchets.

**Required artifact:** `M2_TRACE_REDACTION_AND_RETENTION_EVIDENCE.md`.

**Required behavior:**

- redaction before every retention/render/export/log/index/backup boundary;
- failure-closed redaction, bounded payloads, backpressure, capture-state
  markers, retention/delete/export behavior, and workspace isolation;
- honest backend evidence level and unavailable state;
- no hidden-thought claim and no change to provider context caused by display
  settings.

**Exact gates:**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase21Trace|FullyQualifiedName~Phase21Redaction|FullyQualifiedName~Phase21TraceLifecycle"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
git diff --check
```

**Exit:** Redacted trace evidence is inspectable for supported backends and
truthfully unavailable otherwise; no unredacted retained path exists.

**Rollback:** Disable capture first, flush/reject pending inputs, revert M2,
and remove only M2 trace records through the approved deletion path. Preserve
audit/conversation/usage/memory records.

### M3 — Usage and cost evidence

**Goal:** Preserve and present truthful usage/cost evidence without converting
missing or backend-reported data into false billing certainty.

**Allowed production surfaces:**

- M1-approved neutral usage ledger contracts/domain/application/persistence;
- narrow Native Harness and ACP usage evidence adapters;
- capability snapshot mapping for truthful usability/degradation;
- Agents/Townhall presentation for per-run/session summaries and provenance.

**Allowed test surfaces:**

- `tests/Zaide.Tests/Features/Agents/Transparency/Usage/`;
- backend-specific usage mapping tests;
- architecture ratchets.

**Required artifact:** `M3_USAGE_AND_COST_EVIDENCE.md`.

**Required behavior:**

- retain original metrics/units and evidence source;
- distinguish reported/measured/calculated/estimated/invoiced/unavailable;
- versioned price source and formula for any calculation;
- explicit currency, effective time, rounding, and uncertainty;
- correction/dispute, retention, export, delete, backup, migration, replay,
  duplicate, and workspace isolation behavior;
- no zero-cost default.

**Exact gates:**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase21Usage|FullyQualifiedName~Phase21Cost"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
git diff --check
```

**Exit:** Users can understand what Zaide knows, what the backend reported,
how a cost was calculated, and what remains unavailable.

**Rollback:** Revert M3 presentation/adapters and quarantine M3 ledger records
whose schema is no longer readable. Never rewrite them as zero or verified.

### M4 — Session continuity and explicit termination

**Goal:** Reconcile interrupted sessions after restart and offer explicit
recovery only when current evidence and capabilities make it safe; otherwise
offer explicit termination with truthful acknowledgement state.

**Allowed production surfaces:**

- neutral durable session/recovery records and application coordinator;
- `IAgentSessionService`/`AgentSessionService` additive recovery/termination
  boundary;
- explicit backend continuity adapters under Native Harness and ACP owners;
- binding store and capability snapshot only for persisted/revalidated facts;
- `ApplicationShutdown` and app startup composition for checkpoint/reconcile;
- Townhall/Agents presentation for interrupted, recoverable, indeterminate,
  terminated, and acknowledgement states.

**Allowed test surfaces:**

- `tests/Zaide.Tests/Features/Agents/Continuity/`;
- deterministic crash/restart/fake-backend fixtures;
- existing Phase 17 permission and stale-proposal regressions;
- architecture ratchets.

**Required artifacts:**

- `M4_RECOVERY_STATE_MACHINE.md`;
- `M4_RESTART_AND_TERMINATION_EVIDENCE.md`;
- backend capability matrix for Native Harness and ACP.

**Required behavior:**

- durable checkpoint before/after material lifecycle transitions;
- startup reconciliation with no automatic side-effect resume;
- explicit user resume; identity/workspace/runtime/schema/capability
  revalidation;
- all prior action permission decisions invalidated for resumed work;
- explicit terminate/abandon/archive distinctions and acknowledgement;
- idempotent repeat startup/reconcile/terminate/resume commands;
- late completion and backend disconnect remain representable.

**Exact gates:**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase21Recovery|FullyQualifiedName~Phase21Termination|FullyQualifiedName~Phase21Restart"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase17ProposalBroker|FullyQualifiedName~Phase17PermissionLifecycle"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
git diff --check
```

**Exit:** Every interrupted durable session becomes explicitly recoverable,
terminal, or indeterminate; no side-effecting work resumes silently.

**Rollback:** Disable new resume admission, explicitly terminate or quarantine
any Phase 21-owned live recovery attempt, preserve durable interruption
evidence, then revert M4. Never delete an interrupted record merely to make an
older binary start.

### M5 — Durable scoped memory

**Goal:** Store user-controllable derived knowledge at explicit Session,
Agent, Conversation, or Project/Shared scope without yet injecting it
automatically into runs.

**Allowed production surfaces:**

- neutral memory contracts/domain/application/persistence under the M1
  ownership decision;
- existing Actor, Conversation, Session, Run, Workspace, and context-manifest
  contracts as identifiers/evidence only;
- Agents/Townhall presentation for inspect/create/correct/disable/supersede/
  delete and scope control.

**Allowed test surfaces:**

- `tests/Zaide.Tests/Features/Agents/Memory/Store/`;
- memory threat/policy/lifecycle tests;
- architecture/workspace-isolation ratchets.

**Required artifacts:**

- `M5_MEMORY_RECORD_AND_POLICY.md`;
- `M5_MEMORY_LIFECYCLE_EVIDENCE.md`.

**Required behavior:**

- provenance, author/source revision, validation time, explicit scope,
  workspace, version, conflict, supersession, disablement, and deletion;
- retention/export/backup/migration/replay/idempotency semantics;
- cross-workspace denial by default;
- poisoning/staleness/conflict handling;
- no automatic prompt injection or embedding/network service.

**Exact gates:**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase21MemoryStore|FullyQualifiedName~Phase21MemoryPolicy|FullyQualifiedName~Phase21MemoryLifecycle"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
git diff --check
```

**Exit:** Durable memory is inspectable and controllable, but storage alone
does not influence a model request.

**Rollback:** Stop new writes, export/quarantine readable M5 records, revert
M5, and preserve conversation/audit/trace/session records unchanged.

### M6 — Memory influence and integrated management

**Goal:** Add budgeted memory retrieval with influence attribution and complete
cross-record lifecycle/management integration without weakening the individual
record contracts.

**Allowed production surfaces:**

- memory retrieval/ranking policy under Agents application;
- Phase 18 context-manifest assembly only through an explicit memory source
  with provenance, redaction, exclusions, and token budget;
- neutral export/delete/backup/restore/migration coordinators over record-owner
  contracts;
- existing Townhall/Agents presentation for trace, usage/cost, interruption,
  and memory management;
- App composition only for admitted owners.

**Allowed test surfaces:**

- `tests/Zaide.Tests/Features/Agents/Memory/Retrieval/`;
- `tests/Zaide.Tests/Features/Agents/Transparency/Integration/`;
- Townhall accessibility/presentation tests;
- Phase 18 context bypass and architecture ratchets.

**Required artifacts:**

- `M6_MEMORY_INFLUENCE_EVIDENCE.md`;
- `M6_RETENTION_EXPORT_DELETE_BACKUP_EVIDENCE.md`;
- `M6_TOWNHALL_ACCESSIBILITY_EVIDENCE.md`.

**Required behavior:**

- deterministic eligibility, ranking, token budget, provenance, conflict, and
  stale-memory rules;
- each run records memory revision influence or a truthful unavailable marker;
- disabled/deleted/out-of-scope memory cannot be retrieved;
- redaction and Phase 18 hard exclusions remain final;
- export/delete/backup/restore/migration retain record-owner semantics and
  partial/unavailable evidence;
- keyboard/focus/screen-reader and bounded large-history behavior.

**Exact gates:**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase21MemoryRetrieval|FullyQualifiedName~Phase21MemoryInfluence"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase21TransparencyIntegration|FullyQualifiedName~Phase21Townhall|FullyQualifiedName~Phase21Export|FullyQualifiedName~Phase21Backup|FullyQualifiedName~Phase21Migration"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase18ContextBypass|FullyQualifiedName~Architecture"
git diff --check
```

**Exit:** Supported Phase 21 behavior is manageable through equal backend-
neutral surfaces, and memory influence is explicit, scoped, budgeted, and
attributable.

**Rollback:** Disable retrieval/injection before reverting. Keep durable M5
memory inspectable/exportable where possible; do not silently continue
injecting memory through an older path.

### M7 — Adversarial and release closeout

**Goal:** Prove the integrated privacy, data-lifecycle, recovery, migration,
replay, corruption, and capability boundaries and close Phase 21 without
adding new behavior.

**Allowed surfaces:**

- Phase 21 production/test files admitted by M1–M6;
- architecture/bypass ratchets;
- Phase 21 evidence and current status documents.

**Required artifact:** `M7_CLOSEOUT_EVIDENCE.md`.

**Required coverage:**

- redaction failure, secret variants, sensitive files, malformed/oversized
  traces, backpressure, export, backup, and restore;
- usage duplicates, unit/currency mismatch, stale/missing pricing, disputed
  evidence, and no-zero fallback;
- clean shutdown, crash, partial write, corrupt store, unsupported version,
  interrupted migration, multi-window contention, replay gap/duplicate, and
  idempotent startup;
- recoverable/terminal/indeterminate classification, runtime mismatch,
  workspace mismatch, capability revocation, late completion, and no silent
  side-effect resume;
- permission decisions never replayed and `TryConsume()` remains final;
- memory poisoning, stale/conflicting/superseded/deleted/disabled records,
  cross-workspace leakage, budget enforcement, and influence attribution;
- conversation/audit/trace/usage/session/memory deletion independence;
- equal Native Harness/ACP placement with truthful unavailable states;
- Phase 21 exclusions remain absent.

**Exact gates:**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase21Adversarial"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase21TransparencyIntegration|FullyQualifiedName~Phase21Recovery|FullyQualifiedName~Phase21MemoryInfluence"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase17ProposalBroker|FullyQualifiedName~Phase17PermissionLifecycle|FullyQualifiedName~Phase18ContextBypass"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
dotnet test Zaide.slnx --no-build
dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings
git diff --check
```

Every gate must discover tests where a filter is present and pass with zero
failures.

**Exit:** Automated and manual evidence supports the roadmap outcome, current
limitations are explicit, one closeout commit is published, and final human
acceptance remains a separate gate.

**Rollback:** Revert M7 evidence/ratchets only. If a defect requires production
rollback, roll back the owning M6–M1 milestone in reverse order using its
specific data-preservation procedure.

---

## Global allowed and forbidden surfaces

Unless a milestone explicitly admits a surface, it is forbidden.

**Always preserve:**

- Phase 20 as an accepted independent ACP sibling backend;
- Native Harness and ACP backend-private ownership;
- Phase 15 session/event identity and lifecycle truth;
- Phase 17 broker authority and final `TryConsume()` ordering;
- Phase 18 context redaction, exclusion, provenance, and budget boundaries;
- Conversations as the authoritative communication/history owner;
- `AgentConversationEventProjection` as the sole normalized event-to-
  conversation writer;
- evidence-level and six-fact capability distinctions.

**Forbidden without approved plan amendment:**

- root `Infrastructure/`, `UI/Shared`, new assembly/project, plugin system, or
  public API;
- direct backend writes to conversation persistence or Phase 21 stores;
- direct Presentation-to-Infrastructure dependency;
- new package, database, service, model, embedding/index engine, telemetry, or
  network dependency;
- test deletion, skip, baseline masking, allowlist growth without evidence, or
  parallelism disablement;
- credentials, inherited secrets, provider login, paid calls, candidate
  acquisition/execution, or Phase 16 activity;
- unredacted retained payloads;
- automatic resume/retry of side-effecting work;
- action execution outside `IAgentActionBroker`;
- silent Native Harness/ACP fallback or runtime/Actor rebinding;
- conversation deletion presented as provider/backend deletion;
- memory treated as authoritative history or inserted wholesale into prompts;
- unrelated cleanup or refactoring.

---

## Limitations by design

- Phase 21 can expose only evidence a backend supplies or Zaide can observe.
  ACP protocol visibility may stop at protocol frames; Native Harness
  visibility may differ.
- No hidden reasoning or chain-of-thought is promised or retained.
- Cost may remain unavailable or estimated when authoritative pricing/account
  evidence is absent.
- Local export/deletion/termination cannot prove deletion or termination in a
  provider-controlled system without acknowledgement evidence.
- Recovery is capability- and checkpoint-dependent. Some interruptions must
  remain terminal or indeterminate.
- No side-effecting operation is automatically resumed.
- Memory is fallible derived knowledge. Provenance, validation, correction,
  conflicts, and user control remain visible.
- Cross-device/cloud synchronization is not included.
- Existing conversation schema v1 remains the live starting point; M0 does
  not select or authorize a schema change.
- M0 does not select a persistence engine, encryption design, pricing source,
  trace default, memory index, or UI layout.

---

## Stop conditions

Stop and ask before continuing if:

1. A milestone needs a different owner, new assembly/root infrastructure, or a
   breaking Phase 15/17/18/20 contract change.
2. A new dependency, database, encryption library, embedding model, vector
   store, hosted service, telemetry service, or network lookup is proposed.
3. A persistence, migration, backup, restore, or deletion decision risks
   irreversible user-data loss without a verified rollback.
4. A canonical workspace identity or multi-writer decision remains unresolved
   when implementation would encode one.
5. Any unredacted secret could reach persistence, rendering, export, logs,
   backup, indexing, telemetry, or another process.
6. Redaction failure would fall back to retaining the original payload.
7. Usage/cost would be shown as zero, verified, or invoiced without supporting
   evidence.
8. Backend-reported activity would be labeled Zaide-executed or Zaide-mediated
   without proof.
9. Resume would occur without explicit user action, current capability,
   compatible schema, matching identity/workspace/runtime, and a verified
   checkpoint.
10. A side-effect or permission decision would be replayed, or
    `TryConsume()` would cease to be the final authorization step.
11. A stale proposal would consume a published decision.
12. Runtime/process/provider/session IDs would silently replace or rebind
    Zaide Actor identity.
13. A backend failure would silently select the sibling backend.
14. Memory would cross workspaces, be injected without selection/budget/
    provenance, or rewrite conversation/audit history.
15. Provider/account deletion, termination, usage, or trace visibility would
    be claimed without evidence.
16. Credentials, authentication, candidate/provider execution, network,
    external activity, or paid services are needed without exact
    authorization.
17. Human-to-Human messaging, a public agent API, Phase 16 evaluation work, or
    another Phase 21 exclusion becomes necessary.
18. A required filtered test discovers zero tests.
19. Build/test/architecture verification fails and correction exceeds the
    authorized milestone.
20. A destructive or irreversible action is proposed.
21. An unresolved open decision materially changes privacy, architecture,
    compatibility, cost, recovery, or data lifecycle.

---

## Phase exit conditions

M0 does not satisfy these phase-level conditions. They become reviewable only
after M7:

- redacted backend-exposed traces are inspectable with no unredacted retained
  path;
- usage/cost presentation preserves evidence, provenance, uncertainty, units,
  currency, and unavailable states;
- restart classifies interrupted sessions and supports explicit safe recovery
  or explicit termination without silent side-effect resume;
- durable scoped memory is inspectable, correctable, disableable, deletable,
  budgeted, and attributable when it influences a run;
- conversation, audit, trace, usage, session, and memory ownership remain
  distinct;
- retention, export, deletion, backup, restore, migration, ordering,
  idempotency, replay, schema versioning, workspace isolation, and restart
  rules are implemented and tested for each durable record;
- Native Harness and ACP retain equal placement and truthful capability
  differences;
- all M7 gates pass with zero failures;
- current limitations and any external/manual evidence gaps are recorded;
- one reviewable closeout commit is published and the tree is clean;
- final human acceptance is recorded separately.

---

## M3 next gate

M3 is published. Stop for review/acceptance. Do not begin M4 or create any
session recovery, memory, or integrated management behavior without separate
M4+ authorization.
