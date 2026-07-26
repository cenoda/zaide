# Refactor 9: Test Execution Structure — TOFIX

## Status

M0 live audit is complete. The clean baseline is commit `e1873ce8` on
`perf/test-structure-phase1`; the fast suite passed 3065/3065 in the prior
closeout. M1 is implemented and ready for review.

## Current findings

- Phase 16 and Linux PTY tests already have isolated collections, but their
  external-resource nature is not selectable through a common trait.
- Production DAP proof tests start adapter and fixture processes when their
  prerequisites are present, but are not isolated from one another by a shared
  collection.
- `Task.Delay` polling, repeated ReactiveUI bootstrap, filesystem setup, and
  gate contention remain separate follow-up slices.

## M1 result (2026-07-26)

- Added the `SlowIntegration` category to Phase 16 sandbox, Linux PTY, and
  production DAP proof tests.
- Added the shared `SlowExternalResources` collection with parallelization
  disabled for DAP/process proof classes.
- The slow selection passed 36/36 in 9.663 seconds.
- The ordinary selection passed 3029/3029 in 9.127 seconds.
- The unfiltered regression could not be completed because the shared host
  already contained seven stale testhost/vstest processes and three unrelated
  external processes; the attempted run left those counts unchanged.

The two filtered selections cover all 3065 discovered tests. The unfiltered
gate must be repeated on a clean test host before treating the refactor as
fully closed.

## Next task

After review, repeat the unfiltered full regression on a clean test host. If
it is green, continue with M2: replace the highest-value `Task.Delay` polling
sites with event/signal-based waits.
