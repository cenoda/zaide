# Phase 21 M5 — Memory Lifecycle Evidence

**Milestone:** M5 — durable scoped memory records
**Depends on:** M1 `Memory` record class
**Status:** Complete; published; verification gates pass with zero failures.
**Published commit:** `59f2050c` (`feat(phase-21): establish M5 durable scoped memory records`)

---

## 1. Verification gates

```bash
git diff --cached --check
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase21MemoryStore|FullyQualifiedName~Phase21MemoryPolicy|FullyQualifiedName~Phase21MemoryLifecycle"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
git diff --cached --name-only -- src tests tools
git diff --cached --name-only
git diff --check
```

---

## 2. Gate results

| Gate | Result |
|------|--------|
| M5 memory tests | 22 discovered, 0 failures |
| Architecture | 81 discovered, 0 failures |

---

## 3. Required behavior checklist

| Required behavior | M5 evidence |
|-------------------|-------------|
| Provenance, author, source revision, workspace, scope, timestamps, schema version, status | `AgentMemoryPayload`, `AgentMemoryRecord`, store tests |
| Inspect, correct, disable, supersede, delete | `AgentMemoryCoordinator`, store tests |
| Conflict, poisoning, stale-fact, supersession | `AgentMemoryPolicyEvaluator`, policy tests |
| Retention/export/backup/migration/replay/idempotency | lifecycle tests, M1 Memory partition |
| Cross-workspace denial | store + ratchet tests |
| Separate from conversation/audit | ratchet tests |
| No automatic injection | ratchet tests |
| No embeddings/network | ratchet tests |

---

## 4. Test files

| File | Coverage |
|------|----------|
| `tests/Zaide.Tests/Features/Agents/Memory/Store/Phase21MemoryTestSupport.cs` | Fixtures |
| `tests/Zaide.Tests/Features/Agents/Memory/Store/Phase21MemoryStoreTests.cs` | CRUD, scopes, idempotency, workspace isolation |
| `tests/Zaide.Tests/Features/Agents/Memory/Store/Phase21MemoryPolicyTests.cs` | Poisoning, conflict, stale, supersession guards |
| `tests/Zaide.Tests/Features/Agents/Memory/Store/Phase21MemoryLifecycleTests.cs` | Export, backup, replay, retention, migration partition |
| `tests/Zaide.Tests/Architecture/Phase21MemoryRatchetTests.cs` | M1 routing, feature ownership, no injection |

---

## 5. M5 limitations preserved

- Storage alone does not influence model requests (M6 owns retrieval/influence).
- Append-only revisions retain full mutation history; compaction is not implemented.
- User-controlled retention default is 0 (no automatic expiry) per M1 metadata.
- M6–M7 remain not started and not authorized.

---

## 6. Rollback

1. Stop new memory writes in composition.
2. Export readable M5 records through `AgentMemoryLifecycleService`.
3. Revert the single M5 commit.
4. Preserve conversation, audit, trace, usage, and session records unchanged.
