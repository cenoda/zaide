# Phase 22.4: Trace / Memory / Usage User Surfaces — TOFIX

## Status

**M0, M1, and M2 are complete; M3–M4 remain unauthorized.** A4 package
4 is assigned here. Phase 22.2 is complete with package-2 PASS restored, so
the ordering dependency is satisfied.

## Work Board

- [x] Draft trace, memory, usage/cost, Phase 21 preservation, and re-smoke
  boundaries.
- [x] Wait for accepted Phase 22.2 closeout (package PASS restored).
- [x] Obtain authorization for Phase 22.4 M0 documentation only.
- [x] Verify production reachability and application ownership in read-only M0.
- [x] Publish the verified seams, exact filters, isolated A3 procedure,
  rollback, backup, migration, and stop boundaries in
  [M0_SEAM_VERIFICATION.md](./M0_SEAM_VERIFICATION.md).
- [x] Record human M0 acceptance and separate M1-only implementation
  authorization under the user's standing GO direction (2026-08-04).
- [x] Implement and accept M1 — Townhall trace surface, explicit
  application-lifetime capture control, opened-workspace inspection, command
  reachability, and independent Native Harness / ACP evidence hooks.
- [x] Implement M2 — scoped memory lifecycle surface only.
- [ ] Implement M3 — usage/cost surface only (requires separate authorization).
- [ ] Implement M4 — integrated reachability, accessibility, backup safety,
  regression, and isolated `A1-TC-02` / `A1-TC-03` / `A1-TC-08` re-smoke.
- [ ] Re-smoke `A1-TC-02`, `A1-TC-03`, and `A1-TC-08`.

## Verified M0 findings

- Production DI registers the integrated and child inspection ViewModels, but
  no shell/Townhall View or command consumes them. All three user rows remain
  unreachable at the M0 baseline.
- Trace, usage, and memory projections default to the `ws:unbound` partition;
  opened-workspace selection, loading, retry, failure, and record selection
  are not implemented.
- Trace/usage capture is disabled by default. Native Harness and ACP evidence
  sources are registered independently but have no execution-path submit
  caller. Missing evidence must remain unavailable, never zero or verified.
- ACP `usage_update` context values are point-in-time and its optional cost is
  cumulative per session. M3 must preserve the locked aggregation semantics;
  cumulative snapshots are never summed as deltas.
- Memory lifecycle/retrieval/influence ownership is complete and remains
  separate from conversation history; only user lifecycle reachability is
  missing.
- No schema migration or new persistence owner is required. Existing durable
  partitions must remain unchanged across rollback.
- The current lifecycle Backup failure path is internally inconsistent for a
  missing/unavailable partition; M4 must correct/test it before exposing
  Backup. Restore and Migrate are not Phase 22.4 user surfaces.
- Native Harness and ACP remain independent sibling backends with equal
  placement and truthful evidence-depth differences.

## Blockers

- M3–M4 remain unauthorized. Do not auto-start them from this board.
- G5 remains blocked until accepted M1–M4 implementation and the owned
  affected-row re-smoke complete.

## Next Task

Wait for separate M3-only implementation authorization. Do not begin M3–M4,
Phase 22.5, G5, or V4 from this board.

## M1 Trace Surface (2026-08-04) — accepted

M1 adds the user-reachable Trace entry in Townhall and the `agent.trace.open`
command. The surface reads only redacted records for the opened workspace and
provides an explicit capture enable/disable action for the current application
lifetime; capture remains disabled after restart and no setting/schema was
added. Native Harness captures only admitted public request/response evidence;
ACP records public envelope facts with opaque body markers. The two backends
remain independent and have no fallback or shared private state.

The change also resolves the live trace composition cycle: the source registry
initializes registered source writers only after the coordinator exists, while
retaining the coordinator's registered-source admission check.

| Gate | Result |
|------|--------|
| `dotnet build Zaide.slnx --no-incremental` | PASS — 0 warnings, 0 errors |
| M1 trace filter | PASS — 63/63 |
| Architecture ratchet filter | PASS — 13/13 |
| `dotnet test Zaide.slnx --no-build` | PASS — 3964/3964 |
| `git diff --check` | Pending final commit check |

M1 is accepted under the user's standing GO direction (2026-08-04). M2-only
implementation is authorized. No runtime smoke, A3 execution, G5, or V4 work
was performed.

## M2 Memory Surface (2026-08-04) — complete; await M3 authorization

M2 adds the user-reachable Memory entry in Townhall and the `agent.memory.open`
command (category `Agent`, no default gesture). The surface lists, selects, and
mutates scoped durable memory for the opened workspace through
`AgentMemoryCoordinator` only:

- create / correct / disable / supersede / delete (tombstone)
- provenance (`AgentMemorySourceKind.User`), conflict, status, and scope labels
- influence evidence kept attribution-only and never editable as lifecycle records
- presentation states `Loading` / `Ready` / `Empty` / `Unavailable` / `Failed`
  with bounded retry; failed and unavailable never masquerade as empty
- workspace from `IWorkspaceActionAuthority`; author from
  `IActorCatalog.CanonicalHuman`; Session/Agent/Conversation from selected
  Townhall direct-conversation context; Project/Shared from opened workspace
  identity; missing required scope context disables create with a visible reason

Backup/Restore/Migrate UI, usage/cost surface, accessibility package re-smoke,
and A3 producer closeout remain out of scope (M3/M4).

| Gate | Result |
|------|--------|
| `dotnet build Zaide.slnx --no-incremental` | PASS — 0 warnings, 0 errors |
| M2 memory filter | PASS — 46/46 (includes discovered `Phase22MemorySurfaceTests`) |
| Architecture / DI inventory ratchets | PASS |
| `dotnet test Zaide.slnx --no-build` (serial fallback) | PASS — 3973/3973 |
| `git diff --check` | PASS |

M2-only implementation was authorized under the standing GO. Do not start M3,
M4, Phase 22.5, G5, or V4 without separate authorization. No product-readiness
claim.
