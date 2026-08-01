# A3 Clean-Profile Smoke — Search and Command Discovery (`A1-SC-01` … `A1-SC-03`)

**Audit name:** `v1-v3-product-reality`
**Phase scope of this note:** **A3 search / command discovery execution slice only** — rows
`A1-SC-01` through `A1-SC-03`.
**Evidence date:** 2026-08-01
**Repo head at run:** `e49bb2361e3a061ef3f858cee2843200eb062ffd`

---

## 0. Charter and status

| Item | Status |
|------|--------|
| **Document class** | **A3 product-runtime clean-profile smoke evidence** (SC-01…SC-03 only) |
| **A3 slice** | Search and Command Discovery (`A1-SC-01`…`A1-SC-03`) |
| **A3 as a whole** | **Incomplete** — build/run/test, debugging, Git, Townhall, agents, permissions, trace, memory, restart-recovery, residual journeys **not executed** in this note |
| **A4 / stabilization / V4** | **Not begun** |
| Real desktop UI / xdtools / screenshots / desktop pointer automation / manual desktop | **Not used** |
| Production code modified | **No** |
| Tracked tests modified | **No** |
| `Avalonia.Headless` added to tracked project | **No** |
| `Directory.Packages.props` changed | **No** |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` rewritten to “A3 complete” | **No** |
| Prior A2 / A3 evidence rewritten | **No** |
| Real user `~/.config/zaide` read or written by this run | **No** (disposable `HOME` + `XDG_*` only) |
| Registry unit tests used as A3 proof | **No** (explicitly forbidden) |

**Authority inputs:**

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) (H0)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md) (H1)
- [A2_SEARCH_AND_COMMAND_DISCOVERY.md](./A2_SEARCH_AND_COMMAND_DISCOVERY.md)
- Prior FN product-runtime effects (find/replace/fold/tab) used only as **side effects** when invoked through the palette — **not re-scored** here

**Out of scope for this slice (explicit):**

- Re-scoring FN search/replace/folding behavioral verdicts
- Build/Run/Test, debugging, Git, Townhall, agents, permissions, trace, memory, restart
- A4, stabilization, V4 planning
- Production code, tracked tests, package pins, audit policy edits
- Unit tests as A3 proof
- Visual overlay paint quality (**UNVERIFIED-VIS** where noted)

---

## 1. Three-row classification table (authoritative for this slice)

| id | A3 classification | Summary |
|----|-------------------|---------|
| `A1-SC-01` | **WORKS_WITH_FRICTION** | Cold headless launch captures production `ICommandRegistry` (**38** commands) with stable IDs, default gestures, and **28** materialized `MainWindow.KeyBindings`. Settings panel opens with Editor / Terminal / LLM only — **no keybindings editor** (product gap). Hand-seeded disposable `settings.json` override rebinds `editor.find`→`Ctrl+K`, unbinds `editor.replace` (`""`), and conflicts `file.save` on the same gesture; **Ordinal-min winner `editor.find`**; conflict emitted as **ILogger Warning only** (user UI absent). **Friction:** no user-facing keybindings editor; conflict surface is log-only; hand-edit is escape-hatch evidence, not product editor equivalence. |
| `A1-SC-02` | **WORKS_WITH_FRICTION** | Palette opens via registered `Ctrl+Shift+P` / `palette.open`; case-insensitive filter `"FiNd"`; deterministic Category/DisplayName/Id order; Enter executes `editor.find` (search bar visible); empty query result shows **“No matching commands”**. With no tabs: editor/tab/fold commands **visible but unavailable** (gray `#555566`); registry/Enter refuse unavailable execution. **Pointer press on non-selected row executes keyboard selection** (`debug.startOrContinue` vs clicked `editor.find`) — source-predicted product gap, **not repaired**. **Friction:** pointer-row selection mismatch; headless focus manager reported null before/after (focus restore path wired; visual focus **UNVERIFIED-VIS**). |
| `A1-SC-03` | **WORKS** | Unbound Phase 9 IDs `editor.replaceAll`, `editor.foldAll`, `tab.closeOthers` present with empty default gestures; palette-reachable and available when preconditions hold; dispatch via palette/execute path invokes production handlers: fold status **“Folded all regions”**, close-others **3→1** tabs, replace-all **“Replaced 5 occurrences”** with text change. FN behavioral rows are **not** re-verdicted. |

Allowed classifications only: `WORKS`, `WORKS_WITH_FRICTION`, `UNDISCOVERABLE`, `UNWIRED`, `BROKEN`, `TEST_ONLY`, `DOCS_ONLY`, `UNVERIFIED`, `BLOCKED`, `UNVERIFIED-VIS`.

**Evidence-class distinction:**

| Class | Meaning in this slice |
|-------|------------------------|
| `product-runtime` / **registry-runtime** | Production DI + `MainWindow` + `ICommandRegistry` / resolved keybindings under headless |
| **user-reachable palette** | Palette open/filter/select/execute/dismiss through real overlay + gestures |
| **hand-edited settings escape-hatch** | Seeded disposable `settings.json` keybindings map (not a Settings UI editor) |
| **visual-only overlay** | Paint/pixel quality of palette rows/backdrop — not claimed under headless drawing |

---

## 2. Harness construction (temporary; deleted after evidence capture)

Recreated the H1-proven out-of-tree Avalonia.Headless **12.0.5** runner under `/tmp` (not tracked).

| Item | Value |
|------|--------|
| Runner root | `/tmp/zaide-a3-sc/` (removed after evidence capture) |
| Project | `/tmp/zaide-a3-sc/runner/Zaide.Tests.csproj` |
| Assembly name | **`Zaide.Tests`** (`InternalsVisibleTo` without production edits) |
| TFM | `net10.0` |
| Package | `Avalonia.Headless` **12.0.5** only |
| Project reference | `/home/cenoda/zaide/src/Zaide.csproj` (unchanged) |
| Audit entry | `A3HeadlessEntry.BuildAvaloniaApp()` — does **not** call/patch `Program.BuildAvaloniaApp` |
| Lifetime | `UseHeadless(UseHeadlessDrawing=true)` + `SetupWithClassicDesktopLifetime` + production `Program.ConfigureServices` |
| Isolation | One disposable profile **per independent scenario process**; `HOME` + all absolute `XDG_*` set **before** production composition |
| Folder open | `OpenFolderCommand` + LIFO `PickFolder` Interaction → disposable workspace |
| File open | Production `EditorTabs.OpenFileCommand` |
| Keyboard | Headless `KeyPress` / `KeyRelease` (`Ctrl+Shift+P`, Enter) |
| Pointer | Synthetic `PointerPressed` on palette row borders only (headless API / raise path) — **not** desktop pointer automation |
| Not used | xdtools, OS screenshots, desktop pointer automation, manual UI, service replacements, unit tests as proof |

### 2.1 Isolation protocol

| Variable | Disposable value |
|----------|------------------|
| `HOME` | `$PROFILE_ROOT/home` |
| `XDG_CONFIG_HOME` | `$PROFILE_ROOT/config` (absolute) |
| `XDG_DATA_HOME` | `$PROFILE_ROOT/data` |
| `XDG_STATE_HOME` | `$PROFILE_ROOT/state` |
| `XDG_CACHE_HOME` | `$PROFILE_ROOT/cache` |

Preflight asserted `SettingsPathResolver.GetSettingsDirectory()` under `$XDG_CONFIG_HOME/zaide` and **not** the real-user `~/.config/zaide`.

Workspace fixtures (when used) were copied under `$PROFILE_ROOT/workspace` — never the repository tree as workspace root.

### 2.2 Package versions

| Package | Version | Notes |
|---------|---------|--------|
| Avalonia (repo pin) | 12.0.5 | Unchanged `Directory.Packages.props` |
| Avalonia.Headless | 12.0.5 | Out-of-tree runner only |
| Avalonia.Headless.XUnit | n/a | Not required |
| ReactiveUI.Avalonia.ME.DI (via app) | 12.0.3 | Production DI bootstrap |

### 2.3 Runner command pattern

```bash
PROFILE_ROOT="$(mktemp -d /tmp/zaide-a3-sc-profile-XXXXXXXX)"
export HOME="${PROFILE_ROOT}/home"
export XDG_CONFIG_HOME="${PROFILE_ROOT}/config"
export XDG_DATA_HOME="${PROFILE_ROOT}/data"
export XDG_STATE_HOME="${PROFILE_ROOT}/state"
export XDG_CACHE_HOME="${PROFILE_ROOT}/cache"
mkdir -p "$HOME" "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_STATE_HOME" "$XDG_CACHE_HOME"
# optional: cp -a fixtures/workspace "$PROFILE_ROOT/workspace"
# optional: seed $XDG_CONFIG_HOME/zaide/settings.json for override scenario

dotnet "/tmp/zaide-a3-sc/out/Release/net10.0/Zaide.Tests.dll" \
  --scenario <id> \
  --profile "$PROFILE_ROOT" \
  --evidence "/tmp/zaide-a3-sc/evidence/<id>.json" \
  --repo-head "e49bb2361e3a061ef3f858cee2843200eb062ffd" \
  [--workspace "$PROFILE_ROOT/workspace"]
```

### 2.4 Disposable profiles (final capture)

| Scenario | Profile root | Exit | Assertions |
|----------|--------------|------|------------|
| `A1-SC-01-registry` | `/tmp/zaide-a3-sc-profile-wzSjtbks` | **0** | 30/30 pass |
| `A1-SC-01-overrides` | `/tmp/zaide-a3-sc-profile-NO7j7cyg` | **0** | 9/9 pass |
| `A1-SC-02-palette` | `/tmp/zaide-a3-sc-profile-ZPiVOgey` | **0** | 9/9 pass |
| `A1-SC-02-no-tabs` | `/tmp/zaide-a3-sc-profile-4zBw5YNX` | **0** | 9/9 pass |
| `A1-SC-02-pointer` | `/tmp/zaide-a3-sc-profile-7tEaVMYm` | **0** | 3/3 pass |
| `A1-SC-03-phase9` | `/tmp/zaide-a3-sc-profile-FoyVOdcF` | **0** | 10/10 pass |

**Total:** 70 product-runtime assertions, all pass on final capture.

---

## 3. Disposable fixtures

Canonical fixture template under `/tmp/zaide-a3-sc/fixtures/workspace/` (copied per profile; never under the repo as workspace root):

```text
workspace/
  Main.cs          # 5× MARKER_SEARCH_A + nested braces
  Second.cs        # second tab companion
  NestedFolds.cs   # multi-brace C# for folding
```

Override seed (disposable profile only; **not** a product keybindings editor):

```json
{
  "schemaVersion": 3,
  "editor": { "...defaults..." },
  "llm": { "...defaults..." },
  "keybindings": {
    "editor.find": "Ctrl+K",
    "editor.replace": "",
    "file.save": "Ctrl+K"
  },
  "debug": { "breakpointsByWorkspaceRoot": {} }
}
```

---

## 4. `A1-SC-01` — command registry, materialization, overrides, conflicts

### 4.1 Cold registry + defaults + Settings surface

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Cold headless launch; clean disposable profile | registry-runtime |
| 2 | Capture `ICommandRegistry.GetAll()` (38 commands) | registry-runtime |
| 3 | Assert Phase 9 IDs + defaults / unbound | registry-runtime |
| 4 | `ResolveKeyBindings` + inspect `MainWindow.KeyBindings` (28) | registry-runtime |
| 5 | Open Settings via production `ShowSettings` | product-runtime |
| 6 | Scan panel text for keybindings editor | product-runtime (absence = product gap) |

**Observed defaults (subset):**

| Command ID | Display | Category | Default gesture(s) |
|------------|---------|----------|--------------------|
| `palette.open` | Open Command Palette | Palette | `Ctrl+Shift+P` |
| `editor.find` | Find | Editor | `Ctrl+F` |
| `editor.replace` | Replace | Editor | `Ctrl+H` |
| `editor.findNext` | Find Next | Editor | `F3` |
| `editor.findPrevious` | Find Previous | Editor | `Shift+F3` |
| `editor.replaceAll` | Replace All | Editor | *(unbound)* |
| `editor.foldAll` | Fold All | Editor | *(unbound)* |
| `tab.closeOthers` | Close Other Tabs | Tab | *(unbound)* |
| `file.save` | Save | File | `Ctrl+S` |

**Materialization:** `Ctrl+Shift+P` and `Ctrl+F` present on `MainWindow.KeyBindings`.

**Settings surface:** panel opens; sections **Editor**, **Terminal**, **LLM** present; **no** keybindings / keyboard-shortcut editor text. Classified as **product gap**, not harness failure. Hand-editing `settings.json` is **not** equivalent to a user-facing keybindings editor.

### 4.2 Full production command registry inventory (38)

| Command ID | Display name | Category | Default gesture(s) |
|------------|--------------|----------|--------------------|
| `debug.pause` | Pause | Debug | *(unbound)* |
| `debug.startOrContinue` | Start Debugging / Continue | Debug | `F5` |
| `debug.stepInto` | Step Into | Debug | `F11` |
| `debug.stepOut` | Step Out | Debug | `Shift+F11` |
| `debug.stepOver` | Step Over | Debug | `F10` |
| `debug.stop` | Stop Debugging | Debug | `Shift+F5` |
| `debug.toggleBreakpoint` | Toggle Breakpoint | Debug | `F9` |
| `editor.documentSymbol` | Go to Symbol in Editor | Editor | `Ctrl+Shift+O` |
| `editor.find` | Find | Editor | `Ctrl+F` |
| `editor.findNext` | Find Next | Editor | `F3` |
| `editor.findPrevious` | Find Previous | Editor | `Shift+F3` |
| `editor.foldAll` | Fold All | Editor | *(unbound)* |
| `editor.foldToggle` | Toggle Current Fold | Editor | *(unbound)* |
| `editor.formatDocument` | Format Document | Editor | `Ctrl+Shift+I` |
| `editor.goToDefinition` | Go to Definition | Editor | `F12` |
| `editor.replace` | Replace | Editor | `Ctrl+H` |
| `editor.replaceAll` | Replace All | Editor | *(unbound)* |
| `editor.replaceNext` | Replace Next | Editor | *(unbound)* |
| `editor.triggerSuggest` | Trigger Suggest | Editor | `Ctrl+Space` |
| `editor.unfoldAll` | Unfold All | Editor | *(unbound)* |
| `explorer.toggleHiddenFiles` | Toggle Hidden Files | Explorer | `Ctrl+Shift+H` |
| `file.save` | Save | File | `Ctrl+S` |
| `palette.open` | Open Command Palette | Palette | `Ctrl+Shift+P` |
| `project.build` | Build | Project | `Ctrl+Shift+B` |
| `project.cancel` | Cancel Build/Run/Test | Project | `Ctrl+F2` |
| `project.run` | Run | Project | `Ctrl+F5` |
| `project.test` | Run Tests | Project | *(unbound)* |
| `sourcecontrol.commit` | Commit | Source Control | *(unbound)* |
| `sourcecontrol.refresh` | Refresh | Source Control | *(unbound)* |
| `tab.close` | Close Tab | Tab | `Ctrl+W`, `Ctrl+F4` |
| `tab.closeAll` | Close All Tabs | Tab | *(unbound)* |
| `tab.closeOthers` | Close Other Tabs | Tab | *(unbound)* |
| `tab.next` | Next Tab | Tab | `Ctrl+Tab` |
| `tab.previous` | Previous Tab | Tab | `Ctrl+Shift+Tab` |
| `view.toggleBottomPanel` | Toggle Bottom Panel | View | `Ctrl+Oem3`, `Ctrl+J` |
| `workbench.symbol` | Go to Symbol in Workspace | Editor | `Ctrl+T` |
| `workspace.closeFolder` | Close Folder | Workspace | *(unbound)* |
| `workspace.openFolder` | Open Folder | Workspace | `Ctrl+O` |

### 4.3 Override / unbind / conflict (hand-edited escape hatch)

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Seed disposable `settings.json` keybindings map | hand-edited escape-hatch |
| 2 | Cold restart under same profile | product-runtime |
| 3 | `LoadResult=Loaded`; map present | product-runtime |
| 4 | Resolve + materialize | registry-runtime |
| 5 | Open Settings; scan for conflict UI | product-runtime |

| Check | Observed |
|-------|----------|
| `editor.find` rebind | **`Ctrl+K`** (default `Ctrl+F` removed from window) |
| `editor.replace` unbind (`""`) | **No** resolved binding |
| Conflict `Ctrl+K` (`editor.find` vs `file.save`) | **Winner: `editor.find`** (Ordinal-min); `file.save` unbound from `Ctrl+K` |
| Conflict log | `Gesture conflict for 'Ctrl+K': 'editor.find' wins over 'file.save'` (**twice** — resolve + materialize path) |
| Conflict user UI | **None** (status empty; settings panel has no conflict banner) |
| Classification of hand-edit | **Escape hatch**, not Settings keybindings editor |

### 4.4 Classification rationale — `A1-SC-01` = **WORKS_WITH_FRICTION**

Registry, default gestures, window materialization, override/unbind/conflict resolution are product-runtime proven. Friction is the **missing user-facing keybindings editor** and **log-only conflict visibility** — product gaps, not test failures.

### 4.5 Machine-readable evidence (excerpts)

```json
{
  "schema_version": "a3-evidence-1",
  "phase": "A3-SC",
  "scenario_id": "A1-SC-01-registry",
  "a1_row_ids": ["A1-SC-01"],
  "exit_code": 0,
  "host": {
    "os": "linux",
    "rid": "arch-x64",
    "repo_head": "e49bb2361e3a061ef3f858cee2843200eb062ffd",
    "harness": "a3-search-command-discovery-headless",
    "harness_version": "a3-sc-0.1"
  },
  "package_versions": {
    "Avalonia.Headless": "12.0.5",
    "Avalonia (repo pin)": "12.0.5",
    "Avalonia.Headless.XUnit": "not_referenced_not_required"
  },
  "isolation": {
    "profile_root": "/tmp/zaide-a3-sc-profile-wzSjtbks",
    "xdg_config_home": "/tmp/zaide-a3-sc-profile-wzSjtbks/config",
    "resolved_settings_dir": "/tmp/zaide-a3-sc-profile-wzSjtbks/config/zaide",
    "preflight_ok": true
  },
  "observed_view_model_state": {
    "registry.count": 38,
    "mainwindow.keybindings.count": 28,
    "settings.LoadResult": "Missing",
    "settings.Keybindings.Count": 0
  },
  "classification_hint": "WORKS_WITH_FRICTION"
}
```

```json
{
  "scenario_id": "A1-SC-01-overrides",
  "exit_code": 0,
  "isolation": {
    "profile_root": "/tmp/zaide-a3-sc-profile-NO7j7cyg",
    "resolved_settings_dir": "/tmp/zaide-a3-sc-profile-NO7j7cyg/config/zaide"
  },
  "observed_view_model_state": {
    "settings.LoadResult": "Loaded",
    "settings.Keybindings": {
      "editor.find": "Ctrl+K",
      "editor.replace": "",
      "file.save": "Ctrl+K"
    },
    "conflict.user_visible_ui": false,
    "conflict.log_observed": true,
    "conflict.log_excerpt": "Gesture conflict for 'Ctrl+K': 'editor.find' wins over 'file.save'"
  },
  "assertions": [
    { "id": "override.editor.find.rebind", "result": "pass", "detail": "Ctrl+K" },
    { "id": "unbind.editor.replace", "result": "pass", "detail": "unbound" },
    { "id": "conflict.winner", "result": "pass", "detail": "editor.find owns Ctrl+K; file.save lost conflict" },
    { "id": "conflict.not_user_visible", "result": "pass", "detail": "no user-visible conflict UI (log-only product behavior)" }
  ]
}
```

---

## 5. `A1-SC-02` — Command Palette discovery, filter, availability, execute, pointer gap

### 5.1 With editor tab — open / filter / execute / no-match

| Step | Action | Result |
|------|--------|--------|
| 1 | Open disposable workspace + `Main.cs` | tab open |
| 2 | Headless `Ctrl+Shift+P` | palette `IsOpen=true`, overlay visible |
| 3 | Query `FiNd` (case-insensitive) | `[editor.find, editor.findNext, editor.findPrevious]` |
| 4 | Ordering | deterministic Category / DisplayName / Id |
| 5 | Enter on `editor.find` | palette dismisses; `EditorSearchViewModel.IsVisible=true` |
| 6 | Query `zzz_no_such_command_xyz` | empty list + **“No matching commands”** |

Focus manager under headless reported focused element `(null)` before and after palette; production `RestoreFocusAfterPalette` is wired when `ActiveTab` exists. Visual focus quality remains **UNVERIFIED-VIS**.

### 5.2 No editor tabs — unavailable but visible

| Check | Observed |
|-------|----------|
| Tabs | 0 |
| Palette entry count (empty query) | 38 |
| `editor.find` | visible, **`IsAvailable=false`** |
| `tab.close` | visible, unavailable |
| `editor.foldAll` | visible, unavailable |
| Gray styling | `#555566` observed on unavailable rows |
| `registry.Execute("editor.find")` | **false** |
| Keyboard selection | skips unavailable (selected available `debug.startOrContinue`) |

### 5.3 Pointer press on non-selected row (known gap)

| Field | Value |
|-------|--------|
| Available rows | ≥2 (13 available in empty-query list) |
| Keyboard selection | index 1 / `debug.startOrContinue` |
| Clicked row | index 7 / `editor.find` |
| Outcome | Palette **dismissed**; selection remained keyboard row; search bar **not** opened |
| Verdict | **Product-runtime pointer gap** — `OnEntryPointerPressed` does not reselect clicked row before `ExecuteSelected()`; **not a harness defect**; **not repaired** |

### 5.4 Classification rationale — `A1-SC-02` = **WORKS_WITH_FRICTION**

Palette open/filter/order/execute/empty-state/unavailable-gray paths work under product-runtime. Friction is the **pointer-row selection mismatch** (and headless focus paint not claimed).

### 5.5 Machine-readable evidence (excerpts)

```json
{
  "scenario_id": "A1-SC-02-palette",
  "exit_code": 0,
  "isolation": { "profile_root": "/tmp/zaide-a3-sc-profile-ZPiVOgey" },
  "observed_view_model_state": {
    "palette.query": "FiNd",
    "palette.filtered": [
      { "Id": "editor.find", "DisplayName": "Find", "Category": "Editor", "IsAvailable": true },
      { "Id": "editor.findNext", "DisplayName": "Find Next", "Category": "Editor", "IsAvailable": true },
      { "Id": "editor.findPrevious", "DisplayName": "Find Previous", "Category": "Editor", "IsAvailable": true }
    ],
    "palette.selected_before_enter": "editor.find"
  }
}
```

```json
{
  "scenario_id": "A1-SC-02-pointer",
  "exit_code": 0,
  "observed_view_model_state": {
    "pointer.keyboard_selected_id": "debug.startOrContinue",
    "pointer.clicked_id": "editor.find",
    "pointer.executed_uses_keyboard_selection": true,
    "pointer.palette_still_open": false,
    "pointer.search_visible": false,
    "pointer.outcome": "Palette dismissed after pointer press; keyboard selection was 'debug.startOrContinue', clicked was 'editor.find'. OnEntryPointerPressed does not reselect clicked row before ExecuteSelected (source-predicted gap)."
  }
}
```

---

## 6. `A1-SC-03` — Phase 9 unbound IDs via palette

### 6.1 Sequence

| Step | Action | Result |
|------|--------|--------|
| 1 | Open `Main.cs`, `Second.cs`, `NestedFolds.cs` | 3 tabs |
| 2 | Assert IDs registered, default gestures empty | pass |
| 3 | Palette → `editor.foldAll` | **“Folded all regions”** (`CanExecute=true`, `FoldingEditor` set) |
| 4 | Palette → `tab.closeOthers` | tabs **3 → 1** |
| 5 | Re-open `Main.cs`; set replace preconditions; palette → `editor.replaceAll` | **“Replaced 5 occurrences”**; text changed |

### 6.2 Classification rationale — `A1-SC-03` = **WORKS**

Unbound Phase 9 commands are registry-present, palette-reachable, available when preconditions hold, and dispatch the same production handlers. This slice records **reachability + dispatch only**; prior FN row verdicts are unchanged.

### 6.3 Machine-readable evidence (excerpt)

```json
{
  "scenario_id": "A1-SC-03-phase9",
  "exit_code": 0,
  "isolation": { "profile_root": "/tmp/zaide-a3-sc-profile-FoyVOdcF" },
  "observed_view_model_state": {
    "foldAll.canExecute": true,
    "foldAll.FoldStatusMessage": "Folded all regions",
    "closeOthers.tabs_before": 3,
    "closeOthers.tabs_after": 1,
    "replaceAll.canExecute": true,
    "replaceAll.matchCount": 5,
    "replaceAll.status": "Replaced 5 occurrences",
    "replaceAll.text_changed": true
  },
  "classification_hint": "WORKS"
}
```

---

## 7. Cross-cutting limitations

1. **A3 overall is incomplete** — this note covers SC-01…SC-03 only.
2. Headless drawing: palette/overlay **visual paint** is **UNVERIFIED-VIS**; functional overlay visibility and gray `#555566` brushes were observed via control tree.
3. Focus manager returned `(null)` under headless for focus-before/after palette; production restore wiring exists — not claimed as desktop focus QA.
4. Hand-edited `settings.json` keybindings are an **escape hatch**, not a Settings keybindings editor.
5. Pointer gap is a **product-runtime finding**, not a harness defect; not repaired.
6. Temporary runner and all disposable profiles/logs removed after capture.
7. No production code, tracked tests, package pins, or audit policy files modified.
8. Registry unit tests were **not** used as A3 evidence.

---

## 8. Cleanup performed

| Artifact | Action |
|----------|--------|
| `/tmp/zaide-a3-sc/` runner, obj, out, fixtures, working evidence JSON | Removed after preserving this summary |
| Disposable profiles `/tmp/zaide-a3-sc-profile-*` | Removed after evidence capture |
| Repository tracked content | Only this evidence file under `docs/audits/.../evidence/` |
| Production / tests / packages | Unchanged |

---

## 9. Path isolation verification

| Check | Result |
|-------|--------|
| All `resolved_settings_dir` under disposable profiles | **Yes** |
| Real user `~/.config/zaide` used | **No** |
| Repository tree used as workspace root | **No** |
| Workspace fixtures only under `$PROFILE_ROOT/workspace` | **Yes** |

---

## 10. Next bounded A3 slice

**A3 remains incomplete.** Recommended next bounded slice (not begun here):

- Continue remaining A3 journeys that are still open after FN core, language intelligence, terminal, workspace, first-launch, and this SC slice — e.g. **Build/Run/Test**, **Debugging**, **Git**, **Townhall**, agents, permissions, or restart/recovery — **one journey pack at a time**, still under disposable headless profiles, without A4/V4 work.

---

## 11. Status line

**A3 Search and Command Discovery (`A1-SC-01`…`A1-SC-03`): executed (product-runtime smoke).**

| Row | Classification |
|-----|----------------|
| `A1-SC-01` | **WORKS_WITH_FRICTION** |
| `A1-SC-02` | **WORKS_WITH_FRICTION** |
| `A1-SC-03` | **WORKS** |

**A3 as a whole: incomplete.**

**A4 / stabilization / V4: not begun.**

---

*Recorded 2026-08-01. Out-of-tree Avalonia.Headless 12.0.5 product-runtime runner executed clean-profile search/command-discovery smoke under disposable XDG; temporary runner and profiles removed; no production edits.*
