# Phase 21: Agent Transparency, Continuity, and Memory — TOFIX

## Status

**M0–M7 complete, published, and accepted. Final closeout recorded in `docs(phase-21): accept final closeout`. External candidate/provider smoke remains not executed (separate authorization not provided). Phase 22 remains not started and not authorized.**

**M1 is complete and published at `4db8320293bf443b6249b70fd2c42eab8d13b7a6`.**
**M2 is complete and published at `f75de2344ee57fdd59f771c7e17c9059edffcca1`.**
**M3 is complete and published at `ae920091eb931eba2f0e43be086cab7f7fecff6d`.**
**M4 is complete and published at `4b2af9f341df21cea11c99787a2de95b7be0e7f7`.**
**M5 is complete and published at `59f2050c987131a4e2c124406ac51ddce8096835`.**
**M5 final evidence/hash correction at `04aa3692ca00879e67bf2cf16de0d26d45cbcf01`.**
**M6 is complete and published at `928a17c801f664bd43896d10cff2cde2ed968934`.**
**M6 publication-record correction at `85af80d3f89fa25288f5282654da6267bdba9e3a`.**
**M7 is complete and published at `4ec4f31febfb963e5373d72b749519c788d319cf` (`docs(phase-21): establish M7 adversarial and release closeout`).**

Phase 20 remains complete, published, accepted, and unchanged. It is an
independent ACP sibling backend, not a Native Harness wrapper or fallback.

M5 adds durable scoped memory records with inspect/create/correct/disable/
supersede/delete controls over the M1 `Memory` record class. M5 does not
introduce retrieval, prompt injection, or any M6+ product behavior.

M6 adds budgeted memory retrieval with influence attribution, Phase 18
`DurableMemory` context-source integration, cross-record export/backup/restore
coordination, and Townhall management presentation bounds. M6 does not weaken
M1–M5 record ownership or begin M7 adversarial closeout.

M7 establishes the adversarial closeout, mapping every M7-required coverage
row to a live regression test already admitted by M1–M6, plus static
ratchets for M0 architecture inventory, conversation-store bypass,
root-infrastructure bypass, embeddings/vector/network exclusion, and the
required M1–M6 test file presence. M7 also corrects five stale expected
counts in three M0/M6f-era test files (`Phase18ContextAssemblyTests`,
`AgentsRegistrationModuleTests`, `LegacyOpenAiCompatibleAgentBackendTests`)
so the full fast and serial suites pass with zero failures. No test is
removed, skipped, weakened, masked, or disabled; the only new file is
`Phase21AdversarialTests` and the only new document is
`M7_CLOSEOUT_EVIDENCE.md`. M7 adds no new product behavior and does not
begin Phase 22 or weaken Phase 17, Phase 18, or any other previously
admitted boundary.

## M7 work board

- [x] `Phase21AdversarialTests` mapping M7 coverage rows to live regression tests.
- [x] Static ratchets: M0 inventory unchanged, no conversation-store writes, no root infrastructure, no embeddings/vector/network.
- [x] Preserve M1–M6 test files; no removal, skip, or baseline masking.
- [x] Publish `M7_CLOSEOUT_EVIDENCE.md` and update owning status surfaces.
- [x] Run M7 verification gates with zero failures.
- [x] Publish one reviewable M7 commit and post-push audit.
- [x] Record explicit human acceptance: `docs(phase-21): accept final closeout`.

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

Phase 21 M0–M7 is complete, published, and accepted. Phase 22 remains not started and not authorized. Do not begin Phase 22.
