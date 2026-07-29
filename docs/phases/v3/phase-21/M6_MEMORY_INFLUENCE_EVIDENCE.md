# Phase 21 M6 — Memory Influence Evidence

**Milestone:** M6 — memory influence and integrated management (retrieval/influence)
**Depends on:** M1 `Memory` record class; M5 durable scoped memory records
**Status:** Complete; published; verification gates pass with zero failures.
**Published commit:** `928a17c801f664bd43896d10cff2cde2ed968934`
**Publication-record correction:** `85af80d3f89fa25288f5282654da6267bdba9e3a`

---

## 1. Verification gates

```bash
git diff --cached --check
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase21MemoryRetrieval|FullyQualifiedName~Phase21MemoryInfluence"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase18ContextBypass|FullyQualifiedName~Architecture"
git diff --cached --name-only -- src tests tools
git diff --cached --name-only
git diff --check
```

---

## 2. Gate results

| Gate | Result |
|------|--------|
| M6 memory retrieval/influence tests | 7 discovered, 0 failures |
| Phase 18 context bypass + architecture | 82 discovered, 0 failures |

---

## 3. Required behavior checklist

| Required behavior | M6 evidence |
|-------------------|-------------|
| Deterministic eligibility, ranking, provenance, conflict, stale handling | `AgentMemoryRetriever`, retrieval tests |
| Token budgeting via Phase 18 manifest assembly | `AgentContextManifestBuilder.AppendMemoryCandidates`, influence tests |
| Each run records influenced revisions or unavailable marker | `AgentMemoryInfluenceRecorder`, session wiring, influence tests |
| Disabled/deleted/superseded/stale/out-of-scope memory not retrieved | retrieval tests |
| Phase 18 redaction, exclusions, provenance, budget remain final | manifest builder, policy exclusion test |
| Memory never inserted wholesale into prompts | explicit `DurableMemory` source items only; ratchet |
| No embeddings, vector store, network, or new dependencies | ratchet tests; no new packages |

---

## 4. Production surfaces

| Surface | Owner |
|---------|-------|
| `AgentMemoryRetriever` | Agents Application/Memory |
| `AgentMemoryInfluenceRecorder` | Agents Application/Memory |
| `AgentContextManifestBuilder.AppendMemoryCandidates` | Agents Application (Phase 18 integration) |
| `AgentSessionService` influence recording | Agents Application |

---

## 5. Test files

| File | Coverage |
|------|----------|
| `tests/Zaide.Tests/Features/Agents/Memory/Retrieval/Phase21MemoryRetrievalTests.cs` | Eligibility, ranking, stale, scope, budget inputs |
| `tests/Zaide.Tests/Features/Agents/Memory/Retrieval/Phase21MemoryInfluenceTests.cs` | Influence recording, policy exclusion, unavailable |
| `tests/Zaide.Tests/Architecture/Phase21MemoryRatchetTests.cs` | Manifest-only integration, no wholesale injection |

---

## 6. M6 limitations preserved

- Retrieval is deterministic rule-based ranking; no embedding or vector retrieval.
- M1–M5 record ownership and contracts are unchanged.
- M7 adversarial closeout remains not started and not authorized.

---

## 7. Rollback

1. Disable memory retrieval/influence registration in composition.
2. Export memory influence payloads through M1 `Memory` partition replay.
3. Revert the single M6 commit.
4. Preserve conversation, audit, trace, usage, session, and M5 memory records unchanged.
