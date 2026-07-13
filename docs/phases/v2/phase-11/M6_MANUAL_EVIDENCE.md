# Phase 11 M6: Closeout — Manual Evidence

## Status: M6 complete (Phase 11 complete for V2)

| Field | Value |
|---|---|
| Date | 2026-07-14 |
| Host | Linux `cenoda` 7.1.3-arch1-1, x86_64 |
| `dotnet` | 10.0.109 (`/usr/bin/dotnet`) |
| Closeout commit | `1582f38` (`docs(phase-11): M6 closeout`; use `git rev-parse HEAD` after checkout of this milestone) |
| Scope | Verify, U6 status polish, docs truth-sync — no Phase 12 / DAP |

## Full regression gates (sequential)

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build
git diff --check
```

| Gate | Result |
|---|---|
| `dotnet build Zaide.slnx --no-restore` | pass — 0 warnings, 0 errors |
| `dotnet test Zaide.slnx --no-build` | **1827 passed, 0 failed, 0 skipped** |
| `git diff --check` | clean |

## Focused workflow filters

| Filter | Result |
|---|---|
| `FullyQualifiedName~ProjectWorkflow` | 34 passed |
| `FullyQualifiedName~ProjectBuild` | 16 passed |
| `FullyQualifiedName~ProjectRun` | 18 passed |
| `FullyQualifiedName~ProjectTest` | 19 passed |
| `FullyQualifiedName~BuildDiagnostic` | 11 passed |
| `FullyQualifiedName~TestResults` | 15 passed |
| `FullyQualifiedName~ProjectOutput` | 5 passed |
| `FullyQualifiedName~CanonicalCommand` | 22 passed |
| `FullyQualifiedName~ProjectWorkflowStatusPolicy` | 17 passed (M6 U6) |

Note: filter counts overlap (same test can match multiple name fragments). Full suite total is authoritative: **1827**.

## Command availability matrix (matches live CanExecute + registration)

| Command | Gestures | Eligible when | Notes |
|---|---|---|---|
| `project.build` | `Ctrl+Shift+B` | Eligible context + idle | Solution + project |
| `project.run` | `Ctrl+F5` | Eligible + `CSharpProject` + idle | U1a: solution false; U7: no OutputType probe |
| `project.test` | _(none)_ | Eligible context + idle | Solution + project |
| `project.cancel` | _(none)_ | Starting or Running | Any of Build / Run / Test |

**Ineligible context** (`Unloaded`, `Loading`, `NoProject`, `Unsupported`, `Ambiguous`, `Failed`): build / run / test false.

**Busy slot:** build / run / test false; cancel true.

**API dual path:** programmatic start while busy → `RejectedConcurrent`; bad context → `RejectedContext`.

Automated coverage: `ProjectBuildCommandTests`, `ProjectRunCommandTests`, `ProjectTestCommandTests`, `CanonicalCommandRegistrationTests`. No registration/gesture changes in M6.

## U6 — status feedback decision

**Option A (chosen):** Workflow status lives on Output / Test surfaces via
`ProjectWorkflowViewModel.StatusMessage` mapped by
`ProjectWorkflowStatusPolicy`. Main status bar remains multi-writer as today;
M6 does **not** add a merge fight with LSP / save / terminal on
`MainWindowViewModel.StatusText`.

**Implementation:** `ProjectWorkflowSnapshot` / `ProjectOutputSnapshot` retain
`LastOperation` through idle terminal outcomes so messages are truthful:

| Operation | In progress | Succeeded | Failed | Startup failed | Cancelled |
|---|---|---|---|---|---|
| Build | Building … | Build succeeded. | Build failed. | Build could not start. | Build cancelled. |
| Run | Running … | Run succeeded. | Run failed. | Run could not start. | Run cancelled. |
| Test | Testing … | Tests succeeded. | Tests failed. | Tests could not start. | Tests cancelled. |

Tests: `ProjectWorkflowStatusPolicyTests` (17).

## CLI fixture smoke

| Fixture | Path | Command | Result |
|---|---|---|---|
| Console (build/run) | `tests/fixtures/workflow-console/` | `dotnet build` + `dotnet run` | pass — stdout `workflow-console smoke` |
| Fail-build (diags) | `tests/fixtures/workflow-fail-build/` | `dotnet build` | **expected fail** — CS1002 |
| Tests pass | `tests/fixtures/workflow-tests-pass/` | `dotnet test` | pass — 1 passed |
| Tests fail | `tests/fixtures/workflow-tests-fail/` | `dotnet test` | **expected fail** — 1 failed |

## Accessibility / keyboard smoke (Linux)

Interactive UI not exercised in this headless closeout session. Substitutes:

| Check | Result |
|---|---|
| Palette / registry: `project.build`, `project.run`, `project.test`, `project.cancel` metadata | pass — `CanonicalCommandRegistrationTests` + command tests |
| `Ctrl+Shift+B` → Build; `Ctrl+F5` → Run; F5 **not** claimed as Debug | pass — default gestures in registration tests; no Debug command registration |
| Build → Output lines; show-on-start | pass — automated `ProjectBuildCommandTests` / `ProjectOutputServiceTests` |
| Fail-build → Problems `[build]`; LSP retained | pass — `BuildDiagnosticsServiceTests` / Problems projection tests + fail-build fixture |
| Run console fixture → Output; cancel | pass — `ProjectRunCommandTests` + CLI run fixture |
| Test pass/fail → Test Results + Output | pass — `ProjectTestCommandTests` / `TestResultsServiceTests` + fixtures |
| Bottom modes: Terminal \| Problems \| Output \| Test Results | pass — live `BottomPanelMode` + mode strip in `MainWindow.axaml.cs` |
| Navigation uses existing editor seams | pass — no second document host (M3/M5 design) |
| Automation names on Output / Test Results | present — `Output lines`, `Test results list` (and related) |

## Residual known limits (honest)

- U1a: solution-level **Run** ineligible (no multi-startup picker).
- U7: no `OutputType` / Sdk probe; library projects may Run → `Failed`.
- Test results: console-first parse only; no TRX; fail-open to exit code + Output.
- Run uses redirected stdout/stderr (not TTY/PTY); interactive apps may misbehave.
- Build diagnostics: end-of-build MSBuild-style parse only.
- No DAP / F5 Debug (Phase 12).
- No watch mode, coverage, Live Unit Testing, smart `--no-build`, agent tools.
- No main status-bar merge with workflow (Option A).

## Hygiene

- `M5_MANUAL_EVIDENCE.md` Commit field corrected to product M5 commit **`a4f9727`**
  (was incorrectly `545ac16` / dirty `7088bb8`).

## Acceptance checklist

- [x] Full sequential gates green; `git diff --check` clean
- [x] Command matrix recorded and matches live CanExecute + registration
- [x] U6: outcome text distinguishes Build / Run / Test; Option A documented; tests cover it
- [x] Exit conditions in IMPLEMENTATION_PLAN checked off truthfully
- [x] Roadmap + OVERVIEW truth-synced: Phase 11 complete
- [x] This evidence file complete; M5 SHA corrected
- [x] Smoke: build / run / test / problems path recorded (CLI minimum)
- [x] No Phase 12 code
- [x] Focused closeout commit(s)
