# Phase 17 M8 — Session/Event Integration Evidence

Date: 2026-07-25  
Baseline: M7 GO at `ebdbec85`

## Scope delivered

- Wired run-scoped `ContractAgentActionBroker` into `AgentSessionService` for
  `IAgentActionRequestCapableBackend` backends only; legacy backend keeps
  `UnavailableAgentActionBroker`.
- Added typed action facts to `AgentEventKind` with `AgentActionFactPayload`
  and `AgentActionAuditRecord` snapshots through `AgentActionAuditStore`.
- Published ordered facts via `RunScopedAgentActionEventPublisher` at request,
  classification, permission decision, execution start, terminal result,
  reconciliation, and revocation points in the broker.
- Extended `AgentConversationEventProjection` to append bounded
  `SystemNotification` entries for terminal action results only.
- Propagated run cancellation, session end, workspace invalidation, and
  application shutdown broker revocation through `AgentSessionService` and
  `ApplicationShutdown`.
- Added repository-owned `FakeActionRequesterBackend` for integration tests.
- Added `Phase17BypassRatchetTests` for editor I/O, workflow runner, BCL,
  service-location, presentation, and conversation-write bypass prevention.

## Gate results

| Gate | Result |
|------|--------|
| `dotnet build Zaide.slnx --no-restore` | pass, 0 errors |
| `Phase17SessionEventIntegration` | pass, 10/10 |
| `Phase17BypassRatchet` | pass, 5/5 |
| `Phase17` (all filters) | pass, 284/284 |
| `Architecture` | pass, 284/284 |
| Full fast suite | pass, 3049/3049 (1 pre-existing parallel fd-count flake under fast mode) |
| Serial fallback | pass, 3049/3049 |
| `git diff --check` | pass, clean |

## Manual notes

- No production tool-using backend was added.
- Audit snapshots are in-memory and current-lifetime bounded to 256 records.
- Revocation facts are truthful and bound to real action context; broker-level revocations do not fabricate action details.
- M9 closeout completed on 2026-07-25; see `M9_CLOSEOUT_EVIDENCE.md`.
