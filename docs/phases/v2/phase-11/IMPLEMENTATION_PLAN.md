# Phase 11: Project Workflow (Build / Run / Test) — Implementation Plan

## Status

**M0 complete** (planning/proof gate, 2026-07-14; review-hardening doc pass
same day). Evidence: [M0_DISCOVERY_PROOF.md](M0_DISCOVERY_PROOF.md).

**M1 complete** (2026-07-14, commit `6484fb1`). UI-independent
`IProjectWorkflowService` / `IManagedProcessRunner`, target resolution,
one-operation-at-a-time, cancel, generation, dispose-before-language.

**M2 complete** (2026-07-14). `project.build` / `project.cancel` on
`ICommandRegistry`; `IProjectOutputService` + Output panel (`BottomPanelMode.Output`);
show-on-build affordance; fixture `tests/fixtures/workflow-console/`.
Evidence: [M2_MANUAL_EVIDENCE.md](M2_MANUAL_EVIDENCE.md).

**M3 complete** (2026-07-14). `IBuildDiagnosticsService` + MSBuild parse;
Problems merge (LSP retained across build lifecycle); navigation via existing
editor seams; fixture `tests/fixtures/workflow-fail-build/`.
Evidence: [M3_MANUAL_EVIDENCE.md](M3_MANUAL_EVIDENCE.md).

**M4 complete** (2026-07-14). `project.run` for eligible `CSharpProject` only
(U1a); output reuse; cancel while running; library projects may `Failed` (U7).
Evidence: [M4_MANUAL_EVIDENCE.md](M4_MANUAL_EVIDENCE.md).

**Prerequisite:** Phase 10 complete (M7 closeout, 2026-07-14). Phase 8.3
project context and Phase 8.2 command registry are the authoritative seams.

## Scope

**Goal:** Support the everyday C# build → run → test loop inside Zaide using
the already-selected Phase 8.3 project context, with structured Output,
Problems integration for parsed build diagnostics, a Test Results surface,
cancellation, one-operation-at-a-time policy, and navigation to source through
existing editor/document seams.

### Included (M1+)

- Target resolution from `IProjectContextService` / `ProjectContext` only
- Explicit `project.build`, `project.run`, `project.test`, and
  `project.cancel` commands on `ICommandRegistry`
- Default execution profiles derived from `ProjectCandidate.Kind` (`.sln` /
  `.slnx` / `.csproj`) via `dotnet`
- UI-independent process execution with redirected stdout/stderr (not PTY)
- Structured Output panel distinct from interactive terminal sessions
- Parsed build diagnostics projected into Problems with source attribution
- Test-results surface for `dotnet test` structured outcomes
- Cancellation and one-operation-at-a-time policy
- Navigation from build/test/diagnostic locations through
  `EditorTabViewModel.OpenFileCommand` + `EditorViewModel.RequestNavigate`

### Boundaries (YAGNI)

- No general-purpose task-runner ecosystem, MSBuild project system host, or
  second solution/project discovery model
- No silent auto-build/run/test on folder open
- No DAP debugging (Phase 12); do not claim F5 as Debug
- No multi-target matrix UI, custom task JSON schema, or remote execution
- No replacement of `ITerminalService` / PTY sessions for interactive shells
- No agent automation or tool schemas (V3)
- Do not clear or replace LSP diagnostics with build output; merge by source
- No `OutputType` / Sdk probe for “is this runnable?” in Phase 11 (see U7 /
  limitations)
- No new NuGet required for M1 process execution (`System.Diagnostics.Process`
  matches `CsharpLsSession`); CliWrap remains catalogued only

## Pre-Implementation Verification (M0)

- [x] Live ownership graph verified: `Program.cs` DI, `IProjectContextService`,
      `CommandRegistry` registration/materialization, `MainWindowViewModel`,
      `ProblemsViewModel` navigation, terminal host, `Workspace` / tab open path
- [x] Locked contracts **1–8** (see below) recorded in plan + proof
- [x] Milestone decomposition M0–M6 with tests and commit boundaries
- [x] Concrete verification commands and Linux smoke requirements recorded
- [x] Limitations, YAGNI, and Rollback Plan recorded
- [x] Stale Phase 10 closeout dates (roadmap/architecture/library catalog)
      truth-synced to 2026-07-14
- [x] `git diff --check` clean for M0 docs
- [x] M1 production core (workflow service + runner + DI)
- [x] M2 build command + structured Output (not M3+)
- [x] M3 build diagnostics + Problems merge (not M4+)
- [x] M4 Run command (output reuse, cancel, CSharpProject-only CanExecute)
- [ ] M5–M6 production implementation

## Locked Contracts (1–8)

Full field-level evidence lives in
[M0_DISCOVERY_PROOF.md](M0_DISCOVERY_PROOF.md). The numbered set below is the
authoritative contract inventory; plan and proof use the same **1–8** numbering.

### 1. Target resolution

| `ProjectContext.State` | Build/Run/Test eligible? | Behavior |
|---|---|---|
| `SingleProject` | **Yes** when `SelectedProject != null` | Use that candidate |
| `Selected` | **Yes** when `SelectedProject != null` | Use that candidate |
| `Unloaded` | No | Commands unavailable; truthful idle |
| `Loading` | No | Commands unavailable; do not queue |
| `NoProject` | No | Commands unavailable |
| `Unsupported` | No | Commands unavailable |
| `Ambiguous` | No | Commands unavailable until user selects |
| `Failed` | No | Commands unavailable; surface context error only |

**Winning target:** `ProjectContext.SelectedProject.FilePath` (normalized
absolute path). Working directory: `Path.GetDirectoryName(FilePath)`.
Default tool: host `dotnet` on `PATH`.

**Locked default argv** (quote path as needed for the process API):

| Kind | Build | Run | Test |
|---|---|---|---|
| `Solution` / `SolutionX` | `dotnet build <file>` | **Ineligible** for Phase 11 default (U1 option a) | `dotnet test <file>` |
| `CSharpProject` | `dotnet build <file>` | `dotnet run --project <file>` | `dotnet test <file>` |

- Smart `--no-build` after an in-session successful build is **deferred**
  (optional later); it is not the Phase 11 default.
- Folder open alone never starts a process. `Workspace.WorkspacePath` is **not**
  a build-target fallback.
- **Non-executable projects (U7 / limitation):** any eligible `CSharpProject`
  (including class libraries) may still enable Run by context eligibility.
  Phase 11 does **not** parse `OutputType` / Sdk heuristics. `dotnet run` may
  exit non-zero; surface as structured `Failed` (or `StartupFailed` if start
  fails). No special CanExecute probe.

### 2. Commands

| Command ID | Display name | Category | Default gestures | Registers where (planned) |
|---|---|---|---|---|
| `project.build` | Build | Project | `Ctrl+Shift+B` | Workflow command host (ViewModel or dedicated registrar) |
| `project.run` | Run | Project | `Ctrl+F5` | same |
| `project.test` | Run Tests | Project | _(none)_ | same |
| `project.cancel` | Cancel Build/Run/Test | Project | _(none)_ | same |

- **Only** `ICommandRegistry` / `CommandDescriptor` — no parallel menu-command
  system. Palette and keybinding materialization reuse Phase 8.2
  (`CommandRegistry.ResolveKeyBindings` + `MainWindow.MaterializeRegistryBindings`).
- **UI `CanExecute`:** build/run/test true only when context is eligible (and
  for run: kind supports Run profile) **and** no operation is active;
  `project.cancel` true only when an operation is active.
- **API dual path (M1 must implement both):** when a caller invokes the
  workflow service while a slot is busy, return structured
  `RejectedConcurrent` even if UI `CanExecute` already hid the command.
  Similarly return `RejectedContext` for ineligible context. UI path and
  programmatic path must not diverge in outcome kinds.
- ReactiveCommand `CanExecute` is the UI availability source; registry
  `Execute` already respects `ICommand.CanExecute`.

### 3. Service ownership (UI-independent) and M1 DI surface

**Phase-wide ownership (by milestone of first introduction):**

| Concern | Owner | First milestone |
|---|---|---|
| Target resolution + one operation + cancel + generation | `IProjectWorkflowService` | **M1** |
| Process start, stdout/stderr capture, kill tree, exit | `IManagedProcessRunner` (public DI **or** nested type owned by workflow) | **M1** |
| Execution profile (argv, cwd, env) | Pure helper / `ProjectExecutionProfileResolver` — **not** required as a public DI singleton if kept internal to workflow | **M1** |
| Structured output lines | `IProjectOutputService` | **M2** (not M1) |
| Parsed build diagnostics | `IBuildDiagnosticsService` | **M3** (not M1) |
| Test results | `ITestResultsService` | **M5** (not M1) |
| Views/ViewModels | Projection + command invoke only | M2+ |

**M1 DI registration (explicit YAGNI list):**

- `IProjectWorkflowService` → `ProjectWorkflowService` (singleton)
- `IManagedProcessRunner` → production runner **or** inject via workflow
  constructor without a second empty facade
- **Do not** register Output, build-diagnostics, or test-results services in M1
- **Do not** register `project.*` commands in M1 unless tests need a temporary
  host; product command registration is M2+ (Build/Cancel), M4 (Run), M5 (Test)

### 4. Output / Problems / Test Results vs Terminal

- **Terminal** (`ITerminalService`, `ITerminalHost`): interactive PTY shells.
  Phase 11 must not send Build/Run/Test through the PTY.
- **Output** (M2): structured, append-only (per operation) process log with
  timestamps/stream tags; clear or replace on new operation start.
- **Problems** (M3): continues to project LSP diagnostics; **also** projects
  build diagnostics with `Source = "build"` (or equivalent). Build diagnostics
  are replaced on each new build, not on LSP publish. **LSP items must never
  be cleared by build start.** Separate `IBuildDiagnosticsService` + VM
  projection — do not merge into `ILanguageDiagnosticsService`.
- **Test Results** (M5): dedicated structured surface — not terminal
  `LogCategorizer` heuristics.

### 5. Process lifecycle / results

Structured outcome kinds (names illustrative; exact types in M1):

| Outcome | Meaning |
|---|---|
| `Succeeded` | Process started and exited 0 |
| `Failed` | Process started and exited non-zero |
| `StartupFailed` | Binary missing, start threw, or invalid argv |
| `Cancelled` | User/system cancel; process tree killed when started |
| `RejectedConcurrent` | Second build/run/test while one active — no start |
| `RejectedContext` | Ineligible/stale project context — no start |
| `TimedOut` | **Not required for Phase 11 exit**; deferred |

Rules:

1. **One operation at a time** across Build, Run, and Test (shared mutex/slot).
2. **Generation** increments on each accepted start and on project-context
   replacement that invalidates the active target; late stdout/exit from old
   generation is ignored.
3. Project-context transition away from eligible selected path **cancels** the
   active operation (same spirit as language-session generation).
4. Cancellation is never reported as `Failed` exit alone — use `Cancelled`.
5. **App dispose order (locked):** cancel/dispose **workflow first** (kill any
   `dotnet` process tree), then the existing language stack, then
   `IProjectContextService`, then `ITerminalHost`. Live exit path today
   (`App.axaml.cs`) disposes language → project context → terminal; M1 must
   insert workflow dispose **before** language session dispose so workflow
   children are never orphaned after app exit. Bounded wait after cancel is
   allowed; never rely on process exit alone without `Kill(entireProcessTree)`.

### 6. Navigation

- Open/activate file: `EditorTabViewModel.OpenFileCommand` only
  (same path as `ProblemsViewModel.NavigateToProblemAsync`).
- Caret/selection: `EditorViewModel.RequestNavigate(offset, length)` after
  re-validating offsets against live document text.
- Build diagnostic locations from MSBuild-style `path(line,col)` / `path(line)`
  parsing; invalid/missing files no-op safely.
- Do not invent a second document host.

### 7. Verification gates

- Sequential full gates (never concurrent build+test):

  ```bash
  dotnet build Zaide.slnx --no-restore
  dotnet test Zaide.slnx --no-build
  git diff --check
  ```

- Focused filters per milestone (see milestones table).
- Linux manual smoke with recorded evidence M2–M6.
- **Default fixture path (locked for smoke reproducibility):**
  `tests/fixtures/workflow-console/` — a small console + optional fail-build
  and test projects created at first smoke milestone that needs them (M2).
  Do not rely on ad-hoc temp folders without recording the path in evidence.

### 8. Limitations, YAGNI, and rollback

See [Phase 11 Limitations](#phase-11-limitations) and
[Rollback Plan](#rollback-plan). Phase 11 does not add task runners, DAP,
auto-build, multi-language builds, or OutputType probing.

## Milestones (Incremental)

| Milestone | Scope and independent completion condition | Focused verification | Commit boundary |
|---|---|---|---|
| **M0** ✅ | Planning/proof only. Lock contracts **1–8**; write this plan + `M0_DISCOVERY_PROOF.md`; truth-sync Phase 10 closeout dates; no production workflow code. | Live-code inspection recorded; `git diff --check` | `docs(phase-11): M0 project workflow plan` |
| **M1** ✅ | UI-independent core only: target resolution, process runner, `IProjectWorkflowService` (one-at-a-time, cancel, generation, context-change cancel, structured outcomes including `RejectedConcurrent` / `RejectedContext`), dispose-before-language wiring, DI for workflow (+ runner). Profile helper may be internal. **No** Output/build-diags/test-results services, **no** product UI, **no** required `project.*` registration. | `ProjectTargetResolutionTests`, `ManagedProcessRunnerTests`, `ProjectWorkflowServiceTests` (include concurrent reject + dispose kill), DI resolve tests | `workflow: add project process orchestration core` |
| **M2** ✅ | Build command + structured Output service + Output panel projection; register `project.build` / `project.cancel`; wire CanExecute. **Session risk:** if too large, split **M2a** (workflow Build API + Output service, no panel chrome) and **M2b** (bottom-panel mode + Output UI). Prefer the split over a rushed combined session. | `ProjectBuildCommandTests`, `ProjectOutputServiceTests`, Output VM tests; Linux smoke against `tests/fixtures/workflow-console/` | `workflow: build command and structured output` (or M2a/M2b commits) |
| **M3** ✅ | Parse build diagnostics; `IBuildDiagnosticsService`; Problems **merge** (LSP + build by source) + navigation; clear **only** build diags on new build. **Acceptance must prove** LSP diagnostics survive build start/finish. | `BuildDiagnosticParserTests`, `BuildDiagnosticsServiceTests`, `ProblemsBuildProjectionTests` (LSP retention + build replace), navigation tests; Linux smoke: intentional CS error → Problems → jump | `workflow: build diagnostics in problems` |
| **M4** ✅ | Run command for `CSharpProject` only (U1a); output reuse; cancel while running; library projects may `Failed` (U7). | `ProjectRunCommandTests`; Linux smoke: run console fixture | `workflow: run command` |
| **M5** | Test command + `ITestResultsService` + Test Results surface. Parse: console-first (U4). **Explicit exit condition:** if console parse fails, still report structured operation outcome from process exit code + raw Output lines; do not invent fake passed tests. TRX only if console is insufficient in the same milestone budget. | `ProjectTestCommandTests`, `TestResultsServiceTests`, VM tests; Linux smoke: pass + fail test | `workflow: test command and results surface` |
| **M6** | Closeout: full regression, command availability matrix, status feedback (prefer dedicated workflow status property or single merge policy — U6; do not fight multi-writer `StatusText` ad hoc), docs truth-sync, accessibility/keyboard smoke. | Full sequential build/test; `git diff --check`; `M6_MANUAL_EVIDENCE.md` | `docs(phase-11): M6 closeout` |

### Milestone dependencies

```text
M0 → M1 → M2 (or M2a → M2b) → M3
                          ↘ M4 (after Output stream exists)
                          ↘ M5 (after Output stream exists)
M3 + M4 + M5 → M6
```

Prefer sequential M1→M2→M3→M4→M5→M6 unless explicitly slicing M2.

## Test and Verification Strategy

Prefer fake process runners for service tests; use real `dotnet` only in
bounded Linux smoke / optional integration tests.

**Sequential full gates (never concurrent build+test):**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build
git diff --check
```

**Focused examples (M1+):**

```bash
dotnet test Zaide.slnx --no-build --filter FullyQualifiedName~ProjectWorkflow
dotnet test Zaide.slnx --no-build --filter FullyQualifiedName~ProjectTargetResolution
dotnet test Zaide.slnx --no-build --filter FullyQualifiedName~ManagedProcessRunner
```

**Linux manual smoke (required M2–M6):** record host, date, commands, fixture
path (`tests/fixtures/workflow-console/` or subprojects), and pass/fail in
`M*_MANUAL_EVIDENCE.md`.

| Milestone | Smoke |
|---|---|
| M2 | Build fixture; Output lines; cancel mid-build |
| M3 | Build with deliberate error; Problems entry; navigate; **LSP diags still present if any** |
| M4 | Run console app; stdout in Output; cancel; optional library Failed path |
| M5 | `dotnet test` pass and fail; Test Results or structured fallback |
| M6 | Full loop build→test→problems nav + command palette + keybindings |

## Phase 11 Limitations

- Linux is the primary validation platform (roadmap V2).
- C# / `dotnet` only; no multi-language build systems.
- Solution-level **Run** is out of Phase 11 default (U1a); Run is
  `CSharpProject` only via `dotnet run --project <file>`.
- **Run may fail for non-executable projects** (class libraries, etc.); no
  `OutputType` probe in Phase 11 (U7).
- No watch mode, continuous test runner, code coverage UI, or Live Unit Testing.
- No custom user task files in Phase 11 exit.
- Interactive programs that require a TTY may misbehave under redirected
  stdout/stderr; Phase 11 documents that limit rather than emulating a PTY for
  Run.
- `LogCategorizer` terminal heuristics are **not** the build-diagnostics source
  of truth.
- Build diagnostic parse (M3) is end-of-build, CLI `path(line,col): error|warning`
  form only; relative paths resolve against target parent directory.
- Test console parse is best-effort; structured exit + raw Output is the
  fallback when parse fails (M5).

## Unresolved Decisions

| # | Decision | Resolve by | M0 recommendation |
|---|---|---|---|
| U1 | Solution-level Run startup project | M4 | **(a)** Run ineligible for `Solution`/`SolutionX` |
| U2 | Incremental vs end-of-build diagnostic parse | M3 | End-of-build parse; stream still goes to Output |
| U3 | Partial diagnostics after cancel | M3 | Keep last partial set + Cancelled banner |
| U4 | Test result format | M5 | Console first; TRX only if needed; fail-open to exit+Output |
| U5 | Bottom-panel enum for Output/Test | M2 / M5 | Extend `BottomPanelMode` |
| U6 | Status-bar operation text owner | M2 (design), M6 (polish) | Dedicated workflow status property or single merge policy — avoid ad-hoc multi-writer `StatusText` fights with LSP |
| U7 | Non-executable `CSharpProject` Run policy | M4 (behavior already limited) | **No OutputType probe**; allow Run CanExecute on eligible project; surface `Failed`/`StartupFailed` truthfully |

## Exit Conditions

- [ ] M0–M6 complete with named tests and recorded Linux evidence
- [ ] Eligible project context can Build, Run (per locked policy), and Test
- [ ] One-operation-at-a-time and cancel work; API returns `RejectedConcurrent`
- [ ] Output, Problems (build+LSP without clearing LSP), and Test Results remain
      distinct from Terminal
- [ ] Navigation uses existing editor/tab seams only
- [ ] No second project discovery model
- [ ] `dotnet build Zaide.slnx --no-restore`, `dotnet test Zaide.slnx --no-build`,
      and `git diff --check` pass at closeout

## Rollback Plan

- Each milestone receives one focused commit after its verification gate.
- Revert only the completed milestone commit(s); preserve M0 discovery evidence.
- If a structural Phase 11 implementation reset is required, record
  `docs/phases/v2/phase-11/REVERT_LOG.md` and reset to the last known-good
  commit (typically pre-M1 or last green milestone).
- Do not leave half-registered command IDs in production without matching
  service implementations — roll back registration with the owning milestone.

## Exact Next Step

**M5 — Test command** only: `project.test` for eligible projects, `ITestResultsService`,
Test Results surface, `dotnet test` parse. Do not start DAP (Phase 12) until M5 is
complete unless explicitly re-planned.
