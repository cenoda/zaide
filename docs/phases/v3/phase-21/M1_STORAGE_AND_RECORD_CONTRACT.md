# Phase 21 M1 — Storage and Record Contract

**Milestone:** M1 — durable record and storage foundation
**Status:** Complete (published after verification gate)
**Depends on:** M0 accepted at `8bb1a179` (`docs(phase-21): establish M0 plan`)  
**Published commit:** `4db8320293bf443b6249b70fd2c42eab8d13b7a6` (`feat(phase-21): establish M1 durable record storage foundation`)

M1 resolves the M0 storage-ownership open decision with live evidence. It
implements only backend-neutral versioned record envelopes, workspace-isolated
file partitions, ordering/idempotency/replay semantics, migration/quarantine
behavior, and fail-closed multi-writer coordination. No trace capture, usage
UI, session resume, memory retrieval, or prompt injection is included.

---

## 1. Ownership decision

| Decision | M1 lock |
|----------|---------|
| Durable record owner | **Agents application** through `IAgentDurableRecordStore` and `AgentDurableRecordCoordinator` |
| Physical persistence owner | **Agents infrastructure** through `AgentDurableRecordFileStore` under `src/Features/Agents/Infrastructure/Transparency/Storage/` |
| Conversation store | Remains authoritative communication history; does not own Phase 21 durable records |
| Backend adapters | Remain evidence producers only; they do not write Phase 21 stores directly |
| Root `Infrastructure/` | **Rejected** — storage remains feature-owned under Agents |

---

## 2. Storage engine proof

| Candidate | M1 result |
|-----------|-----------|
| New database (SQLite, etc.) | **Rejected** — adds dependency and migration risk without M1 product need |
| New NuGet serializer/ORM | **Rejected** — `System.Text.Json` already used by conversation/settings persistence |
| Embedding / vector index | **Rejected** — out of M1 scope; no retrieval product yet |
| Network / hosted service | **Rejected** — excluded by phase authorization |
| Agents-owned JSON file partitions | **Accepted** — sibling layout under `{config}/agents-durable/{workspaceKey}/` |

Physical layout per workspace partition:

```text
agents-durable/
  ws:{hash}/
    index.json
    index.json.lastknowngood
    index.pre-migration-backup
    index.json.tmp                  (atomic write only; never authoritative)
    .partition.lock                 (exclusive writer coordination)
    records/
      Trace|Usage|SessionRecovery|Audit|Memory/
        {sequence}_{recordId}.json
    quarantine/
      {timestamp}_{reason}_{file}
```

---

## 3. Record classes and envelope contract

Five durable record classes are admitted at M1 with distinct policy owners:

| Class | Purpose at M1 | Default retention metadata |
|-------|---------------|----------------------------|
| `Trace` | Future redacted trace evidence | 30 days |
| `Usage` | Future usage/cost ledger entries | 365 days |
| `SessionRecovery` | Future interruption/recovery checkpoints | 90 days |
| `Audit` | Future durable security audit continuity | 365 days |
| `Memory` | Future scoped memory records | user-controlled (0 = no default expiry) |

Envelope fields (schema version **1**):

- `schemaVersion`
- `recordId`
- `recordClass`
- `workspaceKey`
- `orderingSequence` (monotonic per class within a workspace partition)
- `idempotencyKey`
- `recordedAtUtc`
- optional scope references: `conversationId`, `sessionId`, `runId`, `backendId`
- opaque `payloadJson` (typed payloads arrive in M2–M5)

---

## 4. Workspace isolation

- `AgentDurableWorkspaceStorageKey` derives from the normalized absolute workspace
  root path (`ws:{sha256-prefix}`).
- Different workspace roots never share a partition directory.
- Cross-workspace replay/append requests operate only on the keyed partition.
- Workspace identity generation/fingerprint refinements remain open for later
  milestones; M1 locks path-derived isolation only.

---

## 5. Ordering, idempotency, and replay

| Concern | M1 behavior |
|---------|-------------|
| Ordering key | `orderingSequence` per `recordClass` within one workspace partition |
| Gap behavior | Replay returns only records with `orderingSequence > cursor`; gaps are visible as missing sequences |
| Idempotency | Append with an existing class-scoped `idempotencyKey` returns `DuplicateIgnored` and does not create a second record |
| Replay cursor | `AgentDurableRecordReplayCursor(recordClass, afterOrderingSequence)` |
| Duplicate handling | Idempotent append is safe; duplicate payload changes are ignored |

---

## 6. Migration, unknown version, interrupted write, quarantine

| Concern | M1 behavior |
|---------|-------------|
| Forward migration | Ordered `IAgentDurableRecordMigration` chain; v0→v1 synthetic migration proves backup-before-migration |
| Backup before migration | Source index copied to `index.pre-migration-backup` before rewrite |
| Unknown future schema | `UnsupportedVersion` load outcome; writes disabled fail-closed |
| Interrupted index write | Stray `index.json.tmp` is quarantined; committed `index.json` / last-known-good remain authoritative |
| Interrupted record write | Record writes use temp file + atomic rename before index update |
| Corrupt/unreadable record | Record file moved to `quarantine/`; partition may load as `Quarantined` |
| Rollback | Revert M1 commit; restore `index.pre-migration-backup` if a test/store migration occurred |

---

## 7. Multi-writer behavior

M1 locks **single-writer fail-closed** coordination:

- Each append acquires an exclusive `.partition.lock` file (`FileShare.None`).
- A second writer in another process/window receives `ContentionFailed`.
- No optimistic merge, CRDT, or cross-process write queue is introduced at M1.
- Multi-window/product coordination beyond OS file locking remains an open
  decision for later milestones.

---

## 8. Composition and shutdown

- `AgentsServiceCollectionExtensions` registers `IAgentDurableRecordStore` and
  `AgentDurableRecordCoordinator`.
- `ApplicationShutdown` disposes/flushes the durable record store after session
  teardown and before ACP process termination.

---

## 9. M1 exclusions preserved

- No trace capture/redaction pipeline (M2)
- No usage/cost ledger presentation (M3)
- No session recovery/termination product (M4)
- No durable memory product (M5)
- No retrieval/injection/integration UI (M6–M7)
