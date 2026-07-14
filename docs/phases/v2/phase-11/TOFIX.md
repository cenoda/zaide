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

## Resolved — F3: Hung process holds the global Build/Run/Test slot forever

**Severity:** High (product risk; partial YAGNI accept)  
**Area:** `ManagedProcessRunner.RunAsync`, cancel discovery

**Problem:** `WaitForExitAsync` has **no overall operation timeout**.
`TimedOut` is deferred in the plan (contract 5). A hung `dotnet` (or child)
occupies the one-at-a-time slot until the user cancels. Cancel worked via
API/UI, but `project.cancel` had **no default keybinding**; discoverability
depended on palette search or panel-local Cancel buttons.

**Resolution (2026-07-14):** **Cancel discoverability** — not operation timeout.

**Product decision:** Improve discoverability via **default keyboard gesture**
(`Ctrl+F2`, JetBrains-style Stop) **plus** shared Cancel UI on Output and Test
Results (F6). **Do not** add a bounded operation timeout or `TimedOut` outcome
in F3 — contract 5 keeps timeout deferred; killing a hung process still
requires explicit cancel (gesture, palette, or panel button). Never map hang to
`Failed`.

- **`ProjectWorkflowViewModel`** — `project.cancel` default gesture
  `Ctrl+F2`; materialized window-wide via existing registry bindings so cancel
  is reachable during Build / Run / Test regardless of bottom-panel mode or
  focus. `CanExecute` remains operation-active only (one-at-a-time preserved).
- **Shared Cancel UI** — unchanged from F6 (Output + Test Results headers).

**Tests:** `BuildAndCancel_AreRegisteredWithMetadata`,
`Cancel_DefaultGesture_ResolvesToProjectCancel`,
`Cancel_RegistryExecute_WhenIdle_ReturnsFalse`, existing
`Cancel_InvokesCancelAsyncWhileRunning` / run+test cancel tests,
`CanonicalCommandRegistrationTests.DefaultGestures_MatchD6a`.

**Remaining limitation:** No overall operation timeout; a hung `dotnet` still
holds the slot until the user cancels. `TimedOut` remains a future plan-aware
change if product needs automatic release.

**Gates:** `dotnet build`, `dotnet test`, `git diff --check`.

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

## Resolved — F6: Test UX — no Cancel on Test Results; Output flash then hide

**Severity:** Medium (UX)  
**Area:** `MainWindowViewModel` show-on-test, `TestResultsPanel`,
`ProjectWorkflowViewModel`

**Resolution (2026-07-14):** Added Cancel to Test Results and truthful cancel
automation names for Build / Run / Test.

- **`ProjectWorkflowStatusPolicy.MapCancelAutomationName`** — derives
  screen-reader name from active (or last) operation: "Cancel build",
  "Cancel run", "Cancel tests".
- **`ProjectWorkflowViewModel.CancelAutomationName`** — exposed on the workflow
  VM; updated in `ApplyStateOnly` alongside status text.
- **`OutputPanel`** — binds Cancel automation name from the workflow VM instead
  of hardcoded "Cancel build".
- **`TestResultsViewModel.Workflow`** — holds the shared
  `ProjectWorkflowViewModel` for cancel chrome (same `CancelCommand` /
  `project.cancel`; no second command system).
- **`TestResultsPanel`** — Cancel button in the header, mirroring Output:
  visibility via `IsOperationActive`, click invokes `CancelCommand`.

**Tests:** `MapCancelAutomationName_*` (status policy),
`ViewModel_CancelAutomationName_MatchesActiveOperation` (output VM),
`Workflow_CancelCommand_IsEnabledWhileTestOperationActive` and
`Workflow_CancelAutomationName_MatchesActiveOperation` (test-results VM).

**Gates:** `dotnet build`, focused `TestResults` / `ProjectWorkflow` / `Output`
tests, full suite, `git diff --check` all pass.

---

## Resolved — F7: Console test parse brittle without locked verbosity/logger

**Severity:** Medium  
**Area:** `ProjectExecutionProfileResolver`, `TestResultsParser`,
`TestResultsService`

**Problem:** U4 console-first parse was best-effort. Default `dotnet test`
often emits **summary only** (empty case list). xUnit/VSTest format variants
and missing failed-case rows could mark `IsPartial` and push users to Output.
Navigable failed cases depended on stack frames matching `at … in path:line N`
only.

**Resolution (2026-07-14):** **Keep locked test argv unchanged** (no
`--logger` / verbosity flags). Default `dotnet test` already emits the
`Passed!` / `Failed!` summary banner; adding console logger verbosity would
increase Output noise without improving pass-run case detail. **Strengthen
parser + structural completeness** instead.

- **`ProjectExecutionProfileResolver`** — documents that test profile stays
  `dotnet test "<path>"`; parser handles alternate console shapes fail-open.
- **`TestResultsParser`** — VSTest `Total tests` / indented count lines;
  xUnit `[FAIL]` banners; `path(line,col): at …` stack frames alongside
  `in path:line N`; deduped failed-case assembly; `Test Run Successful`
  without counts stays incomplete; never invents passes.
- **`TestResultsService`** — `IsStructurallyComplete`: summary with
  `Failed > 0` but fewer parsed failed cases than the summary reports →
  `IsPartial` (user falls back to Output). Summary-only pass runs remain
  complete with empty cases.

**Tests:** Expanded `TestResultsParserTests` (default + VSTest pass/fail,
xUnit stack variants, malformed/truncated output, non-navigable stacks) and
`TestResultsServiceTests` (summary-only pass not partial; fail summary without
cases partial; VSTest summary path).

**Remaining limitations:** TRX deferred; NUnit/MSTest-only shapes unsupported;
multi-failure runs with fewer parsed cases than `Failed` count stay partial;
non-English CLI output unsupported (see F8 for build diagnostics); custom
`dotnet test` logger/verbosity from user env not locked.

**Gates:** `dotnet build`, `dotnet test`, `git diff --check`.

---

## Resolved — F8: Build diagnostic parse is English-only MSBuild CLI shape

**Severity:** Medium (locale / tooling)
**Area:** `BuildDiagnosticParser`

**Problem:** Regex required English `error`/`warning` and
`path(line[,col]):` form only. Localized MSBuild, multi-line messages, or
non-CLI tooling → empty Problems while Output still shows failures.

**Resolution (2026-07-14):** Documented invariant English CLI policy and
expanded parser to cover additional MSBuild severity keywords.

- **Invariant CLI policy:** `BuildDiagnosticParser` regex uses
  `CultureInvariant` and matches English severity keywords only
  (`error`, `warning`, `done`, `message`). `DOTNET_CLI_UI_LANGUAGE=en`
  should be set on build child processes to guarantee English output
  regardless of host locale. Enforcement of this environment variable in
  `ManagedProcessRunner` is deferred (requires process-request
  infrastructure change beyond F8 scope).
- **Parser expansion:** Regex now matches `done` (→ `Information`) and
  `message` (→ `Hint`) severity keywords alongside existing `error` and
  `warning`. Code-less diagnostics (`path(line): severity message`
  without a `CODE:` prefix) are explicitly supported. All severity
  matching is case-insensitive. Malformed and unsupported-severity lines
  continue to fail open (silently ignored).
- **LSP retention:** Unchanged — build parse misses never clear LSP
  diagnostics (existing merge-by-source policy preserved).

**Tests:** `Parse_DiagnosticWithoutCode_ParsesMessageOnly`,
`Parse_DoneSeverity_MapsToInformation`,
`Parse_MessageSeverity_MapsToHint`,
`Parse_SeverityKeyword_IsCaseInsensitive`,
`Parse_MixedSeverities_AllParsedAndSorted`,
`Parse_DoneWithoutCode_ParsesCorrectly`,
`Parse_UnsupportedSeverity_IsIgnored`,
`BuildComplete_ParsesDoneAndMessageSeverities` (service flow-through).
Existing English fixtures unchanged.

**Remaining limitations:** Non-English CLI output still unparseable
without `DOTNET_CLI_UI_LANGUAGE=en` enforcement in the process runner;
multi-line diagnostic messages (continuation lines) unsupported;
paths containing unbalanced parentheses remain an edge case.

**Gates:** `dotnet build`, `dotnet test`, `git diff --check`.

---

## Resolved — F9: No save-before-build / run / test

**Severity:** Medium (product footgun; out of Phase 11 exit scope)  
**Area:** `ProjectWorkflowViewModel` / workflow start path, editor dirty state

**Resolution (2026-07-14):** Auto-save all dirty editor tabs before Build, Run,
or Test. Any save failure prevents the workflow from starting and surfaces the
error via the existing `LastSaveError` → status-bar subscription.

**Policy: automatic save (not prompt-based).** Rationale: prompting before every
build is disruptive to the edit→build→test loop. The user already has dirty-state
visibility via the ● prefix on tab names. If a save fails (disk full, permissions),
the workflow is blocked and the error is surfaced truthfully.

- **`EditorTabViewModel.SaveAllDirtyTabsAsync`** — iterates all `OpenTabs`;
  for each dirty tab calls `EditorViewModel.SaveCommand.Execute()` (reusing the
  existing save seam, including Format on Save). Stops on first failure, sets
  `LastSaveError`, and returns `false`. Returns `true` when all dirty tabs were
  saved or no tabs were dirty.
- **`ProjectWorkflowViewModel.SaveAllDirtyTabsAsync`** — internal delegate
  property (`Func<Task<bool>>?`). Null by default so existing tests that don't
  wire it are unaffected. In `ExecuteBuildAsync` / `ExecuteRunAsync` /
  `ExecuteTestAsync`, the delegate is called via `EnsureDirtyTabsSavedAsync()`
  before the workflow service. If the delegate returns `false`, the workflow is
  never started.
- **`MainWindowViewModel` constructor** — wires the delegate:
  `ProjectWorkflowViewModel.SaveAllDirtyTabsAsync = () => editorTabViewModel.SaveAllDirtyTabsAsync();`

**Tests (15 new — 5 EditorTabViewModel + 10 ProjectWorkflowSaveBeforeStart):**

| Test | What it proves |
|---|---|
| `SaveAllDirtyTabs_NoDirtyTabs_ReturnsTrue` | Clean buffers → no saves, returns true |
| `SaveAllDirtyTabs_SavesAllDirtyTabs` | Multiple dirty tabs → all saved, all clean |
| `SaveAllDirtyTabs_SaveFailure_StopsAndReturnsFalse` | Save fails → LastSaveError set, returns false |
| `SaveAllDirtyTabs_SkipsCleanTabs` | Only dirty tabs are saved; clean tabs untouched |
| `SaveAllDirtyTabs_MultipleDirty_StopsOnFirstFailure` | Mid-iteration failure → later tabs not saved |
| `Build_SavesDirtyTabsBeforeStart` | Save delegate called; Build proceeds |
| `Run_SavesDirtyTabsBeforeStart` | Save delegate called; Run proceeds |
| `Test_SavesDirtyTabsBeforeStart` | Save delegate called; Test proceeds |
| `Build_SaveFailure_PreventsWorkflowStart` | Save fails → Build not started |
| `Run_SaveFailure_PreventsWorkflowStart` | Save fails → Run not started |
| `Test_SaveFailure_PreventsWorkflowStart` | Save fails → Test not started |
| `Build_CleanBuffers_DoesNotTriggerUnnecessarySaves` | Delegate invoked each time; dirty check is delegate's responsibility |
| `Build_WhenDelegateNotWired_ProceedsToWorkflow` | Null delegate → no-op guard, backward compat |
| `AllCommands_ShareSaveBeforeStartPolicy` | Build+Run+Test each call save once, then proceed |
| `AllCommands_ShareSaveFailurePolicy` | Build+Run+Test all blocked by save failure |

**Remaining limitations:**
- Untitled (unsaved, no-file-path) tabs are skipped by `SaveCommand` (it returns
  `false` for empty `FilePath`), which blocks workflow start. This is conservative
  — the user must explicitly save-as or close the untitled tab before building.
- The save delegate is called unconditionally before each workflow; the dirty
  check is delegated to `SaveAllDirtyTabsAsync` inside `EditorTabViewModel`.
  This means the status-bar `LastSaveError` subscription fires even when the
  workflow was invoked via the command palette or keybinding (not only from the
  Output panel). This is intentional — the user should see the save error
  regardless of how they invoked the workflow.
- No prompt for unsaved new (untitled) buffers — they block the workflow.
  A future enhancement could offer a save-as dialog for untitled dirty tabs.

**Gates:** `dotnet build`, `dotnet test` (full 1881), `git diff --check` all pass.

---

## Resolved — F10: Downstream workflow services not disposed on app exit

**Severity:** Low  
**Area:** `App.axaml.cs`, `ProjectOutputService`, `BuildDiagnosticsService`,
`TestResultsService`

**Problem:** Exit disposed `IProjectWorkflowService` (and language stack) but
not Output / build-diagnostics / test-results services. They usually completed
when workflow subjects completed; incomplete hygiene vs explicit language
dispose. Workflow also **owns dispose of shared `IManagedProcessRunner`** —
fine today, fragile if another owner appears.

**Resolution (2026-07-14):** Explicit projection dispose on the locked exit
path, after workflow process teardown.

**Dispose order (locked):**

1. Resolve `IProjectOutputService`, `IBuildDiagnosticsService`, and
   `ITestResultsService` singletons (supports lazy DI — constructors subscribe
   to workflow and must not run after workflow subjects are disposed).
2. `IProjectWorkflowService` — cancel/kill managed `dotnet` trees and complete
   workflow subjects (unchanged M1 rule; must stay before language).
3. Projection services (resolved above) — release workflow subscriptions and
   complete projection subjects. After workflow so processes are killed first;
   before language so projection teardown does not race language shutdown.
4. Language stack → `IProjectContextService` → `ITerminalHost` (unchanged).

- **`App.DisposeServicesOnExit`** — extracted exit sequence for tests; desktop
  `Exit` handler delegates to it.
- **`IProjectOutputService`** — now extends `IDisposable`; implementation is
  idempotent.
- **`BuildDiagnosticsService` / `TestResultsService`** — unchanged dispose
  semantics; now invoked explicitly on exit.

**Tests:** `ProjectWorkflowProjectionShutdownTests` — exit path disposes all
three projections; subscription release and subject completion; workflow kill
before language; dispose order workflow → projections → language; repeated
dispose safe; single shared `IManagedProcessRunner` registration.

**Remaining limitation:** `IManagedProcessRunner` remains a public DI singleton
disposed only by `ProjectWorkflowService`. A second owner would require an
explicit ownership contract change — not introduced in F10.

**Gates:** `dotnet build`, `dotnet test`, `git diff --check`.

---

## Open — F11: Polish (a11y, scroll, path identity)

**Severity:** Low  
**Area:** `OutputPanel`, `TestResultsPanel`, context-cancel path compare

| Item | Detail |
|---|---|
| Cancel a11y | ~~Automation name always "Cancel build" for Run/Test~~ (F6) |
| Output scroll | Full list rebuild jumps scroll/selection; no stick-to-end |
| Cancel gesture | ~~Plan allows none~~ — F3: default `Ctrl+F2` + palette + panel buttons |
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
- Dispose **order** policy: workflow → projections → language
- Command IDs/gestures on `ICommandRegistry`
- YAGNI: no task runner, DAP, OutputType probe, auto-build on folder open

---

## Suggested fix order

1. ~~**F1** — Output incremental / non-O(n²) projection~~
2. ~~**F2** — Gate-safe Dispose~~
3. ~~**F4** — ProcessId after start~~
4. ~~**F5** — Residual stream line~~
5. ~~**F6** — Test Results Cancel + a11y~~
6. ~~**F3** — Timeout and/or cancel discoverability (product decision)~~
7. ~~**F7** — console test parse hardening~~ / ~~**F8** — build diagnostic parse hardening~~
8. ~~**F9** — save-before-build/run/test~~
9. ~~**F10** — projection dispose on exit~~ / **F11** — product polish

Prefer one focused commit per finding (or tightly related pair). Re-run
sequential full gates after each code fix.

---

*Recorded: 2026-07-14 (post-Phase-11 closeout audit).*
