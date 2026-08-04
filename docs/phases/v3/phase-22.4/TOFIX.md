# Phase 22.4: Trace / Memory / Usage User Surfaces — TOFIX

## Status

**M0 live-seam verification complete; awaiting human acceptance; not
implemented.** A4 package 4 is assigned here. Phase 22.2 is complete with
package-2 PASS restored, so the ordering dependency is satisfied. M1–M4 remain
unauthorized until human M0 acceptance and a separate implementation approval.

## Work Board

- [x] Draft trace, memory, usage/cost, Phase 21 preservation, and re-smoke
  boundaries.
- [x] Wait for accepted Phase 22.2 closeout (package PASS restored).
- [x] Obtain authorization for Phase 22.4 M0 documentation only.
- [x] Verify production reachability and application ownership in read-only M0.
- [x] Publish the verified seams, exact filters, isolated A3 procedure,
  rollback, backup, migration, and stop boundaries in
  [M0_SEAM_VERIFICATION.md](./M0_SEAM_VERIFICATION.md).
- [ ] Record explicit M0 acceptance and separate implementation approval.
- [ ] Implement only accepted surface milestones.
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

- Human M0 acceptance is not recorded.
- Phase 22.4 implementation approval is not recorded.
- G5 remains blocked until accepted M1–M4 implementation and the owned
  affected-row re-smoke complete.

## Next Task

Human review of [M0_SEAM_VERIFICATION.md](./M0_SEAM_VERIFICATION.md) is the next
task. If M0 is accepted, request a separate M1-only implementation approval.
Do not begin M1, Phase 22.5, G5, or V4 from this board.
