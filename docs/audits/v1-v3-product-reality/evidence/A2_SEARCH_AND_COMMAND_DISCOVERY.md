# A2 Wiring Audit — `A2_SEARCH_AND_COMMAND_DISCOVERY`

**Audit name:** `v1-v3-product-reality`
**Slice:** `A2_SEARCH_AND_COMMAND_DISCOVERY` (eleventh A2 slice; prior:
`A2_AGENT_SEND`, `A2_MULTI_AGENT_ROUTING`,
`A2_TRACE_MEMORY_USAGE_TERMINATION`, `A2_RESTART_RECOVERY_AND_CONTEXT`,
`A2_TOOLS_PERMISSIONS`, `A2_AGENT_CREATION_AND_BACKEND_ONBOARDING`,
`A2_TOWNHALL_AND_CONVERSATIONS`, `A2_FIRST_LAUNCH_AND_SETTINGS`,
`A2_WORKSPACE_AND_PROJECT_OPENING`, `A2_FILE_NAVIGATION_AND_EDITING`)
**Evidence date:** 2026-07-31
**Baseline:** branch `master`, HEAD
`e764784b09d88da0cc21fb568b9685b8456f0a7d` (matches `origin/master`)
**Method:** read-only source inspection (`rg`, file reads). No app launch,
build, test execution, production-code edits, commits, or pushes.

---

## 1. Audit identity, baseline, and safety boundary

| Check | Result |
|-------|--------|
| Branch | `master` |
| `git rev-parse HEAD` | `e764784b09d88da0cc21fb568b9685b8456f0a7d` |
| `git rev-parse origin/master` | `e764784b09d88da0cc21fb568b9685b8456f0a7d` |
| Working tree at audit start | Clean (`git status --short` empty) |
| Ten published A2 evidence files | Present (Agent Send through File Navigation/Editing) |
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
source is verdict authority. Tests and historical Phase 8.2 / Phase 9
closeout evidence are corroboration only. Live keyboard delivery, Avalonia
`KeyBinding` precedence against control-local handlers, focus restoration
on a real Linux desktop, and settings-file round-trip of hand-edited
keybinding overrides are not claimed from source alone. **No real user
profile, settings, secrets, or opened workspace path was accessed.**

**Verdict rows (this slice only):** `A1-SC-01`, `A1-SC-02`, `A1-SC-03`
(each exactly once in §3). No new verdicts for FL, WO, FN, BR, DB, TR, GT,
TH, AC, AS, TP, MR, or TC/XX rows.

**Cross-slice ownership (not re-verdicted here):**

- Active-document find/replace *behavior* (literal match, wrap, Replace All
  undo, status messages): [A2_FILE_NAVIGATION_AND_EDITING.md](./A2_FILE_NAVIGATION_AND_EDITING.md)
  (`A1-FN-03` path). This slice only audits **command registration,
  discovery, and invocation** of those IDs.
- Folding *behavior* and tab lifecycle UX: same FN evidence (`A1-FN-04`,
  `A1-FN-05`). Fold/tab commands appear here only as registry/palette
  discovery rows.
- Settings shell host, schema load/save, and conflict rebase for editor
  settings: [A2_FIRST_LAUNCH_AND_SETTINGS.md](./A2_FIRST_LAUNCH_AND_SETTINGS.md).
  SC re-traces only the **keybindings map** and the absence of a
  keybindings editor surface.
- Build/run/test, debug, and source-control commands may appear in the
  production registry; they are inventory context only, not BR/DB/GT
  verdicts.

---

## 2. Sources inspected

### 2.1 Documentation

- [docs-rules.md](../../../../docs-rules.md)
- [AUDIT_PLAN.md](../AUDIT_PLAN.md) (A2 progress; journey 4 Search and
  command discovery)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md) (§4 Search and Command Discovery;
  §17.8 A2 progress — this slice was “next recommended; explicitly not
  begun” before this write)
- [A1_ACCEPTANCE.md](../A1_ACCEPTANCE.md)
- Prior A2 boundaries:
  - [A2_FILE_NAVIGATION_AND_EDITING.md](./A2_FILE_NAVIGATION_AND_EDITING.md)
  - [A2_FIRST_LAUNCH_AND_SETTINGS.md](./A2_FIRST_LAUNCH_AND_SETTINGS.md)
  - [A2_WORKSPACE_AND_PROJECT_OPENING.md](./A2_WORKSPACE_AND_PROJECT_OPENING.md)
- Phase 8.2: [phase-8.2/IMPLEMENTATION_PLAN.md](../../../phases/v2/phase-8/phase-8.2/IMPLEMENTATION_PLAN.md)
  (registry, overrides, materialization; **out of scope:** conflict-
  resolution UI, Command Palette)
- Phase 8 umbrella / 8.1.x notes on non-editable keybindings UI:
  [phase-8/IMPLEMENTATION_PLAN.md](../../../phases/v2/phase-8/IMPLEMENTATION_PLAN.md);
  [phase-8.1.5](../../../phases/v2/phase-8/phase-8.1/phase-8.1.5/IMPLEMENTATION_PLAN.md)
  (“Keybindings are read-only if shown. Do not add registry-based
  editing.”)
- Phase 9: [phase-9/IMPLEMENTATION_PLAN.md](../../../phases/v2/phase-9/IMPLEMENTATION_PLAN.md)
  (M1–M5a palette, search commands, unbound fold/tab commands);
  [V2.md §"Phase 9 — Editor UX"](../../../roadmap/V2.md#phase-9--editor-ux)

### 2.2 Production source

**Registry / keybinding resolution**

- [ICommandRegistry.cs](../../../../src/App/Composition/ICommandRegistry.cs)
- [CommandRegistry.cs](../../../../src/App/Composition/CommandRegistry.cs)
- [CommandDescriptor.cs](../../../../src/App/Composition/CommandDescriptor.cs)
- [ResolvedKeyBinding.cs](../../../../src/App/Composition/ResolvedKeyBinding.cs) (record used by
  resolution)
- [KeyBindingConverter.cs](../../../../src/App/Shell/KeyBindingConverter.cs)
- [MainWindow.axaml.cs](../../../../src/App/Shell/MainWindow.axaml.cs)
  (`MaterializeRegistryBindings`, settings `WhenChanged` refresh)
- [App.axaml.cs](../../../../src/App/Composition/App.axaml.cs) (eager
  resolve of palette / search / language / debug registrars)
- [AppCoreServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AppCoreServiceCollectionExtensions.cs)

**Command Palette**

- [CommandPaletteViewModel.cs](../../../../src/App/Shell/CommandPaletteViewModel.cs)
- [CommandPaletteOverlay.cs](../../../../src/App/Shell/CommandPaletteOverlay.cs)
- [ShellOverlayFocusWiring.cs](../../../../src/App/Shell/ShellOverlayFocusWiring.cs)
- [PaletteEntry.cs](../../../../src/App/Shell/PaletteEntry.cs)

**Phase 9 + shell registration sites**

- [EditorSearchViewModel.cs](../../../../src/Features/Editor/Presentation/EditorSearchViewModel.cs)
- [EditorTabViewModel.cs](../../../../src/Features/Editor/Presentation/EditorTabViewModel.cs)
- [EditorLanguageInputViewModel.cs](../../../../src/Features/Editor/Presentation/EditorLanguageInputViewModel.cs)
- [MainWindowViewModel.cs](../../../../src/App/Shell/MainWindowViewModel.cs)
- [FileTreeViewModel.cs](../../../../src/Features/Workspace/Presentation/FileTreeViewModel.cs)
- [ProjectWorkflowViewModel.cs](../../../../src/Features/ProjectSystem/Presentation/ProjectWorkflowViewModel.cs)
- [DebugSessionViewModel.cs](../../../../src/Features/Debugging/Presentation/DebugSessionViewModel.cs)
- [EditorBreakpointViewModel.cs](../../../../src/Features/Debugging/Presentation/EditorBreakpointViewModel.cs)
- [SourceControlViewModel.cs](../../../../src/Features/SourceControl/Presentation/SourceControlViewModel.cs)

**Settings keybindings persistence (no UI editor)**

- [SettingsModel.cs](../../../../src/Features/Settings/Domain/SettingsModel.cs)
- [SettingsSerializer.cs](../../../../src/Features/Settings/Infrastructure/SettingsSerializer.cs)
- [SettingsService.cs](../../../../src/Features/Settings/Infrastructure/SettingsService.cs)
- [SettingsViewModel.cs](../../../../src/Features/Settings/Presentation/SettingsViewModel.cs)
- [SettingsPanelView.cs](../../../../src/Features/Settings/Presentation/SettingsPanelView.cs)

### 2.3 Tests / historical closeouts (corroboration only)

Unit tests for registry resolution, palette filter/order/execute, and Phase
9 command registration exist under `tests/` (not executed this session).
Phase 8.2 and Phase 9 plan status checkboxes and manual smoke notes are
historical developer evidence, not A3 clean-profile proof.

---

## 3. Primary verdict table

| Goal ID | Verdict | One-line basis |
|---------|---------|----------------|
| `A1-SC-01` | **Wired-with-gap** | Production `ICommandRegistry` + `CommandRegistry` provide stable IDs, default gestures, settings-driven overrides, empty-string unbind, and gesture-conflict logging; `MainWindow` materializes and live-refreshes Avalonia `KeyBindings`. **Gap:** no user-reachable Settings → keybindings editor; conflict handling is log-only (Phase 8.2 intentionally omitted conflict UI); matrix entry “Open settings → keybindings; rebind a command” is not product-wired. |
| `A1-SC-02` | **Wired-with-gap** | Registry-backed `palette.open` (`Ctrl+Shift+P`), overlay host, case-insensitive literal filter on display name, deterministic category/name/ID ordering, keyboard navigation over **available** entries, execute via `ICommandRegistry.Execute`, unavailable entries grayed and skipped, focus restore to editor after dismiss when an active tab exists. **Gap:** pointer-click on a row does not reselect that row before `ExecuteSelected()`, so mouse execution can run the keyboard selection instead of the clicked command; live gesture/focus quality remains A3. |
| `A1-SC-03` | **Wired** | All Phase 9 M3/M4/M5a command IDs are registered in production with the locked categories/default gestures: `editor.find`/`replace`/`findNext`/`findPrevious`/`replaceNext`/`replaceAll`, `editor.foldToggle`/`foldAll`/`unfoldAll`, `tab.next`/`previous`/`close`/`closeOthers`/`closeAll`. Unbound commands use empty `DefaultGestures` and remain palette-reachable via `GetAll()`; handlers are the same `ICommand` instances used by keybindings. |

---

## 4. Production path traces

### 4.1 Shared registry and keybinding materialization (`A1-SC-01` core)

```text
DI singleton CommandRegistry (ICommandRegistry)
  ← registrars (constructors / App.axaml.cs eager resolve):
      CommandPaletteViewModel, EditorSearchViewModel,
      EditorLanguageInputViewModel, MainWindowViewModel,
      FileTreeViewModel, ProjectWorkflowViewModel,
      DebugSessionViewModel, EditorBreakpointViewModel,
      SourceControlViewModel, EditorTabViewModel (via MainWindow VM)

ResolveKeyBindings(ISettingsService):
  1. For each settings.Current.Keybindings (commandId → gesture),
     ordered by commandId Ordinal:
       - skip unknown command IDs (log Warning)
       - gesture "" → explicit unbind (no default for that command)
       - invalid gesture → skip (log Warning)
       - same gesture → Ordinal-min commandId wins; LogGestureConflict
  2. For each registered command without override, apply DefaultGestures;
     existing gesture wins; conflict logged, loser skipped
  → IReadOnlyList<ResolvedKeyBinding>

MainWindow.WhenActivated:
  MaterializeRegistryBindings()
  _settings.WhenChanged → MaterializeRegistryBindings(snapshot)
    remove prior _registryBindings from Window.KeyBindings
    for each resolved: KeyBindingConverter.TryCreateKeyBinding
      → Window.KeyBindings += { Gesture, descriptor.Command }
```

**Source-proven:** stable string IDs; duplicate `Register` throws
`InvalidOperationException`; execute path checks `CanExecute` then
`Execute`, returns `false` and debug-logs unknown/unavailable; typed
`Execute<T>` never coerces parameters; overrides and unbinds are first-class
in `SettingsModel.Keybindings` and survive serialize/normalize paths.

**Not user-reachable as claimed:** Settings panel sections are Editor,
Terminal, and LLM only (`SettingsPanelView`). `SettingsViewModel` has no
`SetKeybinding` (or similar) mutator — only Editor/LLM setters. A user cannot
rebind from the product UI. Hand-editing `settings.json` could populate
`Keybindings` and, if loaded, would drive resolution — that is a file-level
escape hatch, not the matrix entry point “Open settings → keybindings”.

**Conflict surface:** `LogGestureConflict` writes `ILogger` warnings only.
No dialog, status-bar message, or settings banner. Phase 8.2 plan
explicitly lists conflict-resolution UI as out of scope; the goal matrix
still names “Conflict dialog or surface” under failure recovery → product
gap vs matrix wording, not a missing registry algorithm.

### 4.2 Command Palette discovery and execution (`A1-SC-02`)

```text
User: Ctrl+Shift+P
  → Window KeyBinding for palette.open
  → OpenPaletteCommand → CommandPaletteViewModel.Open()
  → OpenRequested → ShellOverlayFocusWiring → overlay.Show()
       (search box focused, query cleared, entries rebuilt)

User types query
  → TextBox.TextChanged → SetQuery → Filter:
       case-insensitive literal substring on DisplayName
       empty query → all entries
       order: Category, DisplayName, Id (OrdinalIgnoreCase)
  → RebuildEntries; empty list shows "No matching commands"

User navigates (Up/Down)
  → MoveUp/MoveDown over available indices only
  → unavailable rows still listed (gray #555566) but not selected

User confirms (Enter)
  → ExecuteSelected:
       if SelectedEntry null or !IsAvailable → false (no execute)
       else registry.Execute(id); Close(); true
  → overlay raises Dismissed → Hide + RestoreFocusAfterPalette
       (editorView.Focus if ActiveTab non-null and editor visible)

User Escape / backdrop click
  → Dismissed → same hide + focus restore (no execute)
```

**Availability:** `PaletteEntry.IsAvailable` is
`descriptor.Command.CanExecute(null)` at enumeration time. Unavailable
commands remain visible; keyboard selection and `ExecuteSelected` refuse
them. Registry `Execute` double-checks `CanExecute` (race-safe at source
level).

**Pointer path gap:** `OnEntryPointerPressed` does **not** set
`SelectedIndex` to the clicked row’s index. It only guards
`entries[index].IsAvailable` then calls `ExecuteSelected()`, which uses the
**current** keyboard selection. Source-proven defect relative to “click the
command you see.”

**Focus restore nuance:** restoration targets the shared `EditorView` when
an active editor tab exists — not an arbitrary non-editor invoker. Matches
Phase 9 “invoking editor” language when the editor was active; if palette
is opened with no tabs, focus restore is a no-op in source.

**CloseRequested:** raised by `Close()` / `ExecuteSelected` but the shell
wires **Dismissed** from the overlay for hide/focus. Enter path invokes
`Dismissed` after successful execute; Escape/backdrop also use `Dismissed`.
No separate CloseRequested subscriber is required for the documented path.

### 4.3 Phase 9 command registration inventory (`A1-SC-03`)

| Command ID | Display name | Category | Default gesture(s) | Registrar |
|------------|--------------|----------|--------------------|-----------|
| `editor.find` | Find | Editor | `Ctrl+F` | `EditorSearchViewModel` |
| `editor.replace` | Replace | Editor | `Ctrl+H` | `EditorSearchViewModel` |
| `editor.findNext` | Find Next | Editor | `F3` | `EditorSearchViewModel` |
| `editor.findPrevious` | Find Previous | Editor | `Shift+F3` | `EditorSearchViewModel` |
| `editor.replaceNext` | Replace Next | Editor | *(unbound)* | `EditorSearchViewModel` |
| `editor.replaceAll` | Replace All | Editor | *(unbound)* | `EditorSearchViewModel` |
| `editor.foldToggle` | Toggle Current Fold | Editor | *(unbound)* | `EditorTabViewModel` |
| `editor.foldAll` | Fold All | Editor | *(unbound)* | `EditorTabViewModel` |
| `editor.unfoldAll` | Unfold All | Editor | *(unbound)* | `EditorTabViewModel` |
| `tab.next` | Next Tab | Tab | `Ctrl+Tab` | `EditorTabViewModel` |
| `tab.previous` | Previous Tab | Tab | `Ctrl+Shift+Tab` | `EditorTabViewModel` |
| `tab.close` | Close Tab | Tab | `Ctrl+W`, `Ctrl+F4` | `EditorTabViewModel` |
| `tab.closeOthers` | Close Other Tabs | Tab | *(unbound)* | `EditorTabViewModel` |
| `tab.closeAll` | Close All Tabs | Tab | *(unbound)* | `EditorTabViewModel` |

Also registered for completeness (not SC-03 scope, but user-discoverable in
the same palette): `palette.open`; shell `file.save`,
`workspace.openFolder`, `workspace.closeFolder` *(unbound)*,
`view.toggleBottomPanel`; `explorer.toggleHiddenFiles`; project
`project.build` / `run` / `test` *(unbound)* / `cancel`; debug F5 family;
language completion/navigation/symbol/format IDs; source-control commit/
refresh *(unbound)*.

**Unbound-but-palette-reachable:** empty `DefaultGestures` ⇒ no default
window binding after resolution (unless user override map supplies one).
`CommandPaletteViewModel.GetAllEntries()` still lists them. Fold commands
require `FoldingEditor` wired on `EditorTabViewModel` (MainWindow sets
`editorTabs.FoldingEditor = _editorView.Folding` in `WhenActivated`) —
registration is global; **effect** still needs an active foldable document
(`CanExecute`). That availability behavior is intentional, not FN re-audit.

**Optional DI in some VMs:** several registrars use
`commandRegistry?.Register(...)` so unit tests can omit the registry.
Production composition always supplies `ICommandRegistry`; App eagerly
resolves palette/search/language/debug before MainWindow materializes
bindings so default gestures exist at first paint.

### 4.4 Registered / DI / test-only vs user-discoverable

| Class | Examples | User can discover/invoke? |
|-------|----------|---------------------------|
| Production-registered, default-bound | `palette.open`, `editor.find`, `file.save`, `project.build`, … | **Yes** — gesture and/or palette |
| Production-registered, intentionally unbound | `editor.replaceNext`, fold trio, `tab.closeOthers`, `workspace.closeFolder`, `project.test`, … | **Yes via palette** when `CanExecute` true; not via default key |
| Registered but unavailable (`CanExecute` false) | fold with no folds/active tab; tab.next with &lt;2 tabs; project.cancel idle | **Visible in palette** (gray); not executed |
| Not registered / test doubles | tests constructing VMs with `commandRegistry: null` | **No** — test-only omission |
| Settings JSON override without UI | hand-edited `keybindings` map | **Indirect only** — load path exists; no product editor |

There is **no second command catalog**. Palette enumerates solely
`ICommandRegistry.GetAll()`.

---

## 5. User reachability and failure visibility

| Concern | User-reachable? | Failure visibility |
|---------|-----------------|--------------------|
| Open Command Palette | **Yes** — `Ctrl+Shift+P` / `palette.open` | N/A when bound; if override unbinds without alternate path, only other entry is none (no menu item) |
| Filter / empty results | **Yes** | Overlay caption **"No matching commands"** |
| Execute available command | **Yes** — Enter (keyboard); mouse path unreliable (SC-02 gap) | Failed `Execute` returns false + debug log only — palette still closes on successful `ExecuteSelected` path only |
| Unavailable command | **Yes** visible | Gray text; selection skips; Enter no-ops (handled); no toast |
| Unknown command ID (API) | N/A for palette | `Execute` → false + debug log |
| Rebind key in Settings UI | **No** | N/A — surface absent |
| Gesture conflict | **No user surface** | Logger Warning only |
| Invalid override gesture in JSON | Not product UI | Logger Warning; override skipped |
| Override for unregistered ID | Not product UI | Logger Warning; skipped |
| Explicit unbind (`""`) | File-level only | Command loses default gesture; still palette-reachable |
| Live rebind after settings change | **If** `Keybindings` in published snapshot changes | `WhenChanged` → rematerialize (source-proven); no UI to trigger) |
| Focus return after palette | **Yes** when active editor tab | Silent if no tab / editor not visible |
| Unbound Phase 9 commands | **Yes** via palette | Availability via `CanExecute` (e.g. no document) |

---

## 6. Source-proven vs runtime-unproven

| Claim | Class |
|-------|--------|
| Singleton `CommandRegistry` + duplicate-ID throw | Source-proven |
| Descriptor fields Id / DisplayName / Category / DefaultGestures / Command | Source-proven |
| Override map, empty-string unbind, conflict log + Ordinal winner | Source-proven |
| Window binding materialization + settings `WhenChanged` refresh | Source-proven |
| No Settings keybindings editor / no SetKeybinding mutator | Source-proven |
| `palette.open` registration and Ctrl+Shift+P default | Source-proven |
| Palette filter, order, availability projection, empty caption | Source-proven |
| Enter execute + Dismissed hide + editor focus restore path | Source-proven |
| Pointer-click does not select clicked index before execute | Source-proven |
| Phase 9 command ID/gesture table matches plan | Source-proven |
| Unbound commands still in `GetAll()` / palette | Source-proven |
| FoldingEditor wired for fold command effect | Source-proven (wiring); fold quality is FN/A3 |
| Live key delivery / gesture vs control KeyDown precedence | **Runtime-unproven (A3)** |
| Focus return quality on Linux/X11 | **Runtime-unproven (A3)** |
| Hand-edited settings.json keybindings round-trip in clean profile | **Runtime-unproven (A3)** |
| Conflict log observability to end user | **Not product-visible** (by design today) |

---

## 7. Contradiction / reconciliation notes

1. **Matrix SC-01 entry point vs Phase 8.2 scope**
   Goal matrix: “Open settings → keybindings; rebind a command” and
   “Conflict dialog or surface.” Phase 8.2 plan: conflict-resolution UI
   out of scope; Phase 8.1.5: do not add registry-based keybinding
   editing. Production matches the phase plans (engine + persistence),
   not the matrix’s UI entry. A2 verdict is **Wired-with-gap**, not
   Missing, because the registry/override/materialization spine is
   production-complete.

2. **Retired FN-07 → SC-02**
   Palette ownership lives here. FN evidence must not re-score palette
   rows; this slice does not re-score search/replace/fold *editing*
   outcomes.

3. **DI registration timing**
   Commands register in VM constructors. App eagerly resolves the
   registrars that are not constructed solely through
   `MainWindowViewModel` so first `MaterializeRegistryBindings` sees them.
   A test that constructs a VM without the registry is not a product path.

4. **Palette lists more than Phase 9**
   SC-03 requires Phase 9 IDs present; it does not require the palette to
   be Phase-9-only. Extra project/debug/language commands increase
   discovery surface for later journeys without changing SC verdicts.

5. **`CloseRequested` vs `Dismissed`**
   Not a missing hide path for keyboard/backdrop; document the dual
   events so future readers do not invent a false “never closes” finding.

6. **Historical Phase 9 manual smoke ≠ A3**
   Plan status and M2 smoke notes are corroboration. Clean disposable
   profile keyboard/focus proof is deferred to A3.

---

## 8. A3 constraints only (not executed)

A3 for this journey **must** use a disposable isolated profile
(`XDG_CONFIG_HOME` absolute temp directory established **before** process
start). Never the real user profile, real settings/secrets, or a real
developer workspace under the user’s home tree.

Suggested disposable-profile scenarios (description only — **not run**):

1. **SC-02 palette happy path:** cold launch; `Ctrl+Shift+P`; type
   `find`; Enter on Find; confirm search bar opens and editor regains
   focus after dismiss; Escape dismiss without execute; empty filter
   shows “No matching commands”.
2. **SC-02 unavailable:** open palette with no editor tabs; observe
   editor/tab commands grayed; confirm Enter does not throw and does not
   execute; open a file and re-check availability flips.
3. **SC-02 pointer:** with multiple matches visible, click a
   **non-selected** available row; record whether the clicked command or
   the prior selection runs (source predicts mismatch).
4. **SC-03 unbound reachability:** via palette run `editor.replaceAll`
   (with replace mode + matches), `editor.foldAll`, `tab.closeOthers`
   without relying on default keys; confirm effect matches FN contracts
   without reopening FN verdicts here.
5. **SC-01 override escape hatch (optional):** in the disposable profile
   only, hand-edit `settings.json` `keybindings` to rebind `editor.find`
   and to create a deliberate gesture conflict; restart; observe which
   command receives the key and that **no** conflict dialog appears.
   Do **not** use the real user profile.

A3 must not mutate production code, audit plan, or goal matrix. A3 does
not begin in this session.

---

## 9. Next recommended A2 slice (explicitly not started)

**`A2_BUILD_RUN_AND_TEST`** — journey 5 in [AUDIT_PLAN.md](../AUDIT_PLAN.md)
and [GOAL_MATRIX.md](../GOAL_MATRIX.md) §5 (`A1-BR-01` …). Scope build/run/
test target selection, Output panel, Problems projection from build,
cancellation, and one-operation-at-a-time policy.

- Evidence file
  `docs/audits/v1-v3-product-reality/evidence/A2_BUILD_RUN_AND_TEST.md`:
  **absent** (confirmed).
- No BR verdicts assigned in this slice.
- This session does **not** start that slice.

---

## 10. Stop state

- Exactly one new evidence file written:
  `docs/audits/v1-v3-product-reality/evidence/A2_SEARCH_AND_COMMAND_DISCOVERY.md`
- No production, test, issue, deferred, `AUDIT_PLAN.md`, or
  `GOAL_MATRIX.md` edits
- No commit, push, app launch, build, test run, or A3
- Stop for re-audit / human review before any plan progress-table update
  or next A2 slice
