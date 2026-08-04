# Phase 22.4: Trace / Memory / Usage User Surfaces — TOFIX

## Status

**M0–M4 are complete.** Phase 22.4 owns A4 package 4 and has finished the
integrated transparency closeout with dual-backend A3 re-smoke. G5 and V4
remain unauthorized. Do not start Phase 22.5 without separate authorization.

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
- [x] Implement M3 — usage/cost surface only.
- [x] Implement M4 — integrated reachability, accessibility, backup safety,
  regression, and isolated `A1-TC-02` / `A1-TC-03` / `A1-TC-08` re-smoke.
- [x] Re-smoke `A1-TC-02`, `A1-TC-03`, and `A1-TC-08`.

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

- G5 remains unauthorized until a separate human package closeout decides
  the umbrella Phase 22 / A4 package readiness. Phase 22.4 alone does not
  claim product readiness or G5 pass.
- Phase 22.5, G5, and V4 remain unauthorized from this board.

## Next Task

Phase 22.4 M4 is complete. Do not start Phase 22.5, G5 package closeout, or
V4 without separate human authorization.

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

## M3 Usage/Cost Surface (2026-08-04) — complete; await M4 authorization

M3 adds the user-reachable Usage entry in Townhall and the `agent.usage.open`
command (category `Agent`, no default gesture). The surface inspects usage and
cost evidence for the opened workspace through `AgentUsageCoordinator` /
`IAgentUsageInspector` only:

- origin, units, scope, backend/model attribution, pricing/currency/uncertainty
- additive `AgentUsageAggregationSemantics` (`Unknown` / `Delta` /
  `Cumulative` / `PointInTime`); existing records decode as `Unknown` without
  partition migration
- verified cost totals sum only `Delta` cost records and the latest
  `Cumulative` snapshot per backend/session/currency; `Unknown` is listed but
  excluded from a verified aggregate (never shown as zero or invoice fact)
- presentation states `Loading` / `Ready` / `Empty` / `Unavailable` / `Failed`
  with bounded retry; failed and unavailable never masquerade as empty
- explicit application-lifetime capture enable/disable; capture remains
  disabled after restart
- Native Harness producer: Zaide-measured request count/latency plus explicit
  unavailable token/cost markers
- ACP producer: public `usage_update` `used`/`size` as point-in-time Reported
  evidence and optional cumulative session cost; no price catalog or inference
- workspace from `IWorkspaceActionAuthority` — never `ws:unbound` on the user path

Backup/Restore/Migrate UI, accessibility package re-smoke, and A3 producer
closeout remain out of scope (M4).

| Gate | Result |
|------|--------|
| `dotnet build Zaide.slnx --no-incremental` | PASS — 0 warnings, 0 errors |
| M3 usage filter | PASS — 53/53 (includes discovered `Phase22UsageSurfaceTests` and `Phase22UsageProducerTests`) |
| Architecture / DI inventory ratchets | PASS |
| `dotnet test Zaide.slnx --no-build` (serial fallback) | PASS — 3987/3987 |
| `git diff --check` | PASS |

M3-only implementation was authorized under separate human GO. Do not start M4,
Phase 22.5, G5, or V4 without separate authorization. No product-readiness
claim.

## M4 Integration Closeout (2026-08-05) — complete

M4 closes the integrated Townhall transparency surface without adding product
semantics beyond M1–M3:

- integrated production reachability for Trace, Memory, and Usage together
  (`agent.trace.open` / `agent.memory.open` / `agent.usage.open`, Townhall
  named entry buttons, real Loading/Ready/Empty/Unavailable/Failed states)
- real accessibility coverage against live panels: keyboard tab stops, named
  controls, screen-reader value/help text, and bounded paging
- lifecycle Backup failure-path fix: missing/unavailable partitions return a
  truthful `NotFound`/`Rejected` package with an empty path instead of
  throwing; Restore and Migrate remain non-user surfaces
- regression gates for `Phase22Transparency*`, Phase 21 integration/export/
  backup, DI/architecture, and full serial suite
- isolated A3 re-smoke of `A1-TC-02`, `A1-TC-03`, and `A1-TC-08` once per
  sibling backend via `tests/a3-transparency/runner/` published out of tree

| Gate | Result |
|------|--------|
| `dotnet build Zaide.slnx --no-incremental` | PASS — 0 warnings, 0 errors |
| M4 transparency/regression filter | PASS — 113/113 (named `Phase22Transparency*` classes discovered) |
| `dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings` | PASS — 4000/4000 (serial fallback; accepted full-suite mode) |
| Isolated A3 Native Harness (`A1-TC-02/03/08`) | PASS — 55/55 WORKS — [evidence/A1-TC-02-03-08-native-harness.json](./evidence/A1-TC-02-03-08-native-harness.json) |
| Isolated A3 ACP (`A1-TC-02/03/08`) | PASS — 54/54 WORKS — [evidence/A1-TC-02-03-08-acp.json](./evidence/A1-TC-02-03-08-acp.json) |
| `git diff --check` | PASS |

M4-only implementation was authorized under separate human GO. No
product-readiness claim. G5 and V4 remain unauthorized.
