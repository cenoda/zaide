# Phase 13: Release Hardening — Implementation Plan

## Status

**M0 complete (2026-07-16); M2 complete (2026-07-16, evidence-only); M3a
complete (2026-07-16, evidence-only); M3b complete (2026-07-16,
evidence-only); M3c complete (2026-07-16, evidence-only).** The M1a local,
production-neutral measurement runner recorded five samples for Startup, LSP,
Build, Run, Test, and DAP. The M0 test-only app-internal measurement seam
(`Phase13M0EditorMeasurementSeam` +
`tools/phase13-measure.py --areas editor large-file`) recorded 20 functional
samples for editor open/edit/save/restore and 8 MiB document load under
quiet-machine conditions, with post-save restore verification and fixture
SHA-256. Nearest-rank p95 is locked below 50 ms (editor **0.289 ms**, large-file
**15.705 ms**). These are command-path latency budgets, not UX, Avalonia render,
keyboard-routing, or desktop-responsiveness budgets. M4b owns completing
desktop/keyboard/focus/status evidence; M0 only locks the matrix and method.
**M1b is skipped** (all locked budgets already met). **M2 is complete
(evidence-only):** production already satisfied Phase 8 D2 (load reads only the
primary path); the focused proof is
`Phase8ProofOfConceptTests.OrphanTemp_WithValidPrimary_PrimaryRemainsAuthoritative`.
**M3a is complete (evidence-only):** live audit of workflow/process recovery
found no production gap; all recovery rows are green via existing focused tests
or accepted limitations (see `M0_RELEASE_BASELINE_PROOF.md` §5 M3a inventory).
**M3b is complete (evidence-only):** live audit of language-session / LSP
recovery found no production gap; all recovery rows are green via existing
Phase 10 focused tests or accepted limitations (see
`M0_RELEASE_BASELINE_PROOF.md` §5 M3b inventory). **M3c is complete
(evidence-only):** live audit of DAP / debug-session recovery found no
production gap; all recovery rows are green via existing Phase 12 focused tests
or accepted limitations (see `M0_RELEASE_BASELINE_PROOF.md` §5 M3c inventory).
No production behavior change; no new tests. Exact next milestone: **M4a**.

**Out-of-band bugfix (not Phase 13 hardening):** ISSUE-006 fixed a production
crash in Phase 9 M6 selection-status projection (`EditorView` called
`GetOffset` with empty/stale line-column). That change is a focused editor
stability fix only; it does not start M1b, recovery work, or other Phase 13
production scope. After ISSUE-006, the M0 desktop editor measurement path can
resume without the selection-change crash.

**Prerequisite:** Phases 8–12 are complete. Phase 13 hardens their existing
contracts; it does not replace their ownership boundaries or introduce V3
features.

## Scope

**Goal:** Make the completed V2 IDE workflow stable, measurable, recoverable,
and ready for release closeout on the supported Linux validation environment.

### Included

- Measured startup, editor, large-file, LSP, Build/Run/Test, and DAP baselines
  with explicit budgets and reproducible representative fixtures.
- Performance fixes only when a measured baseline exceeds a locked budget. If
  every locked budget is already met under the M0 methodology, no performance
  production change is required.
- Recovery and cleanup verification for settings, language-server, managed
  workflow-process, and DAP-adapter failure paths — inventory-driven and
  gap-only; already-green Phase 8–12 proofs are reused, not recreated.
- A complete settings-schema-v1-to-v3 compatibility matrix: upgrade, unknown-future
  downgrade rejection, corrupt input, interrupted atomic write (Phase 8 D2
  semantics), and last-known-good recovery.
- Critical V2 end-to-end regression coverage, Linux release smoke evidence,
  keyboard-only / focus-visibility / status-readability audit for V2 surfaces,
  and documentation truth-sync.

### Boundaries (YAGNI)

- No V3 agent orchestration, multi-language LSP/DAP, remote workspace,
  collaboration, plugin SDK, or Git remote operations.
- No feature redesign or broad refactor justified only by preference. A change
  must address a recorded Phase 13 budget, recovery failure, resource leak, or
  release-blocking regression.
- Linux is the release-validation target. This phase records other-platform
  validation status; it does not claim parity without evidence. Windows and
  macOS matrix rows default to **not validated** unless evidence is recorded.
- No new settings schema unless M0 proves it is necessary for a hardening fix;
  any such change requires a new migration and an expanded compatibility matrix
  in the same milestone.
- Accessibility in Phase 13 is **keyboard reachability, visible focus, and
  readable status** for V2 surfaces. It is not WCAG certification, screen-reader
  parity, or open deferred UI findings (DF-001–DF-004) unless the M0 triage
  table explicitly adopts one.
- Resource cleanup is limited to owned child processes, `IDisposable`
  service/VM exit order, and measured handle/temp leaks. Do not expand into
  general memory profiling unless M0 records a release-blocking leak.

## Pre-Implementation Verification (M0)

- [x] Read this plan, `docs-rules.md`, `docs/CONVENTIONS.md`, `docs/DESIGN.md`,
      and the closed Phase 8–12 plans/evidence; read every open Phase 13
      `TOFIX.md` if one exists before implementation.
- [x] Verify current production ownership in live code: `SettingsService`,
      `SettingsMigrator` (constructed by `SettingsService`, not a DI singleton),
      `SettingsPathResolver` (static utility; owns `settings.json`, `.tmp`,
      `.lastknowngood`, and `secrets.json` paths), `ManagedProcessRunner`,
      `ProjectWorkflowService`, `LanguageSessionService`,
      `DebugSessionService`, `App.DisposeServicesOnExit`, and
      `Program.ConfigureServices`.
- [x] Record the exact machine/OS, .NET SDK/runtime, Avalonia backend, csharp-ls
      version/path, NetCoreDbg version/path, fixture commit or archive hash,
      and the structural revert baseline: the git commit hash of the
      repository state before any Phase 13 production change (analogous to
      Phase 12's M1 revert target `6222ea5`). Record this in the M0 proof;
      it is the target for `REVERT_LOG.md` if a structural rollback is
      required.
- [x] Create reproducible fixtures outside the application settings directory:
      reuse any committed Phase 11 workflow fixtures first (the existing
      `tests/fixtures/workflow-console/` is the expected base for the M4a
      critical-path C# project; M0 must explicitly inspect `workflow-console`
      against the M4 critical-path step matrix and decide whether a richer
      DAP fixture (multi-thread, deeper call stack) is necessary — the fixture
      is a single-line program with one frame and `args` only; if M0 accepts
      this limitation, document it in the proof; otherwise create the fixture
      at M0, not at M4). For
      large-file performance baselines, do not commit multi-megabyte files:
      commit a generator script in `tools/`, the requested byte sizes, seed
      where applicable, output hash, and generation command instead. The
      generator is an M0 deliverable needed before M1a harnesses can run.
- [x] Publish a fixture-and-test inventory in the M0 proof: each existing
      fixture/test seam, its owner phase, the contract it already proves, and
      whether Phase 13 reuses it, extends it, or has a real gap. First-pass
      inventory must include at least: `Phase8ProofOfConceptTests`,
      `FormatOnSaveTests` (migration/schema), `SecretStoreTests` /
      `FileSecretStorePermissionTests`, `ManagedProcessRunnerTests`,
      `ProjectWorkflowServiceTests`, `LanguageSessionServiceTests`,
      `DebugSessionServiceTests` / `M6DebugRecoveryProofTests`, and
      `ProjectWorkflowProjectionShutdownTests`. Do not recreate already-green
      settings, process, LSP, or DAP recovery coverage merely to label a
      milestone complete.
- [x] Publish a carry-over triage table in the M0 proof covering: (1) Phase
      8–12 **limitations** sections; (1b) Phase 10–12 `TOFIX.md` **remaining
      limitations** subsections (e.g. Phase 11 F3 no operation timeout,
      F7 non-English parse, F8 non-English build diagnostics, F9 untitled-tab
      block, F10 single-owner runner — M0 must decide whether any are
      hardening-relevant in Phase 13); (2) Phase 10–12 `TOFIX.md` items
      (confirm closed vs still open — read live sections, not stale summary
      counts; the Phase 10 audit found three open info-level items:
      F6 `SemaphoreSlim` gate across async LSP I/O, F10 `CsharpLsSession`
      669-line monolith, F11 O(n) document lookup per diagnostics publish;
      M0 must mark each **accept as limitation** or **defer**); (3) Phase 12
      `M7_MANUAL_EVIDENCE.md` display-dependent visual/gesture rows; (4) open
      deferred findings in `docs/deferred/INDEX.md` (DF-001–DF-004). Every
      item must be marked **accept as limitation**, **defer** (with a `DF-###`
      note where appropriate), **fix in a named Phase 13 slice**, or **not a
      Phase 13 concern**. Existing deferred findings remain out of scope
      unless this table explicitly adopts one.
- [x] Run the baseline sequentially, retaining raw timings and test totals.
      Record the exact passing-test count (currently 2053 at plan time) and
      flaky-test status: if any test fails intermittently, note its name,
      failure pattern, and whether Phase 13 should harden it or explicitly
      defer it. Record the flaky assessment in the M0 proof alongside the
      pass count.

  ```bash
  dotnet build Zaide.slnx --no-restore
  dotnet test Zaide.slnx --no-build
  git diff --check
  ```

- [x] Define median and sample-count methodology for every timing (warm/cold
      classification, at least five samples, monotonic clock, excluded setup
      time, fixture path/hash, and pass/fail rule). For each measured area lock:
      (1) where the clock runs (external desktop timer, in-process seam, or
      test host); (2) whether any production change is allowed solely to expose
      a truthful measurement hook; (3) wall-clock and external-tool caps
      (csharp-ls, netcoredbg, `dotnet`); (4) whether the harness is local-only
      or optionally runnable in CI. Use a quiet machine policy, record/retry
      invalid samples caused by unrelated load, and prohibit silent outlier
      removal. Process/desktop timings retain their documented variance rule.
      The app-internal rows instead use an absolute latency budget: 20 samples,
      all functional, nearest-rank p95 below 50 ms. Do not use a developer's
      subjective responsiveness as a budget.
      Editor and 8 MiB rows use the implemented local, test-only app-internal
      seam (`Phase13M0EditorMeasurementSeam` via
      `python3 tools/phase13-measure.py --areas editor large-file`): same open,
      edit, save, restore, and document-load command paths; fixture hash, 20
      raw samples, post-save restoration, and `Stopwatch.GetTimestamp` clock
      boundary. No injected keyboard/pointer events, production telemetry, or
      human stopwatch.
- [x] Define one bounded critical-path scenario before M4: open a selected C#
      project → edit/save → LSP result → build → run or test → debug to one
      breakpoint → stop. Publish a **step matrix** in the M0 proof: for every
      step, state whether it is a deterministic headless seam, a real-child-process
      integration test, or Linux-only manual evidence; name required env vars
      (e.g. `ZAIDE_NETCOREDBG_PATH`, csharp-ls path); and lock a maximum wall-clock
      duration. Prefer composing existing Phase 10–12 proof tests over a new
      monolithic UI automation suite. At most one automated real-child integration
      slice is the default; remaining steps may be manual in M4b. One golden path
      is sufficient; do not turn M4 into broad UI automation.
- [x] Create empty, versioned M0 matrices for Linux x64 validation; Windows and
      macOS status (default **not validated**); keyboard-only focus/command
      paths; focus visibility / status readability; and the carried-over Phase 12
      visual/gesture rows (including display-dependent M7 rows, which must be
      re-smoked or explicitly **not validated** with reason — never silent pass).
      Lock every row to **pass**, **fail**, **unsupported**, or **not
      validated**—never an implicit claim.
- [x] Map every settings compatibility matrix row to a live test name or an
      explicit gap. Interrupted-write follows Phase 8 D2: an orphaned
      `settings.json.tmp` leaves the primary `settings.json` intact; next load
      succeeds from the primary (or LKG/defaults if the primary is also bad). Do
      not invent a new interrupted-write recovery mode.
- [x] Create `M0_RELEASE_BASELINE_PROOF.md` containing the fixture manifest,
      baseline results, budgets, test/gap inventory, carry-over triage,
      compatibility-matrix inputs, platform/accessibility matrices, critical-path
      step matrix, known limitations, and the M1a–M5 handoff (including which
      slices may be no-ops). M0 is documentation/proof only.

## Locked Ownership and Evidence Rules

1. **Measurements are evidence, not telemetry.** A benchmark must be local,
   opt-in test/harness code with no production data collection. Report every
   sample, median, environment, and fixture identity; compare like-for-like
   cold or warm runs only.
2. **Existing service ownership remains authoritative.** Settings recovery stays
   in `ISettingsService`/`SettingsMigrator` (migrator constructed by
   `SettingsService`); Build/Run/Test process ownership remains in
   `IProjectWorkflowService`/`IManagedProcessRunner`; LSP ownership remains in
   `ILanguageSessionService`; DAP ownership remains in `IDebugSessionService`.
   Views and view models may project results but do not start unmanaged
   processes or implement recovery policy.
3. **Compatibility is loss-safe.** V1 and V2 inputs must migrate to current
   **settings schema v3**
   without silently dropping their known fields. A future schema version must
   be rejected without overwrite. Corrupt or unreadable primary settings must
   not be mistaken for a valid default save. Interrupted write follows Phase 8
   D2 (orphaned `.tmp`, primary intact). M0 must decide, against the live
   Settings surface, whether recovery/load state needs a user-visible projection
   in Phase 13 or is an intentionally documented non-surface; M2 may not assert
   a user-visible state that does not exist.
4. **Cleanup has an observable completion condition.** Cancellation, service
   disposal, project-context change, adapter/server exit, and application exit
   must leave no owned child process running and no stale active snapshot. Test
   seams must prove the state transition and bounded cleanup without timing-only
   assertions. Prefer extending existing dispose/kill tests over new policy.
5. **Release claims require both automation and manual evidence.** Automated
   coverage does not substitute for recorded Linux desktop smoke or a
   keyboard/focus/status audit. Unsupported or unvalidated platforms remain
   explicitly documented.
6. **Gap-only hardening.** M2 and M3a–M3c implement or re-prove only rows M0
   marks as real gaps. Green inventory rows are closed by naming the existing
   test evidence; they do not require new production code.

## V2 Exit Conditions → Phase 13 Gates

| V2 exit condition | Phase 13 gate |
|---|---|
| Phase 8–13 plans and independent closure | M0 plan/proof and M5 closeout evidence |
| V1 editor, terminal, agent, Townhall, and Git workflows remain regression-covered | M0 inventory names existing coverage and gaps; M4a adds only necessary regression evidence; M5 runs the full suite |
| C# edit → understand → build → run/test → debug loop | M4a defined golden path and M4b Linux evidence; rechecked by M5 |
| Commands are discoverable and keyboard configurable | M4b Command Palette/keybinding/keyboard matrix; M5 evidence review |
| Credentials are absent from ordinary plaintext settings | M0 inspects `ISecretStore`/`FileSecretStore` and settings JSON shape; M2 proves the matrix row |
| Full settings schema compatibility/recovery | M2 gap-only automated matrix, rechecked by M5 |
| UI-independent services, cancellation, structured results, observable state | M3a–M3c gap-only lifecycle/recovery evidence and M4a critical-path evidence |
| Exact automated/manual documentation and explicit limitations/platform status | M0 matrix templates, M4b recorded results, M5 truth-sync |

## Milestones (Incremental)

| Milestone | Description | Verification gate | Commit boundary |
|---|---|---|---|
| **M0** | Live-code discovery, reuse/gap inventory, carry-over triage, reproducible fixture manifest, baseline measurements, numeric budgets, all matrices, critical-path step matrix, and proof artifact. No production behavior change. | `M0_RELEASE_BASELINE_PROOF.md`; sequential build/test; `git diff --check` | `docs(phase-13): lock M0 release baseline` |
| **M1a** | **Complete (2026-07-15):** local deterministic measurement runner and five-sample evidence for Startup, LSP, Build, Run, Test, and DAP. It changes no production behavior. Editor/8 MiB rows are the M0 app-internal extension (`phase13-measure.py --areas editor large-file`), not manual timing work. | `tools/phase13-measure.py`; [M1A_MEASUREMENT_RUNNER.md](M1A_MEASUREMENT_RUNNER.md); sequential build/test | `test(phase-13): add release performance harnesses` |
| **M1b** | **Skipped (2026-07-16).** Optional performance fixes only for an M0-locked budget miss. Every locked budget already passes under M0/M1a evidence, so M1b is zero slices; M5 remeasurement remains the later recheck. | M0 proof records all budgets already met; no production change | omit (zero slices) |
| **M2** | **Complete (2026-07-16, evidence-only).** M0 named one gap: orphan `settings.json.tmp` beside a valid primary. Live `SettingsService.TryLoadFrom` already reads only the primary path and never promotes/overwrites/deletes the primary from an orphan temp — Phase 8 D2 already held. Added focused proof `Phase8ProofOfConceptTests.OrphanTemp_WithValidPrimary_PrimaryRemainsAuthoritative` (conflicting orphan content; primary values load; primary bytes unchanged; orphan not promoted). No production code change; no new recovery mode, migration policy, or user-facing recovery surface. All other matrix rows remain green via existing named tests. | Focused test + sequential build/test + `git diff --check` | `test(phase-13): prove orphan settings temp leaves primary authoritative` |
| **M3a** | **Complete (2026-07-16, evidence-only).** Live inventory of `ProjectWorkflowService`, `ManagedProcessRunner`, `IProjectOperationGate`, context-change / cancel / dispose / app-exit cleanup found **no real production gap**. All recovery rows green via reused `ManagedProcessRunnerTests`, `ProjectWorkflowServiceTests`, `ProjectWorkflowProjectionShutdownTests` (plus supporting `ProjectOperationGateTests` for gate lease release), or accepted limitations (no op timeout, single runner owner, gate not disposed on app exit). No production code change; no new tests; no multi-child Linux smoke (no orphan-child gap). Focused filter: 35 passed. | Focused workflow/process tests (named existing); sequential build/test | `docs(phase-13): close M3a workflow process recovery inventory` |
| **M3b** | **Complete (2026-07-16, evidence-only).** Live inventory of `LanguageSessionService`, `LanguageDocumentBridge`, start eligibility / missing-server, cancel / restart / process-exit / dispose, project-context change, and stale generation / diagnostics / completion / hover / definition / close-reopen found **no real production gap**. All recovery rows green via reused `LanguageSessionServiceTests`, `LanguageDocumentSyncTests` (plus supporting diagnostics/completion/hover/navigation/DI/shutdown proofs), or accepted limitations (Phase 10 F6/F10/F11; no auto-restart after `ServerExited`). No production code change; no new tests; no Linux child-process re-smoke (no orphan-child gap). Focused filter: 36 passed. | Focused language-session/document-bridge tests (named existing); sequential build/test | `docs(phase-13): close M3b language session recovery inventory` |
| **M3c** | **Complete (2026-07-16, evidence-only).** Live inventory of `DebugSessionService`, `DebugAdapterLocator`, adapter launch/initialize/start failure/missing-adapter/restart, stop/cancel/process-exit/dispose/child-process cleanup, stale events and stack/variable/location projection, breakpoint session scoping, project-context change, and app-exit ordering found **no real production gap**. All recovery rows green via reused `DebugSessionServiceTests`, `M6DebugRecoveryProofTests` (plus supporting M4 real-adapter proof, locator/DI/launch-handoff/shutdown/stack/location tests), or accepted limitations (launch-only scope; real proofs skip without `ZAIDE_NETCOREDBG_PATH`). No production code change; no new tests; no extra Linux real-adapter smoke (no orphan-child gap). Focused filter: 38 passed. | Focused debug-session tests (named existing); sequential build/test | `docs(phase-13): close M3c DAP recovery inventory` |
| **M4a** | Add only the M0-defined automated critical-path regression (prefer composing existing Phase 10–12 proofs). Respect the step matrix: headless vs real-child boundary, env requirements, and max duration. | Focused golden-path test/evidence; sequential build/test | `test(phase-13): cover V2 critical path` |
| **M4b** | Record the Linux release, keyboard/focus/status, and adopted visual/gesture matrices on a real Linux desktop with a display. Fill in the platform matrix rows for Windows and macOS — default **not validated** unless prior evidence exists; M4b does **not** require running on those platforms, only recording their honest status. Display-dependent Phase 12 M7 rows are re-smoked on the Linux desktop or explicitly marked **not validated** with reason. | `M4_RELEASE_SMOKE_EVIDENCE.md`; every matrix row status recorded | `docs(phase-13): record release smoke evidence` |
| **M5** | Close out: repeat all M0 measurements, prove every locked budget and mapped V2 exit row, resolve or explicitly defer findings, truth-sync docs (`V2.md`, `PHASES.md`, `OVERVIEW.md`, `LIBRARIES.md`, `CONVENTIONS.md`, `DESIGN.md`, `README.md`), and run full sequential regression. | Full sequential build/test; `git diff --check`; `M5_RELEASE_CLOSEOUT_EVIDENCE.md` | `docs(phase-13): close release hardening` |

### Milestone dependencies

- M1a–M4b require M0's committed fixtures, methods, budgets, inventories, and
  proof artifact.
- M1b is one budget area per slice and may be zero slices when all budgets are
  already met. M2 and M3a–M3c may proceed independently after M0 and may each be
  evidence-only no-ops when the inventory is fully green; M4a begins only after
  their applicable focused gates (or documented no-ops) are complete.
- Any production change after M4a or M4b—including a late M1b fix—invalidates
  affected manual evidence and requires a targeted re-smoke (update the
  affected sections of `M4_RELEASE_SMOKE_EVIDENCE.md`) before M5.
- M5 requires all adopted M1b (if any), M2, M3a–M3c, M4a, and M4b evidence and
  no unchecked Phase 13 `TOFIX.md` items. Skipped M1b / green M2–M3 no-ops must
  be recorded in the M5 closeout by reference to the M0 inventory.

## Test and Verification Strategy

M0 must replace the placeholders below with exact test class/filter names and
harness commands after inspecting the test suite. Do not use filtered tests as
the only release gate.

| Area | Required evidence |
|---|---|
| Startup | Five-or-more comparable desktop samples, fixture identity, a documented absolute budget, measurement-site lock, and a focused automated regression where feasible. |
| Editor/large file command paths | 20 comparable app-internal samples, fixture identity, all functional samples, nearest-rank p95 below the documented absolute budget, and a focused automated regression. This is not desktop UX or rendering evidence. |
| LSP | C# server resolved, document opened/synced, diagnostics or completion request completed, cancellation/restart behavior observed, and no orphaned server. Prefer existing Phase 10 proofs; add tests only for M0-named gaps. |
| Build/Run/Test | Selected C# project: success, deliberate failure, cancellation, output/result projection, and child-process cleanup. Prefer existing Phase 11 proofs; add tests only for M0-named gaps. |
| DAP | Build-to-debug launch, breakpoint stop, step/continue/stop, adapter failure/restart, and child-process cleanup. Prefer existing Phase 12 recovery proofs; add tests only for M0-named gaps. |
| Settings | schema v1→v3, schema v2→v3, schema v3 round trip, unknown-v4 rejection/no overwrite, corrupt primary, interrupted-write (orphaned `.tmp`, primary intact per Phase 8 D2), and last-known-good path. Map each row to an existing test name or a gap. |
| Keyboard / focus / status | Command Palette/keybindings, tab and bottom-panel focus order, editor search, workflow, and debug controls navigable without mouse; visible focus and readable status recorded. Not WCAG or screen-reader parity. |

Every implementation milestone runs its focused tests first, then—before its
commit—runs the sequential full gate:

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build
git diff --check
```

## Phase 13 Limitations (by design)

- Release validation is Linux-first; other platforms may be documented as not
  validated.
- Performance evidence is valid only for the recorded machine, runtime,
  fixture, and methodology. It establishes a release budget, not a universal
  hardware promise.
- The phase hardens existing V2 workflows; deferred V3 capabilities remain out
  of scope.
- Accessibility is keyboard/focus/status only for V2 surfaces, not full
  accessibility certification.
- Interrupted settings write recovery is the Phase 8 D2 temp-then-rename
  contract, not a separate recovery engine.

## Exit Conditions

- [ ] M0–M5 are complete with named focused tests and evidence artifacts
      (including documented no-ops for green inventory rows and optional M1b).
- [ ] Every M0 performance budget is met by a comparable M5 remeasurement, or
      the documented gate and accepted limitation are explicitly approved and recorded.
- [ ] Every settings compatibility/recovery matrix row has automated evidence
      (existing or new).
- [ ] LSP, workflow-process, and DAP lifecycle/recovery paths have focused
      automated evidence (existing or new) plus Linux smoke where a real child
      process is needed for a remaining gap.
- [ ] The critical C# edit → understand → build → run/test → debug loop has
      passing automated and manual Linux evidence per the M0 step matrix.
- [ ] Keyboard/focus/status and Linux release smoke matrices are recorded with
      pass, fail, unsupported, or not validated status for each row.
- [ ] Full sequential build/test pass, `git diff --check` is clean, all Phase
      13 `TOFIX.md` items are closed, and the following docs are truth-synced
      against the live code: `docs/roadmap/V2.md`, `docs/roadmap/PHASES.md`,
      `docs/architecture/OVERVIEW.md`, `docs/LIBRARIES.md`,
      `docs/CONVENTIONS.md`, `docs/DESIGN.md`, and `README.md`.

## Rollback Plan

Each milestone is independently committed. If a hardening change regresses a
previously proven workflow, revert that milestone's commit, restore the M0
baseline fixture/evidence, run the sequential full gate, and record a
`REVERT_LOG.md` if the phase change is structurally abandoned. Do not revert
completed Phase 8–12 feature commits merely to avoid diagnosing a Phase 13
regression. The M0 proof records the pre-Phase-13 commit hash as the
structural revert baseline; any full-phase rollback targets that commit.

## Exact Next Step

**M0, M2, M3a, M3b, and M3c are closed.** M2 was **evidence-only** (no production
fix):
`Phase8ProofOfConceptTests.OrphanTemp_WithValidPrimary_PrimaryRemainsAuthoritative`
proves Phase 8 D2 for orphan `.tmp` + valid primary. M3a was **evidence-only**
(no production fix, no new tests): workflow/process recovery inventory is fully
green via existing Phase 11/12 proofs; see `M0_RELEASE_BASELINE_PROOF.md` §5.
M3b was **evidence-only** (no production fix, no new tests): language-session /
LSP recovery inventory is fully green via existing Phase 10 proofs; see
`M0_RELEASE_BASELINE_PROOF.md` §5 M3b inventory. M3c was **evidence-only** (no
production fix, no new tests): DAP / debug-session recovery inventory is fully
green via existing Phase 12 proofs; see `M0_RELEASE_BASELINE_PROOF.md` §5 M3c
inventory. Proceed to **M4a only**: add only the M0-defined automated
critical-path regression (compose existing Phase 10–12 proofs per the step
matrix). Do not start M1b (skipped), M4b, M5, or production performance work
unless a later remeasurement creates a real locked-budget miss.
