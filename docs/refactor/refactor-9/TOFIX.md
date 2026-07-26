# Refactor 9: Test Execution Structure — TOFIX

## Status

M0 live audit is complete. The clean baseline is commit `e1873ce8` on
`perf/test-structure-phase1`; the fast suite passed 3065/3065 in the prior
closeout. M1, M2a, M2b, M2c, and M2d are implemented and ready for review.

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

## M2a result (2026-07-26)

- Replaced fixed `Task.Delay` waits in `ProjectContextServiceTests` and
  `ProjectContextServiceIntegrationTests` with snapshot-state, snapshot-count,
  and actual-task completion signals.
- Focused project-system gate: 48/48 passed in 0.569 seconds.
- No fixed-delay polling remains in either project-context test file.

The remaining polling inventory is intentionally not part of M2a. The next
slice is the agent-session/application polling cluster, followed by language
service polling.

## M2b result (2026-07-26)

- Replaced the agent-session admission delay with the typed running-session
  signal.
- Replaced cancellation delays in agent execution and legacy backend tests
  with backend/read-start completion signals.
- Focused agent gate: 94/94 passed in 7.598 seconds.

## M2c result (2026-07-26)

- Converted the parity test `WaitUntilAsync` and `WaitForRunningAsync` helpers
  from fixed-interval polling to `IAgentSessionService.Events` signals.
- Added explicit second-run admission completion signals where the observed
  state change and test variable assignment had a race.
- Focused parity gate: 28/28 passed in 7 seconds.
- No `Task.Delay` remains in `AgentSessionCoordinatorParityTests`.

## M2d result (2026-07-26)

- Converted language navigation cancellation and language symbol dismissal
  waits to language-service change-stream signals.
- Focused language navigation/symbol gate: 32/32 passed.
- Stale-response tests retain bounded delays because those production paths can
  complete without publishing a terminal snapshot; replacing those waits
  requires a request-completion seam rather than a test-only polling trick.

## Full coverage verification (2026-07-26)

- Ordinary selection: 3029/3029 passed in 9 seconds.
- Slow integration selection: 36/36 passed in 9 seconds.
- Combined coverage: 3065/3065 passed with 0 failures.
- Testhost count remained 7 before and after; external-process count remained
  3 before and after.
- The two selections cover every discovered test. The plain unfiltered command
  remains unsuitable on this contaminated shared host because stale testhost
  sessions already exist before the run.

## Next task

After review, repeat the unfiltered full regression on a clean test host. If
it is green, continue with M2e: add a narrowly scoped request-completion seam
for stale language responses, then continue the remaining language polling
inventory.
