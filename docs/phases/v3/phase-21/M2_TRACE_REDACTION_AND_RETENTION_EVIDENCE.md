# Phase 21 M2 — Trace Redaction and Retention Evidence

**Milestone:** M2 — Mandatory-redaction trace capture and bounded inspection
**Depends on:** M1 published at `4db83202` (`feat(phase-21): establish M1 durable record storage foundation`)
**Status:** Implementation complete; verification gates pass with zero failures.

M2 captures and inspects the deepest truthful backend-exposed trace layer
only after mandatory redaction, bounded admission, and explicit
capture-state handling. The phase outcome from `docs/roadmap/V3.md`
becomes inspectable for supported backends and remains truthfully
unavailable otherwise. No unredacted retained path is introduced.

---

## 1. Outcome and ownership decision

| Decision | M2 lock |
|----------|---------|
| Trace capture sink | `AgentTraceCaptureSink` (application façade) over `AgentTraceBoundedCaptureQueue` and the M1 `IAgentDurableRecordStore` |
| Trace redaction | `AgentTraceRedactionProcessor` (fail-closed) — runs before any durable write, render, export, log, index, backup, or cross-process transfer |
| Trace inspection | `IAgentTraceInspector` over the M1 Trace record class; read-side only |
| Source registry | `IAgentTraceSourceRegistry` (auto-populated from `IEnumerable<IAgentTraceBackendEvidenceSource>`) plus a registry filter that rejects un-registered backends |
| Backend evidence adapters | `NativeHarnessAgentTraceSource` and `AcpAgentTraceSource` — narrow, produce neutral trace inputs, never share backend-private internals |
| Presentation | `AgentTraceAvailabilityProjection` (state), `AgentTraceAvailabilityState` (observable), `AgentTraceInspectionViewModel` (entry point) — read-only seam for the existing Townhall/Agents presentation |
| Storage route | M1 Trace record class (`AgentDurableRecordClass.Trace`) under the workspace-isolated `{config}/agents-durable/{wsKey}/` partition |
| Composition root | `AgentsServiceCollectionExtensions.AddZaideAgents`; `ApplicationShutdown` drains the bounded capture queue before the M1 store is disposed |
| Architecture ratchet | `Phase21TraceRatchetTests` — mandatory redaction, bounded queue, backend-private isolation, no Conversation store coupling, no root `Infrastructure/` admission |

The owner is `Zaide.Features.Agents.Application.Transparency.Trace.*` and
the M1-approved persistence adapter. The capture pipeline never writes to
the conversation store and never admits an unredacted payload.

---

## 2. Backend-neutral capture pipeline

```text
Backend evidence (Native Harness, ACP) [future wiring]
   ↓ neutral AgentTraceCaptureRequest via IAgentTraceBackendEvidenceSource
AgentTraceCoordinator.TrySubmit
   ↓ registry filter (only registered backends)
AgentTraceCaptureSink.TrySubmit
   ↓ mandatory redaction
AgentTraceRedactionProcessor.Apply
   ↓ bounded payload enforcement (max bytes)
AgentTraceBoundedCaptureQueue.TryEnqueue
   ↓ typed envelope wrap (TraceRecordEnvelope)
background drain → IAgentDurableRecordStore.TryAppend (Trace class)
   ↓ M1 durable partition
{config}/agents-durable/{wsKey}/records/Trace/{seq}_{recordId}.json
```

| Stage | Guarantee |
|-------|-----------|
| Backend source | Produces neutral evidence only. Never references `NativeHarnessLoopRunner`, `NativeHarnessLoopHistory`, `INativeHarnessProviderTransport`, `AgentExecutionOptions`, `AcpAgentSessionAdapter`, `AcpProtocolSession`, `IAcpSessionClient`, `IAcpProcessLauncher`, or `AcpProcessHostShutdownRegistry`. Source code is enforced by `Phase21TraceRatchetTests.NativeHarnessSource_DoesNotReferenceBackendPrivateTypes` and `AcpSource_DoesNotReferenceBackendPrivateTypes` |
| Registry filter | Admit only backends that the composition root registered. Unregistered backends receive `AgentTraceCaptureStatus.Disabled` with `AgentTraceCaptureState.Disabled`; no payload is queued or persisted |
| Redaction processor | Fail-closed. On exception, the processor returns a bounded failure marker and the sink records `AgentTraceCaptureState.Failed` with status `RedactionFailed`. The original payload is never admitted |
| Capture states | `Disabled`, `Unavailable`, `Captured`, `Redacted`, `Truncated`, `Failed` are admitted this milestone. `Sampled` and `Summarized` are reserved for later evidence layers and are not used as aliases for any other state |
| Bounded payload | Default `MaxPayloadBytes = 64 * 1024`. When exceeded, the redacted content is truncated and a bounded `{ "state": "truncated" }` marker replaces the tail. The capture state is `Truncated`; the status is `Truncated` |
| Bounded queue | Default `MaxQueueDepth = 256`. When the queue is full, new submissions receive `AgentTraceCaptureStatus.BackpressureRejected` and the queue's `DroppedCount` increments. The agent event pipeline is never blocked |
| Drain | A single background task drains the queue, wraps the redacted payload in the typed `TraceRecordEnvelope`, and calls M1 `TryAppend`. Drain errors increment the dropped counter; they never propagate to the event pipeline |
| Envelope wrap | Every M1 Trace record is the typed envelope `{ backendId, kind, evidenceLevel, captureState, redactedPayload, payloadByteCount, capturedAtUtc, redactionReason }`. The inspector deserializes from this envelope; original input payload is never re-read |
| Failure-closed unknown backend | `Coordinator.TrySubmit` consults `IAgentTraceSourceRegistry`. Unregistered backends return `Disabled`. Composition root registers `NativeHarness` and `Acp`; tests register additional sources to simulate other backends |
| Composition cleanup | `ApplicationShutdown.Run` disposes `AgentTraceBoundedCaptureQueue` before `IAgentDurableRecordStore` so the drain task's pending M1 Append calls land before the store closes |

---

## 3. Mandatory redaction before retention

The redaction processor is the only path that mutates a trace payload. The
processor scans the raw payload (not the envelope) and runs four patterns
borrowed and extended from `AgentContextRedactionProcessor` (Phase 18):

- `api-key` — `sk-...`, `ghp_...`, `AKIA...`, `Bearer ...`, `password=...`, `secret=...`
- `connection-string` — `ConnectionString=...`, `Server=...Password=...`
- `private-key` — PEM `-----BEGIN ... PRIVATE KEY-----` blocks
- `hex-secret` — `key/token/secret = <32+ hex chars>`

The processor:
- Strips UTF-8 BOM defensively.
- Returns `Unchanged` for safe payloads (capture state `Captured`).
- Returns a redacted payload and capture state `Redacted` when any pattern matches.
- Returns a bounded failure marker `{ "state": "failed", "reason": "redaction-processing-failed" }` and capture state `Failed` on any exception.
- Never returns the original payload when redaction matched or failed.

The capture sink calls the processor on every non-marker submission. The
unavailable marker is the only path that bypasses redaction; the marker is
a constant bounded string. The sink then calls the bounded-queue writer
through a typed envelope so the inspector always decodes the typed fields
without re-reading the original input.

| Concern | M2 behavior |
|---------|-------------|
| Persistence | Redaction precedes the M1 Trace append. No raw payload is admitted. |
| Rendering | The inspector never returns the original input payload; it returns the redacted envelope. |
| Export | Inspection reads through the M1 record store; no separate export API is added this milestone. |
| Logging | `AgentTraceCaptureSink` does not log the original payload. The bounded failure marker is the only logged failure value. |
| Indexing | Trace records are not indexed. The M1 record store indexes by class and ordering sequence only. |
| Backup | Backup is provided by the M1 partition `index.json.lastknowngood`. The redacted payloads are what gets backed up. |
| Cross-process transfer | Only the M1 file store writes or reads trace records. No additional cross-process transfer is added. |
| Failure-closed redaction | On any redaction exception, the original payload is replaced by `{ "state": "failed", "reason": "redaction-processing-failed" }` and the status is `RedactionFailed`. The capture state is `Failed`. |

---

## 4. Capture states

The capture sink records one explicit per-row capture state. The taxonomy
matches the M2 spec and never collapses an unavailable or failed row into
a captured or redacted row.

| State | Meaning this milestone |
|-------|------------------------|
| `Disabled` | Capture was not enabled at the time of submission, or the backend is not in the source registry |
| `Unavailable` | The backend reports that this evidence layer is not exposed. The sink stores the constant `{ "state": "unavailable" }` marker; the redacted payload is never the backend-private source |
| `Captured` | The submitted payload matched no redaction pattern. The redacted payload is the original payload (post-BOM strip); the source remains backend-neutral |
| `Redacted` | One or more redaction patterns matched. The redacted payload is the post-pattern string. The `redactionReason` field carries the matched secret class |
| `Truncated` | The post-redaction payload exceeded `MaxPayloadBytes`. The tail is replaced with a constant marker. The original raw payload is never retained |
| `Failed` | Redaction processing threw an exception or the original payload was rejected for another fail-closed reason. A bounded failure marker is the only retained value |
| `Sampled` | Reserved. Not used in this milestone; reserved for future sampling policy |
| `Summarized` | Reserved. Not used in this milestone; reserved for future summarization policy |

Honest missing evidence is reported as `Unavailable`, never as
`Captured` or `Redacted`. The capture pipeline never infers a backend
capability that the source did not advertise.

---

## 5. Backend evidence adapters

Two narrow adapters are admitted:

### 5.1 `NativeHarnessAgentTraceSource`

- Exposes kinds `Request`, `Response`, `ToolCall`, `ToolResult`,
  `BackendLoopHistory`, `Error`, `CapabilityDiscovery`, `UnavailableMarker`.
- Refuses `ProtocolFrame` (ACP owns protocol frames) and other unsupported
  kinds with `AgentTraceCaptureStatus.Disabled` and
  `AgentTraceCaptureState.Unavailable`.
- Serializes loop history turns with
  `NativeHarnessAgentTraceSource.SerializeLoopHistoryTurn` into a neutral
  shape `{ backend, kind, turnIndex, recordedAtUtc, publicText }`. Tool
  arguments, model tool names, and internal handoff strings are never
  serialized.

### 5.2 `AcpAgentTraceSource`

- Exposes kinds `Request`, `Response`, `ProtocolFrame`, `Error`,
  `CapabilityDiscovery`, `UnavailableMarker`.
- Refuses `ToolCall` and `BackendLoopHistory` with `Disabled` /
  `Unavailable`.
- Serializes protocol frames with
  `AcpAgentTraceSource.SerializeProtocolFrame` into a neutral shape
  `{ backend, method, id, direction, observedAtUtc, opaqueBodyBase64 }`.
  The body is hashed with SHA-256 (Base64 marker) by
  `AgentTraceBackendEvidenceSourceWriter.ComputeOpaqueBodyMarker`. The
  source never re-exposes the raw JSON-RPC body, so backend-private
  internals remain opaque while the wire shape is still recorded.

The `Phase21TraceRatchetTests.NativeHarnessSource_DoesNotReferenceBackendPrivateTypes`
and `AcpSource_DoesNotReferenceBackendPrivateTypes` ratchets enforce the
isolation statically by scanning the source files for forbidden type
names. New backend internals are not added at the source layer.

---

## 6. Inspection entry point

The M2 presentation surface is read-only and admits only the existing
Agents and Townhall presentation patterns:

- `AgentTraceAvailabilityProjection` (presentation layer) — subscribes
  implicitly to the capture pipeline and republishes a snapshot
  `AgentTraceAvailabilityState` (capture enabled, total records, total
  bytes, latest capture time, counts by capture state, backpressure
  observed). The projection never mutates the underlying trace data.
- `AgentTraceInspectionViewModel` (presentation layer) — exposes the
  inspection summary and the records query methods. The view model
  delegates to the coordinator and the availability projection so the
  M1 record store remains the single source of truth.
- The existing Townhall and Agents presentation consume the projection
  state for "trace availability" and "redaction state" labels; the
  view model exposes the "inspection entry point" without introducing a
  new visual surface this milestone.

The M2 scope does not change the visual design, the existing settings
window, or the conversation projection. The capture sink cannot be
enabled or disabled by display settings; capture is a backend policy
admitted at the composition root.

---

## 7. Bounded payload and queue

| Concern | M2 default | Enforcement |
|---------|------------|-------------|
| Max payload bytes | `64 * 1024` (constant `AgentTraceCaptureLimits.DefaultMaxPayloadBytes`) | `AgentTraceCaptureSink.TruncateForBound` replaces the tail with a bounded marker and records `Truncated` |
| Max queue depth | `256` (constant `AgentTraceCaptureLimits.DefaultMaxQueueDepth`) | `AgentTraceBoundedCaptureQueue` uses a `BlockingCollection`; `TryEnqueue` returns false when full and increments `DroppedCount` |
| Records per page | `128` (constant `AgentTraceCaptureLimits.DefaultMaxRecordsPerPage`) | `AgentTraceInspector.GetRecords` and `GetSummary` page through the M1 store; tests verify `maxRecords = 0` returns empty |
| Backpressure | The capture sink returns `BackpressureRejected` and never blocks the caller | `AgentTraceCaptureSink` uses `TryEnqueue` only; the agent event pipeline is not delayed |
| Composition override | `AgentTraceCaptureLimits` is registered as a singleton with the default values | Composition root can override for tests or future settings without touching the sink |

---

## 8. Workspace isolation and M1 record ownership

- Every capture request carries an `AgentDurableWorkspaceStorageKey`
  derived from the active workspace root (or the unbound `ws:unbound`
  key when none is provided). The M1 store uses the key as the partition
  path, so different workspaces never share a partition.
- The M1 Trace record class is the only durable sink. No additional
  file format, database, or external service is introduced.
- `ApplicationShutdown` disposes the bounded capture queue before the
  M1 store, so any pending M1 `Append` calls land before the store
  closes.
- `Phase21TraceRatchetTests.TraceCapture_FilesDoNotWriteConversationStore`
  and `TracePipelineFiles_DoNotReferenceConversationPersistencePath`
  enforce that no trace file references the conversation store or its
  path resolver.

---

## 9. Tests and ratchets

### 9.1 Test files

| Test file | Surface |
|-----------|---------|
| `tests/Zaide.Tests/Features/Agents/Transparency/Trace/Phase21TraceTestSupport.cs` | Shared fixtures (temp workspace, store, queue, sink, coordinator, requests) |
| `tests/Zaide.Tests/Features/Agents/Transparency/Trace/Phase21RedactionTests.cs` | Redaction patterns, UTF-8 BOM strip, fail-closed behavior, capture state transitions |
| `tests/Zaide.Tests/Features/Agents/Transparency/Trace/Phase21TraceLifecycleTests.cs` | Capture pipeline: disabled, captured, redacted, truncated, failed, unavailable, backpressure, ordering, scope references, class separation |
| `tests/Zaide.Tests/Features/Agents/Transparency/Trace/Phase21TraceBackendAdapterTests.cs` | Native Harness and ACP sources: registered kinds, neutral serialization, redaction before persistence, registry lookup, unavailable marker |
| `tests/Zaide.Tests/Architecture/Phase21TraceRatchetTests.cs` | Mandatory redaction, bounded queue, backend-private isolation, no conversation coupling, M1 Trace class routing, no root `Infrastructure/` admission |

### 9.2 Architecture and ratchet updates

- `ArchitectureInventoryReader.M0TotalTopLevelTypes`: `820 → 854` (+34 trace evidence types).
- `ArchitectureInventoryReader.M0InternalTopLevelTypes`: `469 → 503`.
- `PublicProductionTypeBaseline.TotalTopLevelTypes` and
  `InternalTopLevelTypes` aligned to the same values.
- `ArchitectureInventoryTests` adds per-namespace expectations for
  `Zaide.Features.Agents.Domain.Transparency.Trace (10, 0, 10)`,
  `Zaide.Features.Agents.Contracts.Transparency.Trace (4, 0, 4)`,
  `Zaide.Features.Agents.Application.Transparency.Trace (17, 0, 17)`,
  `Zaide.Features.Agents.Presentation.Transparency (3, 0, 3)`, and
  raises the source file count to `741` and Features folder count to
  `696` (+25 M2 production files).
- Phase 17, 19, 20 adversarial closeout tests now read
  `ArchitectureInventoryReader.M0TotalTopLevelTypes` /
  `M0PublicTopLevelTypes` / `M0InternalTopLevelTypes` so future
  milestones only update the reader constants.

### 9.3 M2 verification gates (exact commands)

```bash
git diff --cached --check
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase21Trace|FullyQualifiedName~Phase21Redaction|FullyQualifiedName~Phase21TraceLifecycle"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
git diff --check
```

The M2 verification suite discovers `55` M2 trace/redaction/lifecycle
tests and `59` architecture tests, all passing with zero failures.

---

## 10. Required behavior checklist (M2 spec)

| Required behavior | M2 evidence |
|-------------------|-------------|
| Redact before retention, rendering, export, logging, indexing, backup, and cross-process transfer | `AgentTraceRedactionProcessor.Apply` runs before any M1 append; the typed envelope wrap is the only value persisted; the M1 store retains redacted payloads only |
| Fail closed when redaction fails | `AgentTraceRedactionProcessor` returns `Failed` on any exception; the sink records `RedactionFailed` status and `Failed` capture state with a bounded failure marker; the original payload is never admitted |
| Bound payload size and queue/backpressure | `AgentTraceCaptureLimits` enforces `MaxPayloadBytes` and `MaxQueueDepth`; `AgentTraceBoundedCaptureQueue.TryEnqueue` returns false when full and increments `DroppedCount`; the sink returns `BackpressureRejected` and never blocks the caller |
| Explicit capture states (disabled, unavailable, captured, redacted, sampled, truncated, summarized, failed) | `AgentTraceCaptureState` enum; `sampled` and `summarized` are reserved for later evidence layers and never aliased |
| Preserve truthful backend evidence levels and unavailable states | `AgentTraceEvidenceLevel` enum (ZaideExecuted, ZaideMediated, BackendExecutedAndReported, ExternallyObserved, Unobservable); ACP source hashes the body and the sink records `Unavailable` for backend-private evidence; missing evidence is never marked captured or redacted |
| Durable security audit independent from optional trace capture | The M1 Audit record class remains separate from Trace; M2 does not write to Audit; capture is enabled by composition and disabled by default for tests that do not call `EnableCapture()` |
| Do not claim hidden reasoning or chain-of-thought | No code path writes hidden reasoning. The redacted payload is a literal post-redaction string; the inspection summary returns counts and timestamps only; no comment in the M2 surface claims hidden reasoning |
| Display settings must not change provider/model context | The display projection is a read-only consumer of the capture pipeline; no display toggle reaches `AgentSessionService` or any backend transport. The M2 spec forbids settings-window redesign |
| Preserve workspace isolation and M1 record ownership | The capture sink uses the M1 workspace storage key for every append; `Phase21StorageOwnershipRatchetTests` continues to assert Agents-only storage and no conversation-path coupling |
| Preserve Native Harness and ACP as independent sibling backends | The source registry registers both adapters independently; the registry filter is the only gate; no backend is collapsed into the other; `IAgentTraceBackendEvidenceSource` is the only contract both backends share |

---

## 11. M2 limitations preserved

- No hidden reasoning or chain-of-thought is promised or retained.
- Capture is not silently enabled by display settings; it is enabled by
  composition. M3+ may add a settings-controlled toggle, but that is
  not authorized by M2.
- The M2 pipeline does not perform sampling or summarization. The
  capture states are reserved but not produced.
- Export, backup, and migration are M1-only; M2 does not add new
  export, backup, or migration APIs for trace records.
- Backend adapters are admitted in composition; the M2 implementation
  does not yet wire Native Harness and ACP execution paths to push
  evidence. That wiring is the responsibility of the future
  continuity/inspection milestones and is not authorized by M2.
- The M2 surface does not change the visual design, the existing
  settings window, the conversation projection, or the agent panel
  layout. The presentation seam exposes availability and an entry
  point; rendering work is deferred.
- Cross-workspace trace sharing is not implemented; trace records are
  isolated by the M1 workspace storage key.
- Encryption at rest is not selected at M2.
- The legacy allowlist is unchanged; no new LocatorSite or root
  admission is admitted by M2.

---

## 12. Rollback

If M2 must be reverted:

1. Disable trace capture at the composition root (no submissions reach
   the sink).
2. Flush and dispose the `AgentTraceBoundedCaptureQueue` before
   disposing the M1 store.
3. Revert the single M2 commit.
4. Remove only M2 trace records through the M1 deletion path
   (the durable record store retains the partition under
   `agents-durable/{wsKey}/records/Trace/`). Audit, usage, recovery,
   and memory records remain untouched.
5. Conversation persistence, settings, and audit evidence are
   unaffected.

---

## 13. Exit

Redacted trace evidence is inspectable for supported backends and
truthfully unavailable otherwise. No unredacted retained path exists.
The M2 contract, the M1 store, the Agents and Townhall presentation,
and the architecture ratchets are coherent. M3–M7 remain not started
and not authorized.
