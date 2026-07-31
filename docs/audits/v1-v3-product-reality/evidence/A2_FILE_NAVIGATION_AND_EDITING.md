# A2 Wiring Audit — `A2_FILE_NAVIGATION_AND_EDITING`

**Audit name:** `v1-v3-product-reality`
**Slice:** `A2_FILE_NAVIGATION_AND_EDITING` (tenth A2 slice; prior:
`A2_AGENT_SEND`, `A2_MULTI_AGENT_ROUTING`,
`A2_TRACE_MEMORY_USAGE_TERMINATION`, `A2_RESTART_RECOVERY_AND_CONTEXT`,
`A2_TOOLS_PERMISSIONS`, `A2_AGENT_CREATION_AND_BACKEND_ONBOARDING`,
`A2_TOWNHALL_AND_CONVERSATIONS`, `A2_FIRST_LAUNCH_AND_SETTINGS`,
`A2_WORKSPACE_AND_PROJECT_OPENING`)
**Evidence date:** 2026-07-31
**Baseline:** branch `master`, HEAD
`cd188ac75e628846972b1d058a5a4f50f67c3ce8` (matches `origin/master`)
**Method:** read-only source inspection (`rg`, file reads). No app launch,
build, test execution, production-code edits, commits, or pushes.

---

## 1. Audit identity, baseline, and safety boundary

| Check | Result |
|-------|--------|
| Branch | `master` |
| `git rev-parse HEAD` | `cd188ac75e628846972b1d058a5a4f50f67c3ce8` |
| `git rev-parse origin/master` | `cd188ac75e628846972b1d058a5a4f50f67c3ce8` |
| Working tree at audit start | Clean (`git status --short` empty) |
| Nine published A2 evidence files | Present (Agent Send through Workspace/Project Opening) |
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
source is verdict authority. Tests and historical phase closeout / manual
evidence documents are corroboration only. Runtime TextMate paint, keyboard
delivery, clipboard paste truth, AvaloniaEdit fold-margin hit testing, live
`csharp-ls` process behavior, and real language-server diagnostics are not
claimed from source alone. **No real user profile, settings, secrets, or
opened workspace path was accessed.**

**Verdict rows (this slice only):** `A1-FN-01` … `A1-FN-06` and
`A1-FN-08` … `A1-FN-15` (14 rows). **Retired `A1-FN-07` is not used as a
verdict.** No new verdicts for FL, WO, SC, BR, DB, TR, GT, TH, AC, AS, TP, MR,
or TC/XX rows.

**Cross-slice ownership (not re-verdicted here):**

- Folder open/close, ignore list, hidden files, new file/folder, and
  project-context selection: [A2_WORKSPACE_AND_PROJECT_OPENING.md](./A2_WORKSPACE_AND_PROJECT_OPENING.md)
  (`A1-WO-01`…`A1-WO-03`). FN-02 re-traces only the tree→editor open pathway,
  splitter bounds, and copy-path chrome that the FN goal names.
- Settings shell / Format-on-Save checkbox host:
  [A2_FIRST_LAUNCH_AND_SETTINGS.md](./A2_FIRST_LAUNCH_AND_SETTINGS.md).

---

## 2. Sources inspected

### 2.1 Documentation

- [docs-rules.md](../../../../docs-rules.md)
- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md) (§3 File navigation and editing;
  §17.8 A2 progress)
- [A1_ACCEPTANCE.md](../A1_ACCEPTANCE.md)
- Prior A2 evidence (shared shell / project / settings boundaries):
  - [A2_WORKSPACE_AND_PROJECT_OPENING.md](./A2_WORKSPACE_AND_PROJECT_OPENING.md)
  - [A2_FIRST_LAUNCH_AND_SETTINGS.md](./A2_FIRST_LAUNCH_AND_SETTINGS.md)
  - Other published A2 slices listed for completeness only; no FN verdicts there
- V1 Phase 1 / 1.1 / 1.2 / 2:
  [PHASES.md §"Phase 1"](../../../roadmap/PHASES.md#phase-1-file-tree-sidebar);
  [§"Phase 1.1"](../../../roadmap/PHASES.md#phase-11-file-tree-polish);
  [§"Phase 1.2"](../../../roadmap/PHASES.md#phase-12-file-tree-essentials);
  [§"Phase 2: Editor"](../../../roadmap/PHASES.md#phase-2-editor)
- V2 Phase 9 / 10:
  [V2.md §"Phase 9 — Editor UX"](../../../roadmap/V2.md#phase-9--editor-ux);
  [V2.md §"Phase 10 — C# Language Intelligence"](../../../roadmap/V2.md#phase-10--c-language-intelligence);
  [Phase 9 plan](../../../phases/v2/phase-9/IMPLEMENTATION_PLAN.md);
  [Phase 10 plan](../../../phases/v2/phase-10/IMPLEMENTATION_PLAN.md);
  historical M3–M6 manual evidence under `docs/phases/v2/phase-10/`
  (corroboration only)

### 2.2 Production source (minimum required + supporting)

**Tree → open / copy / layout**

- [FileTreeViewModel.cs](../../../../src/Features/Workspace/Presentation/FileTreeViewModel.cs)
- [FileTreeView.cs](../../../../src/Features/Workspace/Presentation/FileTreeView.cs)
- [MainLayoutBuilder.cs](../../../../src/App/Shell/MainLayoutBuilder.cs)
- [MainWindowActivationHost.cs](../../../../src/App/Shell/MainWindowActivationHost.cs)
- [SupportedFileTypes.cs](../../../../src/Features/Editor/Domain/SupportedFileTypes.cs)

**Editor tabs / save / dirty / TextMate / folding / search / tabs**

- [EditorTabViewModel.cs](../../../../src/Features/Editor/Presentation/EditorTabViewModel.cs)
- [EditorViewModel.cs](../../../../src/Features/Editor/Presentation/EditorViewModel.cs)
- [EditorView.cs](../../../../src/Features/Editor/Presentation/EditorView.cs)
- [EditorTabBar.cs](../../../../src/Features/Editor/Presentation/EditorTabBar.cs)
- [EditorSessionFactory.cs](../../../../src/Features/Editor/Presentation/EditorSessionFactory.cs)
- [EditorSearchViewModel.cs](../../../../src/Features/Editor/Presentation/EditorSearchViewModel.cs)
- [SearchBar.cs](../../../../src/Features/Editor/Presentation/SearchBar.cs)
- [SearchEngine.cs](../../../../src/Features/Editor/Domain/SearchEngine.cs)
- [FoldingOperations.cs](../../../../src/Features/Editor/Presentation/FoldingOperations.cs)
- [BraceFoldingStrategy.cs](../../../../src/Features/Editor/Domain/BraceFoldingStrategy.cs)
- [Document.cs](../../../../src/Features/Editor/Domain/Document.cs)
- [FileService.cs](../../../../src/Features/Editor/Infrastructure/FileService.cs)
- [UnsavedDialog.axaml.cs](../../../../src/Features/Editor/Presentation/UnsavedDialog.axaml.cs)
- [EditorServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/EditorServiceCollectionExtensions.cs)
- [MainWindowViewModel.cs](../../../../src/App/Shell/MainWindowViewModel.cs)
  (`file.save` / `SaveActiveTabCommand`)
- [MainWindow.axaml.cs](../../../../src/App/Shell/MainWindow.axaml.cs)
- [StatusBarViewModel.cs](../../../../src/App/Shell/StatusBarViewModel.cs),
  [StatusBar.cs](../../../../src/App/Shell/StatusBar.cs)
- [RightColumnHost.cs](../../../../src/App/Shell/RightColumnHost.cs)

**Language session → features → projection**

- [LanguageServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/LanguageServiceCollectionExtensions.cs)
- [App.axaml.cs](../../../../src/App/Composition/App.axaml.cs) (eager resolve)
- [LanguageSessionService.cs](../../../../src/Features/Language/Application/LanguageSessionService.cs)
- [LanguageSessionStatusPolicy.cs](../../../../src/Features/Language/Application/LanguageSessionStatusPolicy.cs)
- [LanguageServerBinaryLocator.cs](../../../../src/Features/Language/Infrastructure/Lsp/LanguageServerBinaryLocator.cs)
- [LanguageDocumentBridge.cs](../../../../src/Features/Language/Application/LanguageDocumentBridge.cs)
- [LanguageDocumentSyncPolicy.cs](../../../../src/Features/Language/Application/LanguageDocumentSyncPolicy.cs)
- [LanguageCommandAvailability.cs](../../../../src/Features/Language/Application/LanguageCommandAvailability.cs)
- [LanguageDiagnosticsService.cs](../../../../src/Features/Language/Application/LanguageDiagnosticsService.cs)
- [ProblemsViewModel.cs](../../../../src/Features/ProjectSystem/Presentation/ProblemsViewModel.cs)
- [ProblemsPanel.cs](../../../../src/Features/ProjectSystem/Presentation/ProblemsPanel.cs)
- [ProblemItemViewModel.cs](../../../../src/Features/ProjectSystem/Presentation/ProblemItemViewModel.cs)
- [BottomPanelHost.cs](../../../../src/App/Shell/BottomPanelHost.cs)
- [LanguageCompletionService.cs](../../../../src/Features/Language/Application/LanguageCompletionService.cs)
- [LanguageHoverService.cs](../../../../src/Features/Language/Application/LanguageHoverService.cs)
- [LanguageNavigationService.cs](../../../../src/Features/Language/Application/LanguageNavigationService.cs)
- [LanguageSymbolService.cs](../../../../src/Features/Language/Application/LanguageSymbolService.cs)
- [LanguageFormattingService.cs](../../../../src/Features/Language/Application/LanguageFormattingService.cs)
- [LanguageFormattingPolicy.cs](../../../../src/Features/Language/Application/LanguageFormattingPolicy.cs)
- [LanguageNavigationPolicy.cs](../../../../src/Features/Language/Application/LanguageNavigationPolicy.cs)
- [LanguageSymbolPolicy.cs](../../../../src/Features/Language/Application/LanguageSymbolPolicy.cs)
- [LanguageCompletionTriggerPolicy.cs](../../../../src/Features/Language/Application/LanguageCompletionTriggerPolicy.cs)
- [LanguageHoverTriggerPolicy.cs](../../../../src/Features/Language/Application/LanguageHoverTriggerPolicy.cs)
- [EditorLanguageInputViewModel.cs](../../../../src/Features/Editor/Presentation/EditorLanguageInputViewModel.cs)
- [EditorCompletionPopup.cs](../../../../src/Features/Editor/Presentation/EditorCompletionPopup.cs)
- [EditorHoverPopup.cs](../../../../src/Features/Editor/Presentation/EditorHoverPopup.cs)
- [EditorLanguagePickerPopup.cs](../../../../src/Features/Editor/Presentation/EditorLanguagePickerPopup.cs)
- [SettingsModel.cs](../../../../src/Features/Settings/Domain/SettingsModel.cs)
  (`FormatOnSave` default false)
- [SettingsPanelView.cs](../../../../src/Features/Settings/Presentation/SettingsPanelView.cs)

### 2.3 Tests (corroboration only; not verdict authority)

- Editor / search / fold / tab tests under `tests/Zaide.Tests/Features/Editor/`
  (not executed)
- Language / Problems tests under `tests/Zaide.Tests/Features/Language/` and
  project-system tests (not executed)

---

## 3. Fourteen-row verdict table

| id | a2_wiring_verdict | Summary |
|----|-------------------|---------|
| `A1-FN-01` | **Wired** | Single tree open pathway → `OpenFileRequested` → `EditorTabViewModel.OpenFileCommand` opens/activates tabs; shared `EditorView` binds active tab; dirty indicator via `DisplayName` (`●` prefix); save via registry `file.save` / default `Ctrl+S` → `SaveActiveTabCommand` → per-tab `SaveCommand` → `IFileService.WriteAllTextAsync` with IO exception → `LastSaveError` / status `"Save failed: …"`; TextMate installed with extension→grammar selection. Unsupported extensions surface status without opening. Runtime paint of syntax tokens and keyboard delivery are A3. |
| `A1-FN-02` | **Wired-with-gap** | Single open pathway (`RequestOpenFileCommand` + Enter + DoubleTapped + context Open); copy absolute / relative path via context menu → `CopyToClipboard` Interaction → platform clipboard; left `GridSplitter` present. **Gap:** left panel column is `MinWidth = 180`, `MaxWidth = 320` (default 260), not the Phase 1.1 / goal-matrix **180–500px** range. Folder open itself is WO-owned; FN re-confirms only the navigation/copy/splitter claims. |
| `A1-FN-03` | **Wired** | Registry commands `editor.find`/`replace`/`findNext`/`findPrevious` (+ replace next/all); literal case-sensitive-by-default search; Find Next/Previous wrap; zero-match `"No matches found"` on search bar and status bar; Replace All uses one AvaloniaEdit undo group; tab switch resets search via `ActiveDocument` / `ActiveDocumentId`. Scroll-into-view is source-wired (`SetSelection` → `ScrollToLine`). Live keyboard/focus on X11 is A3. |
| `A1-FN-04` | **Wired** | Syntax-neutral brace folding via `BraceFoldingStrategy` + `FoldingOperations` / `FoldingManager`; install on tab content switch; `Clear` on inactive; commands `editor.foldToggle` / `foldAll` / `unfoldAll` registered (default gestures **unbound** — palette-reachable); fold status messages reach status bar. Pointer fold-margin behavior is AvaloniaEdit-provided (A3). |
| `A1-FN-05` | **Wired** | Tab commands `tab.next` / `previous` / `close` / `closeOthers` / `closeAll` with default gestures where specified; `MoveTab` + `EditorTabBar` pointer drag reorder; dirty/active via `DisplayName` / active-tab highlight; dirty close uses `ConfirmClose` → `UnsavedDialog` (`true` save / `false` discard / `null` cancel); save-failure aborts close and sets `LastSaveError`. |
| `A1-FN-06` | **Wired** | Status bar projects active document name, caret + optional `Sel N`, language label from extension, transient `StatusMessage` from save/search/fold/open/language feedback; clears status on active-tab switch. Search outcomes and save failures are piped. Language-session short label is additional Phase 10 projection (not a FN-06 gap). |
| `A1-FN-08` | **Wired-with-gap** | Project-context-eligible session → `csharp-ls` → `LanguageDocumentBridge` → `LanguageDiagnosticsService` → `ProblemsViewModel` / `ProblemsPanel` with `file:line` attribution and navigate-on-activate. Language diagnostics are separate from build diagnostics (retained across build lifecycle in the merged list). **Gaps:** publishDiagnostics accepted **only for open/tracked** documents; no-project / ambiguous / failed project map to generic unavailable Problems status (no Ambiguous-specific copy); server binary not bundled — missing binary surfaces install text in Problems and `"C# · Failed"` on status bar, but cold-profile success requires external `csharp-ls` and eligible `SingleProject`/`Selected` context (ambiguous multi-project blocked per WO-02). |
| `A1-FN-09` | **Wired** | Explicit `editor.triggerSuggest` (`Ctrl+Space`) + automatic trigger-character path; completion popup projection; commit replaces computed range; failed/cancelled/unsupported dismiss without mutating (commit only on Ready selection). Gated on Ready session + `.cs` + `CompletionSupported`. Live server item quality is A3. |
| `A1-FN-10` | **Wired-with-gap** | Hover service + `EditorHoverPopup` wired; failures dismiss to Idle without document mutation. **Gap vs goal wording “Hover over”:** production schedules hover on **caret dwell** (450 ms after caret/text events), not pointer-hover over a glyph. Empty/unsupported hover is silent (no status text) — consistent with “no hover” failure mode, but pointer-only exploration will not trigger. |
| `A1-FN-11` | **Wired** | `editor.goToDefinition` (`F12`) → navigation service → single-result auto-navigate via `OpenFileCommand` + `RequestNavigate`; multi-result picker; feedback messages (`No definition found.`, unavailable/failed) → status bar. Gated on Ready + definition capability + eligible `.cs`. |
| `A1-FN-12` | **Wired** | `editor.documentSymbol` (`Ctrl+Shift+O`) → document-symbol picker; empty/unavailable/failed feedback; accept navigates in-file / open path. Gated on Ready + document-symbol capability. |
| `A1-FN-13` | **Wired** | `workbench.symbol` (`Ctrl+T`) → workspace-symbol picker with debounced query; empty/unavailable/failed feedback; accept opens target file. Gated on Ready + workspace-symbol capability (not active-document-bound for command availability). |
| `A1-FN-14` | **Wired** | `editor.formatDocument` (`Ctrl+Shift+I`) → `LanguageFormattingService.FormatDocumentAsync` → `ApplyFormattedDocument` (one undo group, caret remap); non-accepted outcomes leave text unchanged and set feedback; success/failure messages → status bar via `FeedbackMessage`. |
| `A1-FN-15` | **Wired-with-gap** | Settings schema default `FormatOnSave: false`; Settings UI checkbox; save path calls `FormatDocumentAsync` then writes current text; disabled skips format. **Gaps:** format failure/unavailable/cancel during save is **swallowed** (save continues; no format-failure status — only save success/failure); Format-on-Save applies via `Document.Content` rather than `ApplyFormattedDocument` (different undo/caret path than FN-14); still requires open eligible `.cs` + Ready formatting capability for any format effect. |

Verdict definitions: [AUDIT_PLAN.md §2](../AUDIT_PLAN.md#2-audit-phase-decomposition).

**Retired ID note:** `A1-FN-07` was merged into `A1-SC-02` (command palette) in
A1 round 2 and remains retired. It is **not** a verdict row in this slice.

---

## 4. End-to-end production-path maps

Legend: **T** = type/contract · **R** = production DI · **C** = production caller ·
**U** = user-reachable · **P** = user-visible result/failure · **A3** = runtime
unproven without clean-profile smoke.

### 4.0 Shared cold-profile prerequisites (FN-08…15)

Default cold success for language intelligence assumes **all** of:

1. User opened a folder (WO open path).
2. Project context is `SingleProject` or `Selected` with a non-null candidate
   (auto-select only for exactly one supported root candidate; multi-project
   `Ambiguous` has **no** user picker — prior WO-02).
3. `LanguageServerBinaryLocator` finds `csharp-ls` on `PATH` or
   `~/.dotnet/tools/csharp-ls` (not packaged with the app).
4. Session reaches `LanguageSessionState.Ready`.
5. Active document path is eligible (`.cs` only per
   `LanguageDocumentSyncPolicy`).
6. Server advertises the feature capability used by the command.

DI registration and eager resolve in `App.axaml.cs` start subscriptions; they
do **not** equal user-visible intelligence without the above.

### 4.1 `A1-FN-01` — editor open / tabs / save / dirty / TextMate

```text
[tree open]
  FileTreeView Enter | DoubleTapped | context "Open"
  → RequestOpenFileCommand(FileTreeNode)
  → OpenFileRequested subject
  → MainWindowActivationHost:
       if SupportedFileTypes.GetUnsupportedMessage(path) is null
         → EditorTabs.OpenFileCommand.Execute(path)
         → status "Opened: {name}" on success
       else status = unsupported message

[OpenFileCommand]
  normalize path; if tab exists → ActiveTab = existing
  else FileService.ReadAllTextAsync
    IO/Unauthorized → LastOpenError; false
    else Workspace.OpenDocument → EditorSessionFactory.Create → OpenTabs.Add; ActiveTab

[edit / dirty]
  EditorView TextChanged → EditorViewModel.TextContent → Document.Content → IsDirty
  DisplayName = "● {FileName}" when dirty; EditorTabBar binds DisplayName

[save]
  file.save (Ctrl+S) → MainWindowViewModel.SaveActiveTabCommand
  → activeTab.SaveCommand → SaveAsync
      optional FormatOnSave (FN-15)
      FileService.WriteAllTextAsync → MarkClean
      catch IO/Unauthorized → Document.RecordSaveError → false
  → StatusText "Saved: …" or "Save failed: …"
  LastSaveError also projected from tab manager (close-with-save path)

[syntax]
  EditorView InstallTextMate(DarkPlus); ApplyFileMode SetGrammar(scope by extension)
```

| Layer | Status |
|-------|--------|
| 1. Contract / types | Present — `IFileService`, `Document`, `EditorTabViewModel` |
| 2. DI | Present — `AddZaideEditor` singleton tab manager / session factory |
| 3. Caller | Present — activation host open subscription; `file.save` registry |
| 4. User reachability | **Yes** (tree + save command/gesture) |
| 5. Failure visibility | **Yes** for open/save (status bar); unsupported type status |
| 6. TextMate paint | Source-wired; **A3** for rendered highlighting |

### 4.2 `A1-FN-02` — splitter, single open pathway, copy paths

```text
[single open pathway]
  Only RequestOpenFileCommand publishes OpenFileRequested
  (context Open, Enter, DoubleTapped all call it)

[copy path]
  context "Copy Path" → CopyPathCommand → CopyToClipboard.Handle(FullPath)
  context "Copy Relative Path" → Path.GetRelativePath(RootPath, FullPath)
  FileTreeView handler → TopLevel.Clipboard.SetTextAsync

[splitter]
  MainLayoutBuilder column 1: Width=260, MinWidth=180, MaxWidth=320
  GridSplitter column 2 between left panel and townhall
```

| Concern | Source-proven | Gap? |
|---------|---------------|------|
| Single open pathway | Yes | No |
| Enter + double-click | Yes | Delivery A3 |
| Copy absolute / relative | Yes | Clipboard success A3 |
| Splitter 180–500 | **No** — max **320** | **Yes** vs goal/Phase 1.1 |

### 4.3 `A1-FN-03` — search / replace

```text
[open find/replace]
  editor.find (Ctrl+F) / editor.replace (Ctrl+H)
  → EditorSearchViewModel IsVisible; SearchBar shows
  → PerformSearchWithSelection → SetSelection + StatusMessage

[find next/prev]
  F3 / Shift+F3 → wrap indices → SelectCurrentMatch → ScrollToLine

[replace all]
  ReplaceAllMatches → UndoStack.StartUndoGroup … EndUndoGroup
  StatusMessage "Replaced N occurrence(s)"

[tab switch]
  MainWindow sets ActiveDocument / ActiveDocumentId → Reset/Dismiss
```

### 4.4 `A1-FN-04` — folding

```text
[tab content bind]
  EditorView TextContent subscription on VM change
  → FoldingOperations.Install(text) | Clear()

[commands]
  editor.foldToggle / foldAll / unfoldAll (unbound defaults)
  → FoldingEditor (shared EditorView.Folding)
  → FoldStatusMessage → status bar

[tab switch discard]
  Install clears prior sections; Clear on null/diff tab
```

### 4.5 `A1-FN-05` — tab lifecycle / reorder / unsaved

```text
[commands]
  tab.next (Ctrl+Tab), tab.previous (Ctrl+Shift+Tab)
  tab.close (Ctrl+W, Ctrl+F4), tab.closeOthers, tab.closeAll

[reorder]
  EditorTabBar drag → TabMoveRequested → MoveTab(from,to)

[unsaved close]
  CloseTabAsync if dirty → ConfirmClose Interaction
  → UnsavedDialog ShowDialog<bool?>
  true → SaveCommand; fail → LastSaveError; abort close
  false → close without save
  null → cancel
```

### 4.6 `A1-FN-06` — status bar truth

```text
StatusBarViewModel:
  DocumentText ← ActiveTab.FileName | "—"
  CaretText ← Ln/Col [| Sel N]
  StatusMessage ← MainWindowViewModel.StatusText
  LanguageText ← extension map
  LanguageIntelligenceText ← LanguageSessionStatusPolicy (Phase 10)

MainWindowActivationHost:
  ActiveTab change → clear StatusText
  LastSaveError / LastOpenError / FoldStatusMessage → StatusText
MainWindow:
  search StatusMessage (non-empty) → StatusText
  language FeedbackMessage → StatusText
SaveActiveTabAsync sets Saved/Save failed with stale-tab guard
```

### 4.7 `A1-FN-08` — diagnostics → Problems

```text
IProjectContextService.WhenChanged
  → LanguageSessionService reconcile
  → eligible SingleProject|Selected
  → resolve csharp-ls → start CsharpLsSession → Ready | Failed(MissingServerBinary|…)

LanguageDocumentBridge: Workspace document open/change/close → LSP didOpen/didChange/didClose (.cs only)

csharp-ls publishDiagnostics
  → LanguageDiagnosticsService (only if TryGetOpenDocument + version/generation OK)
  → ProblemsViewModel language list + build list merge
  → ProblemsPanel DisplayText "{sev}: {msg} — {file}:{line}:{col}"

Navigate: double-click/Enter → OpenFileCommand + RequestNavigate(range)

Bottom panel: "Problems" tab → SwitchToProblemsBottomCommand
```

### 4.8 `A1-FN-09` / `A1-FN-10` — completion / hover

```text
[completion]
  Ctrl+Space → TriggerSuggest → RequestExplicit
  TextInput typed char → OnTextEdited → RequestAutomatic(if trigger char)
  Ready snapshot → EditorCompletionPopup
  Commit → replace range insert; Dismiss on empty/fail

[hover]
  OnCaretMoved / OnTextEdited → LanguageHoverService.Schedule (450ms dwell)
  Ready visible → EditorHoverPopup.SetContent
  else Idle / closed popup (silent)
```

### 4.9 `A1-FN-11` / `A1-FN-12` / `A1-FN-13` — definition / symbols

```text
F12 → GoToDefinition → LanguageNavigationService
  single → OpenFileCommand + RequestNavigate
  multi → definition picker
  empty/fail → FeedbackMessage → status bar

Ctrl+Shift+O → Document symbols → document picker / feedback
Ctrl+T → Workspace symbols → query debounce → workspace picker / feedback
```

### 4.10 `A1-FN-14` / `A1-FN-15` — format / format on save

```text
[FN-14]
  Ctrl+Shift+I → FormatDocumentAsync
  accepted + text change → ApplyFormattedDocument (undo group + caret map)
  feedback → FeedbackMessage → status bar
  non-accepted → no text mutation

[FN-15]
  Settings FormatOnSave checkbox (default false)
  SaveAsync:
    if FormatOnSave && services present
      FormatDocumentAsync → if HasTextChange Document.Content = FormattedText
      catch cancel/any → continue
    WriteAllTextAsync → MarkClean
  format errors do not set StatusText (save outcome only)
```

---

## 5. User reachability matrix

| Goal | User entry (source) | Reachable without DI trivia? |
|------|---------------------|------------------------------|
| Open file into editor | Tree Enter / double-click / context Open | **Yes** (after folder open) |
| Switch / close tabs | Click tab; `Ctrl+W`; tab commands | **Yes** |
| Save / dirty | Edit; `Ctrl+S` / `file.save` | **Yes** |
| Syntax highlighting | Automatic on open by extension | **Yes** (wiring); paint A3 |
| Splitter resize | Drag left splitter | **Yes** (bounds ≠ 500) |
| Copy path / relative | Tree context menu | **Yes** when folder open (relative) |
| Find / replace | `Ctrl+F` / `Ctrl+H` / palette | **Yes** with active document |
| Folding | Palette fold commands; fold margin | **Yes** (gestures unbound) |
| Tab reorder | Pointer drag on tab bar | **Yes** |
| Unsaved confirm | Close dirty tab / close all/others | **Yes** |
| Status bar doc/caret/sel/search/save | Automatic projections | **Yes** |
| Problems diagnostics | Bottom “Problems” after Ready + open `.cs` | **Conditional** (cold prereqs) |
| Completion | `Ctrl+Space` / trigger chars | **Conditional** |
| Hover info | Caret dwell on eligible `.cs` | **Conditional**; not pointer-hover |
| Go to Definition | `F12` | **Conditional** |
| Document / workspace symbols | `Ctrl+Shift+O` / `Ctrl+T` | **Conditional** |
| Format Document | `Ctrl+Shift+I` | **Conditional** |
| Format on Save | Settings checkbox + save | **Yes** for toggle; format effect conditional |

---

## 6. Source-proven vs runtime-unproven

| Claim | Class |
|-------|--------|
| Tree → single open pathway → editor tabs | Source-proven |
| Save / dirty / save-error status path | Source-proven |
| TextMate install + grammar selection | Source-proven |
| Splitter max width 320 (not 500) | Source-proven |
| Copy path / relative clipboard Interaction | Source-proven |
| Search literal/case/wrap/replace-all undo group | Source-proven |
| Search reset on tab switch | Source-proven |
| Folding install/clear on tab switch | Source-proven |
| Tab lifecycle commands + unsaved dialog handler | Source-proven |
| Tab drag reorder → `MoveTab` | Source-proven |
| Status bar document/caret/selection/status clear on tab switch | Source-proven |
| Language DI + eager resolve | Source-proven |
| Session eligibility SingleProject\|Selected only | Source-proven |
| csharp-ls not bundled; PATH / ~/.dotnet/tools discovery | Source-proven |
| Diagnostics only for open tracked documents | Source-proven |
| Problems panel navigation path | Source-proven |
| Language vs build diagnostics dual lists | Source-proven |
| Completion/hover/definition/symbol/format command IDs + defaults | Source-proven |
| Hover scheduled from caret, not pointer | Source-proven |
| Format Document apply + non-mutation on failure | Source-proven |
| FormatOnSave default false + silent format failure on save | Source-proven |
| TextMate visual quality | **Runtime-unproven (A3)** |
| Keyboard / gesture delivery on live desktop | **Runtime-unproven (A3)** |
| Clipboard paste verification | **Runtime-unproven (A3)** |
| Live csharp-ls initialize / diagnostic content | **Runtime-unproven (A3)** |
| Completion item filtering quality / hover content | **Runtime-unproven (A3)** |
| Definition/symbol navigation across real projects | **Runtime-unproven (A3)** |
| Format style fidelity | **Runtime-unproven (A3)** |

---

## 7. Contradiction / reconciliation notes

1. **Phase 1.1 “GridSplitter 180–500px” vs production `MaxWidth = 320`**
   Goal matrix and PHASES.md still claim 500px. Production left-panel column
   clamps at 320. Verdict **Wired-with-gap** for FN-02. Do not treat V1
   closeout as proof of the 500px bound.

2. **Open-folder error visibility (WO) vs open-file error visibility (FN)**
   File-tree `StatusText` remains unbound (WO-01). Editor open/save errors
   **are** projected (`LastOpenError` / `LastSaveError` / `SaveActiveTabAsync`).
   Different surfaces; do not conflate.

3. **Ambiguous project blocks all FN-08…15 intelligence**
   Prior WO-02: no `SelectProject` UI; status mislabels Ambiguous as
   `"Project error"`. Language session maps non-eligible context to
   `Unavailable`. Problems shows generic `"Language intelligence unavailable."`
   — truthful that intelligence is off, not specific that the root is
   multi-project.

4. **Phase 10 manual evidence ≠ cold-profile proof**
   M3–M6 evidence files are historical developer runs with a prepared
   environment. A2 does not re-run them. A3 must use a disposable profile
   and disposable project, and must install or deliberately omit `csharp-ls`.

5. **Hover wording vs implementation**
   Matrix “Hover over a C# symbol” reads like pointer hover. Production is
   caret-dwell LSP hover. A2 records **Wired-with-gap** rather than Missing
   because a hover surface and request path exist.

6. **Format Document vs Format on Save apply paths**
   FN-14 uses view-layer `ApplyFormattedDocument` (undo group + caret map +
   feedback). FN-15 mutates `Document.Content` inside save and suppresses
   format exceptions. Enabling Format on Save does not reuse the FN-14
   success/failure status contract.

7. **Diagnostics require an open document**
   `LanguageDiagnosticsService` drops notifications when
   `TryGetOpenDocument` fails. Opening a folder with a compile error but
   never opening the `.cs` file will not populate Problems from LSP. Goal
   scenario “open project, observe Problems” implicitly needs document open
   / server open notifications.

8. **DI / tests / closeouts are not reachability**
   `AddZaideLanguage`, eager resolve, and passing unit tests do not prove a
   user on a clean profile sees completion. Session Ready + binary + project
   eligibility are the real gates.

9. **Retired `A1-FN-07`**
   Palette / command discovery belongs to SC journey (`A1-SC-02`). Fold and
   editor commands being palette-reachable is noted under SC, not re-audited
   as FN-07.

---

## 8. A3 constraints only (not executed)

A3 for this journey **must** use a disposable isolated profile
(`XDG_CONFIG_HOME` absolute temp directory established **before** process
start). Never the real user profile, real settings/secrets, or a real
developer workspace under the user’s home tree.

Suggested disposable-profile scenarios (description only):

1. **FN-01 editor core:** disposable folder with `.cs` and an unsupported
   binary-like extension; open via Enter and double-click; edit; dirty
   indicator; `Ctrl+S`; force save failure if environment allows; observe
   TextMate paint on `.cs`.
2. **FN-02 splitter/copy:** resize left panel; confirm clamp below 500px;
   Copy Path / Copy Relative Path into a disposable text target.
3. **FN-03 search:** open file; `Ctrl+F`; zero-match; Find Next wrap;
   Replace All + single undo.
4. **FN-04 folding:** open multi-brace file; collapse via margin and/or
   palette; switch tabs; confirm folds reset.
5. **FN-05 tabs:** multiple tabs; next/previous; drag reorder; close dirty
   with cancel/save/discard.
6. **FN-06 status bar:** caret/selection/search/save messages; tab switch
   clears stale status.
7. **FN-08…14 with `csharp-ls` present:** single `.csproj` disposable root;
   open erroneous `.cs`; Problems list + navigate; completion; caret-dwell
   hover; F12; symbols; Format Document.
8. **FN-08 without `csharp-ls`:** same project; confirm Failed / install
   message discoverability; commands disabled or feedback unavailable.
9. **FN-08 ambiguous root:** two root `.csproj` (no picker); confirm no
   language readiness.
10. **FN-15 Format on Save:** enable in settings; save formatted/unformatted
    `.cs` with Ready session; disable and confirm no format; optionally
    disable server and confirm silent format skip + successful save.

Production DI is allowed only when the disposable config root is set first.

**A3 is not executed in this session.**

---

## 9. Next recommended A2 slice

**Next recommended A2 slice:** `A2_SEARCH_AND_COMMAND_DISCOVERY`

| Item | Value |
|------|-------|
| Slice name | `A2_SEARCH_AND_COMMAND_DISCOVERY` |
| Goal rows | `A1-SC-01` … `A1-SC-03` |
| Evidence file | `docs/audits/v1-v3-product-reality/evidence/A2_SEARCH_AND_COMMAND_DISCOVERY.md` |
| Status in this session | **Explicitly not started** — file not created; no SC verdicts assigned |

Rationale: matrix journey order after File Navigation and Editing is Search
and Command Discovery. Many FN commands are registry-backed; SC owns palette,
rebinding, and the full Phase 9 command-registration promise (including that
unbound fold commands remain discoverable).

---

## 10. Verification and working-tree closeout

### 10.1 Required content checklist

| Required section | Present |
|------------------|---------|
| 1. Audit identity, baseline, safety | Yes |
| 2. Sources inspected | Yes |
| 3. Fourteen-row verdict table (each in-scope FN id once) | Yes |
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
| DI registration ≠ user reachability | Yes (language services, session) |
| Tests / phase closeouts ≠ production wiring proof | Yes |
| Project eligibility / csharp-ls prereqs explicit | Yes |
| No real profile / workspace access | Yes |
| Prior-slice verdicts not reassigned | Yes |
| Each of `A1-FN-01`…`06` and `08`…`15` exactly once | Yes |
| Retired `A1-FN-07` not used as a verdict | Yes |
| No runtime claims from source alone | Yes |
| No production code / AUDIT_PLAN / GOAL_MATRIX edits | Yes |

### 10.3 Closeout verification commands (post-write)

Executed after writing this file only:

- Confirm exactly one untracked evidence file:
  `docs/audits/v1-v3-product-reality/evidence/A2_FILE_NAVIGATION_AND_EDITING.md`
- Confirm no tracked modifications
- Whitespace check for the **untracked** file:

  ```bash
  git diff --no-index --check /dev/null \
    docs/audits/v1-v3-product-reality/evidence/A2_FILE_NAVIGATION_AND_EDITING.md
  ```

  Exit status **1 is expected** because the files differ; there must be
  **no whitespace-diagnostic output**.
- Relative Markdown paths and fragment links resolve against this tree
- Primary verdicts (14): FN-01 Wired; FN-02 Wired-with-gap; FN-03 Wired;
  FN-04 Wired; FN-05 Wired; FN-06 Wired; FN-08 Wired-with-gap; FN-09 Wired;
  FN-10 Wired-with-gap; FN-11 Wired; FN-12 Wired; FN-13 Wired; FN-14 Wired;
  FN-15 Wired-with-gap
- `A2_SEARCH_AND_COMMAND_DISCOVERY` not created / not started
- `AUDIT_PLAN.md` / `GOAL_MATRIX.md` not edited (Codex synchronizes after publish)

---

*End of `A2_FILE_NAVIGATION_AND_EDITING` evidence. Stop for re-audit. No commit or push.*
