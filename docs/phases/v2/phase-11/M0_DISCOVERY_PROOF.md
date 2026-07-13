# Phase 11 M0: Project Workflow Discovery Proof

## Status: M0 complete (live-code baseline + locked contracts)

**Verified against live code at:** `1569e6dad6e6f1615e3677460a44f2ad5cf8cd42` (2026-07-14)
**Host:** Linux, `dotnet` 10.0.109 (`/usr/bin/dotnet`)
**Scope:** Documentation-only planning/proof gate. **No** production Build, Run,
Test, Output, Problems-build, or test-results application code is introduced in
M0.

This document is the review artifact for Phase 11 M0. Claims about existing
seams cite live types under `src/`. Planned types are marked **(planned M1+)**.

---

## 1. Live Ownership and Connection Graph

```text
FileTreeViewModel.RootPath
  -> MainWindowViewModel.Activate() subscription
  -> Workspace.SetProjectFromPath(root)
  -> Workspace.WorkspaceFolderChanged
  -> ProjectContextService.OnWorkspaceFolderChanged
  -> IProjectContextService.LoadAsync / UnloadAsync
  -> ProjectContext immutable snapshots (Current + WhenChanged)
  -> MainWindowViewModel.CurrentProjectContext (UI projection)
  -> StatusBarViewModel project text

FileTreeViewModel.OpenFileRequested
  -> EditorTabViewModel.OpenFileCommand
  -> IFileService.ReadAllTextAsync
  -> Workspace.OpenDocument(path, content)
  -> EditorTabViewModel.OpenTabs + ActiveTab
  -> EditorViewModel.RequestNavigate (navigation only)

ICommandRegistry (CommandRegistry singleton)
  -> CommandDescriptor registration from ViewModels (constructors)
  -> ResolveKeyBindings(ISettingsService)
  -> MainWindow.MaterializeRegistryBindings
  -> Avalonia KeyBinding + CommandPaletteViewModel enumeration

ILanguageDiagnosticsService
  -> ProblemsViewModel.Activate() / WhenChanged projection
  -> NavigateToProblemAsync
  -> EditorTabViewModel.OpenFileCommand + EditorViewModel.RequestNavigate

ITerminalSessionFactory / ITerminalHost
  -> TerminalViewModel per tab
  -> LinuxTerminalService (PTY) — interactive only

LanguageSessionService (Phase 10)
  -> ProcessStartInfo + Process for csharp-ls only
  -> Not a build/run/test host
```

### Confirmed DI registrations (`Program.ConfigureServices`)

Live as of HEAD (excerpt of relevant singles):

| Registration | Role for Phase 11 |
|---|---|
| `ICommandRegistry` → `CommandRegistry` | Sole command/keybinding discovery surface |
| `IProjectFileSystem` / `IProjectDiscovery` / `IProjectContextService` | Authoritative project target owner |
| `ITerminalSessionFactory` / `ITerminalHost` | Interactive terminal — **not** workflow process host |
| `ILanguageDiagnosticsService` / `ProblemsViewModel` | LSP Problems; M3 extends projection for build diags |
| `MainWindowViewModel`, `EditorTabViewModel`, `Workspace` | UI shell + document open path |
| Language session stack | Unrelated process (LSP); pattern reference only |

**Absent today (must be added M1+):** any `IProjectWorkflowService`, process
runner for `dotnet build|run|test`, structured Output service, build diagnostic
parser, test-results service, or `project.*` command IDs.

### Bottom panel modes (live)

`BottomPanelMode` currently has only:

- `Terminal`
- `Problems`

Phase 11 M2+ will extend this enum (or equivalent host switch) for **Output**
and **Test Results** without aliasing them to `Terminal`.

---

## 2. Authoritative ProjectContext → Build/Run/Test Target Contract

### 2.1 Live types

| Type | Path | Role |
|---|---|---|
| `IProjectContextService` | `src/Services/IProjectContextService.cs` | Load/Reload/Unload/Select; `Current`; `WhenChanged` |
| `ProjectContext` | `src/Services/ProjectContext.cs` | Immutable snapshot |
| `ProjectContextState` | `src/Services/ProjectContextState.cs` | `Unloaded`, `Loading`, `NoProject`, `Unsupported`, `SingleProject`, `Ambiguous`, `Selected`, `Failed` |
| `ProjectCandidate` | `src/Services/ProjectCandidate.cs` | `FilePath`, `DisplayName`, `Kind` |
| `ProjectKind` | `src/Services/ProjectKind.cs` | `Solution`, `SolutionX`, `CSharpProject` |
| Eligibility (LSP mirror) | `LanguageSessionService.IsEligible` | `SelectedProject != null` && state is `SingleProject` or `Selected` |

### 2.2 Locked eligibility (Phase 11)

**Same eligibility gate as Phase 10 language sessions**, applied to workflow
operations:

```text
Eligible ⇔
  context.SelectedProject is not null
  AND context.State is SingleProject or Selected
```

| State | Eligible | Operator-facing behavior |
|---|---|---|
| `Unloaded` | No | No project loaded; commands disabled |
| `Loading` | No | Discovery in progress; do not start or queue |
| `NoProject` | No | Folder has no supported project files |
| `Unsupported` | No | Only unsupported project-like files at root |
| `Ambiguous` | No | Multiple candidates; user must `SelectProject` first |
| `Failed` | No | Discovery failed; surface `ErrorMessage` only; do not run `dotnet` |
| `SingleProject` | **Yes** | Auto-selected sole candidate |
| `Selected` | **Yes** | User-selected candidate from current snapshot |

### 2.3 Target fields

| Field | Source | Notes |
|---|---|---|
| Target file | `SelectedProject.FilePath` | Absolute, normalized; ordinal identity |
| Display name | `SelectedProject.DisplayName` | Presentation only |
| Kind | `SelectedProject.Kind` | Selects default profile argv |
| Working directory | `Path.GetDirectoryName(FilePath)` | Parent of solution/project file |
| Workspace root | `ProjectContext.WorkspaceRoot` | Informational / status; **not** argv target |

**Forbidden fallbacks:**

- `Workspace.WorkspacePath` / `Workspace.ProjectName` as build targets
- Implicit “first .csproj under tree” discovery
- Starting processes because a folder opened (`LoadAsync` side effects must
  remain discovery-only — already true today)

### 2.4 Default execution profiles (locked defaults; settings optional later)

Tool: `dotnet` resolved from `PATH` (same class of host dependency as developer
machine already used for building Zaide).

| Operation | `CSharpProject` | `Solution` / `SolutionX` |
|---|---|---|
| Build | `dotnet build "<FilePath>"` | `dotnet build "<FilePath>"` |
| Test | `dotnet test "<FilePath>"` | `dotnet test "<FilePath>"` |
| Run | `dotnet run --project "<FilePath>"` | **Deferred default:** ineligible until startup-project rule is locked (see §10) |

Additional argv policy for M1:

- Prefer no extra verbosity flags unless needed for parsing.
- Environment: inherit process environment; do not inject agent secrets.
- Working directory: target file parent directory.

### 2.5 Stale context during an operation

1. Workflow service stores **operation generation** + **target FilePath** at start.
2. On each `WhenChanged` snapshot: if no longer eligible **or**
   `SelectedProject.FilePath` differs (ordinal), **cancel** the active operation
   and ignore subsequent events for the old generation.
3. `Loading` while an operation runs (reload): cancel active operation; do not
   start a new one until a new eligible terminal snapshot arrives **and** the
   user re-invokes a command (no auto-restart).

---

## 3. Command IDs, Registry Ownership, CanExecute, Gestures

### 3.1 Live registry contract

- `ICommandRegistry.Register(CommandDescriptor)` — duplicate IDs throw.
- `CommandDescriptor`: `Id`, `DisplayName`, `Category`, `DefaultGestures`, `ICommand`.
- `Execute` / `Execute<T>` return `false` when unknown or `CanExecute` is false;
  exceptions are logged, not thrown to the keybinding layer.
- `ResolveKeyBindings` merges defaults + user overrides; window materializes
  Avalonia bindings only.

**Established registration pattern:** ViewModels register in constructors after
creating `ReactiveCommand` instances (e.g. `MainWindowViewModel` for
`file.save`, `EditorLanguageInputViewModel` for `editor.*`). Phase 11 follows
the same path — **no** second command bus, menu service, or hard-coded
`KeyBinding` list outside registry materialization.

### 3.2 Locked Phase 11 command inventory

| Command ID | DisplayName | Category | DefaultGestures | CanExecute |
|---|---|---|---|---|
| `project.build` | Build | Project | `["Ctrl+Shift+B"]` | Eligible context && !operation active |
| `project.run` | Run | Project | `["Ctrl+F5"]` | Eligible context && Run profile supported for kind && !operation active |
| `project.test` | Run Tests | Project | `[]` (palette only) | Eligible context && !operation active |
| `project.cancel` | Cancel Build/Run/Test | Project | `[]` | Operation active |

**Why these IDs:** dotted stable IDs match Phase 8/9/10 (`file.save`,
`editor.formatDocument`). The `project.` prefix scopes workflow without
colliding with `workspace.*` folder commands or `editor.*` language commands.

**Why `Ctrl+F5` for Run, not `F5`:** Phase 12 owns debugging; `F5` is reserved
for DAP Start. Run-without-debug uses `Ctrl+F5`.

**Why no default for Test/Cancel:** avoids fighting `Ctrl+T` (workspace
symbol) and special-casing Escape. Palette + UI affordances are enough for V2.

### 3.3 Ownership of registration (planned)

| Component | Registers |
|---|---|
| **(planned)** `ProjectWorkflowCommands` host or `MainWindowViewModel` / dedicated thin ViewModel | The four `project.*` commands bound to ReactiveCommands that call `IProjectWorkflowService` |
| `CommandRegistry` | Storage + resolve only |
| `MainWindow` | Materialize keybindings only |

---

## 4. UI-Independent Service Ownership

### 4.1 Planned service map

| Service | Lifetime | Owns | Does not own |
|---|---|---|---|
| `IProjectWorkflowService` | Singleton | One active operation slot; start Build/Run/Test; cancel; generation; observes `IProjectContextService` | UI strings formatting for presentation chrome; project discovery |
| `IProjectExecutionProfileResolver` | Singleton / pure | Argv + cwd + operation kind from `ProjectCandidate` | Process handles |
| `IManagedProcessRunner` | Transient per op or owned by workflow | `ProcessStartInfo`, redirect stdout/stderr, kill tree, exit code, startup failures | PTY, user keystrokes |
| `IProjectOutputService` | Singleton | Structured line buffer for current/last operation; observable snapshot | Terminal tabs |
| `IBuildDiagnosticsService` | Singleton | Parsed build diagnostics for last build generation | LSP diagnostics |
| `ITestResultsService` | Singleton | Structured test outcomes for last test run | Output line dump alone |

Views/ViewModels:

- Project Output VM / Test Results VM / extended Problems VM **project** service
  snapshots.
- Commands invoke workflow methods; they do not call `Process.Start`.

### 4.2 One-operation-at-a-time policy

- Shared slot for Build, Run, and Test (not three parallel queues).
- If slot busy: return structured `RejectedConcurrent` without starting a
  process; do not enqueue.
- `project.cancel` requests cancellation on the active slot only.

### 4.3 Why not reuse terminal or language process types

| Existing type | Why insufficient for workflow |
|---|---|
| `ITerminalService` / `LinuxTerminalService` | PTY + interactive shell; OutputReceived is raw PTY bytes; not cancellable build orchestration |
| `CsharpLsSession` | Long-lived LSP stdio JSON-RPC; wrong lifetime and protocol |
| `AgentExecutionService` | HTTP OpenAI-compatible calls; unrelated |
| `LogCategorizer` | Best-effort terminal line tags; not structured diagnostics |

### 4.4 Process implementation choice (locked)

Use **`System.Diagnostics.Process`** with redirected standard streams (same
family as `CsharpLsSession`). **Do not** add CliWrap in M1 solely for
ergonomics — it remains catalogued in `docs/LIBRARIES.md` if a later milestone
proves need. No new NuGet is required for Phase 11 M1.

---

## 5. Structured Surfaces vs Interactive Terminal

| Surface | Data source | Interaction | Phase |
|---|---|---|---|
| Terminal | PTY child shell | Read/write, TUI apps | V1 / 3.x |
| Output **(Phase 11)** | Redirected process stdout/stderr | Read-only structured log | M2 |
| Problems | LSP diagnostics **+** build diagnostics | Navigate to location | 10 + M3 |
| Test Results **(Phase 11)** | Parsed test outcomes | Navigate when path known | M5 |

Rules:

1. Build/Run/Test **never** attach to `ITerminalHost` sessions.
2. Terminal `[BUILD]` heuristic coloring does **not** feed Problems.
3. Switching bottom-panel mode must not destroy terminal session state
   (existing tab host cache pattern stays).
4. Output clear/replace policy: on each new accepted operation start, begin a
   new output generation (UI may clear or section-break).

---

## 6. Process Lifecycle and Result Contracts

### 6.1 Operation states (workflow snapshot)

Illustrative enum **(planned M1)** — names may match code exactly at M1:

```text
Idle
Starting
Running
Succeeded
Failed          // non-zero exit
Cancelled
StartupFailed
RejectedConcurrent
RejectedContext
```

### 6.2 Event matrix

| Event | Result kind | Process | Output | Diagnostics/Tests |
|---|---|---|---|---|
| User Build, eligible, idle | → Running → Succeeded/Failed | Started | Lines streamed | Build diags replace on build start; reparsed at end or incrementally |
| User Build, ineligible | `RejectedContext` | Not started | Unchanged or status line | Unchanged |
| User Build while Running | `RejectedConcurrent` | Not started | Unchanged | Unchanged |
| Non-zero exit | `Failed` | Exited | Complete | Parser may add errors |
| Exit 0 | `Succeeded` | Exited | Complete | Clear stale errors if rebuild clean |
| `dotnet` missing / Start throws | `StartupFailed` | None or failed start | Error line | No false “build failed” diag set required beyond message |
| Cancel | `Cancelled` | Kill entire process tree when started | Note cancelled | Partial diags may remain from partial output — M3 locks “keep partial vs clear” |
| Context change mid-run | `Cancelled` (system) | Kill | Note context changed | Treat as cancel |
| Dispose service | Cancel + dispose | Kill | Final snapshot idle | Clear or freeze last snapshot — M1 locks freeze-last |

### 6.3 Timeout

**Not required for Phase 11 exit.** Builds/tests may run indefinitely until
cancel. If a watchdog is added later, it must be an explicit settings-backed
timeout with a distinct `TimedOut` kind — do not silently reuse `Failed`.

### 6.4 Disposal

Mirror language-session discipline:

- `IProjectWorkflowService : IDisposable`
- App exit path disposes after language diagnostics (order: cancel workflow
  processes before or with other process owners; exact order recorded at M1 DI
  wiring)
- No orphan `dotnet` children after window close (kill tree)

---

## 7. Navigation Ownership

### 7.1 Live path (proven by Phase 10 Problems)

`ProblemsViewModel.NavigateToProblemAsync`:

1. Re-validate against live diagnostics snapshot (generation, identity).
2. `await _editorTabs.OpenFileCommand.Execute(live.FilePath)`.
3. Confirm `ActiveTab.FilePath` matches.
4. Map range → offsets with `LspUtf16PositionMapper` (LSP) or line/col mapper
   for MSBuild (planned).
5. `tab.RequestNavigate(startOffset, length)`.

### 7.2 Phase 11 lock

| Origin | Open file | Jump caret |
|---|---|---|
| Build diagnostic | `EditorTabViewModel.OpenFileCommand` | `EditorViewModel.RequestNavigate` |
| Test failure with path | same | same |
| Output raw line click (optional M2+) | same | only if parseable location |

**Do not** call `Workspace.OpenDocument` directly from workflow services
(services must not own tabs). Navigation stays in ViewModels, matching
MVVM rules in `docs/CONVENTIONS.md` and `docs-rules.md` §12a.

---

## 8. Milestone Decomposition

See [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) for the full table.
Summary:

| ID | Deliverable | Depends on | Tests (named intent) | Commit |
|---|---|---|---|---|
| M0 ✅ | This proof + plan + date truth-sync | Phase 10 complete | Docs + `git diff --check` | docs M0 |
| M1 | Runner + workflow + profiles + DI | M0 | `ProjectTargetResolutionTests`, `ManagedProcessRunnerTests`, `ProjectWorkflowServiceTests`, DI tests | orchestration core |
| M2 | Build + Output UI + commands | M1 | Build/output command + VM tests; Linux smoke | build + output |
| M3 | Build diags + Problems merge + nav | M2 | Parser + build diags + Problems projection tests; Linux smoke | problems build |
| M4 | Run | M2 | Run command tests; Linux smoke | run |
| M5 | Test + results surface | M2 | Test + results tests; Linux smoke | test results |
| M6 | Closeout | M3–M5 | Full suite + evidence | closeout |

---

## 9. Concrete Verification Commands

### 9.1 M0 gate (this session)

```bash
git diff --check
# Manual: confirm files exist
test -f docs/phases/v2/phase-11/IMPLEMENTATION_PLAN.md
test -f docs/phases/v2/phase-11/M0_DISCOVERY_PROOF.md
```

Optional host sanity (not a product build of workflow code):

```bash
dotnet --version
which dotnet
```

Recorded: `dotnet` 10.0.109 at `/usr/bin/dotnet` on 2026-07-14.

### 9.2 Every production milestone (M1+)

Sequential only — **never** overlapping build and test processes:

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build
git diff --check
```

### 9.3 Focused filters (illustrative M1+)

```bash
dotnet test Zaide.slnx --no-build --filter FullyQualifiedName~ProjectTargetResolution
dotnet test Zaide.slnx --no-build --filter FullyQualifiedName~ProjectWorkflow
dotnet test Zaide.slnx --no-build --filter FullyQualifiedName~ManagedProcessRunner
dotnet test Zaide.slnx --no-build --filter FullyQualifiedName~BuildDiagnostic
```

### 9.4 Linux manual smoke evidence (M2+)

Required evidence files under `docs/phases/v2/phase-11/`:

- `M2_MANUAL_EVIDENCE.md` — build + Output + cancel
- `M3_MANUAL_EVIDENCE.md` — diagnostics + Problems navigate
- `M4_MANUAL_EVIDENCE.md` — run
- `M5_MANUAL_EVIDENCE.md` — test pass/fail
- `M6_MANUAL_EVIDENCE.md` — full loop closeout

Each must record: date, host OS, fixture path, commands, observed result
pass/fail.

---

## 10. Unresolved Decisions (explicit, bounded)

These are **not** blockers for M0 closeout; they must be resolved at the
milestone that first needs them:

| # | Decision | Resolve by | Options |
|---|---|---|---|
| U1 | Solution-level **Run** startup project | M4 | (a) Run ineligible for `Solution`/`SolutionX` until Phase 12/settings; (b) heuristic first executable project; (c) user setting. **M0 recommendation:** (a) |
| U2 | Incremental vs end-of-build diagnostic parse | M3 | Prefer end-of-build parse for YAGNI; stream lines still go to Output |
| U3 | Partial diagnostics after cancel | M3 | Recommend keep last partial set with `Cancelled` status banner |
| U4 | Test result format (`dotnet test` logger) | M5 | Prefer console parse first; optional TRX if console insufficient |
| U5 | Bottom-panel enum shape for Output/Test | M2 / M5 | Extend `BottomPanelMode` vs nested tabs inside Output host |
| U6 | Status-bar operation text owner | M2 | `MainWindowViewModel.StatusText` vs dedicated workflow status property |

---

## 11. Phase 11 Limitations and YAGNI (M0)

- No task-runner plugins, `tasks.json`, or multi-step pipelines.
- No auto-build on save or on folder open.
- No DAP, breakpoints, or Debug Console (Phase 12).
- No multi-framework matrix UI.
- No Windows/macOS parity commitment beyond avoiding unnecessary coupling.
- No agent-invoked build tools (V3).
- No replacement of Phase 8.3 discovery.
- CliWrap not adopted at M0/M1 without proven need.

---

## 12. Rollback Plan

1. M0 is docs-only — revert by deleting/restoring the phase-11 docs and date
   truth-sync hunks.
2. M1+ : one commit per milestone; `git revert` the milestone commit.
3. Structural failure: `docs/phases/v2/phase-11/REVERT_LOG.md` + reset to last
   green milestone or pre-M1 HEAD.
4. Never leave `project.*` commands registered without a working
   `CanExecute`/`Execute` path.

---

## 13. Source Layout Decision

**No broad `src/` reorganization.** Add workflow types under `src/Services/`
(and ViewModels/Views for projection) consistent with Phase 8.3 / Phase 10.
Introduce a subfolder only if file count forces clarity — not at M0.

---

## 14. Truth-Sync Performed in M0

| Document | Change |
|---|---|
| `docs/roadmap/V2.md` | Phase 10 M7 closeout date 2026-07-13 → **2026-07-14**; footer updated |
| `docs/architecture/OVERVIEW.md` | Phase 10 complete dates → **2026-07-14**; Phase 11 M0 note; footer |
| `docs/LIBRARIES.md` | Footer Phase 10 complete date → **2026-07-14** |

Historical milestone dates for Phase 10 M0–M5 evidence files remain
2026-07-13 where those milestones actually landed. Only **M7 closeout**
claims that still said 2026-07-13 were corrected to match
`M7_MANUAL_EVIDENCE.md` / phase-10 plan status (**2026-07-14**).

---

## 15. M0 Exit Checklist

- [x] Live seams inspected (Program, project context, commands, MainWindow VM,
      Problems, terminal, workspace navigation, process precedents)
- [x] Contracts 1–7 locked in this document and the implementation plan
- [x] Milestone M0–M6 decomposition with tests and commit boundaries
- [x] Verification commands and Linux smoke requirements listed
- [x] Limitations, YAGNI, rollback recorded
- [x] Unresolved decisions listed with recommended defaults
- [x] English-only documentation
- [x] No production Phase 11 workflow implementation
- [x] Recommended next milestone: **M1** only
