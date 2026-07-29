# Phase 21 M5 — Memory Record and Policy

**Milestone:** M5 — durable scoped memory records
**Depends on:** M1 published at `4db83202`
**Status:** Complete; published; verification gates pass with zero failures.
**Published commit:** `9f779fc0` (`feat(phase-21): establish M5 durable scoped memory records`)

M5 stores user-controllable derived knowledge at explicit Session, Agent,
Conversation, or Project/Shared scope without automatically injecting it into
model runs. Memory is separate from conversation history, audit records, and
backend-private memory.

---

## 1. Outcome and ownership decision

| Decision | M5 lock |
|----------|---------|
| Memory store writer | `AgentMemoryStoreWriter` over M1 `IAgentDurableRecordStore` (`AgentDurableRecordClass.Memory`) |
| Memory coordinator | `AgentMemoryCoordinator` — create, correct, disable, supersede, delete |
| Memory inspector | `AgentMemoryInspector` — replay projection of append-only revisions |
| Policy evaluator | `AgentMemoryPolicyEvaluator` — conflict, poisoning, stale-fact, supersession rules |
| Lifecycle service | `AgentMemoryLifecycleService` — export and backup packages |
| Presentation | `AgentMemoryAvailabilityProjection`, `AgentMemoryAvailabilityState`, `AgentMemoryInspectionViewModel` |
| Composition root | `AgentsServiceCollectionExtensions.AddZaideAgents` |
| Architecture ratchet | `Phase21MemoryRatchetTests` |

Owner is `Zaide.Features.Agents.Application.Memory.*` and the M1-approved
persistence adapter. Memory never writes to the conversation store and never
injects into prompts or context manifests.

---

## 2. Record contract

Envelope payload schema version **1** fields:

- `memoryId` — stable identity across revisions
- `operation` — `create`, `correct`, `disable`, `supersede`, `delete`
- `schemaVersion`
- `scope` — `session`, `agent`, `conversation`, `projectShared`
- scope owner identifiers (`sessionId`, `actorId`, `conversationId`, `projectId`)
- `content` — derived knowledge text (max 16 KiB)
- provenance (`authorActorId`, `sourceRevision`, `sourceKind`, `sourceDescription`)
- `status` — `active`, `disabled`, `superseded`, `deleted`
- `supersededByMemoryId`, `supersedesMemoryId`
- `createdAtUtc`, `updatedAtUtc`, `lastValidatedAtUtc`
- policy markers (`conflictKind`, `isPoisoningSuspect`, `isStaleFact`)

Projection replays append-only revisions and returns the latest revision per
`memoryId`. Deleted records remain in the durable audit trail but are excluded
from default inspection unless `includeDeleted` is requested.

---

## 3. Admitted scopes

| Scope | Owner key |
|-------|-----------|
| `Session` | `AgentSessionId` |
| `Agent` | `ActorId` |
| `Conversation` | `ConversationId` |
| `ProjectShared` | project id string |

Application/global policy is not a memory scope.

---

## 4. Policy behavior

| Concern | M5 behavior |
|---------|-------------|
| Content conflict | Active memory in the same scope with different content hash → `ContentConflict` (accepted with marker; not auto-merged) |
| Poisoning suspect | Import source or regex match for instruction-override/exfiltration patterns → `PoisoningSuspect`; not retrievable |
| Stale fact | `lastValidatedAtUtc` older than 90 days → `isStaleFact` marker |
| Supersession | Old record marked `superseded` with `supersededByMemoryId`; replacement record links `supersedesMemoryId` |
| Disable | Status `disabled`; not retrievable |
| Delete | Status `deleted`; tombstone retained; excluded from default lists |
| Cross-workspace | Denied by default; operations against foreign workspace keys return `NotFound` or `WorkspaceDenied` |
| Backend-private memory | Not imported, trusted, or deleted by implication |

---

## 5. M5 exclusions preserved

- No automatic prompt injection or context-manifest retrieval (M6)
- No embeddings, vector search, network services, or hosted memory
- No conversation or audit history rewrite on correct/delete
- No dedicated settings window or visual redesign
- M6–M7 remain not started and not authorized
