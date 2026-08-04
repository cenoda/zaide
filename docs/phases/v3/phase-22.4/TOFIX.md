# Phase 22.4: Trace / Memory / Usage User Surfaces — TOFIX

## Status

**M0 and M1 are complete; M2-only implementation is authorized.** A4 package
4 is assigned here. Phase 22.2 is complete with package-2 PASS restored, so
the ordering dependency is satisfied. M3–M4 remain unauthorized.

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
- [ ] Implement M2 — scoped memory lifecycle surface only.
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

- M2 implementation is authorized; M3–M4 remain unauthorized.
- G5 remains blocked until accepted M1–M4 implementation and the owned
  affected-row re-smoke complete.

## Next Task

Implement M2 — scoped memory lifecycle surface only — within the accepted M0
boundary. Do not begin M3–M4, Phase 22.5, G5, or V4 from this board.

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
