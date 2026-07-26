# Refactor 9: Test Execution Structure — Implementation Plan

## Pre-Implementation Verification

- [x] Confirmed the clean baseline at `e1873ce8` on
      `perf/test-structure-phase1`.
- [x] Confirmed the fast suite is green at 3065/3065 and that the repository
      already has isolated Phase 16, PTY, and Avalonia collections.
- [x] Confirmed there is no shared slow-test trait or dedicated external
      resource boundary.

## Scope

**Goal:** Reduce test-run contention and make expensive external-resource
tests explicitly selectable without changing production behavior.

**Boundaries:** No CUDA/SIMD work, no production singleton redesign, no
impacted-test engine, and no broad `Task.Delay` rewrite in this refactor.
Those are later milestones and must retain independent verification gates.

## Milestones

| Milestone | Description | Test |
|---|---|---|
| M0 | Baseline and live ownership audit | `dotnet test Zaide.slnx --no-build` |
| M1 | Mark process/PTY/DAP proof tests as `SlowIntegration` and serialize them through one external-resource collection | filtered fast/slow commands plus full suite |
| M2 | Replace high-value polling waits with event/signal-based synchronization | focused affected tests, then full suite |
| M3 | Share Avalonia/ReactiveUI bootstrap with resettable test infrastructure | UI-focused tests, then full suite |
| M4 | Reuse immutable filesystem fixtures and isolate writable cases | project/language/filesystem-focused tests, then full suite |
| M5 | Measure and split remaining gate/singleton contention; document impacted-test feasibility | full suite plus timing/process evidence |

## M1 Entry and Exit Conditions

- External process, PTY, and production DAP proof tests carry the
  `SlowIntegration` trait.
- Those tests share a collection with parallelization disabled.
- The default test set remains behaviorally unchanged; filtered commands prove
  that the slow set is selectable and the ordinary set remains green.
- No testhost, fixture process, PTY child, or adapter process remains after the
  focused slow run beyond pre-existing processes measured before the run.

## M1 Verification Record

| Gate | Result |
|---|---|
| `dotnet build Zaide.slnx --no-restore` | pass, 0 errors, 4 existing warnings |
| `Category=SlowIntegration` | pass, 36/36 in 9.663s |
| `Category!=SlowIntegration` | pass, 3029/3029 in 9.127s |
| Full unfiltered run | blocked by pre-existing orphaned test sessions; the run started for this gate was cancelled after inspection, with testhost count unchanged at 7 and external-process count unchanged at 3 |
| `git diff --check` | pass |

## Limitations

- A trait is a selection boundary, not a performance fix by itself.
- Tests that use only in-memory fakes remain in the ordinary suite even when
  their production area also has integration proofs.
- The current repository does not yet have dependency graph data for
  impacted-test selection; that remains M5 investigation.

## Rollback Plan

Revert the M1 commit and return to `e1873ce8`.
