# ISSUE-005: `dotnet test` hangs under redirected output

**Label:** BUG
**Status:** closed
**Priority:** high
**Related:** `tests/Zaide.Tests/XunitSettings.cs`, `tests/Zaide.Tests/Views/TerminalRenderControlTests.cs`, `tests/Zaide.Tests/Views/TextStylesTests.cs`

## Description

`dotnet test Zaide.slnx --no-build` could finish all visible assertions but keep
`dotnet`, `vstest.console`, and `testhost.dll` alive when stdout/stderr were
redirected through a shell pipeline, for example:

```shell
timeout 120 dotnet test Zaide.slnx --no-build 2>&1 | tail -5
```

The failure was easiest to reproduce in the `Zaide.Tests.Views` subset after
Avalonia static property registration tests and `TextBlock` factory tests ran in
parallel. The command looked like a test loop, but process inspection showed the
testhost stuck after test execution started and no application child process was
left running.

This issue is closed because the current test assembly now disables xUnit test
collection parallelization. It remains documented because future Avalonia UI
tests can reintroduce global initialization races if parallelization is enabled
again.

## Steps to Reproduce

1. Build the repo.
2. Run:
   ```shell
   timeout 120 dotnet test Zaide.slnx --no-build 2>&1 | tail -5
   ```
3. If diagnosing the pipeline status, run the same command under
   `bash -o pipefail` so `tail` does not hide `timeout` exit code `124`.

**Expected behavior:** The command exits after the test summary is emitted.
**Actual behavior:** The command can time out while only the startup banner is
printed by `tail`.

## Debug Log

### Attempt 1: Check terminal integration tests
- **Hypothesis:** `LinuxTerminalServiceTests` leaked a shell, PTY fd, or reader
  thread.
- **Action:** Ran the terminal integration class repeatedly under redirected
  output and inspected live process trees during a full-suite hang.
- **Result:** The terminal class passed repeatedly, and stuck full-suite runs
  had no child bash process left.
- **Error / Output:** Only `dotnet test`, `vstest.console`, and `testhost.dll`
  remained alive.

### Attempt 2: Bisect by test namespace and class combinations
- **Hypothesis:** A smaller view-test subset triggered the shutdown hang.
- **Action:** Ran redirected subsets with `pipefail`; `Zaide.Tests.Views`
  reproduced the hang while individual view test classes passed alone.
- **Result:** The repro required multiple view classes, including Avalonia
  property-system and `TextBlock` construction tests.
- **Error / Output:** Detailed output stopped around the Avalonia-facing view
  tests before the process timed out.

### Attempt 3: Serialize xUnit test execution
- **Hypothesis:** Avalonia global/static initialization is unsafe under xUnit
  test collection parallelization.
- **Action:** Disabled xUnit test collection parallelization for
  `Zaide.Tests`.
- **Result:** The minimal redirected `Views` subset, the exact full-solution
  command, and repeated full-solution runs with `pipefail` all exited cleanly.
- **Error / Output:** `413` tests passed.

## Resolution

- **Root cause:** Avalonia-facing tests touched global/static UI infrastructure
  concurrently under xUnit collection parallelization. In redirected-output
  runs, that could leave the VSTest testhost alive after visible test progress
  stopped.
- **Fix:** Disable xUnit test collection parallelization for the test assembly.
- **Commit:** This commit.
- **Closed date:** 2026-07-06
