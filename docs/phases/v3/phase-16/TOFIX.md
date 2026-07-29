# Phase 16: Controlled Native Harness Evaluation — TOFIX

## Status

Phase 16 is parked. M0, M1, M2a, and M2b are historical completed work. The
Qwen observational qualification path was reverted; see `REVERT_LOG.md`.

Phase 16 is not a production Native Harness implementation gate and does not
block planning an independent production Native Harness or ACP phase.

## Current work

- [x] Define the post-Phase-16 V3 roadmap continuation before starting a new
      V3 implementation phase. This was accepted on 2026-07-24.
- [x] Harden the repository-owned Phase 16 sandbox lifecycle test on branch
      `perf/test-structure-phase1`: cancellation is recorded independently of
      the process-exit race, process-tree termination runs from the cleanup
      path, and the cancellation test uses `CancelAfter` rather than a test
      body delay.

  Verification record:
  - Focused cancellation test: 10/10 passed after the change.
  - Phase 16 evaluation collection: 75/75 passed.
  - Full serial suite: 3065/3065 passed in 83.3 seconds. The earlier 52-second
    baseline was not reproduced on the contaminated host, so this change is
    not being claimed as a suite-time improvement yet.
  - The baseline environment already contained 4 testhost processes and 29
    sandbox/fixture-matching processes; after verification, no Phase 16 marker
    process or Bubblewrap process remained, and the 3 remaining testhosts were
    pre-existing processes outside this run.

## Next task

Roadmap V3 is complete and closed. Phase 16 remains parked historical
evaluation work with no active task. Do not resume candidate qualification
unless a future roadmap explicitly assigns and authorizes that work.
