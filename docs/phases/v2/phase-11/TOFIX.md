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

## Open — F1: Output projection is O(n²) and UI-hostile under real builds

**Severity:** High  
**Area:** `ProjectWorkflowService.AppendOutputLine`, `ProjectOutputService`,
`ProjectWorkflowViewModel.ApplySnapshot`, `OutputPanel`

**Problem:** Every stdout/stderr line does all of:

1. Append + **full list copy** + publish a new workflow snapshot
   (`_outputLines.ToArray()` inside the gate).
2. Map to another snapshot (`ProjectOutputService.Map`).
3. On the UI thread: **`Lines.Clear()` + re-add every line** as new
   `OutputLineViewModel` instances.

A multi-thousand-line MSBuild log thrashes GC and Avalonia bindings and can
freeze or jank the Output surface on non-trivial solutions.

**Direction:** Prefer line-delta events (or append-only collection updates) for
UI projection; avoid full snapshot rebuild per line. Throttle or batch UI
updates if full snapshots remain required for idle terminal state. Keep
generation filtering.

**Acceptance sketch:**

- Build / run / test still stream lines into Output.
- Idle terminal snapshot still has complete `OutputLines` for diagnostics/test
  parse consumers.
- Focused tests: output service/VM do not reallocate entire line list per line
  under a multi-line fake runner (or assert append-only behavior).
- Sequential full gates green.

---

## Open — F2: `ProjectWorkflowService.Dispose` not synchronized with the operation gate

**Severity:** High  
**Area:** `ProjectWorkflowService.Dispose`, `AppendOutputLine`,
`PublishRunningIfCurrent`, `CompleteOperationAsync`, `ManagedProcessRunner`

**Problem:** Dispose sets `_disposed`, kills the runner, completes/disposes
subjects, and **disposes `_gate` without taking `_gate`**. Concurrent
`AppendOutputLine` / completion paths may call `_gate.Wait` / `WaitAsync` and
hit `ObjectDisposedException`, or race `OnNext` after subject dispose, during
app exit or dispose-while-running tests.

App exit **order** (workflow before language) is correct in `App.axaml.cs`;
lifecycle is not mutex-safe.

**Direction:** Take the gate (or a dedicated dispose path) before tearing down;
stop pumps / cancel CTS under the gate; complete subjects; dispose gate last.
Ensure late generation-mismatched output is ignored without throwing.

**Acceptance sketch:**

- Existing dispose-kill test still passes.
- Optional stress: dispose while fake runner still emitting lines — no
  unobserved exception; idle/empty terminal state.
- Sequential full gates green.

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

## Open — F4: `ProcessId` on Running snapshots is always null/stale

**Severity:** Medium  
**Area:** `ProjectWorkflowService.StartOperationAsync` /
`PublishRunningIfCurrent`

**Problem:** Running is published **before** `IManagedProcessRunner.RunAsync`
starts the process:

```text
PublishRunningIfCurrent(..., _runner.ProcessId);  // before start
await _runner.RunAsync(...);
```

No later snapshot updates PID after start. Consumers of
`ProjectWorkflowSnapshot.ProcessId` are wrong. Tests only assert
`State == Running`, so this was uncaught.

**Direction:** Publish Running after process start (or emit a follow-up
snapshot when PID is available). Assert non-null PID (or known fake id) while
running in service tests with the fake runner that sets `IsRunning`/`ProcessId`
inside `RunAsync`.

**Acceptance sketch:**

- While fake/real process is running, snapshot `ProcessId` matches runner.
- Idle/start/fail paths still null ProcessId as appropriate.

---

## Open — F5: Last partial stream line dropped (no trailing newline)

**Severity:** Medium  
**Area:** `ManagedProcessRunner.PumpStreamAsync`

**Problem:** `ReadLineAsync` never emits a final buffer that lacks `\n` when
the process exits. Truncates last Output line; can drop the last diagnostic or
test summary fragment, especially after cancel.

**Direction:** After EOF / exit, if a residual buffer remains, emit one final
line (same generation/stream). Cover with a runner unit test that writes
unterminated final bytes.

**Acceptance sketch:** Unterminated final stdout appears as one Output line;
existing line-oriented tests unchanged.

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

1. **F1** — Output incremental / non-O(n²) projection  
2. **F2** — Gate-safe Dispose  
3. **F4** — ProcessId after start (small, good follow-on)  
4. **F5** — Residual stream line  
5. **F6** — Test Results Cancel + a11y  
6. **F3** — Timeout and/or cancel discoverability (product decision)  
7. **F7** / **F8** — parse quality  
8. **F9**–**F11** — product polish / hygiene  

Prefer one focused commit per finding (or tightly related pair). Re-run
sequential full gates after each code fix.

---

*Recorded: 2026-07-14 (post-Phase-11 closeout audit).*
