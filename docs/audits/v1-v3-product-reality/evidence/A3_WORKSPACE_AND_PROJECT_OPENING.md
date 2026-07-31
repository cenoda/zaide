# A3 Clean-Profile Smoke — Workspace and Project Opening (`A1-WO-01` … `A1-WO-03`)

**Audit name:** `v1-v3-product-reality`
**Phase scope of this note:** **A3 workspace / project opening execution slice only** — rows
`A1-WO-01`, `A1-WO-02`, and `A1-WO-03`.
**Evidence date:** 2026-07-31
**Repo head at run:** `612ffebe0c614562e4dec7b5eb7ced89ff069a30`

---

## 0. Charter and status

| Item | Status |
|------|--------|
| **Document class** | **A3 product-runtime clean-profile smoke evidence** (workspace/project rows only) |
| **A3 slice** | Workspace and Project Opening (`A1-WO-01`…`A1-WO-03`) |
| **A3 as a whole** | **Incomplete** — editor/LSP, build/run/test, debugging, Git workflow, Townhall, agents, permissions, trace, memory, restart-recovery rows **not executed** |
| **A4 / stabilization / V4** | **Not begun** |
| Real desktop UI / xdtools / screenshots / pointer automation / manual desktop | **Not used** |
| Production code modified | **No** |
| Tracked tests modified | **No** |
| `Avalonia.Headless` added to tracked project | **No** |
| `Directory.Packages.props` changed | **No** |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` rewritten to “A3 complete” | **No** |
| Prior A2 / A3 evidence rewritten | **No** |
| Real user `~/.config/zaide` read or written by this run | **No** (disposable `HOME` + `XDG_*` only) |

**Authority inputs:**

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) (H0)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md) (H1)
- [A2_WORKSPACE_AND_PROJECT_OPENING.md](./A2_WORKSPACE_AND_PROJECT_OPENING.md)

**Out of scope for this slice (explicit):**

- Editor open/edit/save/LSP rows, Build/Run/Test, Debugging, Git mutation workflow,
  Townhall, agents, permissions, trace, memory, restart mid-run
- A4, stabilization, V4 planning
- Production code, tracked tests, package pins, audit policy edits

---

## 1. Three-row classification table (authoritative for this slice)

| id | A3 classification | Summary |
|----|-------------------|---------|
| `A1-WO-01` | **WORKS_WITH_FRICTION** | Registered `workspace.openFolder` + deterministic production `PickFolder` Interaction handler opens disposable Workspace A. `RootPath` and `RootNodes` populate; `node_modules` / `bin` / `obj` / `.git` absent; visible `VisibleSource.cs` present. Hidden `.hidden-secret` absent by default; `explorer.toggleHiddenFiles` command and `Ctrl+Shift+H` gesture toggle show/hide. `CreateNodeCommand` creates new file and folder on disk and in tree. **Friction:** native OS folder-picker UX itself **UNVERIFIED-VIS** (path injected via Interaction, not StorageProvider dialog); New File/New Folder **name-prompt modal** (`ShowNamePromptAsync` / `Window.ShowDialog`) **UNVERIFIED** under headless; invalid-folder open **preserves prior tree** and sets `FileTreeViewModel.StatusText` but that failure is **not projected** to `MainWindowViewModel.StatusText` / status bar / shell control tree (**missing failure projection**). |
| `A1-WO-02` | **WORKS_WITH_FRICTION** | Workspace B → `ProjectContextState.NoProject`, status bar `"No project"`. Single-project Workspace A → `SingleProject`, auto-selected `WorkspaceA`, status bar `"WorkspaceA"`. Workspace C (two candidates) → `Ambiguous`, candidates `Alpha` (`.csproj`) + `Beta` (`.sln`), `SelectedProject=null`. **Friction / gaps:** status bar maps `Ambiguous` to **`"Project error"`** (no dedicated case); `SelectProject` has **no** registered shell command and **zero** production callers → selection path **UNWIRED** / user-facing multi-project pick **UNDISCOVERABLE**. Harness did **not** call `SelectProject` to fabricate user-facing selection. |
| `A1-WO-03` | **WORKS_WITH_FRICTION** | Open git Workspace A: `FileTree.RootPath` = `Workspace.WorkspacePath`; `WorkspaceFolderChanged` fires; project context → `SingleProject`/`WorkspaceA`; Source Control `LastRefreshStatus=Success`, branch `master`. Close via registered `workspace.closeFolder`: paths null, project `Unloaded`, status `"Zaide"`, SC `NotARepository`. Open Workspace A2: no stale A path/project; selected `OnlyOne`; SC not retaining A’s Success. **Friction / known limitation:** SC refresh is driven by the host `RootPath` subscription (`SetProjectFromPath` + `RefreshCommand`), **not** a direct `WorkspaceFolderChanged` subscription on Source Control (production open/close still refreshes correctly via that host path). |

Allowed classifications only: `WORKS`, `WORKS_WITH_FRICTION`, `UNDISCOVERABLE`, `UNWIRED`, `BROKEN`, `TEST_ONLY`, `DOCS_ONLY`, `UNVERIFIED`, `BLOCKED`, `UNVERIFIED-VIS`.

**Evidence-class distinction:**

| Class | Meaning in this slice |
|-------|------------------------|
| `product-runtime` | Headless shell / ViewModel / command / Interaction / control-tree observation under production DI |
| `control-tree-only` | Control presence / inspectability without claiming paint success |

Native folder-picker **paint/dialog UX** is **UNVERIFIED-VIS**. Tree paint proportions are not claimed as `WORKS`.

---

## 2. Harness construction (temporary; deleted after evidence capture)

Recreated the H1-proven out-of-tree Avalonia.Headless **12.0.5** runner under `/tmp` (not tracked).

| Item | Value |
|------|--------|
| Runner root | `/tmp/zaide-a3-wo/` (removed after evidence capture) |
| Project | `/tmp/zaide-a3-wo/runner/Zaide.Tests.csproj` |
| Assembly name | **`Zaide.Tests`** (`InternalsVisibleTo` without production edits) |
| TFM | `net10.0` |
| Package | `Avalonia.Headless` **12.0.5** only |
| Project reference | `/home/cenoda/zaide/src/Zaide.csproj` (unchanged) |
| Audit entry | `A3HeadlessEntry.BuildAvaloniaApp()` — **does not** call/patch `Program.BuildAvaloniaApp` |
| Lifetime | `UseHeadless(UseHeadlessDrawing=true)` + `SetupWithClassicDesktopLifetime` + production `Program.ConfigureServices` |
| Folder picker | Deterministic production `PickFolder` Interaction handler (LIFO) returns disposable workspace path; production services **not** replaced; command path still `workspace.openFolder` → `OpenFolderCommand` → `PickFolder` → `FileTreeViewModel.OpenFolderCommand` |
| Observation | ViewModel state, `IProjectContextService`, `Workspace`, StatusBar, Source Control refresh, control-tree scan for status projection, headless key input |
| Not used | xdtools, OS screenshots, desktop pointer automation, manual UI, service replacements |

### 2.1 Isolation protocol

One disposable profile **per independent scenario process**. `HOME` and all `XDG_*` set **before** production composition.

| Variable | Disposable value |
|----------|------------------|
| `HOME` | `$PROFILE_ROOT/home` |
| `XDG_CONFIG_HOME` | `$PROFILE_ROOT/config` (absolute) |
| `XDG_DATA_HOME` | `$PROFILE_ROOT/data` |
| `XDG_STATE_HOME` | `$PROFILE_ROOT/state` |
| `XDG_CACHE_HOME` | `$PROFILE_ROOT/cache` |

Preflight asserted `SettingsPathResolver.GetSettingsDirectory()` under `$XDG_CONFIG_HOME/zaide` and **not** the real-user `~/.config/zaide`.

### 2.2 Package versions

| Package | Version | Notes |
|---------|---------|--------|
| Avalonia (repo pin) | 12.0.5 | Unchanged `Directory.Packages.props` |
| Avalonia.Headless | 12.0.5 | Out-of-tree runner only |
| Avalonia.Headless.XUnit | n/a | Not required |
| ReactiveUI.Avalonia.ME.DI (via app) | 12.0.3 | Production DI bootstrap |

### 2.3 Runner command pattern

```bash
PROFILE_ROOT="$(mktemp -d /tmp/zaide-a3-wo-profile-XXXXXXXX)"
export HOME="${PROFILE_ROOT}/home"
export XDG_CONFIG_HOME="${PROFILE_ROOT}/config"
export XDG_DATA_HOME="${PROFILE_ROOT}/data"
export XDG_STATE_HOME="${PROFILE_ROOT}/state"
export XDG_CACHE_HOME="${PROFILE_ROOT}/cache"
mkdir -p "$HOME" "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_STATE_HOME" "$XDG_CACHE_HOME"

dotnet "/tmp/zaide-a3-wo/out/Release/net10.0/Zaide.Tests.dll" \
  --scenario A1-WO-0N \
  --profile "$PROFILE_ROOT" \
  --evidence "/tmp/zaide-a3-wo/evidence/<label>.json" \
  --repo-head "612ffebe0c614562e4dec7b5eb7ced89ff069a30" \
  --workspace "/tmp/zaide-a3-wo/fixtures/..." \
  [--workspace2 "..."] [--invalid-path "..."]
```

### 2.4 Observed disposable profiles (final runs)

| Scenario / label | Profile root | Exit |
|------------------|--------------|------|
| `A1-WO-01` | `/tmp/zaide-a3-wo-profile-BlNNEOyx` | **0** (14 pass / 1 expected gap fail) |
| `A1-WO-02` no-project | `/tmp/zaide-a3-wo-profile-xbf8rHB1` | **0** (5 pass) |
| `A1-WO-02` single-project | `/tmp/zaide-a3-wo-profile-hGp05AVJ` | **0** (5 pass) |
| `A1-WO-02` ambiguous | `/tmp/zaide-a3-wo-profile-y7Trjq2K` | **0** (7 pass / 1 expected reachability fail) |
| `A1-WO-03` | `/tmp/zaide-a3-wo-profile-qMwMxmIy` | **0** (15 pass) |

---

## 3. Disposable workspace fixtures

All fixtures under `/tmp/zaide-a3-wo/fixtures/` only (never under the repo or real user home as a workspace root).

### 3.1 Workspace A — tree + ignores + single project + git

```text
workspace-a/
  VisibleSource.cs          # visible source file
  .hidden-secret            # hidden file (dot-prefix)
  WorkspaceA.csproj         # single supported project
  src/                      # empty visible directory
  node_modules/pkg/index.js # ignored
  bin/out.dll               # ignored
  obj/tmp                   # ignored
  .git/                     # ignored in tree; real git repo for SC
```

**Git fixture identity:**

| Field | Value |
|-------|--------|
| Repository root | `/tmp/zaide-a3-wo/fixtures/workspace-a` |
| HEAD | `cd57971fc44aec0285de118e00f6fe703dc26682` |
| Branch observed after open | `master` |
| Commit message | `a3 fixture initial` |
| Identity (fixture-local) | `A3 Audit <a3-audit@example.invalid>` |

### 3.2 Workspace B — no supported project

```text
workspace-b/
  README.md
  notes.txt
```

### 3.3 Workspace C — two supported candidates at root

```text
workspace-c/
  Alpha.csproj
  Beta.sln
  shared.txt
```

### 3.4 Workspace A2 — second disposable single-project (stale-state check)

```text
workspace-a2/
  OnlyOne.csproj
  a2.txt
```

### 3.5 Invalid path

`/tmp/zaide-a3-wo/fixtures/DOES_NOT_EXIST_FOLDER_A3` (does not exist).

---

## 4. Scenario `A1-WO-01` — open folder, tree, ignores, hidden, create, invalid open

### 4.1 Interaction / command sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Cold headless launch; disposable profile | product-runtime |
| 2 | Register `PickFolder` Interaction handler → Workspace A | product-runtime (not service replace) |
| 3 | `ICommandRegistry.Execute("workspace.openFolder")` | product-runtime |
| 4 | Observe `RootPath`, `RootNodes`, ignores, hidden default | product-runtime |
| 5 | `explorer.toggleHiddenFiles` command → show hidden | product-runtime |
| 6 | Headless `KeyPressQwerty(H, Ctrl\|Shift)` → hide again | product-runtime |
| 7 | `CreateNodeCommand` new file + new folder (command body) | product-runtime |
| 8 | Name-prompt modal path | **UNVERIFIED** |
| 9 | `PickFolder` → invalid path; re-execute `workspace.openFolder` | product-runtime |
| 10 | Observe tree preservation + status projection | product-runtime + control-tree scan |

### 4.2 Expected vs observed

| Check | Expected | Observed |
|-------|----------|----------|
| Open command | RootPath = Workspace A | `/tmp/zaide-a3-wo/fixtures/workspace-a` |
| RootNodes (initial) | visible sources; no ignored dirs | `VisibleSource.cs`, `WorkspaceA.csproj`, `src` |
| Ignores | no `node_modules`, `bin`, `obj`, `.git` | **absent** |
| Hidden default | `.hidden-secret` hidden; `ShowHiddenFiles=false` | **true** |
| Toggle on (command) | hidden appears; ignores still out | `.hidden-secret` present; ignores still absent |
| Toggle off (gesture) | hidden disappears | `ShowHiddenFiles=false`; hidden absent |
| New file | on disk + in tree | `A3CreatedFile.txt` **both** |
| New folder | on disk + in tree | `A3CreatedFolder` **both** |
| Name prompt modal | if headless-infeasible → UNVERIFIED | **UNVERIFIED** |
| Invalid open | prior tree preserved | RootPath unchanged; node count 5→5; names preserved |
| FileTree StatusText | set on failure | `Error: Directory not found at '…DOES_NOT_EXIST_FOLDER_A3'…` |
| Shell / status bar failure | user-visible if projected | **Not projected** (`MainWindow.StatusText=null`; status bar still `WorkspaceA`; control-tree scan found no matching TextBlock) |

### 4.3 Assertions

| id | result | evidence_class | detail |
|----|--------|----------------|--------|
| `open_folder_command` | **pass** | product-runtime | RootPath set via registry command |
| `root_path_matches_fixture` | **pass** | product-runtime | path match |
| `visible_source_in_tree` | **pass** | product-runtime | `VisibleSource.cs` present |
| `ignore_node_modules_bin_obj_git` | **pass** | product-runtime | ignored dirs absent |
| `hidden_absent_by_default` | **pass** | product-runtime | default hide |
| `toggle_hidden_command_on` | **pass** | product-runtime | command path |
| `ignores_persist_with_hidden_on` | **pass** | product-runtime | ignores not demoted by hidden toggle |
| `toggle_hidden_gesture` | **pass** | product-runtime | `Ctrl+Shift+H` |
| `new_file_create_node_command` | **pass** | product-runtime | on disk |
| `new_file_tree_reflects_create` | **pass** | product-runtime | in tree |
| `new_folder_create_node_command` | **pass** | product-runtime | on disk |
| `new_folder_tree_reflects_create` | **pass** | product-runtime | in tree |
| `invalid_open_preserves_tree` | **pass** | product-runtime | prior tree kept |
| `invalid_open_sets_filetree_statustext` | **pass** | product-runtime | StatusText set on VM |
| `invalid_open_failure_user_visible_in_shell` | **fail** (gap) | product-runtime | **not** user-visible |

### 4.4 Classification rationale

Core open/tree/ignore/hidden/create paths are product-runtime proven through registered commands and Interaction. Classification is **WORKS_WITH_FRICTION** because:

1. Open-folder **failure messages** land only on unbound `FileTreeViewModel.StatusText` (missing failure projection).
2. New File/New Folder **modal name prompt** is **UNVERIFIED** under headless (command body proven; view dialog not).
3. Native StorageProvider folder-picker **dialog UX** is **UNVERIFIED-VIS** (Interaction injects path).

### 4.5 Machine-readable excerpt (WO-01)

```json
{
  "schemaVersion": "a3-evidence-1",
  "scenarioId": "A1-WO-01",
  "exitCode": 0,
  "host": {
    "os": "linux",
    "rid": "arch-x64",
    "repoHead": "612ffebe0c614562e4dec7b5eb7ced89ff069a30",
    "harness": "a3-workspace-project-opening-headless",
    "harnessVersion": "a3-wo-0.1"
  },
  "isolation": {
    "profileRoot": "/tmp/zaide-a3-wo-profile-BlNNEOyx",
    "resolvedSettingsDir": "/tmp/zaide-a3-wo-profile-BlNNEOyx/config/zaide",
    "preflightOk": true
  },
  "observedViewModelState": {
    "RootPath.after_open": "/tmp/zaide-a3-wo/fixtures/workspace-a",
    "RootNodes.after_open": ["VisibleSource.cs", "WorkspaceA.csproj", "src"],
    "ignored_dirs_present": [],
    "ShowHiddenFiles.initial": false,
    "hidden_present.after_toggle_on": true,
    "hidden_present.after_gesture": false,
    "new_file.on_disk": true,
    "new_file.in_tree": true,
    "new_file.modal_path": "UNVERIFIED",
    "new_folder.on_disk": true,
    "new_folder.in_tree": true,
    "invalid.after_root": "/tmp/zaide-a3-wo/fixtures/workspace-a",
    "invalid.FileTree.StatusText": "Error: Directory not found at '/tmp/zaide-a3-wo/fixtures/DOES_NOT_EXIST_FOLDER_A3'. Details: Directory not found: /tmp/zaide-a3-wo/fixtures/DOES_NOT_EXIST_FOLDER_A3",
    "invalid.MainWindow.StatusText": null,
    "invalid.status_text_user_visible": false
  },
  "limitations": [
    "Native New File/New Folder name prompt (FileTreeView.ShowNamePromptAsync → Window.ShowDialog) not exercised under headless; classified UNVERIFIED rather than fabricated success."
  ]
}
```

---

## 5. Scenario `A1-WO-02` — project discovery states

Three **independent** processes (separate disposable profiles). No `SelectProject` invocation to claim user selection.

### 5.1 No-project (Workspace B)

| Field | Observed |
|-------|----------|
| Profile | `/tmp/zaide-a3-wo-profile-xbf8rHB1` |
| `ProjectContext.State` | **NoProject** |
| Candidates | `[]` |
| SelectedProject | `null` |
| Status bar `ProjectText` | **`No project`** |

Assertions: open, left Loading, `state_no_project`, `status_no_project`, `no_selected_project` — all **pass**.

### 5.2 Single-project (Workspace A)

| Field | Observed |
|-------|----------|
| Profile | `/tmp/zaide-a3-wo-profile-hGp05AVJ` |
| `ProjectContext.State` | **SingleProject** |
| Candidates | `WorkspaceA` → `…/WorkspaceA.csproj` (`CSharpProject`) |
| SelectedProject | **auto** `WorkspaceA` |
| Status bar `ProjectText` | **`WorkspaceA`** |

Assertions: open, left Loading, `state_single_project`, `auto_selected`, `status_shows_project_name` — all **pass**.

### 5.3 Ambiguous (Workspace C)

| Field | Observed |
|-------|----------|
| Profile | `/tmp/zaide-a3-wo-profile-y7Trjq2K` |
| `ProjectContext.State` | **Ambiguous** |
| Candidates (2) | `Alpha` (`.csproj` / `CSharpProject`), `Beta` (`.sln` / `Solution`) |
| SelectedProject | **`null`** |
| Status bar `ProjectText` | **`Project error`** (mislabeled — no `Ambiguous` case in `MapProjectText`) |
| `SelectProject` shell command | **none** (`has_select_project_command=false`) |
| Registry project-related scan | no `*select*project*` command |
| Production callers of `SelectProject` | **zero** outside service implementation (source + runtime) |
| Reachability label | **UNWIRED** / user pick **UNDISCOVERABLE** |

Assertions: open, state Ambiguous, ≥2 candidates, selected null, status `"Project error"` — **pass**.  
`select_project_user_reachable` — **fail** (expected gap; recorded, not fabricated).

### 5.4 Classification rationale

No-project and single-project automatic selection are product-runtime proven with truthful status text. Multi-project discovery **publishes** `Ambiguous` correctly, but:

1. Status bar **mislabels** Ambiguous as `"Project error"`.
2. User cannot resolve Ambiguous: **UNWIRED** `SelectProject`, no palette/command path → **UNDISCOVERABLE** picker.

Overall row: **WORKS_WITH_FRICTION**.

### 5.5 Machine-readable excerpts (WO-02)

**No-project:**

```json
{
  "scenarioId": "A1-WO-02",
  "isolation": { "profileRoot": "/tmp/zaide-a3-wo-profile-xbf8rHB1" },
  "observedViewModelState": {
    "fixture_kind": "no-project",
    "ProjectContext.State": "NoProject",
    "StatusBar.ProjectText": "No project",
    "ProjectContext.SelectedProject": null
  },
  "exitCode": 0
}
```

**Single-project:**

```json
{
  "scenarioId": "A1-WO-02",
  "isolation": { "profileRoot": "/tmp/zaide-a3-wo-profile-hGp05AVJ" },
  "observedViewModelState": {
    "fixture_kind": "single-project",
    "ProjectContext.State": "SingleProject",
    "ProjectContext.SelectedProject": {
      "displayName": "WorkspaceA",
      "filePath": "/tmp/zaide-a3-wo/fixtures/workspace-a/WorkspaceA.csproj",
      "kind": "CSharpProject"
    },
    "StatusBar.ProjectText": "WorkspaceA"
  },
  "exitCode": 0
}
```

**Ambiguous:**

```json
{
  "scenarioId": "A1-WO-02",
  "isolation": { "profileRoot": "/tmp/zaide-a3-wo-profile-y7Trjq2K" },
  "observedViewModelState": {
    "fixture_kind": "ambiguous",
    "ProjectContext.State": "Ambiguous",
    "ProjectContext.Candidates": [
      {
        "displayName": "Alpha",
        "filePath": "/tmp/zaide-a3-wo/fixtures/workspace-c/Alpha.csproj",
        "kind": "CSharpProject"
      },
      {
        "displayName": "Beta",
        "filePath": "/tmp/zaide-a3-wo/fixtures/workspace-c/Beta.sln",
        "kind": "Solution"
      }
    ],
    "ProjectContext.SelectedProject": null,
    "StatusBar.ProjectText": "Project error",
    "has_select_project_command": false,
    "select_project_reachability": "UNWIRED — no production caller / no shell command for SelectProject"
  },
  "exitCode": 0
}
```

---

## 6. Scenario `A1-WO-03` — open/close propagation

### 6.1 Sequence

| Step | Action |
|------|--------|
| 1 | Subscribe to `Workspace.WorkspaceFolderChanged` |
| 2 | `PickFolder` → Workspace A; `workspace.openFolder` |
| 3 | Capture FileTree / Workspace / ProjectContext / SC / status bar |
| 4 | `workspace.closeFolder` (registered; CanExecute true while open) |
| 5 | Capture cleared/unloaded/not-a-repo state |
| 6 | `PickFolder` → Workspace A2; open; assert no stale A state |

### 6.2 Open Workspace A (git + single project)

| Surface | Observed |
|---------|----------|
| `FileTreeViewModel.RootPath` | `/tmp/zaide-a3-wo/fixtures/workspace-a` |
| `Workspace.WorkspacePath` | same |
| `Workspace.ProjectName` | `workspace-a` |
| `WorkspaceFolderChanged` events | **1** |
| `ProjectContext.State` | **SingleProject** |
| Selected | **WorkspaceA** |
| Status bar | **WorkspaceA** |
| SC `LastRefreshStatus` | **Success** |
| SC branch | **master** |
| SC status message | empty (success) |

### 6.3 Close via `workspace.closeFolder`

| Surface | Observed |
|---------|----------|
| `RootPath` | **null** |
| `WorkspacePath` | **null** |
| `WorkspaceFolderChanged` events | **1** (null path) |
| `ProjectContext.State` | **Unloaded** |
| Status bar | **`Zaide`** |
| SC `LastRefreshStatus` | **NotARepository** |
| SC status message | `No repository — open a folder inside a git repository` |
| SC branch | `no repo` |
| Staged/unstaged counts | **0** / **0** |

### 6.4 Open Workspace A2 (no stale A)

| Surface | Observed |
|---------|----------|
| `RootPath` / `WorkspacePath` | Workspace A2 (not A) |
| Project selected | **OnlyOne** (not WorkspaceA) |
| Status bar | **OnlyOne** |
| SC | **NotARepository** (A2 is not a git repo; does **not** retain A’s Success) |

### 6.5 Known limitation (explicit)

Source Control refresh is triggered by `MainWindowActivationHost`’s subscription to
`FileTreeViewModel.RootPath` (which calls `Workspace.SetProjectFromPath` then
`SourceControlViewModel.RefreshCommand`). Source Control does **not** subscribe
directly to `WorkspaceFolderChanged`. Production open/close still refreshes
correctly via that host path; the coupling remains a maintenance hazard if a
second RootPath writer ever appeared.

### 6.6 Assertions

All **15** assertions **pass** (open A ×5, close ×6, open A2 ×4).

### 6.7 Classification rationale

Open/close propagation to Workspace, ProjectContext, status bar, and Source Control
is product-runtime proven with a real git fixture and a second workspace for
stale-state absence. Classification is **WORKS_WITH_FRICTION** solely for the
documented SC host-path coupling limitation (behavior for production open/close is
correct; architecture is fragile by design).

### 6.8 Machine-readable excerpt (WO-03)

```json
{
  "scenarioId": "A1-WO-03",
  "exitCode": 0,
  "isolation": {
    "profileRoot": "/tmp/zaide-a3-wo-profile-qMwMxmIy",
    "preflightOk": true
  },
  "observedViewModelState": {
    "open_A": {
      "fileTreeRootPath": "/tmp/zaide-a3-wo/fixtures/workspace-a",
      "workspacePath": "/tmp/zaide-a3-wo/fixtures/workspace-a",
      "projectContextState": "SingleProject",
      "selectedProject": "WorkspaceA",
      "statusBarProjectText": "WorkspaceA",
      "scLastRefreshStatus": "Success",
      "scCurrentBranch": "master"
    },
    "close": {
      "projectContextState": "Unloaded",
      "statusBarProjectText": "Zaide",
      "scLastRefreshStatus": "NotARepository",
      "scStatusMessage": "No repository — open a folder inside a git repository",
      "scCurrentBranch": "no repo"
    },
    "open_A2": {
      "fileTreeRootPath": "/tmp/zaide-a3-wo/fixtures/workspace-a2",
      "workspacePath": "/tmp/zaide-a3-wo/fixtures/workspace-a2",
      "projectContextState": "SingleProject",
      "selectedProject": "OnlyOne",
      "statusBarProjectText": "OnlyOne",
      "scLastRefreshStatus": "NotARepository"
    }
  },
  "limitations": [
    "Source Control refresh is triggered by MainWindowActivationHost RootPath subscription (then Workspace.SetProjectFromPath + RefreshCommand), not by a direct WorkspaceFolderChanged subscription on SourceControlViewModel."
  ]
}
```

---

## 7. Explicit limitations (slice-wide)

| Limitation | Classification impact |
|------------|----------------------|
| Native OS folder picker dialog UX not shown | **UNVERIFIED-VIS** (path via Interaction) |
| New File/New Folder name-prompt modal not exercised | **UNVERIFIED** sub-path |
| `FileTreeViewModel.StatusText` open/create failures not bound to shell | Missing failure projection (WO-01 friction) |
| `MapProjectText` omits `Ambiguous` → `"Project error"` | WO-02 friction |
| `SelectProject` unreachable | **UNWIRED** / multi-pick **UNDISCOVERABLE** |
| SC refresh host-coupled to `RootPath`, not SC’s own `WorkspaceFolderChanged` subscription | WO-03 known limitation |
| Headless drawing — tree/chrome paint not asserted | no visual `WORKS` claim |
| Real desktop / xdtools / pointer not used | charter |

---

## 8. Cleanup and safety

Performed after evidence capture:

1. Removed `/tmp/zaide-a3-wo/` (runner, out, obj, fixtures, evidence JSON copies).
2. Removed disposable profile dirs `/tmp/zaide-a3-wo-profile-*`.
3. Verified **no** production code, tracked tests, or package pins changed.
4. Verified workspace fixtures were only under `/tmp/zaide-a3-wo/fixtures/` — **not** the repository tree and **not** a real user project path.
5. Settings/secrets only under disposable `$XDG_CONFIG_HOME/zaide`.

---

## 9. Closeout checklist

| Check | Result |
|-------|--------|
| Evidence file created | **Yes** — this document |
| Only new evidence staged | **Yes** (closeout commit) |
| `git diff --check` | clean |
| Commit message | `docs(audit): execute A3 workspace project opening smoke` |
| Push `master` → `origin` | performed at closeout |
| `HEAD == origin/master` + clean tree | re-verified at closeout |
| A3 overall complete? | **No** — explicitly incomplete |
| A4 / V4 begun? | **No** |

---

## 10. Next bounded A3 slice

| Field | Value |
|-------|--------|
| Recommended next slice | **`A3_FILE_NAVIGATION_AND_EDITING`** |
| Goal rows | `A1-FN-*` (file open/edit/save, search/replace/fold/tabs as scoped by charter) |
| Rationale | Next journey after workspace in [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) §6.3; depends on open-folder path proven here |
| Still not authorized by this note | A4, stabilization, V4, agent/Git/debug/build journeys |

---

## 11. Summary for re-audit

| id | Classification |
|----|----------------|
| `A1-WO-01` | **WORKS_WITH_FRICTION** |
| `A1-WO-02` | **WORKS_WITH_FRICTION** |
| `A1-WO-03` | **WORKS_WITH_FRICTION** |

**Evidence path:**
`docs/audits/v1-v3-product-reality/evidence/A3_WORKSPACE_AND_PROJECT_OPENING.md`

*End of `A3_WORKSPACE_AND_PROJECT_OPENING` evidence for `A1-WO-01`…`A1-WO-03`. A3 overall remains incomplete.*
