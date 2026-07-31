# A2 Wiring Audit — `A2_WORKSPACE_AND_PROJECT_OPENING`

**Audit name:** `v1-v3-product-reality`
**Slice:** `A2_WORKSPACE_AND_PROJECT_OPENING` (ninth A2 slice; prior:
`A2_AGENT_SEND`, `A2_MULTI_AGENT_ROUTING`,
`A2_TRACE_MEMORY_USAGE_TERMINATION`, `A2_RESTART_RECOVERY_AND_CONTEXT`,
`A2_TOOLS_PERMISSIONS`, `A2_AGENT_CREATION_AND_BACKEND_ONBOARDING`,
`A2_TOWNHALL_AND_CONVERSATIONS`, `A2_FIRST_LAUNCH_AND_SETTINGS`)
**Evidence date:** 2026-07-31
**Baseline:** branch `master`, HEAD
`4c8addcf08beeea9c8413e2444b6aef1e2655f85` (matches `origin/master`)
**Method:** read-only source inspection (`rg`, file reads). No app launch,
build, test execution, production-code edits, commits, or pushes.

---

## 1. Audit identity, baseline, and safety boundary

| Check | Result |
|-------|--------|
| Branch | `master` |
| `git rev-parse HEAD` | `4c8addcf08beeea9c8413e2444b6aef1e2655f85` |
| `git rev-parse origin/master` | `4c8addcf08beeea9c8413e2444b6aef1e2655f85` |
| Working tree at audit start | Clean (`git status --short` empty) |
| Eight published A2 evidence files | Present (Agent Send, Multi-Agent Routing, Trace/Memory/Usage/Termination, Restart/Recovery/Context, Tools/Permissions, Agent Creation/Backend Onboarding, Townhall/Conversations, First Launch/Settings) |
| This slice evidence file before write | Absent |
| A1 acceptance authority | [A1_ACCEPTANCE.md](../A1_ACCEPTANCE.md) (2026-07-30) |
| Production code modified | No |
| Tests modified | No |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` edited | No |
| Earlier evidence edited | No |
| Issues / deferred findings edited | No |
| Real user profile / settings / secrets / workspace read or written | No |
| App launched | No |
| Build or tests run | No |
| A3 executed | No |
| Commit / push | No |

**Safety boundary:** this slice is A2 wiring inspection only. Production
source is verdict authority. Tests and historical phase closeout documents
are corroboration only. Runtime folder-picker UI, keyboard delivery,
`FileSystemWatcher` event delivery, git discovery against a real tree, and
live status-bar paint are not claimed from source alone. **No real user
profile, settings, secrets, or opened workspace path was accessed.**

**Verdict rows (this slice only):** `A1-WO-01` … `A1-WO-03`. No new verdicts
for AS, MR, TC, TP, AC, TH, FL, FN, or XX rows.

---

## 2. Sources inspected

### 2.1 Documentation

- [docs-rules.md](../../../../docs-rules.md)
- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md) (§2 Workspace / project opening;
  §17.8 A2 progress)
- [A1_ACCEPTANCE.md](../A1_ACCEPTANCE.md)
- Published A2 evidence (workspace-ownership intersections):
  - [A2_FIRST_LAUNCH_AND_SETTINGS.md](./A2_FIRST_LAUNCH_AND_SETTINGS.md)
    (next-slice charter; status bar / settings shell)
  - [A2_RESTART_RECOVERY_AND_CONTEXT.md](./A2_RESTART_RECOVERY_AND_CONTEXT.md)
    (opened-workspace vs process-CWD durable keys; conversation store not
    workspace-scoped)
  - [A2_TOOLS_PERMISSIONS.md](./A2_TOOLS_PERMISSIONS.md)
    (workspace-root containment for agent actions)
  - Other published slices listed for completeness only; no WO verdicts there
- V1 Phase 1 / 1.1 / 1.2:
  [PHASES.md §"Phase 1: File Tree Sidebar"](../../../roadmap/PHASES.md#phase-1-file-tree-sidebar);
  [§"Phase 1.1"](../../../roadmap/PHASES.md#phase-11-file-tree-polish);
  [§"Phase 1.2"](../../../roadmap/PHASES.md#phase-12-file-tree-essentials);
  [§"V1 Closeout"](../../../roadmap/PHASES.md#v1-closeout)
- V2 Phase 8 / 8.3:
  [V2.md §"Phase 8 — Core Platform and Settings"](../../../roadmap/V2.md#phase-8--core-platform-and-settings);
  [Phase 8 plan §"Live Baseline"](../../../phases/v2/phase-8/IMPLEMENTATION_PLAN.md#live-baseline-verified-2026-07-10);
  [Phase 8 plan §"Sub-Phase Decision"](../../../phases/v2/phase-8/IMPLEMENTATION_PLAN.md#sub-phase-decision-m0);
  [Phase 8.3 plan](../../../phases/v2/phase-8/phase-8.3/IMPLEMENTATION_PLAN.md)

### 2.2 Production source (minimum required + supporting)

**Open folder / file tree**

- [FileTreeViewModel.cs](../../../../src/Features/Workspace/Presentation/FileTreeViewModel.cs)
- [FileTreeView.cs](../../../../src/Features/Workspace/Presentation/FileTreeView.cs)
- [FileTreeService.cs](../../../../src/Features/Workspace/Infrastructure/FileTreeService.cs)
- [IFileTreeService.cs](../../../../src/Features/Workspace/Contracts/IFileTreeService.cs)
- [Workspace.cs](../../../../src/Features/Workspace/Domain/Workspace.cs)
- [WorkspaceActionAuthority.cs](../../../../src/Features/Workspace/Infrastructure/WorkspaceActionAuthority.cs)
- [WorkspaceServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/WorkspaceServiceCollectionExtensions.cs)
- [AppCoreServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AppCoreServiceCollectionExtensions.cs)
  (`Workspace` singleton)

**Shell open/close command path**

- [MainWindowViewModel.cs](../../../../src/App/Shell/MainWindowViewModel.cs)
- [MainWindow.axaml.cs](../../../../src/App/Shell/MainWindow.axaml.cs)
- [MainWindowActivationHost.cs](../../../../src/App/Shell/MainWindowActivationHost.cs)
- [StatusBarViewModel.cs](../../../../src/App/Shell/StatusBarViewModel.cs)

**Project context**

- [IProjectContextService.cs](../../../../src/Features/ProjectSystem/Contracts/IProjectContextService.cs)
- [ProjectContextService.cs](../../../../src/Features/ProjectSystem/Infrastructure/ProjectContextService.cs)
- [ProjectDiscovery.cs](../../../../src/Features/ProjectSystem/Infrastructure/ProjectDiscovery.cs)
- [ProjectContext.cs](../../../../src/Features/ProjectSystem/Domain/ProjectContext.cs),
  [ProjectContextState.cs](../../../../src/Features/ProjectSystem/Domain/ProjectContextState.cs),
  [ProjectCandidate.cs](../../../../src/Features/ProjectSystem/Domain/ProjectCandidate.cs)
- [ProjectSystemServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/ProjectSystemServiceCollectionExtensions.cs)
- [ApplicationShutdown.cs](../../../../src/App/Composition/ApplicationShutdown.cs)
  (dispose `IProjectContextService`)

**Downstream consumers (same authoritative context)**

- [ProjectTargetResolver.cs](../../../../src/Features/ProjectSystem/Infrastructure/ProjectTargetResolver.cs)
- [ProjectWorkflowService.cs](../../../../src/Features/ProjectSystem/Infrastructure/ProjectWorkflowService.cs)
- [ProjectWorkflowViewModel.cs](../../../../src/Features/ProjectSystem/Presentation/ProjectWorkflowViewModel.cs)
- [LanguageSessionService.cs](../../../../src/Features/Language/Application/LanguageSessionService.cs)
- [LanguageSessionStatusPolicy.cs](../../../../src/Features/Language/Application/LanguageSessionStatusPolicy.cs)
- [DebugSessionService.cs](../../../../src/Features/Debugging/Application/DebugSessionService.cs)
- [ProjectDebugLaunchService.cs](../../../../src/Features/ProjectSystem/Infrastructure/ProjectDebugLaunchService.cs)
- [BreakpointService.cs](../../../../src/Features/Debugging/Application/BreakpointService.cs)

**Source Control workspace refresh**

- [SourceControlViewModel.cs](../../../../src/Features/SourceControl/Presentation/SourceControlViewModel.cs)
- [SourceControlSnapshotMapper.cs](../../../../src/Features/SourceControl/Application/SourceControlSnapshotMapper.cs)

### 2.3 Tests (corroboration only; not verdict authority)

- Project-context lifecycle / discovery tests under
  `tests/Zaide.Tests/Features/ProjectSystem/` (not executed)
- File-tree and workspace tests under
  `tests/Zaide.Tests/Features/Workspace/` (not executed)
- Source Control workspace-path refresh tests (not executed)

---

## 3. Three-row verdict table

| id | a2_wiring_verdict | Summary |
|----|-------------------|---------|
| `A1-WO-01` | **Wired-with-gap** | User-reachable open-folder paths exist: `workspace.openFolder` / default `Ctrl+O` → `PickFolder` → native folder picker → `FileTreeViewModel.OpenFolderCommand` → `SetRootPath`; alternate header click “Open Folder…” on `FileTreeView` invokes the same command. Successful open enumerates via `FileTreeService` (default ignores include `node_modules`, `bin`, `obj`, `.git`, plus additional names), starts `FileSystemWatcher`, and binds `RootNodes`. Context menu offers Open / Expand All / Collapse All / New File / New Folder (plus Delete, Show Hidden Files, Refresh, Copy Path / Copy Relative Path). Hidden-files toggle is registered as `explorer.toggleHiddenFiles` with default `Ctrl+Shift+H` and a checkable menu item. Validate-before-teardown preserves the prior tree on failed open. **Gaps:** `FileTreeViewModel.StatusText` (open/create/delete/refresh failures) has **no production UI subscriber** — open-folder error strings are set but not projected to status bar or tree chrome; picker cancel is silent by design; `IOException` is not among `SetRootPath` catch clauses (unexpected I/O may surface as unhandled command fault rather than `StatusText`); live picker, keyboard delivery, and watcher behavior are A3. |
| `A1-WO-02` | **Wired-with-gap** | Singleton `IProjectContextService` / `ProjectContextService` is production-composed; discovers root-level `.sln` / `.slnx` / `.csproj` (and known unsupported extensions); implements load/reload/unload with sequence protection and structured states (`Unloaded`, `Loading`, `NoProject`, `Unsupported`, `SingleProject`, `Ambiguous`, `Selected`, `Failed`). Automatic selection occurs only for exactly one supported candidate (`SingleProject`). `MainWindowViewModel.CurrentProjectContext` projects `WhenChanged`; status bar maps NoProject / Unsupported / Failed / Loading / selected name. LSP (`LanguageSessionService`), Build/Run/Test (`ProjectWorkflowService` / `ProjectWorkflowViewModel` via `ProjectTargetResolver.IsEligible`), and Debug (`DebugSessionService`) all subscribe to the **same** service and treat only `SingleProject` / `Selected` with a non-null candidate as eligible. **Gaps:** `SelectProject` has **zero production callers** outside the service itself — ambiguous multi-project roots cannot be resolved by the user; status-bar `MapProjectText` has **no `Ambiguous` case** and falls through to `"Project error"`; Failed shows `"Project error"` without `ErrorMessage` detail; Reload is not user-commanded from shell chrome. DI/contracts alone do not equal a reachable project picker. |
| `A1-WO-03` | **Wired-with-gap** | Single production open/close truth for the loaded folder is `FileTreeViewModel.RootPath`. `MainWindowActivationHost` subscribes to `RootPath` (including null close), calls `Workspace.SetProjectFromPath`, updates `WorkspaceProjectName`, and executes `SourceControlViewModel.RefreshCommand`. `Workspace.SetProjectFromPath` updates `WorkspacePath` / `ProjectName` then raises `WorkspaceFolderChanged`. `ProjectContextService` production constructor subscribes to that event: non-null path → `LoadAsync`, null → `UnloadAsync` (observed, not unobserved fire-and-forget). Close folder is user-reachable via header close button / `CloseFolderRequested` → `workspace.closeFolder` / `CloseFolderCommand` → `SetRootPath(null)`. **Gaps:** project-context discovery is async after SC refresh (temporary `Loading` is expected, not stale); Failed/Ambiguous projection limitations (see WO-02); tree `StatusText` failures do not update workspace (correct for failed open) but also never reach the user; SC does not subscribe to `WorkspaceFolderChanged` itself — it is only refreshed on the `RootPath` host path (sufficient for production open/close, fragile if a second writer appeared); runtime proof of refresh against a real git tree is A3. |

Verdict definitions: [AUDIT_PLAN.md §2](../AUDIT_PLAN.md#2-audit-phase-decomposition).

---

## 4. End-to-end production-path maps

Legend: **T** = type/contract · **R** = production DI · **C** = production caller ·
**U** = user-reachable · **P** = user-visible result/failure · **A3** = runtime
unproven without clean-profile smoke.

### 4.1 `A1-WO-01` — open folder, tree, ignores, hidden, new file/folder

```text
[entry: Ctrl+O / command palette workspace.openFolder]
  CommandRegistry → MainWindowViewModel.OpenFolderCommand
  → PickFolder Interaction
  → MainWindow.axaml.cs: StorageProvider.OpenFolderPickerAsync
  → if path non-null: FileTreeViewModel.OpenFolderCommand.Execute(path)

[entry: Explorer header "Open Folder..."]
  FileTreeView header PointerPressed
  → OpenFolderPickerAsync → OpenFolderCommand.Execute(localPath)
  (does not use PickFolder Interaction)

[common open body]
  OpenFolderCommand → SetRootPath(path)
    normalize Path.GetFullPath + trim trailing separator
    EnumerateDirectory(normalized, ShowHiddenFiles)  // validate first
    on success: stop old watcher, set RootPath, replace RootNodes, StartWatching
    on DirectoryNotFound / UnauthorizedAccess / NotSupported / Argument:
         set FileTreeViewModel.StatusText; return false (preserve prior tree)

[RootPath change → workspace / SC / project context]  // see WO-03

[hidden files]
  explorer.toggleHiddenFiles (Ctrl+Shift+H) OR context menu "Show Hidden Files"
  → ToggleHiddenFilesCommand
  → flip ShowHiddenFiles; SetRootPath(same RootPath); revert flag on failure

[new file / new folder]
  context menu → modal name prompt → CreateNodeCommand
  → FileTreeService.CreateFile / CreateDirectory
  → watcher Created event → tree insert
  on IOException/UnauthorizedAccess: FileTreeViewModel.StatusText only

[ignore list — always]
  FileTreeService.DefaultIgnores:
    node_modules, bin, obj, .git, .vs, .idea, __pycache__, .DS_Store, Thumbs.db
  + hidden (name starts with '.') when includeHidden == false
```

| Layer | Status | Evidence |
|-------|--------|----------|
| 1. Type / contract | Present | `IFileTreeService`, `FileTreeNode`, `OpenFolderCommand` |
| 2. DI / composition | Present | `AddZaideWorkspace` → `IFileTreeService` / `FileTreeViewModel` singleton |
| 3. Production caller | Present | Registry `workspace.openFolder` [MainWindowViewModel.cs](../../../../src/App/Shell/MainWindowViewModel.cs) L293–296; picker [MainWindow.axaml.cs](../../../../src/App/Shell/MainWindow.axaml.cs) L318–326; header [FileTreeView.cs](../../../../src/Features/Workspace/Presentation/FileTreeView.cs) L56–67 |
| 4. User reachability | **Yes** (keyboard + header) | Default gesture materialization is registry-driven (same shell pattern as FL slice); header is pointer-reachable |
| 5. Tree contents / ignores | Source-proven | [FileTreeService.cs](../../../../src/Features/Workspace/Infrastructure/FileTreeService.cs) L20–24, L105–112 |
| 6. Context menu Open / Expand / Collapse / New File / New Folder | Source-proven | [FileTreeView.cs](../../../../src/Features/Workspace/Presentation/FileTreeView.cs) L327–380 |
| 7. Failure visibility | **Gap** | Status set on VM only ([FileTreeViewModel.cs](../../../../src/Features/Workspace/Presentation/FileTreeViewModel.cs) L335–349, L171–173); **no** `WhenAnyValue` on `FileTreeViewModel.StatusText` in shell or view |
| 8. Crash avoidance on known open faults | Source-proven for four catch types | Validate-before-teardown L280–352; uncaught `IOException` not mapped |

**Not in this row’s primary promise (owned by FN journey):** file open into editor, copy path, grid splitter width — noted only as co-located tree chrome.

### 4.2 `A1-WO-02` — discovery, selection, lifecycle, status, consumers

```text
[DI]
  AddZaideProjectSystem:
    IProjectFileSystem, IProjectDiscovery=ProjectDiscovery,
    IProjectContextService=ProjectContextService (singleton)
  AppCore: Workspace singleton
  ProjectContextService(workspace, discovery, logger) ctor:
    subscribe WorkspaceFolderChanged; if WorkspacePath already set → load

[load path — workspace event]
  WorkspaceFolderChanged → ReconcileFromWorkspace
    path != null → LoadAsync(path) → emit Loading → DiscoverAsync → MapResult
      empty supported+unsupported → NoProject
      unsupported only → Unsupported
      1 supported → SingleProject (auto-select that candidate)
      >1 supported → Ambiguous (SelectedProject=null)
      Failure → Failed(ErrorMessage)
    path == null → UnloadAsync → Unloaded

[selection]
  IProjectContextService.SelectProject(candidate)
  // production callers: NONE outside the service implementation

[UI projection]
  Activate: projectContext.WhenChanged.ObserveOn(UI) → CurrentProjectContext
  StatusBarViewModel.MapProjectText(CurrentProjectContext):
    SingleProject/Selected → DisplayName
    Loading → "Loading…"
    NoProject → "No project"
    Unsupported → "Unsupported project"
    Failed → "Project error"
    Unloaded → "Zaide"
    Ambiguous → default → "Project error"   // no dedicated case

[consumers — same IProjectContextService]
  LanguageSessionService: WhenChanged → reconcile; eligible SingleProject|Selected
  ProjectWorkflowService / ViewModel: IsEligible → Build/Run/Test can-execute
  DebugSessionService: IsDebugEligible → SingleProject|Selected + CSharpProject
  ProjectDebugLaunchService / BreakpointService: inject IProjectContextService
```

| Concern | Source-proven | User-reachable? |
|---------|---------------|-----------------|
| Discovery at opened root only | Yes — root file enumerate, no recursion into subfolders | Automatic on open |
| No-project / unsupported / failed states | Yes — structured `ProjectContextState` | Partially — short status text; no error detail |
| Single-project auto-select | Yes | Yes (implicit) |
| Ambiguous multi-candidate | State published | **Status mislabeled**; **no picker UI** |
| SelectProject API | Implemented | **Not reachable** |
| ReloadAsync | Implemented | No shell command registration found |
| LSP / Build / Debug same context | Yes — shared DI singleton | Downstream eligibility only when selected/single |

**DI vs reachability:** registering `IProjectContextService` and injecting it into
workflow/LSP/debug proves a shared contract. It does **not** prove the user can
choose among multiple `.sln`/`.csproj` files. That requires a UI or command path
calling `SelectProject`; none exists in `src/`.

### 4.3 `A1-WO-03` — WorkspacePath notification and consumer refresh

```text
[open or close folder — sole production writer of RootPath]
  FileTreeViewModel.SetRootPath(path|null)
  → RootPath property change

[MainWindowActivationHost]
  WhenAnyValue(FileTreeViewModel.RootPath)
    → Workspace.SetProjectFromPath(path)   // raises WorkspaceFolderChanged
    → WorkspaceProjectName = Workspace.ProjectName
    → SourceControlViewModel.RefreshCommand.Execute()

[ProjectContextService]
  WorkspaceFolderChanged handler
    → LoadAsync / UnloadAsync (async; sequence-guarded)

[Source Control]
  Refresh(workspace.WorkspacePath) → NotARepository / Success / Failed snapshot
  // no direct WorkspaceFolderChanged subscription

[close entry points]
  Header close button → CloseFolderRequested → CloseFolderCommand → SetRootPath(null)
  workspace.closeFolder command (no default keybinding)
```

| Consumer | Trigger | Clears on close? |
|----------|---------|------------------|
| `Workspace.WorkspacePath` | `SetProjectFromPath` | Yes → null |
| `WorkspaceFolderChanged` | After path update | Yes (null path event) |
| `ProjectContextService` | Event → UnloadAsync | Yes → `Unloaded` |
| Status bar project text | `CurrentProjectContext` | → `"Zaide"` when Unloaded |
| Source Control | `RefreshCommand` after path update | Refresh with null/empty path → empty / not-a-repo projection |
| LSP / Build / Debug | `WhenChanged` ineligible states | Tear down / unavailable (source-proven eligibility gates) |

**Stale-state precision:**

| Scenario | Source behavior | Gap? |
|----------|-----------------|------|
| Failed open (bad path) | `SetRootPath` returns false; `RootPath` unchanged; workspace event not re-fired for the failed path | Correct preservation; **error string not user-visible** |
| Successful open then close | Null RootPath → workspace null → unload + SC refresh | Wired |
| Ambiguous multi-project open | Context stays `Ambiguous` until folder change | **No selection path** → persistent non-selected state (not “stale wrong selection,” but stuck) |
| Overlapping loads | Sequence owner suppresses stale publish | Wired in service |
| SC after open | Sync refresh after `SetProjectFromPath` | Wired; live git result A3 |
| Agent durable storage | Prior A2: process CWD, not `WorkspacePath` | **Out of WO promise**; intersection only ([A2_RESTART…](./A2_RESTART_RECOVERY_AND_CONTEXT.md)) |

---

## 5. User reachability matrix

| Goal | User entry (source) | Reachable without DI trivia? |
|------|---------------------|------------------------------|
| Open folder | `Ctrl+O` / palette `workspace.openFolder`; Explorer header click | **Yes** (source); gesture delivery A3 |
| Close folder | Header close (X); `workspace.closeFolder` if discovered via palette | **Yes** for header; close has no default keybinding |
| Tree contents | After successful open | **Yes** |
| Ignore rules | Automatic in enumerate | **Yes** (observable absence of ignored dirs) |
| Hidden files | `Ctrl+Shift+H`; context menu check item | **Yes** (source) |
| New File / New Folder | Tree context menu + modal | **Yes** when folder open (`GetParentDirForCreation`) |
| Open-folder failure message | `FileTreeViewModel.StatusText` only | **No UI projection** |
| Project status (no/unsupported/failed/loading/name) | Status bar `ProjectText` | **Yes** for mapped states |
| Ambiguous project pick | `SelectProject` | **No** production UI |
| Project reload | `ReloadAsync` | **No** user command |
| SC refresh on open/close | Automatic on `RootPath` | **Yes** (host path) |

---

## 6. Source-proven vs runtime-unproven

| Claim | Class |
|-------|--------|
| Open-folder command registration and picker Interaction wiring | Source-proven |
| Header alternate open path | Source-proven |
| Validate-before-teardown open | Source-proven |
| Default ignore set includes promised names | Source-proven (plus extra names) |
| Hidden toggle command + menu | Source-proven |
| New File / New Folder create path | Source-proven |
| `FileTreeViewModel.StatusText` unbound | Source-proven absence |
| `WorkspaceFolderChanged` after `SetProjectFromPath` | Source-proven |
| Project discovery state machine + MapResult | Source-proven |
| `SelectProject` without production callers | Source-proven absence |
| Ambiguous → status `"Project error"` | Source-proven mapping gap |
| LSP / Build / Debug share `IProjectContextService` eligibility | Source-proven |
| SC refresh on RootPath including null | Source-proven |
| Native picker UI success/cancel UX | **Runtime-unproven (A3)** |
| Keyboard delivery of Ctrl+O / Ctrl+Shift+H | **Runtime-unproven (A3)** |
| Watcher live-sync under load | **Runtime-unproven (A3)** |
| Git status truth after open | **Runtime-unproven (A3)** |
| Multi-sln workspace user experience | **Runtime-unproven (A3)**; selection UI missing already blocks success |

---

## 7. Contradiction / reconciliation notes

1. **Phase closeouts vs open-folder errors**
   Phase 1.1 claims OpenFolderCommand exception handling. Production does catch
   four exception types in `SetRootPath` and avoids tearing down the prior tree.
   That is **not** the same as user-visible recovery: messages land on an unbound
   `StatusText`. Closeout “handled” ≠ status-bar projection.

2. **“Pick a project” vs automatic SingleProject**
   Goal matrix entry “Open a folder; pick a project” and Phase 8.3
   `SelectProject` describe selection. Production auto-selects only the single-
   candidate case. Multi-candidate `Ambiguous` is a real state with **no** user
   resolution path. Do not treat phase closeout “selection” as a product picker.

3. **Status bar “Project error” for Ambiguous**
   `MapProjectText` omits `ProjectContextState.Ambiguous`, so multi-solution
   folders display like I/O failure. Consumers correctly refuse eligibility
   (`IsEligible` false), but the label is untruthful.

4. **Shared context is real; reachability of selection is not**
   LSP, Build, and Debug wiring to one `IProjectContextService` is production-
   proven. That satisfies the “same context” half of WO-02. The “authoritative
   selection the user chose” half is only proven for auto-`SingleProject`.

5. **Workspace vs opened-folder agent storage**
   [A2_RESTART_RECOVERY_AND_CONTEXT](./A2_RESTART_RECOVERY_AND_CONTEXT.md)
   already records Phase 21 durable keys as process-CWD-keyed, not
   `Workspace.WorkspacePath`. This slice does not re-verdict XX rows; it only
   notes that opening a folder does not, by itself, re-key those stores.

6. **Historical Phase 8 Live Baseline is pre-implementation**
   The Phase 8 umbrella “Live Baseline” table describes the pre-8.x world
   (no project context, no `WorkspaceFolderChanged`). Current code has those
   seams. A2 treats the baseline as historical documentation, not live truth.

7. **Source Control dual path**
   SC refresh is driven by `RootPath` host logic, not by a direct
   `WorkspaceFolderChanged` subscription. Ordering is safe today because
   `SetProjectFromPath` runs first in the same handler. Project context uses the
   event. Both refresh on the production open/close path.

---

## 8. A3 constraints only (not executed)

A3 for this journey **must** use a disposable isolated profile
(`XDG_CONFIG_HOME` absolute temp directory established **before** process
start). Never the real user profile, real settings/secrets, or a real
developer workspace under the user’s home tree.

Suggested disposable-profile scenarios (description only):

1. **Open folder (WO-01):** disposable tree with `bin/`, `node_modules/`,
   `.git/`, a normal file, and a `.hidden` file. Open via `Ctrl+O` and via
   header. Confirm tree omits ignored dirs; toggle hidden with
   `Ctrl+Shift+H` / menu; New File / New Folder; observe create failures if
   permissions block (expect silent `StatusText` unless A3 also checks for
   missing UI).
2. **Open-folder failure (WO-01):** attempt an inaccessible or deleted path if
   the environment can construct one; confirm app does not crash and prior
   folder remains; note whether any user-visible error appears (source predicts
   none on status bar).
3. **No-project (WO-02):** open a folder with no project-like files; status bar
   shows `"No project"`; Build/Run/Test disabled / LSP unavailable.
4. **Unsupported (WO-02):** root-only known unsupported project extension
   (e.g. disposable `.vbproj`); status `"Unsupported project"`.
5. **Single project (WO-02):** one disposable `.csproj` or `.sln`; status shows
   display name; optional light LSP/build eligibility observation without
   re-auditing full FN/BD journeys.
6. **Ambiguous (WO-02):** two disposable `.sln` or `.csproj` at root; status
   currently expected as `"Project error"` (mapping gap); confirm no picker;
   Build/LSP remain non-eligible.
7. **Open then close (WO-03):** open a git disposable repo; SC shows branch or
   not-a-repo truthfully; close folder; project text → `"Zaide"` / Unloaded;
   SC clears; no lingering project name from the closed folder.

Production DI is allowed only when the disposable config root is set first.

**A3 is not executed in this session.**

---

## 9. Next recommended A2 slice

**Next recommended A2 slice:** `A2_FILE_NAVIGATION_AND_EDITING`

| Item | Value |
|------|-------|
| Slice name | `A2_FILE_NAVIGATION_AND_EDITING` |
| Goal rows | `A1-FN-01` … `A1-FN-06`, `A1-FN-08` … `A1-FN-15` (FN-07 retired) |
| Evidence file | `docs/audits/v1-v3-product-reality/evidence/A2_FILE_NAVIGATION_AND_EDITING.md` |
| Status in this session | **Explicitly not started** — file not created; no FN verdicts assigned |

Rationale: matrix journey order after Workspace / Project Opening is File
Navigation and Editing; WO open-folder wiring is the prerequisite for editor
and LSP rows that assume an opened folder/project.

---

## 10. Verification and working-tree closeout

### 10.1 Required content checklist

| Required section | Present |
|------------------|---------|
| 1. Audit identity, baseline, safety | Yes |
| 2. Sources inspected | Yes |
| 3. Three-row verdict table (each WO id once) | Yes |
| 4. Production-path maps | Yes |
| 5. User reachability | Yes |
| 6. Source-proven vs runtime-unproven | Yes |
| 7. Contradiction / reconciliation | Yes |
| 8. A3 constraints only | Yes |
| 9. Next slice explicitly not started | Yes |
| 10. Verification closeout | Yes |

### 10.2 Truth-constraint self-check

| Constraint | Honored? |
|------------|----------|
| DI registration ≠ user reachability | Yes (`SelectProject`, Reload) |
| Tests / phase closeouts ≠ production wiring proof | Yes |
| Shared service injection ≠ project picker | Yes |
| No real profile / workspace access | Yes |
| Prior-slice verdicts not reassigned | Yes |
| Each of `A1-WO-01`…`A1-WO-03` exactly once in primary table | Yes |
| No runtime claims from source alone | Yes |
| No production code / AUDIT_PLAN / GOAL_MATRIX edits | Yes |

### 10.3 Closeout verification commands (post-write)

Executed after writing this file only:

- Confirm exactly one untracked evidence file:
  `docs/audits/v1-v3-product-reality/evidence/A2_WORKSPACE_AND_PROJECT_OPENING.md`
- Confirm no tracked modifications
- Whitespace check for the **untracked** file:

  ```bash
  git diff --no-index --check /dev/null \
    docs/audits/v1-v3-product-reality/evidence/A2_WORKSPACE_AND_PROJECT_OPENING.md
  ```

  Exit status **1 is expected** because the files differ; there must be
  **no whitespace-diagnostic output**.
- Relative Markdown paths and fragment links resolve against this tree
- Primary verdicts: `A1-WO-01` Wired-with-gap; `A1-WO-02` Wired-with-gap;
  `A1-WO-03` Wired-with-gap
- `A2_FILE_NAVIGATION_AND_EDITING` not created / not started
- `AUDIT_PLAN.md` / `GOAL_MATRIX.md` not edited (Codex synchronizes after publish)

---

*End of `A2_WORKSPACE_AND_PROJECT_OPENING` evidence. Stop for re-audit. No commit or push.*
