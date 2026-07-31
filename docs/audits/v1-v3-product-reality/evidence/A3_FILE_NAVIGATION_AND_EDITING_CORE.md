# A3 Clean-Profile Smoke — File Navigation and Editing Core (`A1-FN-01` … `A1-FN-06`)

**Audit name:** `v1-v3-product-reality`
**Phase scope of this note:** **A3 file navigation / editing core execution slice only** — rows
`A1-FN-01` through `A1-FN-06`.
**Evidence date:** 2026-07-31
**Repo head at run:** `6636e705435f47768b4b13a61e5b199f3cdfc57b`

---

## 0. Charter and status

| Item | Status |
|------|--------|
| **Document class** | **A3 product-runtime clean-profile smoke evidence** (core FN rows only) |
| **A3 slice** | File Navigation and Editing Core (`A1-FN-01`…`A1-FN-06`) |
| **A3 as a whole** | **Incomplete** — language-intelligence FN-08…FN-15, build/run/test, debugging, Git, Townhall, agents, permissions, trace, memory, restart-recovery rows **not executed** |
| **A4 / stabilization / V4** | **Not begun** |
| Real desktop UI / xdtools / screenshots / pointer automation / manual desktop | **Not used** |
| Production code modified | **No** |
| Tracked tests modified | **No** |
| `Avalonia.Headless` added to tracked project | **No** |
| `Directory.Packages.props` changed | **No** |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` rewritten to “A3 complete” | **No** |
| Prior A2 / A3 evidence rewritten | **No** |
| Real user `~/.config/zaide` read or written by this run | **No** (disposable `HOME` + `XDG_*` only) |
| `csharp-ls` installed or provided | **No** (out of scope for this slice) |

**Authority inputs:**

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) (H0)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md) (H1)
- [A3_WORKSPACE_AND_PROJECT_OPENING.md](./A3_WORKSPACE_AND_PROJECT_OPENING.md) (open-folder path prerequisite)
- [A2_FILE_NAVIGATION_AND_EDITING.md](./A2_FILE_NAVIGATION_AND_EDITING.md)

**Out of scope for this slice (explicit):**

- `A1-FN-08`…`A1-FN-15` (language intelligence / `csharp-ls` / Problems / completion / hover / definition / symbols / format / format-on-save)
- Build/Run/Test, Debugging, Git, Townhall, agents, permissions, trace, memory, restart mid-run
- A4, stabilization, V4 planning
- Production code, tracked tests, package pins, audit policy edits
- Unit tests as A3 proof

---

## 1. Six-row classification table (authoritative for this slice)

| id | A3 classification | Summary |
|----|-------------------|---------|
| `A1-FN-01` | **WORKS_WITH_FRICTION** | Tree `RequestOpenFileCommand` → activation host → `OpenFileCommand` opens `Main.cs`; tab, active document, status language `C#`, clean→dirty→save via registered `file.save`; disk hash changes with edit marker; unsupported `.bin` rejected with status. **Friction:** TextMate syntax **paint** **UNVERIFIED-VIS** (grammar scope `source.cs` product-runtime proven only). |
| `A1-FN-02` | **WORKS_WITH_FRICTION** | Left panel column **MinWidth=180**, **MaxWidth=320** (default 260); clamp helper yields ActualWidth **180** / **320** at extremes; `GridSplitter` present. Copy Path / Copy Relative Path produce absolute/relative strings via production `CopyToClipboard` Interaction. **Friction:** live pointer drag **UNVERIFIED-VIS**; OS clipboard **UNVERIFIED**; production max **320** (not goal-matrix historical **500**). |
| `A1-FN-03` | **WORKS** | `editor.find` / next / previous; 5× `MARKER_SEARCH_A`; zero-match `"No matches found"`; `editor.replaceAll` replaces 5 in one undo group; one `TryUndo` restores pre-replace text; tab switch resets search state. |
| `A1-FN-04` | **WORKS** | `NestedFolds.cs` installs **9** brace folds; `editor.foldToggle` / `foldAll` / `unfoldAll` via production commands; fold status messages; fold-all state discarded/recomputed on tab switch (return = 0 folded / 9 total). Fold-margin paint not claimed. |
| `A1-FN-05` | **WORKS_WITH_FRICTION** | Next/previous/close/close-others/close-all; dirty Cancel / Discard / Save via production `ConfirmClose` Interaction (LIFO headless answers); disk/tab outcomes correct. Deterministic `MoveTab` reorder proven. **Friction:** pointer tab-drag **UNVERIFIED**; under headless, `ICommandRegistry.Execute("tab.close")` CanExecute sometimes lagged (discard used production `CloseTabCommand` after registry false). |
| `A1-FN-06` | **WORKS** | Status bar document name, language `C#`, caret `Ln 7, Col 23 \| Sel 15`, search `"1 of 5"`, save `"Saved: Main.cs"`; tab switch replaces document/status (`Second.cs`, `"Opened: Second.cs"`). |

Allowed classifications only: `WORKS`, `WORKS_WITH_FRICTION`, `UNDISCOVERABLE`, `UNWIRED`, `BROKEN`, `TEST_ONLY`, `DOCS_ONLY`, `UNVERIFIED`, `BLOCKED`, `UNVERIFIED-VIS`.

**Evidence-class distinction:**

| Class | Meaning in this slice |
|-------|------------------------|
| `product-runtime` | Headless shell / ViewModel / registered command / Interaction / production `EditorView` surface under production DI |
| `control-tree-only` | Layout/control presence and bound properties without claiming paint success |

---

## 2. Harness construction (temporary; deleted after evidence capture)

Recreated the H1-proven out-of-tree Avalonia.Headless **12.0.5** runner under `/tmp` (not tracked).

| Item | Value |
|------|--------|
| Runner root | `/tmp/zaide-a3-fn/` (removed after evidence capture) |
| Project | `/tmp/zaide-a3-fn/runner/Zaide.Tests.csproj` |
| Assembly name | **`Zaide.Tests`** (`InternalsVisibleTo` without production edits) |
| TFM | `net10.0` |
| Package | `Avalonia.Headless` **12.0.5** only |
| Project reference | `/home/cenoda/zaide/src/Zaide.csproj` (unchanged) |
| Audit entry | `A3HeadlessEntry.BuildAvaloniaApp()` — **does not** call/patch `Program.BuildAvaloniaApp` |
| Lifetime | `UseHeadless(UseHeadlessDrawing=true)` + `SetupWithClassicDesktopLifetime` + production `Program.ConfigureServices` |
| Folder picker | Deterministic production `PickFolder` Interaction handler (LIFO, re-registered after activation) |
| Editor surface | Production `EditorView` via `EditorSearchViewModel.ActiveDocument` (`IEditorTextOperations`) — **no editor doubles** |
| Observation | ViewModels, registered commands, control-tree splitter, fold manager sections, file hashes |
| Not used | xdtools, OS screenshots, desktop pointer automation, manual UI, service replacements, unit tests as proof |

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

Each scenario used its **own workspace copy** under `$PROFILE_ROOT/workspace` (never the repository tree).

### 2.2 Package versions

| Package | Version | Notes |
|---------|---------|--------|
| Avalonia (repo pin) | 12.0.5 | Unchanged `Directory.Packages.props` |
| Avalonia.Headless | 12.0.5 | Out-of-tree runner only |
| Avalonia.Headless.XUnit | n/a | Not required |
| ReactiveUI.Avalonia.ME.DI (via app) | 12.0.3 | Production DI bootstrap |

### 2.3 Runner command pattern

```bash
PROFILE_ROOT="$(mktemp -d /tmp/zaide-a3-fn-profile-XXXXXXXX)"
export HOME="${PROFILE_ROOT}/home"
export XDG_CONFIG_HOME="${PROFILE_ROOT}/config"
export XDG_DATA_HOME="${PROFILE_ROOT}/data"
export XDG_STATE_HOME="${PROFILE_ROOT}/state"
export XDG_CACHE_HOME="${PROFILE_ROOT}/cache"
mkdir -p "$HOME" "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_STATE_HOME" "$XDG_CACHE_HOME"
cp -a /tmp/zaide-a3-fn/fixtures/workspace "$PROFILE_ROOT/workspace"

dotnet "/tmp/zaide-a3-fn/out/Release/net10.0/Zaide.Tests.dll" \
  --scenario A1-FN-0N \
  --profile "$PROFILE_ROOT" \
  --evidence "/tmp/zaide-a3-fn/evidence/A1-FN-0N.json" \
  --repo-head "6636e705435f47768b4b13a61e5b199f3cdfc57b" \
  --workspace "$PROFILE_ROOT/workspace"
```

### 2.4 Observed disposable profiles (final runs)

| Scenario | Profile root | Exit | Assertions |
|----------|--------------|------|------------|
| `A1-FN-01` | `/tmp/zaide-a3-fn-profile-JKAJCZC5` | **0** | 17/17 pass |
| `A1-FN-02` | `/tmp/zaide-a3-fn-profile-VnBY4GDB` | **0** | 11/11 pass |
| `A1-FN-03` | `/tmp/zaide-a3-fn-profile-c0tHkDmz` | **0** | 16/16 pass |
| `A1-FN-04` | `/tmp/zaide-a3-fn-profile-T5eOdXX7` | **0** | 13/13 pass |
| `A1-FN-05` | `/tmp/zaide-a3-fn-profile-Bxjk5mWH` | **0** | 18/18 pass |
| `A1-FN-06` | `/tmp/zaide-a3-fn-profile-Ahl7A4Uk` | **0** | 11/11 pass |

**Total:** 86 product-runtime assertions, all pass on final capture.

---

## 3. Disposable workspace fixtures

Canonical fixture template under `/tmp/zaide-a3-fn/fixtures/workspace/` (copied per profile; never under the repo or real user home as a workspace root).

```text
workspace/
  Main.cs            # multiple methods + 5× MARKER_SEARCH_A
  Second.cs          # tab-switch companion
  Third.cs           # third tab for lifecycle
  NestedFolds.cs     # multi-brace C# for folding
  unsupported.bin    # binary-like unsupported type
```

### 3.1 Initial file hashes (SHA-256)

| File | SHA-256 |
|------|---------|
| `Main.cs` | `64ac4da17f3addf89f1354fbad98f71515b837aadc3cbb338025ffa5d6b7fc66` |
| `Second.cs` | `72e8c2b147c7497b63f1ffeb8f685ffdc626e1bee509f9170c407e3647e1490c` |
| `Third.cs` | `dde41785925a05c6cb531b302f6e2d6fba56dea9fa336297b3a4981bddb1ca18` |
| `NestedFolds.cs` | `3378bde07108c1f0f010b795d9536d9478e62cebd615598fce1924d01d65a03c` |
| `unsupported.bin` | `e4ebdb46c81d363678fef2a6a45accdf7a2266854a7fd8ce7ab3a109bce5fd35` |

### 3.2 `Main.cs` search markers

Five literal `MARKER_SEARCH_A` occurrences (case-sensitive search/replace smoke).

### 3.3 Tree-to-editor open path (all scenarios)

1. `workspace.openFolder` + LIFO `PickFolder` → disposable workspace
2. Locate `FileTreeNode` by name in `RootNodes`
3. `FileTreeViewModel.RequestOpenFileCommand` → `OpenFileRequested` → `MainWindowActivationHost` → `EditorTabs.OpenFileCommand`

---

## 4. Scenario `A1-FN-01` — open / edit / save / dirty / syntax mode

### 4.1 Sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Cold headless launch; disposable profile | product-runtime |
| 2 | Open workspace; tree-open `Main.cs` | product-runtime |
| 3 | Observe tab, active doc, language, clean dirty | product-runtime |
| 4 | Grammar scope `EditorView.GetGrammarScope` → `source.cs` | product-runtime |
| 5 | Edit via production `EditorView.SetText` | product-runtime |
| 6 | Dirty indicator `● Main.cs` | product-runtime |
| 7 | `file.save` registered command | product-runtime |
| 8 | Disk hash + `"Saved: Main.cs"` status | product-runtime |
| 9 | Tree-open `unsupported.bin` | product-runtime |
| 10 | Syntax paint | **UNVERIFIED-VIS** |

### 4.2 Expected vs observed

| Check | Expected | Observed |
|-------|----------|----------|
| Tab created | 1 tab `Main.cs` | **pass** |
| Language | `C#` | **C#** |
| Grammar scope | `source.cs` | **source.cs** |
| Dirty after edit | `● Main.cs` | **pass** |
| Save command | `file.save` | **Execute=True** |
| Disk | edit marker present; hash change | before `64ac4da1…` → after `b808ecf6…` |
| Save status | `Saved: Main.cs` | **pass** |
| Unsupported `.bin` | no tab; status message | `"Unsupported file type: .bin"` |
| Syntax paint | if not observable | **UNVERIFIED-VIS** |

### 4.3 Classification rationale

Core open/edit/save/dirty/status path is product-runtime proven through the real tree→editor command path and production `EditorView`. Classification is **WORKS_WITH_FRICTION** solely because rendered TextMate highlighting paint cannot be proven under headless drawing.

### 4.4 Machine-readable excerpt

```json
{
  "scenarioId": "A1-FN-01",
  "exitCode": 0,
  "isolation": { "profileRoot": "/tmp/zaide-a3-fn-profile-JKAJCZC5" },
  "observedViewModelState": {
    "syntax.grammar_scope": "source.cs",
    "syntax.paint_classification": "UNVERIFIED-VIS",
    "editor_surface_type": "Zaide.Features.Editor.Presentation.EditorView",
    "dirty.DisplayName": "● Main.cs",
    "save.StatusText": "Saved: Main.cs",
    "Main.cs.hash_before": "64ac4da17f3addf89f1354fbad98f71515b837aadc3cbb338025ffa5d6b7fc66",
    "Main.cs.hash_after": "b808ecf666d4c95c30affefcd7f01cd7746785d2de4d9f6d365a9b3ec049880b",
    "unsupported.StatusText": "Unsupported file type: .bin"
  }
}
```

---

## 5. Scenario `A1-FN-02` — splitter bounds and copy-path

### 5.1 Sequence

| Step | Action |
|------|--------|
| 1 | Open disposable workspace |
| 2 | Locate main layout left-panel column (index 1) |
| 3 | Read MinWidth / MaxWidth / default Width |
| 4 | Set Width 100 / 500; apply `GridLayoutResizeHelper.PreservePixelColumnAndNormalizeStarColumns` |
| 5 | `CopyPathCommand` / `CopyRelativePathCommand` with LIFO Interaction capture |

### 5.2 Expected vs observed

| Check | Expected | Observed |
|-------|----------|----------|
| MinWidth | 180 | **180** |
| MaxWidth | 320 (production; not 500) | **320** |
| Default Width | 260 | **260** (ActualWidth initial 260) |
| Clamp low | ≥180 | ActualWidth **180** after set 100 |
| Clamp high | ≤320 | ActualWidth **320** after set 500 |
| GridSplitter | present | **present** |
| Copy absolute | full path | `/tmp/zaide-a3-fn-profile-VnBY4GDB/workspace/Main.cs` |
| Copy relative | `Main.cs` | **Main.cs** |
| OS clipboard | if unobservable | **UNVERIFIED** |
| Pointer drag feel | if unobservable | **UNVERIFIED-VIS** |

### 5.3 Classification rationale

**WORKS_WITH_FRICTION**: bounds and copy Interaction are proven; production max width is **320** (goal-matrix/historical 500 gap confirmed at runtime); OS clipboard and live splitter drag paint are not claimed.

### 5.4 Machine-readable excerpt

```json
{
  "scenarioId": "A1-FN-02",
  "exitCode": 0,
  "isolation": { "profileRoot": "/tmp/zaide-a3-fn-profile-VnBY4GDB" },
  "observedViewModelState": {
    "splitter.MinWidth": 180,
    "splitter.MaxWidth": 320,
    "splitter.ActualWidth_after_set_100_clamped": 180,
    "splitter.ActualWidth_after_set_500_clamped": 320,
    "copy_path.absolute_captured": "/tmp/zaide-a3-fn-profile-VnBY4GDB/workspace/Main.cs",
    "copy_path.relative_captured": "Main.cs",
    "copy_path.os_clipboard": "UNVERIFIED"
  }
}
```

---

## 6. Scenario `A1-FN-03` — search / replace / wrap / undo

### 6.1 Sequence

| Step | Action |
|------|--------|
| 1 | Tree-open `Main.cs` |
| 2 | `editor.find`; query `MARKER_SEARCH_A` |
| 3 | Find Next / Previous; zero-match query |
| 4 | `editor.replace` + Replace All → `MARKER_REPLACED_A3` |
| 5 | One `EditorView.TryUndo` |
| 6 | Open `Second.cs`; assert search reset |

### 6.2 Expected vs observed

| Check | Expected | Observed |
|-------|----------|----------|
| Marker count | ≥2 | **5** |
| MatchCount | 5 | **5**; status `"1 of 5"` |
| Selection | length 15 | selOffset=73, selLen=15 |
| Find next | advances | 0 → 1 |
| Zero match | `"No matches found"` | shell + search bar + status bar |
| Replace All | 5 occurrences | `"Replaced 5 occurrences"`; remaining old=0 |
| Undo once | pre-replace text restored | **undoOk=true equal=true** |
| Tab switch | search cleared | IsVisible=false; Query empty; MatchCount=0 |
| Disk hashes | unchanged (in-memory edit only) | before==after for all files |

### 6.3 Classification rationale

**WORKS** — full product-runtime path on production editor surface and registered commands; no unresolved friction for this row.

### 6.4 Machine-readable excerpt

```json
{
  "scenarioId": "A1-FN-03",
  "exitCode": 0,
  "isolation": { "profileRoot": "/tmp/zaide-a3-fn-profile-c0tHkDmz" },
  "observedViewModelState": {
    "search.MatchCount": 5,
    "search.zero.StatusMessage": "No matches found",
    "replace.status": "Replaced 5 occurrences",
    "undo.text_restored": true,
    "tab_switch.search.IsVisible": false,
    "tab_switch.search.MatchCount": 0
  }
}
```

---

## 7. Scenario `A1-FN-04` — folding and tab-switch reset

### 7.1 Sequence

| Step | Action |
|------|--------|
| 1 | Tree-open `NestedFolds.cs` |
| 2 | Ensure `FoldingEditor` available; install if needed |
| 3 | Caret into brace region; `editor.foldToggle` |
| 4 | `editor.foldAll` / `editor.unfoldAll` |
| 5 | Fold all; switch to `Second.cs`; switch back |

### 7.2 Expected vs observed

| Check | Expected | Observed |
|-------|----------|----------|
| Folds discovered | >0 | **9** total, 0 folded |
| Toggle | status + folded change | `"Toggled fold"`; folded **1**/9 |
| Fold all | all collapsed | folded **9**/9; `"Folded all regions"` |
| Unfold all | all expanded | folded **0**/9; `"Unfolded all regions"` |
| Tab switch discard | no leak of fold-all | return NestedFolds: folded **0**/9 (recomputed expanded) |
| Second.cs folds | independent reinstall | total **1**, folded 0 |

### 7.3 Classification rationale

**WORKS** — fold commands, status feedback, and tab-switch non-leak proven via production `FoldingOperations` / `FoldingManager` section state. Fold-margin glyph paint not claimed.

### 7.4 Machine-readable excerpt

```json
{
  "scenarioId": "A1-FN-04",
  "exitCode": 0,
  "isolation": { "profileRoot": "/tmp/zaide-a3-fn-profile-T5eOdXX7" },
  "observedViewModelState": {
    "fold.initial.total": 9,
    "fold.after_foldAll.folded": 9,
    "fold.after_unfoldAll.folded": 0,
    "fold.before_tab_switch.folded": 9,
    "fold.back_to_nested.folded": 0,
    "fold.back_to_nested.total": 9
  }
}
```

---

## 8. Scenario `A1-FN-05` — tab lifecycle and dirty-close decisions

### 8.1 Sequence

| Step | Action |
|------|--------|
| 1 | Open Main / Second / Third |
| 2 | `tab.next` / `tab.previous` |
| 3 | Close Third (clean) |
| 4 | Dirty Cancel (`ConfirmClose` → null) |
| 5 | Dirty Discard (`ConfirmClose` → false) |
| 6 | Re-open Main; Dirty Save (`ConfirmClose` → true) |
| 7 | `tab.closeOthers` / `tab.closeAll` |
| 8 | `MoveTab(0,2)` deterministic reorder |

### 8.2 Expected vs observed

| Check | Expected | Observed |
|-------|----------|----------|
| Three tabs | Main, Second, Third | **pass** |
| Next / Previous | advance / wrap back | Second then Main |
| Close active | Third removed | Main, Second remain |
| Dirty Cancel | tab stays dirty; disk unchanged | count=3 dirty=true disk unchanged |
| Dirty Discard | tab closes; disk no dirty marker | tabs Second, Third; hash still original |
| Dirty Save | tab closes; disk has marker | Main gone; hash `3146ccaf…`; contains `DIRTY_SAVE` |
| Close others | only active remains | Second only |
| Close all | empty | count=0 ActiveTab=null |
| MoveTab | order changes | Main>Second>Third → Second>Third>Main |
| Pointer drag | if unobservable | **UNVERIFIED** |

### 8.3 Classification rationale

**WORKS_WITH_FRICTION**: Cancel/Discard/Save decisions and tab lifecycle commands are product-runtime proven. Pointer drag reorder is **UNVERIFIED**. Headless `ICommandRegistry.Execute("tab.close")` CanExecute occasionally lagged for discard (`executed=false`); production `CloseTabCommand` completed the same ConfirmClose path — recorded friction, not fabricated registry-only success.

### 8.4 Machine-readable excerpt

```json
{
  "scenarioId": "A1-FN-05",
  "exitCode": 0,
  "isolation": { "profileRoot": "/tmp/zaide-a3-fn-profile-Bxjk5mWH" },
  "observedViewModelState": {
    "dirty_close.cancel.still_open": true,
    "dirty_close.discard.tabs": ["Second.cs", "Third.cs"],
    "dirty_close.save.disk_contains_marker": true,
    "tab.after_close_all": 0,
    "tab.reorder.after_MoveTab_0_to_2": ["Second.cs", "Third.cs", "Main.cs"],
    "tab.reorder.pointer_drag": "UNVERIFIED"
  }
}
```

---

## 9. Scenario `A1-FN-06` — status-bar document / caret / selection / search / save

### 9.1 Sequence

| Step | Action |
|------|--------|
| 1 | Open `Main.cs` |
| 2 | Observe DocumentText / LanguageText |
| 3 | Select `MARKER_SEARCH_A` on editor surface |
| 4 | `editor.find` → search status projection |
| 5 | Edit + `file.save` → saved status |
| 6 | Open `Second.cs` → document/status update |

### 9.2 Expected vs observed

| Check | Expected | Observed |
|-------|----------|----------|
| Document name | Main.cs | **Main.cs** |
| Language | C# | **C#** |
| Caret/selection | line/col + Sel N | `Ln 7, Col 23 \| Sel 15`; SelectionText=`MARKER_SEARCH_A` |
| Search status | match status on bar | `"1 of 5"` shell + bar + search VM |
| Save status | Saved: Main.cs | **pass** |
| Tab switch | document updates; stale save not sticky | DocumentText=`Second.cs`; status `"Opened: Second.cs"` |

### 9.3 Classification rationale

**WORKS** — status bar projections for document, language, caret/selection, search, save, and tab-switch replacement are product-runtime proven.

### 9.4 Machine-readable excerpt

```json
{
  "scenarioId": "A1-FN-06",
  "exitCode": 0,
  "isolation": { "profileRoot": "/tmp/zaide-a3-fn-profile-Ahl7A4Uk" },
  "observedViewModelState": {
    "status.DocumentText": "Main.cs",
    "status.LanguageText": "C#",
    "status.CaretText_after_selection": "Ln 7, Col 23 | Sel 15",
    "status.after_search.bar": "1 of 5",
    "status.after_save.bar": "Saved: Main.cs",
    "status.after_tab_switch.DocumentText": "Second.cs",
    "status.after_tab_switch.shell": "Opened: Second.cs"
  }
}
```

---

## 10. Explicit limitations (slice-wide)

| Limitation | Classification impact |
|------------|----------------------|
| TextMate syntax **paint** not observed under headless drawing | FN-01 **UNVERIFIED-VIS** sub-path |
| OS/platform clipboard contents not read | FN-02 copy OS path **UNVERIFIED** (Interaction proven) |
| Live GridSplitter pointer drag feel | FN-02 **UNVERIFIED-VIS** |
| Production left-panel MaxWidth **320** vs goal-matrix historical **500** | FN-02 friction (runtime-confirmed) |
| Pointer tab-drag reorder | FN-05 **UNVERIFIED** (`MoveTab` proven) |
| Headless `tab.close` registry CanExecute lag | FN-05 friction (CloseTabCommand path OK) |
| Native folder picker dialog UX | **UNVERIFIED-VIS** (Interaction injects path; WO-owned) |
| UnsavedDialog visual modal | bypassed via LIFO `ConfirmClose` answers (decision path proven) |
| FN-08…FN-15 / csharp-ls | **not executed** |
| Real desktop / xdtools / pointer automation | not used (charter) |

---

## 11. Cleanup and safety

Performed after evidence capture:

1. Removed `/tmp/zaide-a3-fn/` (runner, out, obj, fixtures, evidence JSON copies).
2. Removed disposable profile dirs `/tmp/zaide-a3-fn-profile-*`.
3. Verified **no** production code, tracked tests, or package pins changed.
4. Verified workspace fixtures lived only under disposable `/tmp` profile paths — **not** the repository tree and **not** a real user project path.
5. Settings/secrets only under disposable `$XDG_CONFIG_HOME/zaide`.

---

## 12. Closeout checklist

| Check | Result |
|-------|--------|
| Evidence file created | **Yes** — this document |
| Only new evidence staged | **Yes** (closeout commit) |
| `git diff --check` | clean |
| Commit message | `docs(audit): execute A3 core file navigation editing smoke` |
| Push `master` → `origin` | performed at closeout |
| `HEAD == origin/master` + clean tree | re-verified at closeout |
| A3 overall complete? | **No** — explicitly incomplete |
| A4 / V4 begun? | **No** |

---

## 13. Next bounded A3 slice

| Field | Value |
|-------|--------|
| Recommended next slice | **`A3_FILE_NAVIGATION_AND_EDITING_LANGUAGE`** (or equivalent charter name) for **`A1-FN-08`…`A1-FN-15`** |
| Goal rows | Language intelligence: Problems diagnostics, completion, hover, go-to-definition, document/workspace symbols, format, format-on-save |
| Prerequisites | Eligible project context + external `csharp-ls` on PATH (not bundled; **not** installed by this slice) |
| Still not authorized by this note | A4, stabilization, V4, agent/Git/debug/build journeys |

---

## 14. Summary for re-audit

| id | Classification | Product-runtime proof |
|----|----------------|----------------------|
| `A1-FN-01` | **WORKS_WITH_FRICTION** | Tree open → edit → dirty → `file.save` → disk + status; syntax paint UNVERIFIED-VIS |
| `A1-FN-02` | **WORKS_WITH_FRICTION** | Splitter 180–**320**; copy path Interaction; OS clipboard / drag UNVERIFIED |
| `A1-FN-03` | **WORKS** | Find/next/prev/zero-match/replace-all/one-undo/tab-reset |
| `A1-FN-04` | **WORKS** | Fold toggle/all/unfold; no fold leak across tabs |
| `A1-FN-05` | **WORKS_WITH_FRICTION** | Tab lifecycle + dirty Cancel/Discard/Save; pointer drag UNVERIFIED |
| `A1-FN-06` | **WORKS** | Status bar document/caret/selection/search/save/tab-switch |

**A3 overall remains incomplete.** This note does not authorize FN-08…15, A4, or V4 work.

---

*Recorded 2026-07-31. Out-of-tree Avalonia.Headless 12.0.5 product-runtime runner executed clean-profile smoke for core file-navigation/editing rows under disposable XDG; temporary runner and profiles removed; no production edits.*
