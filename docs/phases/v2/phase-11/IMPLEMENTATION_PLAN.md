# Phase 11: Project Workflow (Build / Run / Test) — Implementation Plan

## Status

**M0 complete** (planning/proof gate, 2026-07-14). No production Build, Run,
Test, Output, Problems-build, or test-results features yet.
Evidence: [M0_DISCOVERY_PROOF.md](M0_DISCOVERY_PROOF.md).
Verified against live code at `1569e6dad6e6f1615e3677460a44f2ad5cf8cd42`.

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
- No new NuGet required for M1 process execution (`System.Diagnostics.Process`
  matches `CsharpLsSession`); CliWrap remains catalogued only

## Pre-Implementation Verification (M0)

- [x] Live ownership graph verified: `Program.cs` DI, `IProjectContextService`,
      `CommandRegistry` registration/materialization, `MainWindowViewModel`,
      `ProblemsViewModel` navigation, terminal host, `Workspace` / tab open path
- [x] Target-resolution contract locked for every `ProjectContextState`
- [x] Command IDs, registry ownership, CanExecute, and default gestures locked
- [x] Service ownership for execution, output, diagnostics, tests, cancel locked
- [x] Output/Problems/Test Results vs interactive terminal separation locked
- [x] Process lifecycle/result contracts locked
- [x] Navigation ownership locked to existing editor seams
- [x] Milestone decomposition M0–M6 with tests and commit boundaries
- [x] Concrete verification commands and Linux smoke requirements recorded
- [x] Limitations, YAGNI, and Rollback Plan recorded
- [x] Stale Phase 10 closeout dates (roadmap/architecture/library catalog)
      truth-synced to 2026-07-14
- [x] `git diff --check` clean for M0 docs
- [ ] Production implementation (starts at M1 — not this session)

## Locked Contracts (summary)

Full field-level evidence and line-of-code citations live in
[M0_DISCOVERY_PROOF.md](M0_DISCOVERY_PROOF.md). Summary:

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
Default tool: host `dotnet` on `PATH`. Command templates by `ProjectKind`:

| Kind | Build | Run | Test |
|---|---|---|---|
| `Solution` / `SolutionX` | `dotnet build <file>` | Not default-eligible as a whole-solution “run” without a startup project — see M0 unresolved | `dotnet test <file>` |
| `CSharpProject` | `dotnet build <file>` | `dotnet run --project <file> --no-build` only after successful build in-session **or** `dotnet run --project <file>` (M1 locks exact default) | `dotnet test <file>` |

Folder open alone never starts a process. `Workspace.WorkspacePath` is **not**
a build-target fallback.

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
- `CanExecute`: true only when context is eligible **and** (for build/run/test)
  no operation is active; `project.cancel` true only when an operation is active.
- ReactiveCommand `CanExecute` is the availability source; registry
  `Execute` already respects `ICommand.CanExecute`.

### 3. Service ownership (UI-independent)

| Concern | Owner | Notes |
|---|---|---|
| Target resolution + one operation + cancel + generation | `IProjectWorkflowService` | Singleton; consumes `IProjectContextService` only for project truth |
| Process start, stdout/stderr capture, kill tree, exit | `IManagedProcessRunner` (or nested type owned by workflow) | No PTY; not `ITerminalService` |
| Execution profile (argv, cwd, env) | `IProjectExecutionProfileResolver` | Pure function of candidate + operation kind + optional future settings |
| Structured output lines | `IProjectOutputService` | Observable snapshot; not terminal scrollback |
| Parsed build diagnostics | `IBuildDiagnosticsService` | Structured items; Problems projects them |
| Test results | `ITestResultsService` | Structured suite/case outcomes |
| Views/ViewModels | Projection + command invoke only | Never own process lifecycle |

### 4. Output / Problems / Test Results vs Terminal

- **Terminal** (`ITerminalService`, `ITerminalHost`): interactive PTY shells.
  Phase 11 must not send Build/Run/Test through the PTY.
- **Output**: structured, append-only (per operation) process log with
  timestamps/stream tags; clear or replace on new operation start.
- **Problems**: continues to project LSP diagnostics; **also** projects build
  diagnostics with `Source = "build"` (or equivalent). Build diagnostics are
  replaced on each new build, not on LSP publish. LSP items are not cleared by
  build start.
- **Test Results**: dedicated structured surface (bottom-panel mode extension
  or sibling list) — not terminal log heuristics (`LogCategorizer` remains
  terminal-only).

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
| `TimedOut` | **Not required for Phase 11 exit**; document as deferred unless M1 proves need |

Rules:

1. **One operation at a time** across Build, Run, and Test (shared mutex/slot).
2. **Generation** increments on each accepted start and on project-context
   replacement that invalidates the active target; late stdout/exit from old
   generation is ignored.
3. Project-context transition away from eligible selected path **cancels** the
   active operation (same spirit as language-session generation).
4. App dispose / service dispose: cancel active operation, wait bounded, dispose
   runner.
5. Cancellation is never reported as `Failed` exit alone — use `Cancelled`.

### 6. Navigation

- Open/activate file: `EditorTabViewModel.OpenFileCommand` only
  (same path as `ProblemsViewModel.NavigateToProblemAsync`).
- Caret/selection: `EditorViewModel.RequestNavigate(offset, length)` after
  re-validating offsets against live document text.
- Build diagnostic locations from MSBuild-style `path(line,col)` / `path(line)`
  parsing; invalid/missing files no-op safely.
- Do not invent a second document host.

## Milestones (Incremental)

| Milestone | Scope and independent completion condition | Focused verification | Commit boundary |
|---|---|---|---|
| **M0** ✅ | Planning/proof only. Lock contracts 1–9; write this plan + `M0_DISCOVERY_PROOF.md`; truth-sync Phase 10 closeout dates; no production workflow code. | Live-code inspection recorded; `git diff --check` | `docs: phase-11 M0 project workflow plan` |
| **M1** | UI-independent core: target resolution, execution-profile resolver, managed process runner, `IProjectWorkflowService` with one-at-a-time, cancel, generation, context-change cancel, structured outcomes, DI registration. **No** Output UI, Problems build merge, Run/Test product UX yet — optional internal Build smoke via tests only. | `ProjectTargetResolutionTests`, `ManagedProcessRunnerTests`, `ProjectWorkflowServiceTests`, DI resolve tests | `workflow: add project process orchestration core` |
| **M2** | Build command + structured Output service + Output panel projection; register `project.build` / `project.cancel`; wire CanExecute. | `ProjectBuildCommandTests`, `ProjectOutputServiceTests`, Output VM tests; Linux smoke: build fixture project, Output shows lines | `workflow: build command and structured output` |
| **M3** | Parse build diagnostics; `IBuildDiagnosticsService`; Problems merge (LSP + build) + navigation; clear build diags on new build. | `BuildDiagnosticParserTests`, `BuildDiagnosticsServiceTests`, `ProblemsBuildProjectionTests`, navigation tests; Linux smoke: intentional CS error → Problems → jump | `workflow: build diagnostics in problems` |
| **M4** | Run command for eligible `CSharpProject` (and locked solution/run policy from M0 unresolved resolution); output reuse; cancel while running. | `ProjectRunCommandTests`; Linux smoke: run console fixture | `workflow: run command` |
| **M5** | Test command + `ITestResultsService` + Test Results surface (bottom mode or panel); basic TRX/console parse as locked in M5 plan slice; navigation from failing test if location available. | `ProjectTestCommandTests`, `TestResultsServiceTests`, VM tests; Linux smoke: pass + fail test | `workflow: test command and results surface` |
| **M6** | Closeout: full regression, command availability matrix, status-bar/operation feedback, docs/architecture/library truth-sync, accessibility/keyboard smoke, limitations final. | Full sequential build/test; `git diff --check`; `M6_MANUAL_EVIDENCE.md` | `docs(phase-11): M6 closeout` |

### Milestone dependencies

```text
M0 → M1 → M2 → M3
         ↘ M4 (after M2 Output exists; may parallel M3 only if Output API stable)
         ↘ M5 (after M2; Test Results UI may share Output patterns)
M3 + M4 + M5 → M6
```

Prefer sequential M1→M2→M3→M4→M5→M6 unless a later agent explicitly splits
slices (`M4a`/`M4b`) for session size.

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

**Linux manual smoke (required M2–M6):** record host, date, commands, and
pass/fail in `M*_MANUAL_EVIDENCE.md`. Minimum matrix by milestone:

| Milestone | Smoke |
|---|---|
| M2 | Build eligible `.csproj` fixture; Output lines; cancel mid-build |
| M3 | Build with deliberate error; Problems entry; navigate to line |
| M4 | Run console app; stdout in Output; cancel |
| M5 | `dotnet test` pass and fail; Test Results list |
| M6 | Full loop build→test→problems nav + command palette + keybindings |

## Phase 11 Limitations

- Linux is the primary validation platform (roadmap V2).
- C# / `dotnet` only; no multi-language build systems.
- Solution-level **Run** requires an explicit startup-project rule (see M0
  unresolved decisions); until locked, Run may be limited to `CSharpProject`.
- No watch mode, continuous test runner, code coverage UI, or Live Unit Testing.
- No custom user task files in Phase 11 exit.
- Interactive programs that require a TTY may misbehave under redirected
  stdout/stderr; Phase 11 documents that limit rather than emulating a PTY for
  Run.
- `LogCategorizer` terminal heuristics are **not** the build-diagnostics source
  of truth.

## Exit Conditions

- [ ] M0–M6 complete with named tests and recorded Linux evidence
- [ ] Eligible project context can Build, Run (per locked policy), and Test
- [ ] One-operation-at-a-time and cancel work
- [ ] Output, Problems (build+LSP), and Test Results remain distinct from Terminal
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

**M1 — Project process orchestration core** (no Output/Problems/Test UI yet):
implement target resolution, managed process runner, workflow service,
one-at-a-time policy, cancellation, generation, DI, and focused tests.
