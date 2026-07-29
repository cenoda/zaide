# Phase 21: Agent Transparency, Continuity, and Memory — TOFIX

## Status

**M1 is complete and published at `4db8320293bf443b6249b70fd2c42eab8d13b7a6`. M2 and all later milestones are not started.**

Phase 20 remains complete, published, accepted, and unchanged. It is an
independent ACP sibling backend, not a Native Harness wrapper or fallback.

M1 delivered the backend-neutral durable record and storage foundation only.
No trace capture, usage UI, session resume, memory retrieval, prompt injection,
or M2+ product behavior is authorized.

## M1 work board

- [x] Resolve storage ownership with Agents-owned file partitions (no database,
      new package, or root `Infrastructure/`).
- [x] Implement versioned durable envelopes and five record-class policy owners.
- [x] Implement workspace isolation via `AgentDurableWorkspaceStorageKey`.
- [x] Implement ordering, idempotency, replay, and duplicate handling.
- [x] Implement migration with backup-before-migration and unknown-version
      fail-closed behavior.
- [x] Implement interrupted-write, last-known-good, and quarantine handling.
- [x] Lock single-writer fail-closed multi-writer coordination.
- [x] Register store/coordinator in composition and flush on shutdown.
- [x] Add focused Phase 21 storage tests and architecture ratchets.
- [x] Publish `M1_STORAGE_AND_RECORD_CONTRACT.md`, `M1_THREAT_MODEL.md`, and
      `M1_MIGRATION_AND_ROLLBACK_MATRIX.md`.
- [x] Run M1 verification gates with zero failures.

## M1 publication gate

1. Stage exact M1 files.
2. Run required build/test gates.
3. Publish one reviewable commit for M1.
4. Verify clean synchronized post-push state and post-push audit.

## Locked M1 boundaries

- Durable records are Agents-owned; conversation history ownership is unchanged.
- Storage engine is JSON file partitions under `{config}/agents-durable/`.
- Workspace partitions are isolated by path-derived `ws:*` keys.
- Multi-writer behavior is exclusive file-lock fail-closed at M1.
- Unknown/future schema versions disable writes.
- Trace, usage, recovery, audit, and memory payloads remain foundation-only;
  product behavior stays in M2+.

## Open decisions (M2+)

- Trace capture default, redaction detectors, and retention enforcement.
- Usage taxonomy, pricing source, and cost presentation.
- Recovery state machine and backend continuity subset.
- Memory representation, retrieval/index strategy, and influence attribution.
- Canonical workspace identity beyond path-derived storage keys.
- Encryption at rest and cross-device synchronization.

## Next task

Stop for review/acceptance of M1. Do not begin M2 or any later milestone
without separate authorization.
