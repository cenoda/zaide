# Phase 21: Agent Transparency, Continuity, and Memory — TOFIX

## Status

**M1 is complete and published at `4db8320293bf443b6249b70fd2c42eab8d13b7a6`.**
**M2 is complete and published.**
**Implementation commit:** `f75de2344ee57fdd59f771c7e17c9059edffcca1`
**Publication/hash-correction commit:** `474a15b015649d50aa95ae30223a56bcb1bba3e6`
**M3 is complete and published.**
**M4–M7 are not started and not authorized.**

Phase 20 remains complete, published, accepted, and unchanged. It is an
independent ACP sibling backend, not a Native Harness wrapper or fallback.

M1 delivered the backend-neutral durable record and storage foundation only.
M2 adds the redacted trace evidence capture, bounded admission, and
inspection pipeline over the M1 Trace record class.
M3 adds the usage and cost evidence ledger with provenance, zero-cost guard,
and truthful origin distinctions. M3 does not introduce session resume,
memory retrieval, prompt injection, or any M4+ product behavior.

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

## M2 work board

- [x] Capture the deepest truthful backend-exposed trace layer only after
      mandatory redaction (fail-closed) and bounded admission.
- [x] Enforce bounded payload size and bounded queue depth with backpressure
      reporting and nonblocking submission.
- [x] Record explicit capture states: disabled, unavailable, captured,
      redacted, truncated, failed. (Sampled and summarized reserved for later
      evidence layers.)
- [x] Add narrow Native Harness and ACP evidence adapters that produce
      neutral trace inputs without sharing backend-private internals.
- [x] Keep durable security audit independent from optional trace capture.
- [x] Add a presentation seam for trace availability, redaction state, and an
      inspection entry point. (Townhall projection surface is unchanged.)
- [x] Preserve workspace isolation and M1 record ownership.
- [x] Persist through the M1 Trace record class only; no new store or
      dependency.
- [x] Add focused Phase 21 trace/redaction/lifecycle tests and
      `Phase21TraceRatchetTests` (mandatory redaction, bounded queue,
      backend-private isolation, no conversation coupling, no root
      `Infrastructure/` admission).
- [x] Publish `M2_TRACE_REDACTION_AND_RETENTION_EVIDENCE.md`.
- [x] Run M2 verification gates with zero failures.

## M3 work board

- [x] Usage and cost evidence ledger via M1 Usage record class.
- [x] Seven value-origin distinctions (reported, measured, calculated,
      estimated, invoiced, unavailable, disputed).
- [x] Zero-cost guard: reject cost entries with zero value and non-Unavailable
      origin.
- [x] Pricing provenance fields (source id, version, formula, currency,
      effective time, rounding, uncertainty).
- [x] Narrow Native Harness and ACP usage evidence adapters.
- [x] Add presentation seam for usage availability and cost summary.
- [x] Preserve workspace isolation, M1 record ownership, and M2 redaction
      boundaries.
- [x] No new database, NuGet package, hosted service, network lookup,
      credentials, or external activity.
- [x] Add focused Phase 21 usage/cost lifecycle/calculation/adapter tests.
- [x] Add `Phase21UsageRatchetTests` (zero-cost guard, feature ownership,
      M1 Usage class routing, no conversation coupling, backend-private
      isolation, no trace namespace coupling, complete value-origin enum).
- [x] Update architecture inventory and namespace expectations.
- [x] Publish `M3_USAGE_AND_COST_EVIDENCE.md`.
- [x] Run M3 verification gates with zero failures.

## M1 publication gate

1. Stage exact M1 files.
2. Run required build/test gates.
3. Publish one reviewable commit for M1.
4. Verify clean synchronized post-push state and post-push audit.

## M2 publication gate

1. [x] Stage exact M2 files.
2. [x] Run required build/test gates.
3. [x] Publish one reviewable commit for M2.
4. [x] Verify clean synchronized post-push state and post-push audit.
5. [x] Confirm M3–M7 remain not started and not authorized.
6. [x] Stop at the read-only M2 post-push audit gate.

## Locked M1 boundaries

- Durable records are Agents-owned; conversation history ownership is unchanged.
- Storage engine is JSON file partitions under `{config}/agents-durable/`.
- Workspace partitions are isolated by path-derived `ws:*` keys.
- Multi-writer behavior is exclusive file-lock fail-closed at M1.
- Unknown/future schema versions disable writes.
- Trace, usage, recovery, audit, and memory payloads remain foundation-only;
  product behavior stays in M2+.

## M3 publication gate

1. [ ] Stage exact M3 files.
2. [ ] Run required build/test gates.
3. [ ] Publish one reviewable commit for M3.
4. [ ] Verify clean synchronized post-push state and post-push audit.
5. [ ] Confirm M4–M7 remain not started and not authorized.
6. [ ] Stop at the read-only M3 post-push audit gate.

## Locked M2 boundaries

- Trace records are Agents-owned and persisted through the M1 Trace record
  class only.
- Capture is fail-closed: redaction runs before retention, rendering, export,
  logging, indexing, backup, and cross-process transfer.
- Capture state is explicit per record; missing evidence is reported as
  `Unavailable`, never as `Captured` or `Redacted`.
- Capture is bounded: payload size and queue depth; backpressure is
  reported, never silently swallowed; the agent event pipeline is never
  blocked.
- Backend evidence adapters do not share backend-private internals; the
  Native Harness and ACP sources are independent siblings.
- The display surface is read-only; capture is enabled by composition and
  display settings do not change provider or model context.
- M3–M7 remain not started and not authorized.

## Open decisions (M3+)

- Usage taxonomy, pricing source, and cost presentation.
- Recovery state machine and backend continuity subset.
- Memory representation, retrieval/index strategy, and influence attribution.
- Canonical workspace identity beyond path-derived storage keys.
- Encryption at rest and cross-device synchronization.
- Whether backend execution paths forward evidence to the registered
  sources (M2 admits the pipeline; future milestones own the wiring).

## Next task

M3 review and acceptance. M4+ session recovery/termination/memory work remains
unauthorized. Do not begin M4 without separate authorization.
