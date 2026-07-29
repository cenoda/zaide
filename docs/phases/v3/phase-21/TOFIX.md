# Phase 21: Agent Transparency, Continuity, and Memory — TOFIX

## Status

**M1 is complete and published at `4db8320293bf443b6249b70fd2c42eab8d13b7a6`.**
**M2 is complete and published at `f75de2344ee57fdd59f771c7e17c9059edffcca1`.**
**M3 is complete and published at `ae920091eb931eba2f0e43be086cab7f7fecff6d`.**
**M4 is complete and published at `4b2af9f341df21cea11c99787a2de95b7be0e7f7`.**
**M5 is complete and published at `59f2050c987131a4e2c124406ac51ddce8096835`.**
**M5 final evidence/hash correction at `04aa3692ca00879e67bf2cf16de0d26d45cbcf01`.**
**M6 is complete and published at `03d6f5b42cde48c7d4093ed852676f29d308516a`.**
**M7 is not started and not authorized.**

Phase 20 remains complete, published, accepted, and unchanged. It is an
independent ACP sibling backend, not a Native Harness wrapper or fallback.

M5 adds durable scoped memory records with inspect/create/correct/disable/
supersede/delete controls over the M1 `Memory` record class. M5 does not
introduce retrieval, prompt injection, or any M6+ product behavior.

M6 adds budgeted memory retrieval with influence attribution, Phase 18
`DurableMemory` context-source integration, cross-record export/backup/restore
coordination, and Townhall management presentation bounds. M6 does not weaken
M1–M5 record ownership or begin M7 adversarial closeout.

## M6 work board

- [x] `AgentMemoryRetriever` with deterministic eligibility, ranking, and stale handling.
- [x] `AgentMemoryInfluenceRecorder` with per-run revision attribution or unavailable marker.
- [x] Phase 18 manifest integration via explicit `DurableMemory` source only.
- [x] `AgentTransparencyLifecycleCoordinator` for export/backup/restore/migrate.
- [x] `AgentTransparencyManagementViewModel` with keyboard/focus/screen-reader bounds.
- [x] Retrieval, influence, integration, townhall, and architecture ratchet tests.
- [x] Publish `M6_MEMORY_INFLUENCE_EVIDENCE.md`, `M6_RETENTION_EXPORT_DELETE_BACKUP_EVIDENCE.md`, and `M6_TOWNHALL_ACCESSIBILITY_EVIDENCE.md`.
- [x] Run M6 verification gates with zero failures.
- [x] Publish one reviewable M6 commit and post-push audit.

## M5 work board

- [x] Neutral memory contracts/domain/application/persistence under M1 ownership.
- [x] `AgentMemoryCoordinator` with create/correct/disable/supersede/delete.
- [x] `AgentMemoryInspector` replay projection and inspection summary.
- [x] `AgentMemoryPolicyEvaluator` for conflict/poisoning/stale/supersession.
- [x] `AgentMemoryLifecycleService` export/backup semantics.
- [x] Agents/Townhall presentation seam for memory inspect/control.
- [x] Focused memory store/policy/lifecycle tests and architecture ratchets.
- [x] Publish `M5_MEMORY_RECORD_AND_POLICY.md` and `M5_MEMORY_LIFECYCLE_EVIDENCE.md`.
- [x] Run M5 verification gates with zero failures.
- [x] Publish one reviewable M5 commit and post-push audit.

## Next task

M6 post-push audit complete. M7 remains not started and not authorized.
