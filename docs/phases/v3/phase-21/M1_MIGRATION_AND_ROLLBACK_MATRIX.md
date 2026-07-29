# Phase 21 M1 — Migration and Rollback Matrix

**Milestone:** M1 — durable record and storage foundation

---

## 1. Forward migration matrix

| From | To | Trigger | Backup artifact | M1 proof |
|------|----|---------|-----------------|----------|
| missing partition | v1 index | First append/load | none required | `Phase21StorageTests` missing load |
| v0 index | v1 index | Load with `schemaVersion: 0` | `index.pre-migration-backup` | `Phase21MigrationTests.Load_MigratesV0IndexWithPreMigrationBackup` |
| v1 index | v1 index | Normal operation | last-known-good on successful save | `Phase21StorageTests` round-trip append/replay |

v0 index shape (synthetic test-only baseline):

```json
{
  "schemaVersion": 0,
  "workspaceKey": "ws:…",
  "sequences": { "Trace": 3 },
  "records": []
}
```

v1 index shape (production):

```json
{
  "schemaVersion": 1,
  "workspaceKey": "ws:…",
  "classState": {
    "Trace": {
      "nextOrderingSequence": 3,
      "idempotencyKeys": []
    }
  },
  "records": []
}
```

---

## 2. Unknown-version and downgrade matrix

| Condition | Read outcome | Writes | User/data effect |
|-----------|--------------|--------|------------------|
| `schemaVersion` > supported | `UnsupportedVersion` | Disabled | Existing files remain on disk; no silent rewrite |
| Corrupt index | `Corrupt` → last-known-good attempt | Enabled if LKG loads | No automatic deletion |
| Corrupt index + corrupt LKG | `Corrupt` | New empty partition on next append | Prior files may remain in `records/` but are not indexed |
| Corrupt record file | `Quarantined` (when other records valid) | Enabled | Unreadable record moved to `quarantine/` |
| Interrupted `index.json.tmp` | Ignored for authority | Committed index unchanged | Temp quarantined when detected |

Downgrade of production user data is **not** supported at M1. Older binaries must
fail closed on unsupported versions rather than rewrite newer schemas.

---

## 3. Interrupted-write matrix

| Stage interrupted | Authoritative state | Recovery |
|-------------------|---------------------|----------|
| Record temp write | Prior index + prior records | Temp record file ignored on next load if not indexed |
| Index temp write | Prior `index.json` and/or LKG | Stray `index.json.tmp` quarantined; LKG may be used |
| Post-record, pre-index update | Prior index (record file may exist unindexed) | M1 does not auto-index orphan record files; later milestones may add reconciliation |
| Migration rewrite | `index.pre-migration-backup` | Manual restore from backup file |

---

## 4. Rollback procedures

### Code rollback (M1 commit revert)

1. Revert the single M1 commit from `master`.
2. Rebuild and rerun Architecture + Phase 21 M1 gates.
3. No conversation/settings schema rollback required.

### Data rollback (developer/test partition only)

1. Stop the application.
2. Restore `index.pre-migration-backup` over `index.json` if migration occurred.
3. Verify digest/consistency with tests or manual replay.
4. Remove `quarantine/` copies only after confirming they are unwanted.

M1 must not migrate real user production data without a separately accepted
migration decision. Production partitions created after M1 ship use schema v1
from first write.

---

## 5. Dependency / engine decision log

| Proposal | Decision | Evidence |
|----------|----------|----------|
| SQLite/other database | Rejected | Adds package + operational surface; file partitions sufficient for M1 |
| New serializer package | Rejected | `System.Text.Json` already in use |
| Vector/embedding index | Rejected | Not required for storage foundation |
| Agents-owned JSON partitions | Accepted | `AgentDurableRecordFileStore` + tests |
