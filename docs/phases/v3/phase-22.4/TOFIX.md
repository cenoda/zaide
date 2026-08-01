# Phase 22.4: Trace / Memory / Usage User Surfaces — TOFIX

## Status

**Planning; not implemented; dependency blocked.** A4 package 4 is assigned
here. Phase 22.2 must complete and pass local re-smoke first.

## Work Board

- [x] Draft trace, memory, usage/cost, Phase 21 preservation, and re-smoke
  boundaries.
- [ ] Wait for accepted Phase 22.2 closeout.
- [ ] Verify production reachability and application ownership in read-only M0.
- [ ] Record explicit M0 acceptance and separate implementation approval.
- [ ] Implement only accepted surface milestones.
- [ ] Re-smoke `A1-TC-02`, `A1-TC-03`, and `A1-TC-08`.

## Next Task

After 22.2 closeout and explicit authorization, perform M0 live-seam
verification. Do not infer user reachability from existing DI registrations or
view-model classes.
