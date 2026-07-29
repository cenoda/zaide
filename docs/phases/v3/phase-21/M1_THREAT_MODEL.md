# Phase 21 M1 — Threat Model

**Milestone:** M1 — durable record and storage foundation
**Scope:** Storage boundary only (no trace redaction, resume, or memory retrieval product)

---

## 1. Assets

| Asset | Owner | M1 protection goal |
|-------|-------|--------------------|
| Durable record envelopes | Agents store | Integrity, workspace isolation, explicit versioning |
| Workspace partitions | Agents store | No cross-workspace reads or writes |
| Partition index | Agents store | Atomic update, recoverable from last-known-good |
| Idempotency ledger | Partition index | Prevent duplicate side effects on retry |
| Migration backups | Partition directory | Recover from failed forward migration |

Out of scope for M1 threat handling: secret redaction, provider deletion claims,
memory poisoning, and permission replay (owned by M2–M7).

---

## 2. Adversaries and trust boundaries

| Adversary | Example | M1 response |
|-----------|---------|-------------|
| Local process contention | Second app instance/window | Exclusive partition lock; append fails closed |
| Corrupt/partial disk state | Crash during index write | Temp file quarantine; last-known-good fallback |
| Unsupported future binary | Newer schema version | `UnsupportedVersion`; writes disabled |
| Malformed on-disk record | Manual edit or partial write | Record quarantined; committed index retained |
| Cross-workspace probing | Wrong workspace key in request | Partition isolation by storage key; no data leakage across `ws:*` directories |
| Replay duplication | Retried append with same idempotency key | `DuplicateIgnored`; no second record |

Untrusted inputs at M1 are **on-disk artifacts** and **append requests**. Payload
content is opaque and not interpreted beyond JSON envelope validation.

---

## 3. Fail-closed rules locked at M1

1. Unknown/future schema version never enables writes.
2. Unreadable record files are quarantined, not coerced into success.
3. Lock contention never blocks waiting indefinitely for another writer; append fails immediately.
4. Migration always preserves `index.pre-migration-backup` before rewrite.
5. Conversation persistence paths remain isolated from `agents-durable` paths.

---

## 4. Residual risks (explicit, not solved at M1)

| Risk | Status |
|------|--------|
| Secret retention in opaque payloads | Not interpreted/redacted until M2+ |
| Multi-writer merge across machines | Not in scope; file lock is single-host only |
| Workspace path rename/move aliasing | Path-derived key changes on move; canonical workspace identity remains open |
| Encryption at rest | Not selected at M1 |
| Denial of service via huge payloads | Size/quota enforcement deferred to later milestones |
| Provider-side deletion truth | Not claimed |

---

## 5. Verification mapping

| Threat case | Test surface |
|-------------|--------------|
| Ordering + idempotency | `Phase21StorageTests` |
| Workspace isolation | `Phase21WorkspaceIsolationTests` |
| Migration backup / unknown version / quarantine | `Phase21MigrationTests` |
| Multi-writer contention | `Phase21StorageTests.ConcurrentWriter_FailsClosedWithContention` |
| Ownership / path isolation ratchet | `Phase21StorageOwnershipRatchetTests` |
