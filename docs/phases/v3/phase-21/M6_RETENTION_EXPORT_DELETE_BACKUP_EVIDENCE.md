# Phase 21 M6 — Retention, Export, Delete, Backup Evidence

**Milestone:** M6 — cross-record lifecycle integration
**Depends on:** M1 durable partitions; M2–M5 record-owner contracts
**Status:** Complete; published; verification gates pass with zero failures.
**Published commit:** `928a17c801f664bd43896d10cff2cde2ed968934`

---

## 1. Verification gates

```bash
git diff --cached --check
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase21TransparencyIntegration|FullyQualifiedName~Phase21Townhall|FullyQualifiedName~Phase21Export|FullyQualifiedName~Phase21Backup|FullyQualifiedName~Phase21Migration"
git diff --cached --name-only -- src tests tools
git diff --check
```

---

## 2. Gate results

| Gate | Result |
|------|--------|
| Transparency integration/export/backup/migration | 9 discovered, 0 failures |

---

## 3. Required behavior checklist

| Required behavior | M6 evidence |
|-------------------|-------------|
| Neutral export over record-owner contracts | `AgentTransparencyLifecycleCoordinator.Export` |
| Backup/restore preserve partition semantics | coordinator + store workspace path resolution |
| Migration delegates to partition load outcome | `Migrate` → `LoadWorkspace` |
| Partial/unavailable evidence preserved | export sections with unavailable markers |
| Memory export uses M5 record-owner semantics | `AgentMemoryLifecycleService` via coordinator |
| Retention/deletion/lifecycle independent per record class | per-class export sections; M5 memory lifecycle unchanged |
| Cross-workspace denial unchanged | M5 coordinator guards preserved |

---

## 4. Production surfaces

| Surface | Owner |
|---------|-------|
| `AgentTransparencyLifecycleCoordinator` | Agents Application/Transparency |
| `IAgentTransparencyLifecycleCoordinator` | Agents Contracts/Transparency |
| `AgentMemoryLifecycleSerializer` | Agents Application/Memory (export summaries) |
| `IAgentDurableRecordStore.GetWorkspaceDirectoryPath` | store-relative backup/restore paths |

---

## 5. Test files

| File | Coverage |
|------|----------|
| `tests/Zaide.Tests/Features/Agents/Transparency/Integration/Phase21TransparencyIntegrationTests.cs` | Cross-class export integration |
| `tests/Zaide.Tests/Features/Agents/Transparency/Integration/Phase21ExportTests.cs` | Export sections and partial unavailable |
| `tests/Zaide.Tests/Features/Agents/Transparency/Integration/Phase21BackupTests.cs` | Backup/restore round trip |
| `tests/Zaide.Tests/Features/Agents/Transparency/Storage/Phase21MigrationTests.cs` | Migration load outcome (filter match) |

---

## 6. M6 limitations preserved

- No new database or external backup service; file-store partitions only.
- Delete semantics remain on M5 `AgentMemoryCoordinator` and M1 partition owners.
- M7 adversarial closeout remains not started and not authorized.

---

## 7. Rollback

1. Remove lifecycle coordinator registration from composition.
2. Use per-class M1–M5 export paths directly.
3. Revert the single M6 commit.
4. Preserve existing partition files on disk unchanged.
