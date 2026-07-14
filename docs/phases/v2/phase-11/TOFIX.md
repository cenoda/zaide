# Phase 11: Project Workflow — TOFIX

**Status:** Phase 11 complete (M0–M6 closeout, 2026-07-14). Post-closeout
implementation audit recorded open findings below. Items are ordered by
recommended fix priority (highest first).

**Source:** Live-code audit of `IProjectWorkflowService` / `ManagedProcessRunner`,
Output / Problems / Test Results projection, and app dispose wiring against
[IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) contracts 1–8.

**Gates at audit time (claimed at M6; not re-run for this doc):** sequential
`dotnet build Zaide.slnx --no-restore`, `dotnet test Zaide.slnx --no-build`,
`git diff --check` — see [M6_MANUAL_EVIDENCE.md](M6_MANUAL_EVIDENCE.md).

---

## Resolved — F1: Output projection is O(n²) and UI-hostile under real builds

**Severity:** High  
**Area:** `ProjectWorkflowService.AppendOutputLine`, `ProjectOutputService`,
`ProjectWorkflowViewModel.ApplySnapshot`, `OutputPanel`

**Resolution (2026-07-14):** Converted UI projection from full-snapshot rebuild
per line to append-only line deltas.

- **`IProjectOutputService.WhenLineReceived`** — new per-line observable,
  forwards `WhenOutputReceived` so the VM can subscribe to line deltas
  without snapshot overhead.
- **`ProjectWorkflowService.AppendOutputLine`** — no longer publishes a
  full workflow snapshot per line. Only emits on `_outputSubject` and
  silently updates `_current` for the polling `Current` property.
  State-transition publishes (PublishRunningIfCurrent, CompleteOperationAsync,
  context-change) still carry full `OutputLines` so `BuildDiagnosticsService`
  and `TestResultsService` get complete output at operation end.
- **`ProjectWorkflowViewModel`** — split `ApplySnapshot` into
  `ApplyStateOnly` (state/status only, clears lines on `Starting`) and
  `OnLineReceived` (append-only `Lines.Add`). No more `Clear()` + re-add
  per line.

**Tests:** `ViewModel_AppendsLinesWithoutRebuild` asserts items are not
reallocated across multiple line emissions; existing output/VM tests
updated to feed lines via deltas.

**Gates:** `dotnet build`, `dotnet test` (focused + full 1828), `git diff --check`
all pass.

---

## Resolved — F2: `ProjectWorkflowService.Dispose` not synchronized with the operation gate

**Severity:** High  
**Area:** `ProjectWorkflowService.Dispose`, `AppendOutputLine`,
`PublishRunningIfCurrent`, `CompleteOperationAsync`

**Resolution (2026-07-14):** Synchronized Dispose with the operation gate.

- **`Dispose`** — takes `_gate` before any teardown; sets `_disposed` under the
  gate; kills and disposes the runner; completes and disposes subjects; **must
  `Release()` then `Dispose()` the gate** (see F2 follow-up below).
- **`AppendOutputLine`** — early `_disposed` / generation check before touching the
  gate; `_gate.Wait()` and `_gate.Release()` wrapped in `try-catch` for
  `ObjectDisposedException` so late output during shutdown is silently ignored
  instead of throwing.
- **`PublishRunningIfCurrent`** — same defensive pattern as `AppendOutputLine`.
- **`CompleteOperationAsync`** — `_gate.WaitAsync()` wrapped in `try-catch` for
  `ObjectDisposedException`; returns the caller's outcome immediately when the gate
  is already disposed.

**F2 follow-up (same day, with F4):** Initial F2 disposed `_gate` **without
`Release()`**, relying on waiters seeing `ObjectDisposedException`. That is
incorrect: `SemaphoreSlim.Dispose()` does **not** wake waiters already blocked on
`Wait`/`WaitAsync` — they hang forever. Race: Dispose holds gate → KillAsync
unblocks `RunAsync` → `CompleteOperationAsync` queues `WaitAsync` → Dispose
disposes gate without Release → `await buildTask` deadlocks. Fix: `Release()`
then `Dispose()` so in-flight completion can acquire, observe `_disposed`, and
return.

**Tests:** Existing `Dispose_KillsRunnerAndReturnsToIdle` still passes. New
`Dispose_WhileFakeRunnerEmitting_DoesNotThrow` stress test: background thread
emits output continuously while `Dispose` is called; asserts no exception, idle
terminal state, runner disposed, operation cancelled.

**Gates:** `dotnet build`, `dotnet test` (focused 35 + full 1829), `git diff --check`
all pass.

---

## Open — F3: Hung process holds the global Build/Run/Test slot forever

**Severity:** High (product risk; partial YAGNI accept)  
**Area:** `ManagedProcessRunner.RunAsync`, cancel discovery

**Problem:** `WaitForExitAsync` has **no overall operation timeout**.
`TimedOut` is deferred in the plan (contract 5). A hung `dotnet` (or child)
occupies the one-at-a-time slot until the user cancels. Cancel works via
API/UI, but `project.cancel` has **no default keybinding** and Cancel is only
on the Output panel (not Test Results).

**Direction (when fixing):** Either introduce a bounded timeout + `TimedOut`
outcome (plan-aware change), or improve cancel discoverability (gesture +
Cancel on Test Results / shared chrome). Do not silently map hang to `Failed`.

**Acceptance sketch:** Documented policy; cancel always reachable during Run
and Test; tests for any new timeout kind if introduced.

---

## Resolved — F4: `ProcessId` on Running snapshots is always null/stale

**Severity:** Medium  
**Area:** `ProjectWorkflowService.StartOperationAsync` /
`PublishRunningIfCurrent`, `IManagedProcessRunner.ProcessStarted`

**Resolution (2026-07-14):** Publish `Running` only after the child process
has started, when `ProcessId` is known.

- **`IManagedProcessRunner.ProcessStarted`** — raised once after a successful
  process start (`ManagedProcessRunner` after `Process.Start()`; fake after
  setting `IsRunning`). Not raised on startup failure.
- **`ProjectWorkflowService.StartOperationAsync`** — removed the pre-`RunAsync`
  `PublishRunningIfCurrent` call that always passed null/stale PID. Subscribes
  to `ProcessStarted` and publishes `Running` with `_runner.ProcessId` then.
  Slot stays `Starting` (ProcessId null) until start; concurrent rejection
  still covers Starting + Running.
- Idle / Starting / Failed / Cancelled / StartupFailed paths keep `ProcessId`
  null via existing complete/start publishes.

**Tests:** `StartBuildAsync_WhileRunning_SnapshotProcessIdMatchesRunner`
asserts `ProcessId == 9001` (fake) while Running and null when Idle after
success. `StartupFailed_SnapshotProcessIdRemainsNull` asserts null PID on
startup failure.

**Gates:** sequential `dotnet build`, focused `ProjectWorkflow` tests, full
suite, `git diff --check`.

---

## Resolved — F5: Last partial stream line dropped (no trailing newline)

**Severity:** Medium  
**Area:** `ManagedProcessRunner.PumpStreamAsync`

**Resolution (2026-07-14):** Replaced `ReadLineAsync` with `ReadAsync` and
manual `\n`/`\r\n` splitting using a `StringBuilder` remainder. On stream end
(EOF, cancel, or `IOException`), a `finally` block flushes any residual buffer
that was never terminated by a newline.

- **`PumpStreamAsync`** — now reads with `reader.ReadAsync(Memory<char>, CT)`
  into a 4 KiB char buffer, feeds chunks into a `StringBuilder` remainder, and
  extracts complete lines via `EmitCompleteLines`. A `try-finally` block ensures
  the remainder is always emitted, even when the pump exits via cancellation or
  `IOException` (the paths where `ReadLineAsync` silently drops buffered data).
- **`EmitCompleteLines`** — extracts and emits complete lines separated by `\n`
  (with `\r` stripping before `\n`).
- **`IndexOfNewline`** — linear scan for `\n` in the remainder.

**Tests:**
- `RunAsync_UnterminatedFinalLineAfterCancel_EmitsResidualLine` — writes
  "unterminated" (no `\n`) then sleeps; cancels; asserts the residual line
  appears (was empty before the fix, proving the bug).
- `RunAsync_ExitsWithoutTrailingNewline_CapturesFinalLine` — writes
  `line1\nline2\nfinal-without-newline` and exits; asserts all three lines
  including the unterminated final one are captured (regression guard).

**Gates:** `dotnet build`, `dotnet test` (focused 10 + full 1833), `git diff --check`
all pass.

---

## Open — F6: Test UX — no Cancel on Test Results; Output flash then hide

**Severity:** Medium (UX)  
**Area:** `MainWindowViewModel` show-on-test, `TestResultsPanel`,
`ProjectWorkflowViewModel`

**Problem:** On Test start, both show-Output and show-TestResults fire; the
host ends on **Test Results**. That panel has **no Cancel**. Cancel is only on
Output (automation name always "Cancel build") or command palette
(`project.cancel`, no default gesture). Users watching Test Results cannot
cancel without switching panels or palette.

**Direction:** Add Cancel to Test Results (or shared workflow chrome) bound to
`CancelCommand`; fix Cancel automation name for Build/Run/Test; optional
default gesture for cancel if product wants it (plan currently allows none).

**Acceptance sketch:** Cancel visible/enabled while Test is active on Test
Results mode; a11y name truthful; no second command system.

---

## Open — F7: Console test parse brittle without locked verbosity/logger

**Severity:** Medium  
**Area:** `ProjectExecutionProfileResolver`, `TestResultsParser`,
`TestResultsService`

**Problem:** U4 console-first parse is best-effort. Default `dotnet test`
often emits **summary only** (empty case list). No `--logger` / verbosity in
the locked profile. xUnit/VSTest format variants mark `IsPartial` and push
users to Output. Navigable failed cases depend on stack frames matching
`at … in path:line N`.

**Direction:** Optionally lock a default console logger/verbosity that still
fails open; expand parser fixtures from real `dotnet test` captures; keep “no
invented passes” contract.

**Acceptance sketch:** Pass/fail fixtures produce summary and, where console
emits cases, structured rows; junk still fail-open.

---

## Open — F8: Build diagnostic parse is English-only MSBuild CLI shape

**Severity:** Medium (locale / tooling)  
**Area:** `BuildDiagnosticParser`

**Problem:** Regex requires English `error`/`warning` and
`path(line[,col]):` form only. Localized MSBuild, multi-line messages, or
non-CLI tooling → empty Problems while Output still shows failures.

**Direction:** Document as known limit, or add limited alternate patterns /
invariant culture CLI env if product needs it. Do not clear LSP on parse miss.

**Acceptance sketch:** Existing English fixtures still parse; any new patterns
have unit tests; LSP retention tests unchanged.

---

## Open — F9: No save-before-build / run / test

**Severity:** Medium (product footgun; out of Phase 11 exit scope)  
**Area:** `ProjectWorkflowViewModel` / workflow start path, editor dirty state

**Problem:** Unsaved editor buffers are not flushed before `dotnet`. Build/run/test
can run against disk that diverges from open tabs.

**Direction:** Optional dirty-tab save prompt or auto-save policy before
workflow start; reuse existing file-save seams only. YAGNI for Phase 11 exit;
track for Phase 13 hardening if desired.

---

## Open — F10: Downstream workflow services not disposed on app exit

**Severity:** Low  
**Area:** `App.axaml.cs`, `ProjectOutputService`, `BuildDiagnosticsService`,
`TestResultsService`

**Problem:** Exit disposes `IProjectWorkflowService` (and language stack) but
not Output / build-diagnostics / test-results services. They usually complete
when workflow subjects complete; incomplete hygiene vs explicit language
dispose. Workflow also **owns dispose of shared `IManagedProcessRunner`** —
fine today, fragile if another owner appears.

**Direction:** Explicit dispose of projection services after workflow (or
with it); keep runner ownership single and documented.

---

## Open — F11: Polish (a11y, scroll, path identity)

**Severity:** Low  
**Area:** `OutputPanel`, `TestResultsPanel`, context-cancel path compare

| Item | Detail |
|---|---|
| Cancel a11y | Automation name always "Cancel build" for Run/Test |
| Output scroll | Full list rebuild jumps scroll/selection; no stick-to-end |
| Cancel gesture | Plan allows none; discoverability is palette-only |
| Path compare | Context cancel uses `FilePath == TargetFilePath`; both are `GetFullPath` today — fragile if a future path source skips normalization |
| Interactive Run | Redirected stdio (no PTY) — documented; apps that `ReadLine` hang until Cancel |
| U7 libraries | Run enabled for all eligible `.csproj`; class libs surface `Failed` — intentional |

---

## What audit confirmed as solid (do not “fix”)

- Target eligibility (contract 1) and Run = `CSharpProject` only (U1a)
- Outcome kinds + API dual path (`RejectedConcurrent` / `RejectedContext`)
- One-at-a-time slot; context invalidation cancel
- Output vs Terminal separation; no PTY for workflow
- Problems merge: language + build; build cleared only on new **build** start
- Navigation via `OpenFileCommand` + `RequestNavigate` only
- Dispose **order** policy: workflow before language
- Command IDs/gestures on `ICommandRegistry`
- YAGNI: no task runner, DAP, OutputType probe, auto-build on folder open

---

## Suggested fix order

1. ~~**F1** — Output incremental / non-O(n²) projection~~
2. ~~**F2** — Gate-safe Dispose~~
3. ~~**F4** — ProcessId after start~~
4. ~~**F5** — Residual stream line~~
5. **F6** — Test Results Cancel + a11y
6. **F3** — Timeout and/or cancel discoverability (product decision)
7. **F7** / **F8** — parse quality
8. **F9**–**F11** — product polish / hygiene

Prefer one focused commit per finding (or tightly related pair). Re-run
sequential full gates after each code fix.

---

*Recorded: 2026-07-14 (post-Phase-11 closeout audit).*
