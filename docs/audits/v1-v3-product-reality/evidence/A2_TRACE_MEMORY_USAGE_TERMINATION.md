# A2 Wiring Audit — `A2_TRACE_MEMORY_USAGE_TERMINATION`

**Audit name:** `v1-v3-product-reality`
**Slice:** `A2_TRACE_MEMORY_USAGE_TERMINATION` (third A2 slice; prior:
`A2_AGENT_SEND`, `A2_MULTI_AGENT_ROUTING`)
**Evidence date:** 2026-07-31
**Baseline:** branch `master`, HEAD
`ea060644d34d9a3f33f9f1d45b9edb70a513240b` (matches `origin/master`)
**Method:** read-only source inspection (`rg`, file reads). No app launch,
build, test execution, production-code edits, commits, or pushes.

---

## 1. Audit identity, baseline, and safety boundary

| Item | Value |
|------|-------|
| Audit | `v1-v3-product-reality` (see [AUDIT_PLAN.md](../AUDIT_PLAN.md)) |
| Slice name | `A2_TRACE_MEMORY_USAGE_TERMINATION` |
| Prior A2 slices | [A2_AGENT_SEND.md](./A2_AGENT_SEND.md), [A2_MULTI_AGENT_ROUTING.md](./A2_MULTI_AGENT_ROUTING.md) |
| Goal rows to verdict | `A1-TC-02`, `A1-TC-03`, `A1-TC-08`, `A1-TC-09` (per [GOAL_MATRIX.md §14](../GOAL_MATRIX.md#14-trace-context-memory-persistence-restart-and-recovery)) |
| Scoped disposition row | `A1-XX-03` (per [GOAL_MATRIX.md §15](../GOAL_MATRIX.md#15-promises-that-cannot-yet-be-translated-into-user-behavior)) — **not** a user-goal verdict |
| Phase 21 source documents | [IMPLEMENTATION_PLAN.md](../../../phases/v3/phase-21/IMPLEMENTATION_PLAN.md), [M2](../../../phases/v3/phase-21/M2_TRACE_REDACTION_AND_RETENTION_EVIDENCE.md), [M3](../../../phases/v3/phase-21/M3_USAGE_AND_COST_EVIDENCE.md), [M4](../../../phases/v3/phase-21/M4_RESTART_AND_TERMINATION_EVIDENCE.md), [M5](../../../phases/v3/phase-21/M5_MEMORY_LIFECYCLE_EVIDENCE.md), [M6 memory influence](../../../phases/v3/phase-21/M6_MEMORY_INFLUENCE_EVIDENCE.md), [M6 Townhall accessibility](../../../phases/v3/phase-21/M6_TOWNHALL_ACCESSIBILITY_EVIDENCE.md), [M7 closeout](../../../phases/v3/phase-21/M7_CLOSEOUT_EVIDENCE.md) |
| Verdict categories | `Wired`, `Wired-with-gap`, `Missing`, `Ambiguous` (per [AUDIT_PLAN.md §2](../AUDIT_PLAN.md#2-audit-phase-decomposition)) |
| Method constraint | Inspection only; no production-code edits, no test edits, no app launch, no build, no test execution, no A3 smoke, no external backend, no commit or push |

### Baseline and safety confirmation

| Check | Result |
|-------|--------|
| Branch | `master` |
| `git rev-parse HEAD` | `ea060644d34d9a3f33f9f1d45b9edb70a513240b` |
| `git rev-parse origin/master` | `ea060644d34d9a3f33f9f1d45b9edb70a513240b` |
| Working tree at start | Clean (`## master...origin/master`) |
| Production code modified | No |
| Tests modified | No |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` modified | No |
| Real user profile read/written | No |
| App launched | No |
| Build or tests run | No |
| External backend / A3 smoke | No |

---

## 2. Sources inspected

### 2.1 Documentation

- [AGENTS.md](../../../../AGENTS.md), [docs-rules.md](../../../../docs-rules.md)
- [AUDIT_PLAN.md](../AUDIT_PLAN.md), [GOAL_MATRIX.md](../GOAL_MATRIX.md)
- Prior A2: [A2_AGENT_SEND.md](./A2_AGENT_SEND.md), [A2_MULTI_AGENT_ROUTING.md](./A2_MULTI_AGENT_ROUTING.md)
- Phase 21 plan and M2–M7 evidence files listed in §1

### 2.2 Production source (minimum named targets plus DI / callers)

**Trace**

- [AgentTraceCaptureSink.cs](../../../../src/Features/Agents/Application/Transparency/Trace/AgentTraceCaptureSink.cs)
- [AgentTraceCoordinator.cs](../../../../src/Features/Agents/Application/Transparency/Trace/AgentTraceCoordinator.cs)
- [AgentTraceInspector.cs](../../../../src/Features/Agents/Application/Transparency/Trace/AgentTraceInspector.cs)
- [AgentTraceBackendEvidenceSourceWriter.cs](../../../../src/Features/Agents/Application/Transparency/Trace/AgentTraceBackendEvidenceSourceWriter.cs)
- [NativeHarnessAgentTraceSource.cs](../../../../src/Features/Agents/Application/Transparency/Trace/NativeHarnessAgentTraceSource.cs)
- [AcpAgentTraceSource.cs](../../../../src/Features/Agents/Application/Transparency/Trace/AcpAgentTraceSource.cs)
- [AgentTraceRedactionProcessor.cs](../../../../src/Features/Agents/Application/Transparency/Trace/AgentTraceRedactionProcessor.cs)
- [AgentTraceInspectionViewModel.cs](../../../../src/Features/Agents/Presentation/Transparency/AgentTraceInspectionViewModel.cs)
- [AgentTraceAvailabilityProjection.cs](../../../../src/Features/Agents/Presentation/Transparency/AgentTraceAvailabilityProjection.cs)

**Memory**

- [AgentMemoryCoordinator.cs](../../../../src/Features/Agents/Application/Memory/AgentMemoryCoordinator.cs)
- [AgentMemoryLifecycleService.cs](../../../../src/Features/Agents/Application/Memory/AgentMemoryLifecycleService.cs)
- [AgentMemoryInspector.cs](../../../../src/Features/Agents/Application/Memory/AgentMemoryInspector.cs)
- [AgentMemoryStoreWriter.cs](../../../../src/Features/Agents/Application/Memory/AgentMemoryStoreWriter.cs)
- [AgentMemoryRetriever.cs](../../../../src/Features/Agents/Application/Memory/AgentMemoryRetriever.cs)
- [AgentMemoryInfluenceRecorder.cs](../../../../src/Features/Agents/Application/Memory/AgentMemoryInfluenceRecorder.cs)
- [AgentMemoryInspectionViewModel.cs](../../../../src/Features/Agents/Presentation/Memory/AgentMemoryInspectionViewModel.cs)
- [AgentMemoryAvailabilityProjection.cs](../../../../src/Features/Agents/Presentation/Memory/AgentMemoryAvailabilityProjection.cs)
- [AgentMemoryScope.cs](../../../../src/Features/Agents/Domain/Transparency/Memory/AgentMemoryScope.cs)

**Usage / cost**

- [AgentUsageCaptureSink.cs](../../../../src/Features/Agents/Application/Transparency/Usage/AgentUsageCaptureSink.cs)
- [AgentUsageCoordinator.cs](../../../../src/Features/Agents/Application/Transparency/Usage/AgentUsageCoordinator.cs)
- [AgentUsageInspector.cs](../../../../src/Features/Agents/Application/Transparency/Usage/AgentUsageInspector.cs)
- [AgentUsageBackendEvidenceSourceWriter.cs](../../../../src/Features/Agents/Application/Transparency/Usage/AgentUsageBackendEvidenceSourceWriter.cs)
- [NativeHarnessAgentUsageSource.cs](../../../../src/Features/Agents/Application/Transparency/Usage/NativeHarnessAgentUsageSource.cs)
- [AcpAgentUsageSource.cs](../../../../src/Features/Agents/Application/Transparency/Usage/AcpAgentUsageSource.cs)
- [AgentUsageInspectionViewModel.cs](../../../../src/Features/Agents/Presentation/Transparency/AgentUsageInspectionViewModel.cs)
- [AgentUsageAvailabilityProjection.cs](../../../../src/Features/Agents/Presentation/Transparency/AgentUsageAvailabilityProjection.cs)

**Termination / continuity**

- [IAgentSessionService.cs](../../../../src/Features/Agents/Contracts/IAgentSessionService.cs)
- [AgentSessionService.cs](../../../../src/Features/Agents/Application/AgentSessionService.cs) (`EndAsync`, `CancelAsync`, continuity terminate delegate, memory retrieval/influence hooks)
- [AgentSessionContinuityCoordinator.cs](../../../../src/Features/Agents/Application/Continuity/AgentSessionContinuityCoordinator.cs)
- [AgentSessionContinuityInspectionViewModel.cs](../../../../src/Features/Agents/Presentation/Transparency/AgentSessionContinuityInspectionViewModel.cs)
- [AgentExecutionCoordinator.cs](../../../../src/Features/Agents/Application/AgentExecutionCoordinator.cs)
- [App.axaml.cs](../../../../src/App/Composition/App.axaml.cs) (startup continuity reconcile)
- [ApplicationShutdown.cs](../../../../src/App/Composition/ApplicationShutdown.cs) (shutdown checkpoint / queue drain)

**Integrated management + DI + shell**

- [AgentTransparencyManagementViewModel.cs](../../../../src/Features/Agents/Presentation/Transparency/AgentTransparencyManagementViewModel.cs)
- [AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs)
- [TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs), [TownhallView.cs](../../../../src/Features/Townhall/Presentation/TownhallView.cs)
- [AgentBackendBindingPanel.cs](../../../../src/Features/Agents/Presentation/AgentBackendBindingPanel.cs)
- Capability rows: [AcpCapabilityRows.cs](../../../../src/Features/Agents/Domain/AcpCapabilityRows.cs), [NativeHarnessCapabilityRows.cs](../../../../src/Features/Agents/Domain/NativeHarnessCapabilityRows.cs)

### 2.3 Tests (corroboration only; not proof of user wiring)

- Trace: `tests/Zaide.Tests/Features/Agents/Transparency/Trace/*`
- Usage: `tests/Zaide.Tests/Features/Agents/Transparency/Usage/*`
- Continuity: `tests/Zaide.Tests/Features/Agents/Continuity/*`
- Memory store/retrieval: `tests/Zaide.Tests/Features/Agents/Memory/*`
- Townhall accessibility constants: [Phase21TownhallAccessibilityTests.cs](../../../../tests/Zaide.Tests/Features/Townhall/Presentation/Phase21TownhallAccessibilityTests.cs)
- Architecture ratchets: `tests/Zaide.Tests/Architecture/Phase21*RatchetTests.cs`

---

## 3. Four-row verdict table

| id | a2_wiring_verdict | Summary |
|----|-------------------|---------|
| `A1-TC-02` | **Missing** | Neutral redacted-trace store, coordinator, inspector, backend adapter types, and inspection ViewModels are registered, but **no production backend produces trace records**, **capture remains disabled** unless a non-production caller enables it, and **no production View, command, or shell entry point** lets a user open or inspect raw traces. Phase 21 M2 itself labels backend → sink as `[future wiring]`. |
| `A1-TC-03` | **Missing** | Durable memory CRUD/lifecycle, scopes, retrieval, and influence recording exist as application services. Production `AgentSessionService` invokes retrieval and records memory-influence payloads during context assembly when DI services are available, but **no production path creates user-managed lifecycle memory**, and **no production View, command, or shell entry point** exposes list/inspect/create/correct/disable/supersede/delete or influence inspection. Storage/influence capability is not a management surface; the documented user management journey remains absent. |
| `A1-TC-08` | **Missing** | Usage ledger, origin/currency/pricing fields, zero-cost guard, and inspection ViewModels are registered, but **neither production backend writes usage/cost into the ledger**, **capture stays disabled** by default, and **no production UI** shows usage/cost for a session/run/backend. ACP may mark `UsageReporting` observed from `usage_update`; that is capability observation only, not ledger admission. Backend-reported cost is never a Zaide-verified billing fact. |
| `A1-TC-09` | **Missing** | `IAgentSessionService.EndAsync` and continuity `Terminate` APIs exist and are tested, but **no production View/command invokes either** for an active user-driven end. Townhall has no end/stop/cancel gesture. Continuity terminate targets **interrupted continuity records**, not an interactive end of a live session/run. Caller-CTS cancellation in the coordinator is not an explicit user terminate control. |

Verdict definitions: [AUDIT_PLAN.md §2](../AUDIT_PLAN.md#2-audit-phase-decomposition).

---

## 4. User-entry-point reachability matrix

| User-visible entry point | Trace inspect | Memory manage | Usage/cost view | Explicit end session/run | Evidence |
|--------------------------|---------------|---------------|-----------------|--------------------------|----------|
| Townhall direct conversation | **No** | **No** | **No** | **No** | [TownhallViewModel.SendMessageAsync](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L433–497: send only; no end/cancel/inspect commands |
| Townhall channel | **No** | **No** | **No** | **No** | Channel branch logs activity only (L441–450) |
| People panel / open DM | **No** (navigation only) | **No** | **No** | **No** | Opens direct conversation; no transparency chrome |
| Backend binding status panel | **No** | **No** | **No** | **No** | Read-only status only ([AgentBackendBindingPanel.cs](../../../../src/Features/Agents/Presentation/AgentBackendBindingPanel.cs) L11–66) |
| Command Palette / command registry | **No** | **No** | **No** | **No** | No `trace.*`, `usage.*`, `memory.*`, or `session.end` command ids under `src/` shell/command registration |
| Settings surface | **No** | **No** | **No** | **No** | Phase 21 M6 explicitly avoided a dedicated settings window ([M6 Townhall accessibility §6](../../../phases/v3/phase-21/M6_TOWNHALL_ACCESSIBILITY_EVIDENCE.md)) |
| `AgentTransparencyManagementViewModel` | **API only** | **API only** | **API only** | Continuity terminate API only | Registered in DI ([AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs) L181); **no production View binds it**; **no production caller** of `GetRequiredService<AgentTransparencyManagementViewModel>()` |
| Production Avalonia View for transparency/memory | **Absent** | **Absent** | **Absent** | **Absent** | No `*Trace*View*`, `*Usage*View*`, `*Memory*View*`, or `*Continuity*View*` under `src/`; `Presentation/Transparency/` is ViewModels only |

**Conclusion:** the documented user entry points (“Inspect a trace”, “Manage memory records”, “View usage/cost”, “End the active session”) have **no reachable production shell surface**.

---

## 5. Production DI registration versus actual caller table

Legend: **R** = registered in production DI · **C** = called by a production non-test path · **U** = user-reachable · **P** = result projected to visible UI.

| Service / type | R | C | U | P | Notes |
|----------------|---|---|---|---|-------|
| `IAgentDurableRecordStore` / file store | ✓ | ✓ (store ops when invoked) | — | — | Registered L118–119 |
| `AgentTraceCaptureSink` / `AgentTraceCoordinator` / `IAgentTraceInspector` | ✓ | **No production producer** | no | no | L129–133; capture default **disabled** |
| `NativeHarnessAgentTraceSource` / `AcpAgentTraceSource` | ✓ | **No production caller of `Submit`** | no | no | L135–136; adapters only |
| `AgentTraceInspectionViewModel` / availability projection | ✓ | only if management VM constructed | no | no | L137–138; never resolved from shell |
| `AgentUsageCaptureSink` / coordinator / inspector | ✓ | **No production producer** | no | no | L141–143; capture default **disabled** |
| `NativeHarnessAgentUsageSource` / `AcpAgentUsageSource` | ✓ | **No production caller of `Submit`** | no | no | L145–146 |
| `AgentUsageInspectionViewModel` | ✓ | no shell caller | no | no | L148 |
| Continuity checkpoint/writer/inspector/coordinator | ✓ | **Yes** (startup reconcile, event subscriber, shutdown checkpoint) | no end UI | partial (no UI) | L151–159; App startup L93–95; shutdown L80–85 |
| `AgentSessionContinuityInspectionViewModel` | ✓ | no shell caller | no | no | L161 |
| `IAgentSessionService` / `EndAsync` | ✓ | **`EndAsync` has zero production callers** | no | Run terminal events only if admitted | L162; `EndAsync` L270 |
| Memory store/coordinator/lifecycle/inspector | ✓ | **Create/CRUD only if caller invokes** | no | no | L165–173 |
| `IAgentMemoryRetrievalService` / influence recorder | ✓ | **Yes** — production `AgentSessionService` invokes retrieval during context assembly when DI-provided services are available, then records influence via `AgentMemoryInfluenceRecorder` | no influence UI | no | L175–178; session hooks L1313–1391; states `Recorded` / `NoneEligible` / `Unavailable` |
| `AgentTransparencyManagementViewModel` | ✓ | **no production resolution** | no | no | L181 |
| `AgentSessionContinuityStartupReconciler` | ✓ | ✓ App startup | n/a | no UI | [App.axaml.cs](../../../../src/App/Composition/App.axaml.cs) L93–94 |
| `AgentSessionContinuityEventSubscriber` | ✓ | ✓ App startup | n/a | no UI | L95 |

`EnableCapture` for trace and usage exists only on coordinators/sinks. Repository-wide `rg` shows **no production caller** of `EnableCapture` outside tests. Default capture counters start at zero → submissions return **Disabled**.

---

## 6. Trace producer → storage → inspector → UI trace

```text
[intended]
Backend (Native Harness / ACP)
  → IAgentTraceBackendEvidenceSource.Submit
  → AgentTraceBackendEvidenceSourceWriter
  → AgentTraceCoordinator.TrySubmit
  → AgentTraceCaptureSink.TrySubmit
      → registry filter
      → AgentTraceRedactionProcessor (mandatory; fail-closed)
      → AgentTraceBoundedCaptureQueue
      → IAgentDurableRecordStore (Trace class)
  → IAgentTraceInspector / AgentTraceInspectionViewModel
  → Townhall / management View  [MISSING]

[actual production]
No NativeHarness / ACP infrastructure caller constructs or submits
AgentTraceCaptureRequest. Capture remains disabled. Inspection VMs are
registered but unbound. User cannot open a trace.
```

| Layer | Status | Evidence |
|-------|--------|----------|
| 1. Contract/model | Present | Domain types under `Domain/Transparency/Trace/`; `IAgentTraceCaptureSink`, `IAgentTraceInspector`, `IAgentTraceBackendEvidenceSource` |
| 2. Storage / coordinator / inspector | Present | Capture sink, bounded queue, coordinator, inspector, redaction processor |
| 3. Production DI | Present | [AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs) L121–138 |
| 4. Production producer | **Absent** | No references to `NativeHarnessAgentTraceSource` / `AcpAgentTraceSource` / `SerializeLoopHistoryTurn` / `SerializeProtocolFrame` outside Transparency and tests. M2 pipeline diagram marks backend evidence as `[future wiring]` ([M2 §2](../../../phases/v3/phase-21/M2_TRACE_REDACTION_AND_RETENTION_EVIDENCE.md)) |
| 5. Production View / command | **Absent** | ViewModel only ([AgentTraceInspectionViewModel.cs](../../../../src/Features/Agents/Presentation/Transparency/AgentTraceInspectionViewModel.cs)); no Avalonia View |
| 6. Shell reachability | **Absent** | See §4 |
| 7. Visible success/unavailable states | **Not user-visible** | Capture states (`Disabled`, `Unavailable`, `Captured`, `Redacted`, `Truncated`, `Failed`) exist in sink ([AgentTraceCaptureSink.cs](../../../../src/Features/Agents/Application/Transparency/Trace/AgentTraceCaptureSink.cs) L66–120); availability caption exists ([AgentTraceAvailabilityState.cs](../../../../src/Features/Agents/Presentation/Transparency/AgentTraceAvailabilityState.cs) L43–46) but nothing renders them |

**Redaction:** when a submission is admitted, redaction runs **before** queue/persist ([AgentTraceCaptureSink.cs](../../../../src/Features/Agents/Application/Transparency/Trace/AgentTraceCaptureSink.cs) L103–111). Unavailable markers bypass redaction with a constant bounded payload (L86–94). That contract is real for the store path; it is unused by production backends.

**“Missing evidence is unavailable, not zero”:** unavailable markers and capture states are modeled. Because no user surface projects them, the user never sees the distinction.

**Native Harness / ACP capability truth for raw trace:**

- ACP advertises `RawTrace` as **NotSupported** across all six facts ([AcpCapabilityRows.CreateRawTraceRow](../../../../src/Features/Agents/Domain/AcpCapabilityRows.cs) L135–144).
- Native Harness capability snapshot does **not** include a `RawTrace` row at all ([NativeHarnessCapabilityRows.CreateInitialSnapshot](../../../../src/Features/Agents/Domain/NativeHarnessCapabilityRows.cs) L17–28).

---

## 7. Usage producer → storage → inspector → UI trace

```text
[intended]
Backend (Native Harness / ACP)
  → IAgentUsageBackendEvidenceSource.Submit
  → AgentUsageBackendEvidenceSourceWriter
  → AgentUsageCoordinator.TrySubmit
  → AgentUsageCaptureSink.TrySubmit
      → disabled guard
      → zero-cost guard (cost @ 0 with non-Unavailable origin rejected)
      → typed Usage envelope → IAgentDurableRecordStore
  → IAgentUsageInspector / AgentUsageInspectionViewModel
  → Townhall / management View  [MISSING]

[actual production]
No backend writes AgentUsageCaptureRequest. Capture disabled by default.
ACP may observe usage_update and flip UsageReporting capability only.
User cannot open usage/cost details.
```

| Layer | Status | Evidence |
|-------|--------|----------|
| 1. Contract/model | Present | `AgentUsageKind`, `AgentUsageValueOrigin`, units/currency/pricing fields on capture request and payload |
| 2. Storage / coordinator / inspector | Present | Sink, coordinator, inspector; empty summary uses `isEmpty: true` ([AgentUsageInspectionSummary.Empty](../../../../src/Features/Agents/Domain/Transparency/Usage/AgentUsageInspectionSummary.cs)) |
| 3. Production DI | Present | L140–148 |
| 4. Production producer | **Absent** | Usage sources registered but never called from backends. M3 pipeline marks `[future wiring]` ([M3 §2](../../../phases/v3/phase-21/M3_USAGE_AND_COST_EVIDENCE.md)). ACP `usage_update` updates capability observation ([AcpCapabilitySnapshotMapper](../../../../src/Features/Agents/Application/Acp/AcpCapabilitySnapshotMapper.cs); session adapter usageObserved flags) — **not** the usage ledger |
| 5. Production View / command | **Absent** | ViewModel only |
| 6. Shell reachability | **Absent** | See §4 |
| 7. Visible unavailable vs zero | Modeled, not user-visible | Zero-cost guard rejects cost kinds with value 0 and non-`Unavailable` origin ([AgentUsageCaptureSink.cs](../../../../src/Features/Agents/Application/Transparency/Usage/AgentUsageCaptureSink.cs) L56–71). Caption uses “cost unavailable” when currency is null ([AgentUsageAvailabilityState.FormatStatusCaption](../../../../src/Features/Agents/Presentation/Transparency/AgentUsageAvailabilityState.cs) L37–47). No UI binds these captions |

**Billing caution:** even if a future backend submitted `EstimatedCost` / `InvoicedCost` with origin `Reported`, that remains backend-reported evidence, not a Zaide-verified billing fact (per Phase 21 and [A1-TC-08](../GOAL_MATRIX.md#14-trace-context-memory-persistence-restart-and-recovery)).

**Native Harness / ACP capability truth for usage:**

- Native Harness snapshot has **no** `UsageReporting` row.
- ACP can mark `UsageReporting` currently usable after a valid `usage_update` observation; still **no** write into `AgentUsageCaptureSink`.

---

## 8. Memory lifecycle/producer → storage → management UI trace

```text
[store / lifecycle capability — exists; no production Create caller]
AgentMemoryCoordinator.Create/Correct/Disable/Supersede/Delete
  → AgentMemoryStoreWriter → IAgentDurableRecordStore (Memory class)
AgentMemoryInspector list/summary
AgentMemoryLifecycleService export/backup partition helpers
Scopes: Session, Agent, Conversation, ProjectShared
  (matrix language "Shared" ≈ ProjectShared)

[retrieval / influence — production path on session context assembly]
AgentSessionService context assembly (when DI-provided services available)
  → IAgentMemoryRetrievalService.Retrieve
  → AgentContextManifestBuilder.AppendMemoryCandidates
  → AgentMemoryInfluenceRecorder.RecordInfluence
      states: Recorded | NoneEligible | Unavailable
      durable payloads under AgentDurableRecordClass.Memory
      (NOT AgentMemoryPayload lifecycle records; no MemoryId;
       AgentMemoryProjectionEngine skips them during AgentMemoryRecord projection)

[user management surface — MISSING]
AgentMemoryInspectionViewModel CRUD methods exist
  → no Avalonia View, no Townhall binding, no command
  → AgentTransparencyManagementViewModel holds the VM but is unreachable
  → no production UI exposes lifecycle CRUD or influence inspection
```

| User-facing capability | Production storage/API | User-reachable? |
|------------------------|------------------------|-----------------|
| List memory records | `AgentMemoryInspector.GetRecords` / ViewModel `LoadRecordsAsync` | **No** |
| Inspect provenance and scope | record model + inspector | **No** |
| Create | `AgentMemoryCoordinator.Create` / ViewModel `CreateAsync` | **No** production caller |
| Correct / edit | `Correct` / `CorrectAsync` | **No** |
| Disable | `Disable` / `DisableAsync` | **No** |
| Supersede | `Supersede` / `SupersedeAsync` | **No** |
| Delete | `Delete` / `DeleteAsync` | **No** |
| Choose Session / Agent / Shared / Conversation | `AgentMemoryScope` enum (`ProjectShared` = shared) | API only |
| Observe whether memory influenced a run | influence recorder + durable Memory-class influence payloads | **No UI**; production `AgentSessionService` does record influence during context assembly when DI services are available |

**Keep distinct — two memory seams:**

1. **Memory-influence durable payloads** (production path present):
   - Production `AgentSessionService` invokes memory retrieval during context assembly when the DI-provided services are available.
   - It records a memory-influence durable payload through `AgentMemoryInfluenceRecorder`.
   - Recorded states:
     - `Recorded` when eligible revisions influence the manifest
     - `NoneEligible` when retrieval succeeds but no revision is used
     - `Unavailable` when retrieval/configuration fails
   - Influence recording does **not** require eligible memory records.
   - `AgentMemoryInfluenceRecorder` appends these payloads under `AgentDurableRecordClass.Memory`.
   - These are **not** `AgentMemoryPayload` lifecycle records; they contain **no** `MemoryId`; `AgentMemoryProjectionEngine` skips them during `AgentMemoryRecord` projection.

2. **User-managed lifecycle `AgentMemoryRecord` records** (production path absent):
   - No production caller invokes `AgentMemoryCoordinator.Create` to create a user-managed lifecycle memory record.
   - No production UI exposes lifecycle CRUD or influence inspection.

Durable store + coordinator capability is **not** user-facing management. Phase 21 M5 is explicitly store-focused ([M5 §5](../../../phases/v3/phase-21/M5_MEMORY_LIFECYCLE_EVIDENCE.md)); M6 adds retrieval/influence and a management **ViewModel**, not a bound View ([M6 Townhall accessibility §4–§6](../../../phases/v3/phase-21/M6_TOWNHALL_ACCESSIBILITY_EVIDENCE.md)).

---

## 9. Active-session termination → terminal event → visible feedback trace

Do **not** conflate the following (all distinct in code):

| Operation | What it is | Production user reachability |
|-----------|------------|------------------------------|
| **`IAgentSessionService.EndAsync`** | Cancels active run if needed, waits for execution task, emits session ending/ended path, **removes live ownership** ([AgentSessionService.cs](../../../../src/Features/Agents/Application/AgentSessionService.cs) L270–348) | **No caller** in `src/` outside the method definition |
| **`CancelAsync`** | Requests cancellation of active run; does not by itself remove the session ([L226–268](../../../../src/Features/Agents/Application/AgentSessionService.cs)) | Called from `AgentExecutionCoordinator.HandleCallerCancellationAsync` when the **send** `CancellationToken` cancels ([AgentExecutionCoordinator.cs](../../../../src/Features/Agents/Application/AgentExecutionCoordinator.cs) L207–210). Townhall send does **not** expose a user cancel CTS |
| **Continuity `Terminate`** | Records termination of an **interrupted continuity** checkpoint/record via `AgentSessionContinuityCoordinator.Terminate` ([L212+](../../../../src/Features/Agents/Application/Continuity/AgentSessionContinuityCoordinator.cs)); session service exposes `TerminateInterruptedSession` (L409–417) | Inspection ViewModel wraps it; **no UI** invokes it |
| **Disconnect / late completion** | Representable in continuity checkpoints and run statuses | Not an explicit end control |
| **Archive / delete / recovery** | Continuity operation taxonomy and durable records | Recovery reconcile runs at startup without auto side-effect resume; not “end active session” |
| **In-flight bookkeeping `TryEndInFlightAsync`** | Clears coordinator busy ownership after a send finishes ([L517+](../../../../src/Features/Agents/Application/AgentExecutionCoordinator.cs)) | Internal; not session EndAsync |

**Terminal event → conversation projection:**

- Admitted terminal run failures including `RunCancelled` project via `ProjectRunTerminalFailure` ([AgentConversationEventProjection.cs](../../../../src/Features/Agents/Application/AgentConversationEventProjection.cs) L123–128).
- `SessionEnding` / `SessionEnded` are **not** handled in the projection switch (L111–139): no conversation entry solely for session-end lifecycle.
- Townhall shows cancelled admitted runs as agent-error style activity **if** a run was admitted and cancelled through the event path — still not an explicit “End session” affordance with confirmation chrome.

**User can explicitly end and confirm terminal state?** **No.** There is no production button, menu item, command, or Townhall control that calls `EndAsync` or continuity `Terminate`.

---

## 10. Registered-but-unused / dead-seam findings

| Seam | Registered | Dead for user product path? | Detail |
|------|------------|-----------------------------|--------|
| Trace capture pipeline | Yes | **Yes** | No producer; capture disabled |
| Trace backend adapters | Yes | **Yes** | `Submit` only from tests |
| Usage capture pipeline | Yes | **Yes** | Same as trace |
| Usage backend adapters | Yes | **Yes** | Same |
| All inspection ViewModels | Yes | **Yes** | No View / shell resolution |
| `AgentTransparencyManagementViewModel` | Yes | **Yes** | Accessibility constants tested; not hosted |
| `IAgentSessionService.EndAsync` | Yes | **Yes for user path** | Zero production callers |
| Continuity Terminate / Resume inspection APIs | Yes | **Yes for user path** | Startup reconcile/event subscriber **are** live internal paths |
| Memory CRUD coordinator | Yes | **Yes for user path** | No production caller of `AgentMemoryCoordinator.Create`; no user-managed lifecycle records created automatically |
| Memory retrieval + influence | Yes | **Production internal path; not user-visible** | Production `AgentSessionService` invokes retrieval and records influence (`Recorded` / `NoneEligible` / `Unavailable`) via `AgentMemoryInfluenceRecorder` under `AgentDurableRecordClass.Memory`; no influence UI |
| Phase 21 Townhall accessibility tests | n/a | Non-proof | Assert string constants only ([Phase21TownhallAccessibilityTests.cs](../../../../tests/Zaide.Tests/Features/Townhall/Presentation/Phase21TownhallAccessibilityTests.cs)) |

---

## 11. Empty / unavailable / failure-state projection

| Concern | Modeled in code? | User-visible in current shell? |
|---------|------------------|--------------------------------|
| Trace capture disabled | Yes (`AgentTraceCaptureState.Disabled`, caption “Trace capture disabled.”) | **No** |
| Trace unavailable marker | Yes (`UnavailableMarker` kind; state `Unavailable`) | **No** |
| Trace redacted / truncated / failed / backpressure | Yes | **No** |
| Usage capture disabled | Yes | **No** |
| Usage missing cost as `Unavailable` origin (not zero) | Yes (zero-cost guard + origin enum) | **No** |
| Usage empty summary `isEmpty: true` | Yes | **No** |
| Memory “No durable memory records” caption | Yes ([AgentMemoryAvailabilityState](../../../../src/Features/Agents/Presentation/Memory/AgentMemoryAvailabilityState.cs) L55–60) | **No** |
| Memory influence (`Recorded` / `NoneEligible` / `Unavailable`) | Yes — production session records influence payloads during context assembly when DI services are available; does not require eligible records | **No UI** for influence inspection |
| Continuity interrupted counts | Yes (availability state) | **No** |
| Explicit end confirmation | **No UI** | **No** |

**Rule preserved in this audit:** absence of evidence is recorded as **unavailable**, never inferred as zero usage, zero cost, or “no trace occurred.” Production simply never surfaces the distinction.

---

## 12. A1-XX-03 scoped disposition

> **This section is a scoped disposition, not a fifth user-goal verdict.**

| Question | Disposition | Basis |
|----------|-------------|--------|
| Does Native Harness produce trace evidence into the Phase 21 store? | **No production path** | Adapter exists; no backend call to `Submit`; capture disabled |
| Does ACP produce trace evidence into the Phase 21 store? | **No production path**; capability RawTrace **NotSupported** | [AcpCapabilityRows.CreateRawTraceRow](../../../../src/Features/Agents/Domain/AcpCapabilityRows.cs) L135–144; no Submit caller |
| Does Native Harness produce usage/cost ledger evidence? | **No production path** | Adapter exists; no Submit; no UsageReporting capability row |
| Does ACP produce usage/cost ledger evidence? | **No ledger admission**; may mark UsageReporting **observed** only | Capability mapper vs unused `AcpAgentUsageSource` |
| Does production append memory-influence evidence records? | **Yes — production path present** | `AgentSessionService` invokes memory retrieval during context assembly when DI-provided services are available and records influence through `AgentMemoryInfluenceRecorder` (`Recorded` / `NoneEligible` / `Unavailable`) under `AgentDurableRecordClass.Memory`. Influence recording does not require eligible memory records. These are not `AgentMemoryPayload` lifecycle records (no `MemoryId`; projection engine skips them). |
| Does production automatically create user-managed lifecycle `AgentMemoryRecord` records? | **No** | No production caller of `AgentMemoryCoordinator.Create`; no Townhall/backend create; no user-facing memory-management surface |
| Is each inspection/management surface user-reachable? | **No** | ViewModels registered; no Views; no commands; no shell host of `AgentTransparencyManagementViewModel`; no lifecycle CRUD or influence inspection UI |
| Can a user explicitly terminate an active session/run? | **No** | `EndAsync` / continuity Terminate unused by UI |
| What remains impossible to prove without external bound backend or A3 smoke? | Execution of the production memory-influence path during a real admitted backend run; influence from a real lifecycle memory record on a real model response; visible user feedback; live token/time/cost numbers from a real provider; end-to-end redaction under real payloads | Source inspection already proves production DI registers memory retrieval and influence services and that `AgentSessionService` contains their production call path — DI injection itself is **not** unknown. Aligns with M7 “external candidate/provider smoke **Not executed**” ([M7 closeout](../../../phases/v3/phase-21/M7_CLOSEOUT_EVIDENCE.md)) and prior A2 note that backend bind UI is missing ([A2_AGENT_SEND](./A2_AGENT_SEND.md)) |

Phase 21 M0 baseline is **pre-implementation**. M2/M3/M5/M6 completed **neutral-store and management contracts**. A2 current-state answer for `A1-XX-03`: **production appends memory-influence evidence during session context assembly; production does not create user-managed lifecycle memory records or expose memory-management/influence UI; trace/usage producers and termination/user surfaces still do not deliver the documented product outcomes.**

---

## 13. Corroborating tests, clearly marked non-proof

These tests prove **internal contracts**, not user wiring:

| Area | Test locations | What they prove | What they do **not** prove |
|------|----------------|-----------------|----------------------------|
| Trace redaction / lifecycle / adapters | `tests/.../Transparency/Trace/*` | Redaction, truncation, backpressure, adapter Submit when tests call them | That a production backend or UI uses the path |
| Usage zero-cost / origins | `tests/.../Transparency/Usage/*` | Origin taxonomy, zero-cost guard, pricing fields | That users see usage |
| Continuity terminate/resume/restart | `tests/.../Continuity/*` | API semantics for interrupted records | Explicit user end of active session |
| Memory CRUD / policy / retrieval / influence | `tests/.../Memory/*` | Store and influence contracts | User management UI |
| Townhall accessibility | [Phase21TownhallAccessibilityTests.cs](../../../../tests/Zaide.Tests/Features/Townhall/Presentation/Phase21TownhallAccessibilityTests.cs) | Automation name / help text / page-size constants on the ViewModel | That Townhall hosts the ViewModel |
| Phase 21 adversarial / ratchets | `Phase21AdversarialTests`, `Phase21*RatchetTests` | Architectural boundaries | Product discoverability |

Tests that call `EnableCapture()` before Submit demonstrate that **tests** enable capture; production never does.

---

## 14. A3 clean-profile smoke constraints

If A3 later attempts smoke for these rows, constraints from this wiring audit:

1. **Disposable isolated profile only** — never the real user config or store ([AUDIT_PLAN.md §3](../AUDIT_PLAN.md#3-safety-and-isolation-rules-mandatory-for-a0a4)).
2. **Backend bind prerequisite** — production UI still cannot bind Native Harness/ACP ([A2_AGENT_SEND](./A2_AGENT_SEND.md)); A3 cannot assume a bound backend without a controlled non-UI setup or a future bind surface.
3. **Production UI has no trace or usage inspection surface**, so product-level A3 cannot observe the disabled/empty state. A lower-level authorized harness could inspect `Disabled`/`Unavailable` state, but that is **not** proof of user wiring. Absence must **never** be reported as zero trace, zero usage, or zero cost.
4. **Memory manage smoke has no UI entry** — create/list/delete cannot be exercised as a user journey without a new surface or internal harness (harness ≠ product wiring).
5. **Explicit termination smoke has no UI entry** — no end button to click; cancelling a disposable process is not `EndAsync`.
6. **External paid/provider smoke** remains separately authorized and was not executed in Phase 21 M7.
7. A3 must not treat Phase 21 unit/integration green as proof of user-visible delivery.

---

## 15. Exact next recommended A2 slice, explicitly not started

**Next recommended A2 slice (not begun in this session):**
`A2_RESTART_RECOVERY_AND_CONTEXT`

**Suggested goal rows:**

- `A1-TC-01` — live IDE context policy and attachment
- `A1-TC-04` — conversation restart restore / non-restore of sessions
- `A1-TC-05` — interrupted-run classification on restart (no silent resume)
- Scoped disposition only if needed: `A1-XX-05` (workspace isolation constraint)

**Why this next:** it completes the remaining user-goal rows in the same Phase 14/18/21 continuity-and-context neighborhood as this slice, without reopening closed A2 files, and without starting A3/A4/V4. Tools/permissions (`A1-TP-*`) remains a strong alternative after that.

**Explicitly not started here:** that slice, A3, A4, stabilization, V4, or any corrective implementation.

---

## 16. Final safety and working-tree report

| Check | Expected / observed |
|-------|---------------------|
| Production code edits | None |
| Test edits | None |
| Audit plan / goal matrix edits | None |
| Issue / deferred edits | None |
| Build / test / app / external backend | None |
| New evidence file only | `docs/audits/v1-v3-product-reality/evidence/A2_TRACE_MEMORY_USAGE_TERMINATION.md` |
| Verdict IDs in §3 table (exactly once each) | `A1-TC-02`, `A1-TC-03`, `A1-TC-08`, `A1-TC-09` |
| `A1-XX-03` | Scoped disposition in §12 only — **not** a user-goal verdict |
| Verdict terminology | `Missing` throughout for the four user-goal rows; disposition language for XX-03 |

### Closeout verdicts (repeat)

| id | verdict |
|----|---------|
| `A1-TC-02` | **Missing** |
| `A1-TC-03` | **Missing** |
| `A1-TC-08` | **Missing** |
| `A1-TC-09` | **Missing** |
| `A1-XX-03` | Scoped disposition only — production appends memory-influence evidence during context assembly; no user-managed lifecycle memory creation or memory/influence UI; trace/usage producers and termination UI still absent |

**Stop for re-audit.** No next slice started.
