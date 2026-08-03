# ISSUE-012: Full test suite wall time dominated by fixed timeouts and missed process-exit cancel

**Label:** BUG  
**Status:** closed  
**Priority:** high  
**Related:** `tests/Zaide.Tests/XunitSettings.cs`, `tests/Zaide.Tests/slow.runsettings`,
ISSUE-005 (redirected-output hang; parallelization history), Refactor 9 test
structure work

## Description

The full suite (3849 tests) was far slower than the Refactor 9 baseline (~14s
for ~3065 tests under parallel mode). On this host before the fix:

| Mode | Command | Wall time |
|------|---------|-----------|
| Fast | `dotnet test Zaide.slnx --no-build` | ~51s |
| Serial | `dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings` | ~84s |

TRX analysis showed a few tests accounted for most of the sum of durations —
not a broad regression of every test.

## Steps to Reproduce

1. Build: `dotnet build Zaide.slnx`
2. Fast: `dotnet test Zaide.slnx --no-build`
3. Serial: `dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings`
4. Inspect per-test durations from a TRX logger output.

**Expected behavior:** Full suite finishes in roughly the same order of
magnitude as the post–Refactor 9 baseline (low tens of seconds), with no
single unit test waiting a full production initialize or sleep budget.

**Actual behavior:** Fast ~51s / serial ~84s. Largest single test was
`Phase20TransportLifecycleTests.ExitImmediateFixture_SurfacesProcessExitFailure`
at **exactly 30.004s** (default ACP `InitializeTimeout`).

## Debug Log

### Attempt 1: Profile both modes with TRX

- **Hypothesis:** A small number of tests with fixed delays or missed process
  termination dominate wall time.
- **Action:** Ran full fast and serial suites; parsed TRX for top durations and
  class sums.
- **Result:** Confirmed. Top offenders (pre-fix):

| Test / area | Duration | Cause |
|-------------|----------|--------|
| `ExitImmediateFixture_SurfacesProcessExitFailure` | 30s | Child exit did not cancel in-flight initialize |
| `BusyNotificationDrain_SerializesCallbacks…` | 5s | `SetDelayedCompletion(5s)` |
| `RunAsync_SecondStartWhileRunning_Throws` | 5s | Awaited full `sleep 5` |
| `Broker_ConcurrentCommandRequests_RejectSecond` | ~4s | `sleep 2` + `Thread.Sleep(2s)` hold |
| Phase16 cancel / wall proofs | up to 30s under serial load | Unbounded post-kill wait; cancel path could miss |

### Attempt 2: Fix production process-exit cancel + gate tests

- **Hypothesis:** Production ACP host should fail closed on child exit without
  waiting the full operation budget; tests should use gates/signals, not
  multi-second sleeps, when they only need an in-flight window.
- **Action:** Implemented process-exit CTS on `AcpStdioProcessHost`, sticky
  terminal state, Phase16 WhenAny cancel/wall wait and bounded forced-exit
  wait; rewrote the fixed-delay tests listed above.
- **Result:** Exit-immediate test ~12ms; focused cluster green; full suite
  improved sharply (see Resolution).

## Resolution

- **Root cause:**
  1. `AcpStdioProcessHost` observed process exit for state only; in-flight
     protocol ops kept waiting until `InitializeTimeout` (30s), then mapped
     exit after the fact.
  2. Several tests intentionally slept for seconds (busy drain, process
     runner, broker concurrency, disclosure reject) instead of gating.
  3. Phase16 lifecycle wait could run out a long `sleep` if cancel/kill did
     not abort `WaitForExitAsync` promptly under load.
- **Fix:**
  - **Production:** Link a process-exit `CancellationTokenSource` into
    operation timeouts; cancel on `Exited` / already-exited attach; do not
    regress `ProcessExited` → `Running`.
  - **Phase16:** `Task.WhenAny(exit, cancel)` for reliable cancel/wall; bound
    forced post-kill wait; shorter sleep payloads + wall safety nets in
    executor proofs.
  - **Tests:** Gated completions / ManualResetEvent holds / dispose-after-assert
    instead of multi-second delays; tighter malformed-stdout CTS (500ms).
- **Verification (post-fix, 3849/3849):**

| Mode | Wall time | Result |
|------|-----------|--------|
| Fast | ~17s | Passed 3849/3849 |
| Serial | ~39–42s | Passed 3849/3849 |

- **Commit:** `9c4bb94fb9d79694a0fba19263dc28eeca822ca7`
- **Closed date:** 2026-08-03

## Not changed

- Default fast mode remains eight-way collection parallelization
  (`MaxParallelThreads = 8`).
- Opt-in serial mode remains `tests/Zaide.Tests/slow.runsettings`.
- Redirected-output hang guidance in README / ISSUE-005 still applies for
  parallel runs under shell pipelines.
- Remaining multi-second costs are real external work (PTY, bubblewrap
  probes, language dwell policies), not accidental full-budget timeouts.
