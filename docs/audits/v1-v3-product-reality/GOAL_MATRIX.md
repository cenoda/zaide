# V1–V3 Product Reality Audit — Goal Matrix

**Audit name:** `v1-v3-product-reality`
**Owner folder:** `docs/audits/v1-v3-product-reality/`
**Phase:** A1 accepted (2026-07-30); **A2 in progress** (not complete as
a whole). See [§17](#17-a1-closeout-and-status),
[§17.8](#178-current-a2-progress), and
[A1_ACCEPTANCE.md](./A1_ACCEPTANCE.md).
**Audit plan:** [AUDIT_PLAN.md](./AUDIT_PLAN.md)
**Scope:** Every user-observable promise extracted from V1, V2, and V3
roadmaps and the implementation plans and `TOFIX.md` files of the phases
they cover. A1 does not assign implementation verdicts. A2 inspects
production wiring for each row; thirteen A2 slices are complete and
published (see [§17.8](#178-current-a2-progress)).

---

## 0. Reading the Matrix

| Field | Meaning |
|-------|---------|
| `id` | Stable audit identifier. Format: `A1-<journey-key>-<nn>` per [AUDIT_PLAN.md §5](./AUDIT_PLAN.md#5-goal-matrix-schema). The `<journey-key>` is one of `FL, WO, FN, SC, BR, DB, TR, GT, TH, AC, AS, TP, MR, TC`. The `XX` key is reserved for rows that cannot be translated into a user-observable promise and is **not** counted toward the user-goal total. `nn` is a zero-padded sequence number scoped to the journey. IDs are stable and never reused; a retired ID (one whose row was removed, merged into another row, or moved to a different journey) leaves a permanent gap in the original journey. |
| `journey` | One of the 14 journeys in [AUDIT_PLAN.md §4](./AUDIT_PLAN.md#4-inventory-scope--user-journeys). |
| `roadmap_version` | `V1`, `V2`, or `V3`. |
| `phase` | Phase or sub-phase that owns the promise. |
| `source_document` | Clickable repo-relative markdown link plus the exact section/heading cited. |
| `promised_outcome` | The user-observable outcome the document claims. A1 records the document's claim, not whether the claim is implemented. |
| `user_entry_point` | The user action or surface the document names. |
| `success_condition` | The observable behavior that would prove the promise is delivered (recorded from the document, not a verdict). |
| `failure_recovery` | The documented failure or recovery behavior the user should see when the promise fails. |
| `claimed_completion_evidence` | Clickable repo-relative links to existing evidence files; "no evidence file cited" if the document records a claim without naming a file. |
| `likely_a2_target` | Best current guess at the production code/wiring path A2 will inspect. |
| `planned_a3_scenario` | The disposable-profile smoke scenario A3 will execute. |

A1 does not assign implementation verdicts. A1 does not conclude that a
documented promise is or is not implemented. A1 reports what the
documents claim and where the evidence file lives.

Rows in [§15](#15-promises-that-cannot-yet-be-translated-into-user-behavior)
are recorded because the document language suggests a user behavior but
A1 cannot yet map the promise to a user entry point. They use the `XX`
journey key and are **not** counted in the user-goal total.

Duplicate promises across phases are merged into one row; the merged
source list is recorded in `source_document`. A composite promise that
bundles several independently-verifiable outcomes is decomposed into
separate rows so A2 can give one verdict per row.

---

## 1. First Launch and Settings

| id | journey | roadmap | phase | source_document | promised_outcome | user_entry_point | success_condition | failure_recovery | claimed_completion_evidence | likely_a2_target | planned_a3_scenario |
|----|---------|---------|-------|-----------------|------------------|------------------|-------------------|------------------|------------------------------|------------------|---------------------|
| A1-FL-01 | first launch | V1 | Phase 0 | [V1 §"Phase 0: Foundation & Layout"](../../roadmap/PHASES.md#phase-0-foundation--layout) | App launches into a 3-panel grid layout (left sidebar, center, right agent area) with bottom panel placeholder and `Ctrl+\`` toggle. | Launch the application; press `Ctrl+\``. | Visible 3-panel grid; bottom panel appears/disappears on `Ctrl+\``; window chrome (title, min, max, close) functional. | App still launches; toggle does nothing if surface is missing. | V1 closeout ([V1 V1 Closeout](../../roadmap/PHASES.md#v1-closeout) — 817 tests passing). | `src/App/Shell/MainWindow.axaml.cs`, `MainWindow.axaml`, status bar, bottom panel toggle seam. | Cold launch with disposable profile: layout renders, `Ctrl+\`` toggles the bottom panel. |
| A1-FL-02 | first launch | V1 | Phase 0 | [V1 §"Phase 0"](../../roadmap/PHASES.md#phase-0-foundation--layout) | Semi.Avalonia (dark) theme applied; "Ayaka Violet" palette defined. | Launch the application. | Dark Semi.Avalonia theme visible. | App falls back to default theme. | V1 closeout ([V1 V1 Closeout](../../roadmap/PHASES.md#v1-closeout)); refactor-4 visual polish M7 regression sweep. | `src/UI/DesignSystem/`, palette definitions, theme resources. | Cold launch: theme renders; check colors match the palette definition. |
| A1-FL-03 | first launch | V2 | Phase 8.1 | [V2 §"Phase 8 — Core Platform and Settings" Exit Direction](../../roadmap/V2.md#phase-8--core-platform-and-settings); [Phase 8.1 plan §D1–D4](../../phases/v2/phase-8/phase-8.1/IMPLEMENTATION_PLAN.md) | Versioned application settings schema; `SettingsService` with explicit load, save, validation, defaults; JSON at `{XDG_CONFIG_HOME}/zaide/settings.json`; schema v1 initial. | Open settings surface; change a value; restart. | Settings persist; corrupt file falls back to last-known-good; future schema versions do not overwrite. | Interrupted write leaves prior settings intact; corrupt file falls back to defaults with log. | Phase 8.1 closeout (umbrella [Phase 8 plan §"Sub-Phase Decision"](../../phases/v2/phase-8/IMPLEMENTATION_PLAN.md#sub-phase-decision-m0) M0). | `src/Features/Settings/Domain/SettingsModel.cs`, `SettingsService`, secret store. | Disposable profile: write a setting, restart, observe persistence; corrupt the file, observe recovery. |
| A1-FL-04 | first launch | V2 | Phase 8.1 | [Phase 8 plan §D1, §D4](../../phases/v2/phase-8/IMPLEMENTATION_PLAN.md); [V2 §"Phase 8 — Core Platform and Settings" Boundaries](../../roadmap/V2.md#phase-8--core-platform-and-settings) | API keys are not written as plaintext into the ordinary settings file; secret handling boundary uses a separate file with restricted permissions; environment-variable fallback preserved. | Configure LLM/agent credentials. | API key is never visible in `settings.json`; secret file has restricted permissions; env-var fallback still works. | Missing env var + no secret file = no successful send. | Phase 8.1 closeout (per [Phase 8 plan §"Sub-Phase Decision"](../../phases/v2/phase-8/IMPLEMENTATION_PLAN.md#sub-phase-decision-m0)). | Secret store file, settings migration, env-var fallback in `Program.cs`. | Disposable profile: store a key, grep `settings.json` for the key, observe absence. |
| A1-FL-05 | first launch | V2 | Phase 8.1 | [V2 §"Phase 8 — Core Platform and Settings" Exit Direction](../../roadmap/V2.md#phase-8--core-platform-and-settings) | Editor defaults (font, size, whitespace, indentation preferences) are user-configurable, not hardcoded. | Settings surface; open a file. | New editor windows use configured font/size/whitespace. | Inherits last-known-good. | Phase 8.1 closeout (per [Phase 8 plan §"Sub-Phase Decision"](../../phases/v2/phase-8/IMPLEMENTATION_PLAN.md#sub-phase-decision-m0)). | `EditorView`, `TerminalRenderControl`, `TextStyles` injection of `ISettingsService`. | Disposable profile: change editor font, reopen editor, observe change. |
| A1-FL-06 | first launch | V2 | Phase 13 | [V2 §"Phase 13 — Release Hardening"](../../roadmap/V2.md#phase-13--release-hardening); [Phase 13 M5 closeout](../../phases/v2/phase-13/M5_RELEASE_CLOSEOUT_EVIDENCE.md) | Performance and reliability are measurable; locked budgets pass; settings/LSP/process/DAP errors recover. | Run baseline measurements; trigger a failure. | Locked budgets documented and met; recovery paths exercised. | Documented failure paths return to a defined state. | [Phase 13 M5 closeout](../../phases/v2/phase-13/M5_RELEASE_CLOSEOUT_EVIDENCE.md). | Measurement harness, settings/LSP/DAP recovery paths. | Out of A3 scope (A2 wiring only). |

---

## 2. Workspace / Project Opening

| id | journey | roadmap | phase | source_document | promised_outcome | user_entry_point | success_condition | failure_recovery | claimed_completion_evidence | likely_a2_target | planned_a3_scenario |
|----|---------|---------|-------|-----------------|------------------|------------------|-------------------|------------------|------------------------------|------------------|---------------------|
| A1-WO-01 | workspace | V1 | Phase 1 / 1.1 / 1.2 | [V1 §"Phase 1"](../../roadmap/PHASES.md#phase-1-file-tree-sidebar); [V1 §"Phase 1.1"](../../roadmap/PHASES.md#phase-11-file-tree-polish); [V1 §"Phase 1.2"](../../roadmap/PHASES.md#phase-12-file-tree-essentials) | Open a folder via `Ctrl+O` or context menu; file tree shows the folder; ignore list excludes `node_modules`, `bin`, `obj`, `.git`; right-click context menu offers Open/Expand All/Collapse All/New File/New Folder; show hidden files toggle `Ctrl+Shift+H`. | Open a folder; toggle hidden files; create a new file. | Tree populates; hidden files toggle works; ignore list applied; new file/folder works. | Folder picker error handled (`OpenFolderCommand`); open-folder failure does not crash. | Phase 1 + 1.1 + 1.2 closeout (V1 closeout, [V1 V1 Closeout](../../roadmap/PHASES.md#v1-closeout)). | `FileTreeViewModel`, `FileTreeView`, `IFileTreeService`, watcher. | Disposable profile: open a folder with `bin/`, `node_modules/`, `.git/` present; confirm ignore list applied; toggle hidden files. |
| A1-WO-02 | workspace | V2 | Phase 8.3 | [V2 §"Phase 8"](../../roadmap/V2.md#phase-8--core-platform-and-settings); [Phase 8.3 plan](../../phases/v2/phase-8/phase-8.3/IMPLEMENTATION_PLAN.md) | Authoritative C# workspace/project context service with discovery, selection, load/unload/reload lifecycle; structured no-project, unsupported, and ambiguous-selection results. | Open a folder; pick a project. | One project context is authoritative; status reports no-project/unsupported/ambiguous truthfully; LSP, Build, Debug all consume the same context. | No-project workspace is reported, not silently defaulted. | Phase 8.3 closeout (per [Phase 8 plan §"Sub-Phase Decision"](../../phases/v2/phase-8/IMPLEMENTATION_PLAN.md#sub-phase-decision-m0)). | `IProjectContextService`, `ProjectContext`, `Program.ConfigureServices`. | Disposable profile: open a no-C# folder; observe no-project status. Open a folder with two `.sln`s; observe ambiguous-selection. |
| A1-WO-03 | workspace | V2 | Phase 8.3 | [Phase 8 plan §"Live Baseline"](../../phases/v2/phase-8/IMPLEMENTATION_PLAN.md); [V2 §"Phase 8"](../../roadmap/V2.md#phase-8--core-platform-and-settings) | `Workspace.WorkspacePath` change notification + `WorkspaceFolderChanged` event consumed by project context. | Open/close a folder. | Downstream consumers (Source Control, project context) refresh on folder change. | Stale state does not linger. | Phase 8.1 + 8.3 closeout (per [Phase 8 plan §"Sub-Phase Decision"](../../phases/v2/phase-8/IMPLEMENTATION_PLAN.md#sub-phase-decision-m0)). | `Workspace.cs`, `WorkspaceFolderChanged`, `SourceControlViewModel`. | Disposable profile: open a folder, then close; observe consumers refresh. |

---

## 3. File Navigation and Editing

| id | journey | roadmap | phase | source_document | promised_outcome | user_entry_point | success_condition | failure_recovery | claimed_completion_evidence | likely_a2_target | planned_a3_scenario |
|----|---------|---------|-------|-----------------|------------------|------------------|-------------------|------------------|------------------------------|------------------|---------------------|
| A1-FN-01 | file nav/edit | V1 | Phase 2 | [V1 §"Phase 2: Editor"](../../roadmap/PHASES.md#phase-2-editor) | Text editor (AvaloniaEdit), tabbed open/close/switch, save (`Ctrl+S`), dirty flag indicator, syntax highlighting (TextMate). | Open a file from tree; switch tabs; edit; save. | Tabs open and switch; dirty indicator visible; save works; syntax highlighting renders. | Save error reported (e.g. file I/O). | Phase 2 closeout (V1 closeout, [V1 V1 Closeout](../../roadmap/PHASES.md#v1-closeout)). | `EditorView`, `EditorTabViewModel`, `Workspace`, `Document`, TextMate. | Disposable profile: open a C# file, edit, save, observe dirty indicator. |
| A1-FN-02 | file nav/edit | V1 | Phase 1.1/1.2 | [V1 §"Phase 1.1"](../../roadmap/PHASES.md#phase-11-file-tree-polish); [V1 §"Phase 1.2"](../../roadmap/PHASES.md#phase-12-file-tree-essentials) | Open folder, grid splitter (180–500px), single open pathway, Enter + Double-click to open, copy path / copy relative path. | Drag the grid splitter; copy path from context menu. | Splitter resizes; single open pathway; context menu Copy Path / Copy Relative Path. | Errors handled. | Phase 1.1/1.2 closeout (V1 closeout, [V1 V1 Closeout](../../roadmap/PHASES.md#v1-closeout)). | `FileTreeView`, `GridSplitter`, `RequestOpenFileCommand`, context menu. | Disposable profile: copy path, paste to a text file, verify. |
| A1-FN-03 | file nav/edit | V2 | Phase 9 M3 | [Phase 9 plan §"M3 Search and Replace Contract"](../../phases/v2/phase-9/IMPLEMENTATION_PLAN.md); [V2 §"Phase 9 — Editor UX"](../../roadmap/V2.md#phase-9--editor-ux) | Active-document search and replace (literal substring only, case-sensitive by default, Find Next/Previous wrap, Replace Next/Replace All, undo grouping for Replace All). Commands: `editor.find` (Ctrl+F), `editor.replace` (Ctrl+H), `editor.findNext` (F3), `editor.findPrevious` (Shift+F3). | `Ctrl+F`, type query, press F3. | Match is selected and scrolled into view; zero-match shows "No matches found"; Replace All is one undo entry. | Tab switch clears search state. | Phase 9 M3 evidence (per [Phase 9 plan §"Status"](../../phases/v2/phase-9/IMPLEMENTATION_PLAN.md)). | `EditorSearchViewModel`, `SearchEngine`, `IEditorTextOperations`. | Disposable profile: open a file, search, find next, replace all, undo once. |
| A1-FN-04 | file nav/edit | V2 | Phase 9 M4 | [Phase 9 plan §"M4"](../../phases/v2/phase-9/IMPLEMENTATION_PLAN.md) | Code folding in active editor (syntax-neutral heuristic, expand/collapse, caret preservation, discard on tab change). | Trigger fold commands. | Fold regions render and collapse/expand. | Folding is discarded on tab change. | Phase 9 M4 evidence (per [Phase 9 plan §"Status"](../../phases/v2/phase-9/IMPLEMENTATION_PLAN.md)). | `EditorFoldingTests`, `NewFolding`, `FoldingManager.UpdateFoldings`. | Disposable profile: open a C# file, collapse a fold, switch tabs, verify state reset. |
| A1-FN-05 | file nav/edit | V2 | Phase 9 M5a/M5b | [Phase 9 plan §"M5a/M5b"](../../phases/v2/phase-9/IMPLEMENTATION_PLAN.md) | Tab commands: next/previous tab, close active, close other, close all; pointer-driven tab reordering; dirty/active affordances. | Use tab commands; drag a tab. | Tabs switch, close, reorder. | Unsaved-change confirmation (`Interaction<EditorViewModel, bool>`). | Phase 9 M5a/M5b evidence (per [Phase 9 plan §"Status"](../../phases/v2/phase-9/IMPLEMENTATION_PLAN.md)). | `EditorTabBar`, `EditorTabViewModel`, `UnsavedDialog`. | Disposable profile: open multiple tabs, close all, verify dirty confirmation on unsaved tab. |
| A1-FN-06 | file nav/edit | V2 | Phase 9 M6 | [Phase 9 plan §"M6"](../../phases/v2/phase-9/IMPLEMENTATION_PLAN.md) | Status bar truthfully reflects active document, caret, selection, search outcome, save failure. | Edit text, move caret, select. | Status bar updates correctly. | Status text stays truthful across tab switches. | Phase 9 M6 evidence (per [Phase 9 plan §"Status"](../../phases/v2/phase-9/IMPLEMENTATION_PLAN.md)). | `StatusBarViewModel`, `StatusBar.cs`. | Disposable profile: edit, select, observe status bar. |
| A1-FN-08 | file nav/edit | V2 | Phase 10 M3 | [Phase 10 plan §"Status" + §"Scope"](../../phases/v2/phase-10/IMPLEMENTATION_PLAN.md); [V2 §"Phase 10 — C# Language Intelligence"](../../roadmap/V2.md#phase-10--c-language-intelligence) | LSP structured diagnostics projected into a Problems panel. The user sees language-server diagnostics with file and line attribution; LSP diagnostics are retained across build lifecycle. | Open a C# workspace with a compile error; view Problems panel. | Diagnostics listed with file:line attribution; click navigates to source. | Truthful no-project / ambiguous-project handling. | [Phase 10 M3 evidence](../../phases/v2/phase-10/M3_MANUAL_EVIDENCE.md); [Phase 10 plan §"Status"](../../phases/v2/phase-10/IMPLEMENTATION_PLAN.md) — M3 complete 2026-07-13. | `ILanguageDiagnosticsService`, `LanguageDiagnosticsService`, `ProblemsViewModel`, `LanguageDocumentBridge`. | Disposable profile + disposable C# project with a known compile error: open project, observe Problems panel listing diagnostics. |
| A1-FN-09 | file nav/edit | V2 | Phase 10 M4 | [Phase 10 plan §"Status"](../../phases/v2/phase-10/IMPLEMENTATION_PLAN.md) | LSP completion in the active C# document. | Open a C# file; type partial identifier; trigger completion. | Completion items appear filtered; insert replaces the partial identifier. | Failed, cancelled, or unsupported request leaves the document unchanged. | [Phase 10 M4 evidence](../../phases/v2/phase-10/M4_MANUAL_EVIDENCE.md); [Phase 10 plan §"Status"](../../phases/v2/phase-10/IMPLEMENTATION_PLAN.md) — M4 complete 2026-07-13. | `ILanguageCompletionService`, `LanguageCompletionService`, `EditorLanguageInputViewModel`. | Disposable profile + disposable C# project: trigger completion, observe items. |
| A1-FN-10 | file nav/edit | V2 | Phase 10 M4 | [Phase 10 plan §"Status"](../../phases/v2/phase-10/IMPLEMENTATION_PLAN.md) | LSP hover information for C# symbols. | Hover over a C# symbol in the active document. | Hover surface shows symbol type / signature / docs. | No hover when language server lacks the symbol. | [Phase 10 M4 evidence](../../phases/v2/phase-10/M4_MANUAL_EVIDENCE.md); [Phase 10 plan §"Status"](../../phases/v2/phase-10/IMPLEMENTATION_PLAN.md) — M4 complete 2026-07-13. | `ILanguageHoverService`, `LanguageHoverService`. | Disposable profile + disposable C# project: hover over a known symbol, observe hover surface. |
| A1-FN-11 | file nav/edit | V2 | Phase 10 M5 | [Phase 10 plan §"Status"](../../phases/v2/phase-10/IMPLEMENTATION_PLAN.md) | LSP Go to Definition. | Trigger Go to Definition on a C# symbol. | Cursor jumps to the symbol's definition location. | "No definition found" reported when the language server cannot resolve. | [Phase 10 M5 evidence](../../phases/v2/phase-10/M5_MANUAL_EVIDENCE.md); [Phase 10 plan §"Status"](../../phases/v2/phase-10/IMPLEMENTATION_PLAN.md) — M5 complete 2026-07-13. | `ILanguageNavigationService`, `LanguageNavigationService`. | Disposable profile + disposable C# project: trigger Go to Definition on a known symbol. |
| A1-FN-12 | file nav/edit | V2 | Phase 10 M5 | [Phase 10 plan §"Status"](../../phases/v2/phase-10/IMPLEMENTATION_PLAN.md) | LSP document symbols. | Trigger Document Symbols on the active C# file. | Outline surface lists file-level C# symbols. | Empty outline when the language server returns no symbols. | [Phase 10 M5 evidence](../../phases/v2/phase-10/M5_MANUAL_EVIDENCE.md); [Phase 10 plan §"Status"](../../phases/v2/phase-10/IMPLEMENTATION_PLAN.md) — M5 complete 2026-07-13. | `ILanguageSymbolService`, `LanguageSymbolService`. | Disposable profile + disposable C# project: trigger Document Symbols on a multi-class file. |
| A1-FN-13 | file nav/edit | V2 | Phase 10 M5 | [Phase 10 plan §"Status"](../../phases/v2/phase-10/IMPLEMENTATION_PLAN.md) | LSP workspace symbols. | Trigger Workspace Symbols with a query. | Matching workspace symbols returned across files. | Empty results when no symbols match. | [Phase 10 M5 evidence](../../phases/v2/phase-10/M5_MANUAL_EVIDENCE.md); [Phase 10 plan §"Status"](../../phases/v2/phase-10/IMPLEMENTATION_PLAN.md) — M5 complete 2026-07-13. | `ILanguageSymbolService`, workspace-symbol request. | Disposable profile + disposable C# project: trigger Workspace Symbols. |
| A1-FN-14 | file nav/edit | V2 | Phase 10 M6 | [Phase 10 plan §"Status"](../../phases/v2/phase-10/IMPLEMENTATION_PLAN.md) | LSP whole-document formatting via `textDocument/formatting`. `Format Document` command registered as `editor.formatDocument` with default `Ctrl+Shift+I`. Formatting integrated with undo, dirty state, caret, and selection. | Open a C# file with unformatted code; press `Ctrl+Shift+I`. | Document is reformatted in one atomic edit; one undo entry; caret/selection preserved per the locked rule. | Failed, cancelled, timed-out, or unsupported request leaves the document unchanged. | [Phase 10 M6 evidence](../../phases/v2/phase-10/M6_MANUAL_EVIDENCE.md); [Phase 10 plan §"Status"](../../phases/v2/phase-10/IMPLEMENTATION_PLAN.md) — M6 complete 2026-07-14. | `ILanguageFormattingService`, `LanguageFormattingService`, `FormatDocumentCommand`, `IEditorTextOperations`. | Disposable profile + disposable C# project: trigger Format Document, observe atomic edit and one undo. |
| A1-FN-15 | file nav/edit | V2 | Phase 10 M6 | [Phase 10 plan §"Status"](../../phases/v2/phase-10/IMPLEMENTATION_PLAN.md); [V2 §"Phase 10" Scope](../../roadmap/V2.md#phase-10--c-language-intelligence) | Optional Format on Save setting, default disabled. | Enable Format on Save in settings; save a C# file. | Save triggers Format Document. | Setting disabled → no automatic format. | [Phase 10 M6 evidence](../../phases/v2/phase-10/M6_MANUAL_EVIDENCE.md); [Phase 10 plan §"Status"](../../phases/v2/phase-10/IMPLEMENTATION_PLAN.md) — M6 complete 2026-07-14. | `FormatOnSaveTests`, settings schema, save command. | Disposable profile: toggle Format on Save, save a C# file, observe behavior. |

---

## 4. Search and Command Discovery

| id | journey | roadmap | phase | source_document | promised_outcome | user_entry_point | success_condition | failure_recovery | claimed_completion_evidence | likely_a2_target | planned_a3_scenario |
|----|---------|---------|-------|-----------------|------------------|------------------|-------------------|------------------|------------------------------|------------------|---------------------|
| A1-SC-01 | search/command | V2 | Phase 8.2 | [Phase 8.2 plan](../../phases/v2/phase-8/phase-8.2/IMPLEMENTATION_PLAN.md); [V2 §"Phase 8"](../../roadmap/V2.md#phase-8--core-platform-and-settings) | Command registry with stable identifiers, default keybindings, user overrides, conflict handling. | Open settings → keybindings; rebind a command. | Bindings are applied; conflicts detected. | Conflict dialog or surface. | Phase 8.2 closeout (M7a–M10, per [V2 §"Phase 8" Status](../../roadmap/V2.md#phase-8--core-platform-and-settings)). | `ICommandRegistry`, `CommandRegistry`, keybinding resolution. | Disposable profile: rebind a key, verify the new binding works. |
| A1-SC-02 | search/command | V2 | Phase 9 M1/M2 | [Phase 9 plan §"M1/M2" + §"M2 Search and Replace Contract (Locked by M0)" + §"Milestones"](../../phases/v2/phase-9/IMPLEMENTATION_PLAN.md); [V2 §"Phase 9 — Editor UX"](../../roadmap/V2.md#phase-9--editor-ux) | Command Palette backed by `ICommandRegistry`: opens, filters case-insensitive literal, deterministic category ordering, executes commands, restores focus to the invoking editor. Reports unavailable commands without executing them. M2 also locks the registry-backed palette invocation gesture. | Open Command Palette; type; execute. | Palette opens, lists registry descriptors, filters, executes, restores focus. Unavailable commands are visible but not executed. | No match → empty result; registry-backed gesture unchanged. | Phase 9 M1/M2 evidence (per [Phase 9 plan §"Status"](../../phases/v2/phase-9/IMPLEMENTATION_PLAN.md)). | `CommandPaletteViewModel`, `CommandPaletteView`, `ICommandRegistry`. | Disposable profile: open palette, run `editor.find`, observe focus return. |
| A1-SC-03 | search/command | V2 | Phase 9 M3/M4/M5a | [Phase 9 plan §"M3/M4/M5a"](../../phases/v2/phase-9/IMPLEMENTATION_PLAN.md) | All Phase 9 commands registered by ID: `editor.find`, `editor.replace`, `editor.findNext`, `editor.findPrevious`, `editor.replaceNext`, `editor.replaceAll`, fold commands, tab commands. Default keybindings via Phase 8. | Run a registered command by ID. | Commands resolve to the right action. | Unbound commands are reachable via palette. | Phase 9 closeout (per [Phase 9 plan §"Status"](../../phases/v2/phase-9/IMPLEMENTATION_PLAN.md)). | `ICommandRegistry`, descriptor registration. | Disposable profile: run a command via palette. |

---

## 5. Build / Run / Test

| id | journey | roadmap | phase | source_document | promised_outcome | user_entry_point | success_condition | failure_recovery | claimed_completion_evidence | likely_a2_target | planned_a3_scenario |
|----|---------|---------|-------|-----------------|------------------|------------------|-------------------|------------------|------------------------------|------------------|---------------------|
| A1-BR-01 | build/run/test | V2 | Phase 11 | [Phase 11 plan](../../phases/v2/phase-11/IMPLEMENTATION_PLAN.md); [V2 §"Phase 11 — Project Workflow"](../../roadmap/V2.md#phase-11--project-workflow--complete-m0m6-2026-07-14) | Build, Run, Test target selection from authoritative project context; explicit commands; cancellation and one-operation-at-a-time policy; structured Output panel. | Use `project.build`, `project.run`, `project.test`, `project.cancel` commands. | Build/Run/Test run; output shown in Output panel; cancel works. | Failed build surfaces diagnostics in Problems. | [Phase 11 M2 evidence](../../phases/v2/phase-11/M2_MANUAL_EVIDENCE.md); [Phase 11 M4 evidence](../../phases/v2/phase-11/M4_MANUAL_EVIDENCE.md); [Phase 11 M5 evidence](../../phases/v2/phase-11/M5_MANUAL_EVIDENCE.md); [Phase 11 M6 evidence](../../phases/v2/phase-11/M6_MANUAL_EVIDENCE.md). | `IProjectWorkflowService`, `IManagedProcessRunner`, `IProjectOutputService`, `ICommandRegistry`. | Disposable profile + disposable project: trigger build, observe Output panel; trigger cancel mid-run. |
| A1-BR-02 | build/run/test | V2 | Phase 11 M3 | [Phase 11 plan §"M3"](../../phases/v2/phase-11/IMPLEMENTATION_PLAN.md) | Build diagnostics parsed (MSBuild) and projected into Problems with source attribution. LSP diagnostics are retained. | Run a build that fails. | Problems panel lists diagnostics with source location. | Click navigates to source. | [Phase 11 M3 evidence](../../phases/v2/phase-11/M3_MANUAL_EVIDENCE.md). | `IBuildDiagnosticsService`, `ProblemsViewModel`. | Disposable profile + disposable project: trigger a failing build, observe Problems, click to navigate. |
| A1-BR-03 | build/run/test | V2 | Phase 11 M5 | [Phase 11 plan §"M5"](../../phases/v2/phase-11/IMPLEMENTATION_PLAN.md) | Test results surface (Test Results bottom panel) for `dotnet test` structured outcomes; console-first parse, fail-open. | Run tests. | Test Results panel shows pass/fail counts. | Cancel works mid-run. | [Phase 11 M5 evidence](../../phases/v2/phase-11/M5_MANUAL_EVIDENCE.md). | `ITestResultsService`, `BottomPanelMode.TestResults`. | Disposable profile + disposable project: run tests, observe results. |
| A1-BR-04 | build/run/test | V2 | Phase 11 M2 | [Phase 11 plan §"M2"](../../phases/v2/phase-11/IMPLEMENTATION_PLAN.md) | Build command is registered as `project.build`; Output panel is distinct from the interactive terminal. | Trigger `project.build`. | Output appears in Output panel, not the PTY terminal. | Output stream and PTY remain separated. | [Phase 11 M2 evidence](../../phases/v2/phase-11/M2_MANUAL_EVIDENCE.md). | `ICommandRegistry`, `BottomPanelMode.Output`. | Disposable profile: trigger build, observe Output panel mode. |

---

## 6. Debugging and Output

| id | journey | roadmap | phase | source_document | promised_outcome | user_entry_point | success_condition | failure_recovery | claimed_completion_evidence | likely_a2_target | planned_a3_scenario |
|----|---------|---------|-------|-----------------|------------------|------------------|-------------------|------------------|------------------------------|------------------|---------------------|
| A1-DB-01 | debugging | V2 | Phase 12 | [Phase 12 plan](../../phases/v2/phase-12/IMPLEMENTATION_PLAN.md); [V2 §"Phase 12 — C# Debugging"](../../roadmap/V2.md#phase-12--c-debugging--complete-m0m7-2026-07-14) | DAP client and debug-adapter lifecycle, launch configuration for supported C# workflow, breakpoints, step over/into/out, current execution location, threads, call stack, scopes and variables, debug console, truthful adapter failure handling. | Set a breakpoint; launch a debug session. | Breakpoint hits; step controls work; call stack and variables visible. | Adapter startup, disconnect, crash, protocol errors handled. | [Phase 12 M3a proof](../../phases/v2/phase-12/M3a_DEBUG_LAUNCH_HANDOFF_PROOF.md); [Phase 12 M3b proof](../../phases/v2/phase-12/M3b_EDITOR_BREAKPOINT_PROOF.md); [Phase 12 M4 proof](../../phases/v2/phase-12/M4_EXECUTION_CONTROLS_DEBUG_CONSOLE_PROOF.md); [Phase 12 M5 proof](../../phases/v2/phase-12/M5_STACK_VARIABLES_CURRENT_LOCATION_PROOF.md); [Phase 12 M6 proof](../../phases/v2/phase-12/M6_DAP_RECOVERY_PROOF.md); [Phase 12 M7 evidence](../../phases/v2/phase-12/M7_MANUAL_EVIDENCE.md). | DAP services, breakpoint persistence, launch handoff. | Disposable profile + disposable project + NetCoreDbg available: launch debug, hit breakpoint, step. |

---

## 7. Terminal

| id | journey | roadmap | phase | source_document | promised_outcome | user_entry_point | success_condition | failure_recovery | claimed_completion_evidence | likely_a2_target | planned_a3_scenario |
|----|---------|---------|-------|-----------------|------------------|------------------|-------------------|------------------|------------------------------|------------------|---------------------|
| A1-TR-01 | terminal | V1 | Phase 3 + 3.6–3.9 | [V1 §"Phase 3"](../../roadmap/PHASES.md#phase-3-terminal) | Embedded terminal in bottom panel; `Ctrl+\`` toggle; PTY-backed process execution; ANSI/CSI parser, screen buffer; alternate screen; selection, scrollback, search. | Toggle bottom panel; run a TUI. | Terminal renders; TUI alt-screen works; selection and search work. | Restart is safe across service and ViewModel. | Phase 3 + 3.6–3.9 closeout ([V1 §"Phase 3"](../../roadmap/PHASES.md#phase-3-terminal)). | `TerminalPanel`, `TerminalRenderControl`, `TerminalService`, session host. | Disposable profile: open terminal, run `htop` (alt-screen), search. |
| A1-TR-02 | terminal | V1 | Phase 3.9.1 | [Phase 3.9.1 plan](../../phases/v1/phase-3.9.1/IMPLEMENTATION_PLAN.md); [V1 §"Phase 3" — terminal tabs entry](../../roadmap/PHASES.md#phase-3-terminal) | Lightweight terminal tabs (per-tab sessions, session host/factory seam, view-layer panel caching, tab strip UI). | Add a new terminal tab. | Multiple independent terminal sessions. | Per-tab lifecycle. | Phase 3.9.1 closeout (2026-07-07, per [Phase 3.9.1 plan](../../phases/v1/phase-3.9.1/IMPLEMENTATION_PLAN.md)). | `TerminalTabHost`, `TerminalPanel` factory, tab strip. | Disposable profile: add two terminal tabs, run different commands. |

---

## 8. Git Workflow

| id | journey | roadmap | phase | source_document | promised_outcome | user_entry_point | success_condition | failure_recovery | claimed_completion_evidence | likely_a2_target | planned_a3_scenario |
|----|---------|---------|-------|-----------------|------------------|------------------|-------------------|------------------|------------------------------|------------------|---------------------|
| A1-GT-01 | git | V1 | Phase 7 / 7.1 | [Phase 7 plan](../../phases/v1/phase-7/IMPLEMENTATION_PLAN.md); [V1 §"Phase 7"](../../roadmap/PHASES.md#phase-7-git-integration) | Real repository-backed git seam; Source Control panel and status bar reflect live state; "no repo", "—" labels. | Open a folder; view Source Control panel. | Branch and changes match `git status`. | Non-repo workspace shows "no repo". | Phase 7.1 + 7.2 closeout ([V1 §"Phase 7"](../../roadmap/PHASES.md#phase-7-git-integration)). | `ISourceControlSnapshotOrchestrator`, `SourceControlViewModel`. | Disposable profile + disposable git repo: open repo, observe panel. |
| A1-GT-02 | git | V1 | Phase 7.3 | [Phase 7.3 plan](../../phases/v1/phase-7.3/IMPLEMENTATION_PLAN.md); [V1 §"Phase 7"](../../roadmap/PHASES.md#phase-7-git-integration) | Basic diff view (unified diff via `Diff.Compare<Patch>()`); binary file notice; refresh-safe selection. | Select a file in Source Control. | Diff renders. | Binary files show notice. | Phase 7.3 closeout ([V1 §"Phase 7"](../../roadmap/PHASES.md#phase-7-git-integration)). | `IFileDiffService`, `FileDiffService`. | Disposable profile: select a modified file, observe diff. |
| A1-GT-03 | git | V1 | Phase 7.4 | [Phase 7.4 plan](../../phases/v1/phase-7.4/IMPLEMENTATION_PLAN.md); [V1 §"Phase 7"](../../roadmap/PHASES.md#phase-7-git-integration) | Stage/unstage files; local commit; commit message validation; truthful error handling. | Stage/unstage; commit. | Commit succeeds; failure reported. | Validation errors reported. | Phase 7.4 closeout ([V1 §"Phase 7"](../../roadmap/PHASES.md#phase-7-git-integration)). | `SourceControlViewModel` stage/unstage/commit commands. | Disposable profile: stage, commit, observe log. |
| A1-GT-04 | git | V1 | Phase 7 | [V1 §"Phase 7"](../../roadmap/PHASES.md#phase-7-git-integration); [Phase 7.2 plan](../../phases/v1/phase-7.2/IMPLEMENTATION_PLAN.md) | Branch display is truthful (current branch). | View status bar. | Branch name matches `git branch --show-current`. | Detached-HEAD-like state visible. | Phase 7.2 closeout ([V1 §"Phase 7"](../../roadmap/PHASES.md#phase-7-git-integration)). | `StatusBar.cs`, `CurrentBranchName`. | Disposable profile: switch branches, observe status bar. |

---

## 9. Townhall / Conversations

| id | journey | roadmap | phase | source_document | promised_outcome | user_entry_point | success_condition | failure_recovery | claimed_completion_evidence | likely_a2_target | planned_a3_scenario |
|----|---------|---------|-------|------------------|------------------|------------------|-------------------|------------------|------------------------------|------------------|---------------------|
| A1-TH-01 | townhall | V1 | Phase 4 | [V1 §"Phase 4: Agent Workspace Foundations"](../../roadmap/PHASES.md#phase-4-agent-workspace-foundations) | Townhall as a real shared workspace; 8-kind entry taxonomy; auto-logged entries on send/switch; kind-based visual rendering; filter toggle (All/Chat/Activity). | Open Townhall; switch channel; filter. | Entries render; filter works; auto-logged entries appear. | Activity log preserved. | Phase 4 closeout ([V1 §"Phase 4"](../../roadmap/PHASES.md#phase-4-agent-workspace-foundations)). | `TownhallViewModel`, `TownhallMessage`, `TownhallMessageKind`. | Disposable profile: send a channel message, switch channels, filter Activity. |
| A1-TH-02 | townhall | V3 | Phase 14 | [Phase 14 plan §"D02–D06"](../../phases/v3/phase-14/IMPLEMENTATION_PLAN.md) | Unified conversation system; direct conversations are private; UI selection is presentation; every admitted entry has one owning `ConversationId`; Direct conversations are find-or-create by unordered participant pair. | Open Townhall; open a DM with an agent. | DM appears; private by default. | No implicit copy to public. | [Phase 14 M9 evidence](../../phases/v3/phase-14/M9_MANUAL_EVIDENCE.md); [Phase 14 M9 F1 evidence](../../phases/v3/phase-14/M9_F1_MANUAL_EVIDENCE.md). | `ConversationStore`, `TownhallNavigationPanel`, `ActiveConversationId`. | Disposable profile: open a DM, send a message, observe it stays in the DM. |
| A1-TH-04 | townhall | V3 | Phase 14 | [Phase 14 plan §"D17"](../../phases/v3/phase-14/IMPLEMENTATION_PLAN.md) | Agent Panel retired; M8 parity checklist passed; Townhall becomes the single re-entry path. | (No direct user action.) | Agent Panel is no longer in the shell. | Migration or documented deferral. | [Phase 14 M9 evidence](../../phases/v3/phase-14/M9_MANUAL_EVIDENCE.md); [Phase 14 M9 design brief](../../phases/v3/phase-14/M9_DESIGN_BRIEF.md). | `MainLayoutBuilder`, `RightColumnHost`. | Disposable profile: confirm no agent panel chrome. |
| A1-TH-05 | townhall | V1 → V3 | Phase 6 + 6.1 | [Phase 6 plan](../../phases/v1/phase-6/IMPLEMENTATION_PLAN.md); [Phase 6.1 plan](../../phases/v1/phase-6.1/IMPLEMENTATION_PLAN.md) | Townhall surfaces routing failures and routed-flow outcomes; dedicated `AgentRouter` test file. | Send a `@Name` to a valid agent. | Townhall shows routed flow. | Unknown target → Townhall error. | Phase 6 + 6.1 closeout ([V1 §"Phase 6.1"](../../roadmap/PHASES.md#phase-61-routing-visibility-follow-up)). | `MainWindowViewModel.SendAgentMessageAsync`, `AgentRouterTests`. | Disposable profile: send `@alpha hello`, observe Townhall. |

---

## 10. Agent Creation and Backend Onboarding

| id | journey | roadmap | phase | source_document | promised_outcome | user_entry_point | success_condition | failure_recovery | claimed_completion_evidence | likely_a2_target | planned_a3_scenario |
|----|---------|---------|-------|------------------|------------------|------------------|-------------------|------------------|------------------------------|------------------|---------------------|
| A1-AC-01 | agent create | V1 | Phase 5 | [Phase 5 plan](../../phases/v1/phase-5/IMPLEMENTATION_PLAN.md); [V1 §"Phase 5: Agent Panels"](../../roadmap/PHASES.md#phase-5-agent-panels) | Dedicated agent panels; one minimal real direct-execution path to one configured OpenAI-compatible endpoint; direct-agent interactions mirrored into Townhall. | Add a new agent panel; send a message. | Agent-specific output/status visible. | Missing config / endpoint failure / invalid response / cancellation policy handled. | Phase 5 + 5.1–5.5 closeout ([V1 §"Phase 5"](../../roadmap/PHASES.md#phase-5-agent-panels)). | `AgentPanelHost`, `AgentExecutionService`, `AgentPanelView`. | (Deferred: Agent Panel retired in Phase 14; this row remains as historical evidence for V1.) |
| A1-AC-02 | agent create | V3 | Phase 14 + 19 + 20 | [Phase 14 plan](../../phases/v3/phase-14/IMPLEMENTATION_PLAN.md); [V3 §"Phase 19 — Zaide Native Harness Backend" + §"Phase 20 — ACP Agent Backend"](../../roadmap/V3.md#18-v3-feature-sequence) | Native Harness and ACP as independent sibling backends; equal Townhall placement; honest capability limits. User can configure a Native Harness or ACP backend binding and bind it to an agent. | Configure a Native Harness or ACP backend binding; bind it to an agent. | Backend is bound, identity is recorded, capability changes are reported, mediated actions flow through the control plane. | Capability change reported honestly; per-actor identity binding explicit; backend disconnect handled. | [Phase 19 plan](../../phases/v3/phase-19/IMPLEMENTATION_PLAN.md) M0–M6 closeout; [Phase 20 plan](../../phases/v3/phase-20/IMPLEMENTATION_PLAN.md) M0–M6 closeout ([V3 §18](../../roadmap/V3.md#18-v3-feature-sequence)). | `AgentActorBackendBindingStore`, `AgentActorBackendSelectionService`, `AgentBackendBindingPresenter`. | Disposable profile: configure a backend binding from the application; observe bind/send/cleanup behavior. |

---

## 11. Agent Send / Response / Failure Feedback

| id | journey | roadmap | phase | source_document | promised_outcome | user_entry_point | success_condition | failure_recovery | claimed_completion_evidence | likely_a2_target | planned_a3_scenario |
|----|---------|---------|-------|------------------|------------------|------------------|-------------------|------------------|------------------------------|------------------|---------------------|
| A1-AS-01 | agent send | V1 | Phase 5 | [Phase 5 plan](../../phases/v1/phase-5/IMPLEMENTATION_PLAN.md); [V1 §"Phase 5"](../../roadmap/PHASES.md#phase-5-agent-panels) | Direct send from a panel to one configured OpenAI-compatible endpoint; non-streaming; one in-flight per panel. | Type and send in an agent panel. | Response visible. | Failure surfaces in panel and Townhall. | Phase 5 closeout ([V1 §"Phase 5"](../../roadmap/PHASES.md#phase-5-agent-panels)). | `AgentExecutionService`, `AgentExecutionCoordinator`. | (Deferred — Agent Panel retired in Phase 14.) |
| A1-AS-02 | agent send | V3 | Phase 14 + 15 + 19 + 20 | [V3 §"Phase 15 — Backend-Neutral Agent Session and Event Foundation" + §"Phase 19" + §"Phase 20"](../../roadmap/V3.md#18-v3-feature-sequence) | Backend-neutral Agent Session and event foundation; Native Harness and ACP as peer backends; send returns accepted/queued/running/completed/failed/rejected/cancelled/timed-out/disconnected/indeterminate. | Send a message in a direct conversation. | Response or actionable failure visible. | Backend-reported outcomes (accepted/queued/running/completed/failed/rejected/cancelled/timed-out/disconnected/indeterminate) are reflected in the conversation; cancellation is distinct from cancellation acknowledgment and from late completion. | [Phase 14 M9 evidence](../../phases/v3/phase-14/M9_MANUAL_EVIDENCE.md); [Phase 15 plan](../../phases/v3/phase-15/IMPLEMENTATION_PLAN.md); [Phase 19 plan](../../phases/v3/phase-19/IMPLEMENTATION_PLAN.md); [Phase 20 plan](../../phases/v3/phase-20/IMPLEMENTATION_PLAN.md). | `AgentSessionService`, `AgentExecutionCoordinator`, `AgentRouter`. | Disposable profile: send a message, observe response or structured outcome. |

---

## 12. Tools, Permissions, and Workspace Mutation

| id | journey | roadmap | phase | source_document | promised_outcome | user_entry_point | success_condition | failure_recovery | claimed_completion_evidence | likely_a2_target | planned_a3_scenario |
|----|---------|---------|-------|------------------|------------------|------------------|-------------------|------------------|------------------------------|------------------|---------------------|
| A1-TP-01 | tools/permissions | V3 | Phase 17 | [V3 §"Phase 17" + "Phase 17 — Agent Action Control Plane and Workspace Mutation"](../../roadmap/V3.md#18-v3-feature-sequence); [Phase 17 plan](../../phases/v3/phase-17/IMPLEMENTATION_PLAN.md) | Mediated action control plane: agent proposes a file read, edit, or command → capability and policy evaluation → allow/deny/ask → execute or delegate → record result → attribute to run and actor. This is the same seam for direct agent sends (`SendAgentMessageAsync`) and any backend-initiated action. | Agent proposes a file edit / command (from a direct conversation send or any backend). | Permission UI appears for unapproved actions; mediation occurs; audit attribution is recorded. | Unknown / unverifiable actions default to deny/ask. | [Phase 17 M9 closeout](../../phases/v3/phase-17/M9_CLOSEOUT_EVIDENCE.md). | `IAgentActionRequestCapableBackend`, `UnavailableAgentActionBroker`, broker, permission policy, audit store. | Disposable profile: trigger an action requiring permission. |
| A1-TP-02 | tools/permissions | V3 | Phase 17 | [V3 §"13. Tools, Permissions, and Audit"](../../roadmap/V3.md#13-tools-permissions-and-audit); [Phase 17 plan](../../phases/v3/phase-17/IMPLEMENTATION_PLAN.md) | Permission dimensions (read/write, workspace-internal/external, process, network, Git, secrets, destructive, memory, approval scope). Approval binds to canonical action description. | Trigger a destructive action. | Permission UI appears; approval scope selectable. | Revocation propagates. | [Phase 17 M9 closeout](../../phases/v3/phase-17/M9_CLOSEOUT_EVIDENCE.md). | Permission policy, audit store. | Disposable profile: trigger destructive action, observe permission UI. |
| A1-TP-03 | workspace mutation | V3 | Phase 17 | [V3 §"14. Workspace Mutation and Concurrency"](../../roadmap/V3.md#14-workspace-mutation-and-concurrency); [Phase 17 plan](../../phases/v3/phase-17/IMPLEMENTATION_PLAN.md) | Optimistic concurrency/version checks; conflicts between agents and build/test/debug; agent-attributed change set; rollback that removes an agent's changes without destroying unrelated work; cancellation of partially applied multi-file edits. | Trigger a multi-file agent edit. | Edit applies; rollback restores prior state for that agent's changes. | Conflicts surfaced. | [Phase 17 M9 closeout](../../phases/v3/phase-17/M9_CLOSEOUT_EVIDENCE.md). | `Workspace`/`Document` ownership, audit, rollback. | Disposable profile: trigger multi-file edit, observe rollback. |

---

## 13. Multi-Agent Routing

| id | journey | roadmap | phase | source_document | promised_outcome | user_entry_point | success_condition | failure_recovery | claimed_completion_evidence | likely_a2_target | planned_a3_scenario |
|----|---------|---------|-------|------------------|------------------|------------------|-------------------|------------------|------------------------------|------------------|---------------------|
| A1-MR-01 | multi-agent | V1 | Phase 6 | [Phase 6 plan](../../phases/v1/phase-6/IMPLEMENTATION_PLAN.md); [V1 §"Phase 6"](../../roadmap/PHASES.md#phase-6-agent-to-agent-router) | `@mention` parsing (zero or one `@AgentName`, case-insensitive exact match); route to a visible panel; Townhall visibility for direct-send. | Send `@alpha hello` from a panel. | Routed to alpha; Townhall shows the request. | Unknown mention → Townhall `AgentError` (per Phase 6.1). | [Phase 6 plan](../../phases/v1/phase-6/IMPLEMENTATION_PLAN.md) + [Phase 6.1 plan](../../phases/v1/phase-6.1/IMPLEMENTATION_PLAN.md) closeout. | `MentionParser`, `AgentRouter`. | Disposable profile: send `@alpha`, observe routing. |
| A1-MR-03 | multi-agent | V3 | Phase 14 | [Phase 14 plan §"D09"](../../phases/v3/phase-14/IMPLEMENTATION_PLAN.md) | Routing resolution moves from visible-name against open panels to typed `ActorId` / catalog roster. | Send `@alpha` (or catalog name) from any conversation. | Route resolves without requiring a dedicated panel tab. | Catalog list is empty → no resolution. | [Phase 14 M9 evidence](../../phases/v3/phase-14/M9_MANUAL_EVIDENCE.md). | `ActorCatalog`, `IActorCatalog.ListAgents`. | Disposable profile: send `@alpha` without an open alpha panel. |

---

## 14. Trace, Context, Memory, Persistence, Restart, and Recovery

| id | journey | roadmap | phase | source_document | promised_outcome | user_entry_point | success_condition | failure_recovery | claimed_completion_evidence | likely_a2_target | planned_a3_scenario |
|----|---------|---------|-------|------------------|------------------|------------------|-------------------|------------------|------------------------------|------------------|---------------------|
| A1-TC-01 | trace/context | V3 | Phase 18 | [V3 §"10. Live IDE Context"](../../roadmap/V3.md#10-live-ide-context); [Phase 18 plan](../../phases/v3/phase-18/IMPLEMENTATION_PLAN.md) | Selected, budgeted, attributable IDE context attached to a run under visible user policy; explicit exclusion and precedence rules; defaults tuned through implementation evidence. | Configure context policy in settings. | Context included per policy. | Off disables automatic injection. | [Phase 18 plan](../../phases/v3/phase-18/IMPLEMENTATION_PLAN.md) closeout. | `ContextPolicy`, `ContextAssembly`. | Disposable profile: set policy, send, observe behavior. |
| A1-TC-02 | trace/context | V3 | Phase 21 | [V3 §"11. Raw Trace and Transparency"](../../roadmap/V3.md#11-raw-trace-and-transparency); [Phase 21 plan](../../phases/v3/phase-21/IMPLEMENTATION_PLAN.md); [Phase 21 M2 evidence](../../phases/v3/phase-21/M2_TRACE_REDACTION_AND_RETENTION_EVIDENCE.md) | User can inspect a raw trace. The trace exposes the deepest truthful level available; redaction is mandatory before persistence/indexing/rendering/export; retention and size limits; capture state marked (disabled/unavailable/redacted/sampled/truncated). | Inspect a trace. | Trace is redacted (no API keys, tokens, secrets, sensitive env vars, sensitive file content). | "Missing evidence is unavailable, not zero." | [Phase 21 M2 evidence](../../phases/v3/phase-21/M2_TRACE_REDACTION_AND_RETENTION_EVIDENCE.md); [Phase 21 M0–M7 closeout](../../phases/v3/phase-21/M7_CLOSEOUT_EVIDENCE.md). | Trace owner, redaction, retention. | Disposable profile: trigger a backend, inspect trace, observe redaction. |
| A1-TC-03 | memory | V3 | Phase 21 | [V3 §"12. Memory and Context Quality"](../../roadmap/V3.md#12-memory-and-context-quality); [Phase 21 plan §"Scope model"](../../phases/v3/phase-21/IMPLEMENTATION_PLAN.md); [Phase 21 M5 evidence](../../phases/v3/phase-21/M5_MEMORY_LIFECYCLE_EVIDENCE.md); [Phase 21 M6 memory influence evidence](../../phases/v3/phase-21/M6_MEMORY_INFLUENCE_EVIDENCE.md) | User can manage durable memory records at scope (Session, Agent, Shared, Conversation). Each record carries provenance, author, creation time, project/workspace scope. Users can inspect, correct, delete, disable, and scope memory. | Manage memory records. | Memory can be created, listed, edited, deleted. | (Documented.) | [Phase 21 M5 evidence](../../phases/v3/phase-21/M5_MEMORY_LIFECYCLE_EVIDENCE.md); [Phase 21 M6 memory influence evidence](../../phases/v3/phase-21/M6_MEMORY_INFLUENCE_EVIDENCE.md). | Memory owner, record types. | Disposable profile: create a memory record, edit, delete. |
| A1-TC-04 | restart/recovery | V3 | Phase 14 + 21 | [Phase 14 plan §"D11–D14" + §"Persistence and recovery contract"](../../phases/v3/phase-14/IMPLEMENTATION_PLAN.md); [Phase 21 plan §"Conversation and Townhall seams"](../../phases/v3/phase-21/IMPLEMENTATION_PLAN.md) | Restart restores durable conversation records (channels, conversations/entries, active selection, drafts, read cursors, unread, direct participant pairs, last active conversation selection). Atomic write + last-known-good on file failure. Conversation snapshot does not persist sessions, runs, backend bindings, capabilities, normalized events, action audit, usage/cost, traces, or memory. | Restart the application. | Drafts, read cursors, active selection restored; sessions are not restored. | Corrupt file falls back to last-known-good. | [Phase 14 M9 evidence](../../phases/v3/phase-14/M9_MANUAL_EVIDENCE.md); [Phase 21 M0–M7 closeout](../../phases/v3/phase-21/M7_CLOSEOUT_EVIDENCE.md). | `ConversationPersistenceService`. | Disposable profile: write a draft, restart, observe restoration. |
| A1-TC-05 | restart/recovery | V3 | Phase 14 + 21 | [Phase 21 plan §"Restart behavior"](../../phases/v3/phase-21/IMPLEMENTATION_PLAN.md); [Phase 14 plan §"D12/D14"](../../phases/v3/phase-14/IMPLEMENTATION_PLAN.md) | Startup classifies interrupted state. Never infers success. Never silently resumes side-effecting work. In-flight runs become terminal interrupted/cancelled/failed records (or are absent if never durable). User must explicitly re-send after restart. | Restart mid-run. | Run becomes terminal interrupted/cancelled/failed. | No silent re-invocation; user must re-send. | [Phase 21 M0–M7 closeout](../../phases/v3/phase-21/M7_CLOSEOUT_EVIDENCE.md); [Phase 14 M9 evidence](../../phases/v3/phase-14/M9_MANUAL_EVIDENCE.md). | `AgentSessionService`, restart classification. | Disposable profile: trigger send, force-quit, observe terminal. |
| A1-TC-08 | trace/context | V3 | Phase 21 M3 | [Phase 21 plan §"M3 Usage and Cost" + §"Verified live baseline" Usage and cost row + §"Ownership model" Usage and cost evidence row](../../phases/v3/phase-21/IMPLEMENTATION_PLAN.md); [Phase 21 M3 evidence](../../phases/v3/phase-21/M3_USAGE_AND_COST_EVIDENCE.md) | User can view usage and cost evidence for a session/run/backend. Token, time, and cost values are surfaced; units, currency, and pricing source are reported. A backend-reported number is not a Zaide-verified billing fact. | View usage/cost for a session or run. | Usage and cost are visible to the user per backend capability. | "Missing evidence is unavailable, not zero." | [Phase 21 M3 evidence](../../phases/v3/phase-21/M3_USAGE_AND_COST_EVIDENCE.md); [Phase 21 M0–M7 closeout](../../phases/v3/phase-21/M7_CLOSEOUT_EVIDENCE.md). | Usage/cost ledger owner. | Disposable profile: trigger a backend, view usage/cost, observe values and provenance. |
| A1-TC-09 | restart/recovery | V3 | Phase 21 M4 | [Phase 21 plan §"M4 Restart and Termination" + §"Verified live baseline" — `AgentSessionService.EndAsync` row + §"Ownership model" Durable session/recovery record row](../../phases/v3/phase-21/IMPLEMENTATION_PLAN.md); [Phase 21 M4 evidence](../../phases/v3/phase-21/M4_RESTART_AND_TERMINATION_EVIDENCE.md) | User can explicitly terminate an active session/run. Termination cancels the active run, emits terminal session events, and removes live ownership. Termination is distinct from deletion, archive, disconnect, and recovery. | End the active session. | Active session/run is terminated; no side effects continue. | Termination cannot undo prior side effects; backend may still be in flight. | [Phase 21 M4 evidence](../../phases/v3/phase-21/M4_RESTART_AND_TERMINATION_EVIDENCE.md); [Phase 21 M0–M7 closeout](../../phases/v3/phase-21/M7_CLOSEOUT_EVIDENCE.md). | `AgentSessionService.EndAsync`. | Disposable profile: start a run, end it, observe terminal state. |

---

## 15. Promises That Cannot Yet Be Translated Into User Behavior

These rows are recorded because the document language suggests a user
behavior but A1 cannot yet map the promise to a user entry point. A1
reports only what the documents say; whether the documented user entry
point is implemented will be answered by A2's wiring audit. These rows
use the `XX` journey key and are **not** counted in the user-goal
total.

| id | journey | roadmap | phase | source_document | document state (A1 does not verdict) | why A1 cannot translate to user behavior |
|----|---------|---------|-------|-----------------|---------------------------------------|------------------------------------------|
| A1-XX-01 | agent create | V3 | Phase 19 + 20 | [DF-008](../../deferred/open/DF-008-multiple-agent-connections.md); [DF-009](../../deferred/open/DF-009-real-acp-integrations.md); [Deferred Findings Index](../../deferred/INDEX.md) | Documents record a missing user-facing workflow for binding Native Harness or ACP backends; the in-memory binding infrastructure exists. A2 must determine whether the production code exposes a supported user entry point or whether the gap remains. | The user entry point is undefined in the document claim. The document's expected behavior names the workflow but the document does not point to a shipped product surface. |
| A1-XX-02 | multi-agent | V1 | Phase 6 | [Phase 6 plan §"Known Gaps"](../../phases/v1/phase-6/IMPLEMENTATION_PLAN.md); [V1 §"Phase 6" — Debate model row](../../roadmap/PHASES.md#phase-6-agent-to-agent-router) | Phase 6 documents that the debate/disagreement model is not implemented as a specialized feature. The roadmap line "Debate model: disagreements surfaced in Townhall" is recorded as **not implemented as a specialized feature**. A2 must confirm no specialized debate surface exists. (This row also absorbs the prior A1-MR-02 placeholder.) | A1 cannot translate "disagreements are surfaced" into a shipped product surface; the document itself records the absence. |
| A1-XX-03 | trace/context | V3 | Phase 21 | [Phase 21 plan §"Verified live baseline"](../../phases/v3/phase-21/IMPLEMENTATION_PLAN.md); [Phase 21 M0–M7 closeout](../../phases/v3/phase-21/M7_CLOSEOUT_EVIDENCE.md) | Phase 21 documents the M0 verified live baseline (pre-implementation state) and the later M2 trace redaction/retention, M3 usage/cost, M5 memory lifecycle, and M6 memory influence milestones as completed neutral-store and management contracts. The M0 baseline describes an earlier state, not the current state. The current-state question is whether a production backend (Native Harness or ACP) actually produces the data these contracts govern and whether the user can reach the surface that displays it. External candidate/provider smoke was not executed per the V3 closeout wording. A2 must determine whether the production code currently delivers the documented surface to the user. The user-observable rows for these surfaces are [A1-TC-02](#14-trace-context-memory-persistence-restart-and-recovery) (trace), [A1-TC-03](#14-trace-context-memory-persistence-restart-and-recovery) (memory), [A1-TC-08](#14-trace-context-memory-persistence-restart-and-recovery) (usage/cost), and [A1-TC-09](#14-trace-context-memory-persistence-restart-and-recovery) (explicit termination). | A1 does not verdict whether a production backend currently produces the data; A1 records the documented contracts and notes that the production-side verification is open. |
| A1-XX-04 | debugging | V2 | Phase 12 + 13 | [V2 §"Phase 12 — C# Debugging" + "Phase 13 — Release Hardening"](../../roadmap/V2.md#phase-12--c-debugging--complete-m0m7-2026-07-14); [Phase 13 M5 closeout](../../phases/v2/phase-13/M5_RELEASE_CLOSEOUT_EVIDENCE.md) | Documents record that DAP environment validation on Linux is constrained: DAP is not re-measured when NetCoreDbg is absent, and desktop debug UI rows remain not validated. A2 must confirm whether the disposable environment can host NetCoreDbg for a re-measurement, or whether the validation gap is real and blocking. | This is a validation condition, not a separate user-observable behavior. A1 records the constraint; A3 will execute only if the disposable environment satisfies it. |
| A1-XX-05 | trace/context | V3 | Phase 21 | [Phase 21 plan §"Verified live baseline" — `ConversationStore` + "Workspace isolation" rows](../../phases/v3/phase-21/IMPLEMENTATION_PLAN.md); [Phase 14 plan §"D15"](../../phases/v3/phase-14/IMPLEMENTATION_PLAN.md) | Phase 14 and Phase 21 document an internal constraint: the conversation store is application-lifetime for Phase 14 and Phase 21 records the lack of multi-window sync. This is an isolation rule, not a user entry point. A2 must confirm whether the production code observes this rule. | The rule is an internal implementation constraint, not a user-observable promise. A1 records the rule; A2 will verify it. |

---

## 16. Internal Architecture Promises Excluded From This Matrix

The following are documented as internal architecture concerns rather
than user-observable outcomes. They are excluded by [AUDIT_PLAN.md §6
quality gates](./AUDIT_PLAN.md#6-quality-gates) and recorded here for
A2 reference only. They are **not** A2 wiring candidates for
user-observable goals; A2 may still inspect them as internal seams
when an `A1-XX-*` or `A1-TC-*` row requires it.

- [Architecture overview §"Source architecture (target vs current)"](../../architecture/OVERVIEW.md) — feature-first ownership and code
  module rules; Refactor 6.1/6.2/6.3 evidence.
- [V3 §16 "Required Foundation Refactors"](../../roadmap/V3.md#16-required-foundation-refactors) — Refactor 5 historical closeout,
  Refactor 6 family, Refactor 7 (Agent and Conversation Domain), Refactor 8
  (Townhall and Conversation UI Foundation), Refactor family planning rule.
- [V3 §17 "Target Source Direction"](../../roadmap/V3.md#17-target-source-direction) — feature-first tree candidate.
- Architecture baselines, public/internal type inventory, dependency
  ratchets.

---

## 17. A1 Closeout and Status

**A1 status:** **accepted on 2026-07-30.** This matrix is the A1
corrective round 8, drafted after the user rejected round 7 because
two bookkeeping items in §17.7 still mis-attributed the `57 + 5 = 62`
correction to round 5: item 23 said the attribution was corrected in
round 5 (it was not — only the count was updated), and item 25
paraphrased the prior parenthetical as having wrongly claimed round 5
corrected the attribution (it had only mentioned round 5 re-verifying
the count). This round 8 separates round 5's count update from the
round 6 attribution correction across items 23 and 25; no goal row,
source, or implementation scope was changed. The A1-acceptance
proceed decision is recorded in
[A1_ACCEPTANCE.md](./A1_ACCEPTANCE.md).

**A2 status (2026-07-31):** **in progress** (not complete as a whole).
Thirteen wiring-audit slices are complete and published; the next
recommended slice is not begun. See
[§17.8](#178-current-a2-progress).

### 17.1 Counts

| Quantity | Count | Source |
|----------|-------|--------|
| Unique user-observable goal rows (`A1-*-NN`) | **57** | Rows in §1–§14. |
| Rows in §15 that cannot be translated into user behavior (`A1-XX-*`) | **5** | §15. |
| Total rows in this matrix | **62** | §1–§15. |
| Rows in §16 (internal architecture, excluded from user goal count) | n/a | §16. |

The 57 unique user-observable goals are the count that enters the
audit's downstream phases. The 5 `A1-XX-*` rows are A1's contribution
to the A4 gap report.

### 17.2 Coverage by journey

| # | Journey | User-goal rows |
|---|---------|----------------|
| 1 | First launch and settings | 6 |
| 2 | Workspace / project opening | 3 |
| 3 | File navigation and editing | 14 (was 7, then 15 after Phase 10 LSP; A1-FN-07 retired in round 2 and merged into A1-SC-02; Phase 10 rows kept at A1-FN-08..15 in round 3) |
| 4 | Search and command discovery | 3 |
| 5 | Build / run / test | 4 |
| 6 | Debugging and output | 1 (A1-DB-02 was moved to XX in round 2) |
| 7 | Terminal | 2 |
| 8 | Git workflow | 4 |
| 9 | Townhall / conversations | 4 (A1-TH-03 was retired in round 2 and merged into A1-TC-04; A1-TH-04 and A1-TH-05 kept at their original positions in round 3) |
| 10 | Agent creation and backend onboarding | 2 (A1-AC-03 removed; A1-AC-04 split into A1-TC-02/03/05 (merged) plus A1-TC-08/09 (new) in round 2) |
| 11 | Agent send / response / failure feedback | 2 (A1-AS-04 merged into A1-TP-01 in round 2; A1-AS-03 merged into A1-TC-05 in round 4; A1-AS-03 retired) |
| 12 | Tools, permissions, and workspace mutation | 3 |
| 13 | Multi-agent routing | 2 (A1-MR-02 was retired in round 2 and consolidated with A1-XX-02; A1-MR-03 kept at its original position in round 3) |
| 14 | Trace, context, memory, persistence, restart, recovery | 7 (A1-TC-07 (workspace isolation) moved to A1-XX-05; A1-TC-08 (usage/cost) and A1-TC-09 (explicit termination) added from A1-AC-04 split; A1-TC-06 merged into A1-TC-04 in round 4; A1-TC-06 retired) |
| 15 | Cannot be translated into user behavior | 5 (not counted above) |

All 14 user journeys have non-zero coverage. No journey is missing.

### 17.3 Corrective changes through round 4

| # | Change | Reason |
|---|--------|--------|
| 1 | Round 2 ID renumbering was reverted. The original `A1-FN-08..15` (Phase 10 rows), `A1-TH-04, A1-TH-05` (Townhall rows), and `A1-MR-03` (typed routing) are restored to their original positions. The freed IDs `A1-FN-07`, `A1-TH-03`, `A1-MR-02` are retired (gaps remain). | Stable audit identifier rule. Round 2 renumbering would have reused these IDs for different content. |
| 2 | `A1-TC-08` source corrected. The original source pointed at V3 §10.1 (Context Policy), but the row documents usage/cost visibility. The new source points at [Phase 21 plan §"M3 Usage and Cost" + §"Verified live baseline" + §"Ownership model"](../../phases/v3/phase-21/IMPLEMENTATION_PLAN.md) and [Phase 21 M3 evidence](../../phases/v3/phase-21/M3_USAGE_AND_COST_EVIDENCE.md). | The V3 §10.1 section is about context-sharing policy, not usage/cost visibility. |
| 3 | `A1-TC-09` source corrected. The original source pointed at the Phase 21 M0 verified live baseline and V3 §13.1, but the row documents explicit session termination. The new source points at [Phase 21 plan §"M4 Restart and Termination" + §"Verified live baseline" — `AgentSessionService.EndAsync` row + §"Ownership model" Durable session/recovery record row](../../phases/v3/phase-21/IMPLEMENTATION_PLAN.md) and [Phase 21 M4 evidence](../../phases/v3/phase-21/M4_RESTART_AND_TERMINATION_EVIDENCE.md). | The M0 baseline is pre-implementation state; the M4 evidence documents the implementation. |
| 4 | `A1-AC-02` verdicts removed. The original `user_entry_point`, `success_condition`, and `failure_recovery` columns all repeated the same "(No user entry point in production UI per DF-008/DF-009.)" string, which was a current-state verdict. Replaced with the Phase 19/20 document claims: configure a Native Harness or ACP backend binding; backend is bound, identity is recorded, capability changes are reported, mediated actions flow through the control plane. | Phase document claims belong in goal rows; current-state observations belong in §17 ambiguity or §15. |
| 5 | `A1-AS-02` verdict removed. The original `failure_recovery` column cited [ISSUE-008](../../issues/open/ISSUE-008-agent-response-not-showing.md) Attempt 1 and stated "production UI may not project the rejection." Replaced with the Phase 15 document claim: backend-reported outcomes (accepted/queued/running/completed/failed/rejected/cancelled/timed-out/disconnected/indeterminate) are reflected in the conversation; cancellation is distinct from cancellation acknowledgment and from late completion. | ISSUE-008 is a current-state observation; it belongs in §17 ambiguity, not in the goal row. |
| 6 | `A1-XX-03` rewritten. The original row used the M0 verified live baseline to conclude that trace/memory/usage/cost are currently absent. The M0 baseline is pre-implementation state; Phase 21 M2 (trace), M3 (usage/cost), M5 (memory), M6 (memory influence) document later implementation. The row now records the documented contracts (M2/M3/M5/M6 evidence files) and notes that the open question for A2 is whether a production backend currently produces the data and whether the user can reach the surface that displays it. External candidate/provider smoke was not executed per the V3 closeout wording. | A1 must read the Phase 21 timeline correctly. The contracts are documented; production-side verification is open. |
| 7 | `§16` duplicate bullet removed. The "Architecture overview" general reference duplicated the specific `Architecture overview §"Source architecture (target vs current)"` reference. The specific reference remains. | §16 had two bullets that both pointed at the same file. |
| 8 | Merged `A1-AS-03` into `A1-TC-05` (auto-resume prohibition on restart). Both rows documented that runs become terminal interrupted/cancelled/failed and are never silently resumed. `A1-AS-03` is retired. The user-must-re-send detail is folded into the merged `A1-TC-05` row. | Two rows for the same user-observable promise about restart behavior. |
| 9 | Merged `A1-TC-06` into `A1-TC-04` (snapshot persistence scope). Both rows documented what the conversation snapshot does and does not persist. `A1-TC-06` is retired. The session/runs/backend/capability/audit/usage/trace/memory exclusion list is folded into the merged `A1-TC-04` row. | Two rows for the same user-observable promise about persistence scope. |

### 17.4 Unresolved documentation ambiguities (A1 → A4 gap report)

These are A1's contribution to the A4 gap report. They are recorded
because A1 cannot resolve them from documents alone; A2's wiring audit
must.

1. **A1-XX-01** — Production backend-binding workflow absent from the
   UI per [DF-008](../../deferred/open/DF-008-multiple-agent-connections.md)
   and [DF-009](../../deferred/open/DF-009-real-acp-integrations.md).
   The infrastructure exists; the user entry point does not. A2 must
   confirm whether the production code exposes a supported user entry
   point or whether the gap remains as documented. This is the
   current-state observation removed from the [A1-AC-02](#10-agent-creation-and-backend-onboarding)
   goal row in round 3.
2. **A1-XX-02** — Debate/disagreement model is not implemented as a
   specialized feature per
   [Phase 6 plan §"Known Gaps"](../../phases/v1/phase-6/IMPLEMENTATION_PLAN.md).
   A2 must confirm no specialized debate surface exists.
3. **A1-XX-03** — Trace, memory, and usage/cost product surfaces:
   Phase 21 documents the M2/M3/M5/M6 implementation milestones as
   completed neutral-store and management contracts. The M0 verified
   live baseline is pre-implementation state. The remaining
   current-state question for A2 is whether a production backend
   (Native Harness or ACP) actually produces the data these contracts
   govern and whether the user can reach the surface that displays
   it. External candidate/provider smoke was not executed per the V3
   closeout wording. The corresponding user-observable rows are
   [A1-TC-02](#14-trace-context-memory-persistence-restart-and-recovery),
   [A1-TC-03](#14-trace-context-memory-persistence-restart-and-recovery),
   [A1-TC-08](#14-trace-context-memory-persistence-restart-and-recovery),
   and [A1-TC-09](#14-trace-context-memory-persistence-restart-and-recovery).
   Coupled with the open issues
   [ISSUE-008](../../issues/open/ISSUE-008-agent-response-not-showing.md)
   and
   [ISSUE-009](../../issues/open/ISSUE-009-production-di-test-contaminates-conversation-store.md),
   this is the highest-risk area for V1–V3 product reality.
4. **A1-XX-04** — DAP environment validation is constrained. A2 must
   confirm whether the disposable environment can host NetCoreDbg, or
   whether the validation gap is real and blocking. A3 for
   [A1-DB-01](#6-debugging-and-output) is gated on this.
5. **A1-XX-05** — Workspace isolation is an internal constraint. A2
   must confirm whether the production code observes it. A3 does not
   need a smoke scenario for this row.
6. **Routing evolution from Phase 6 to Phase 14.** Phase 6 documents
   panel-chrome-bound routing; [Phase 14 plan §D09](../../phases/v3/phase-14/IMPLEMENTATION_PLAN.md)
   promises `ActorId`/catalog routing. A2 must confirm whether the
   legacy panel-bound path was actually removed.
7. **External candidate/provider smoke is not executed** for
   [Phase 19](../../phases/v3/phase-19/IMPLEMENTATION_PLAN.md),
   [Phase 20](../../phases/v3/phase-20/IMPLEMENTATION_PLAN.md), or
   [Phase 21](../../phases/v3/phase-21/IMPLEMENTATION_PLAN.md) per
   the V3 closeout wording. A1 records this as an evidence gap, not
   a claim of completion. A2 must determine whether the production
   code can be smoke-tested without external candidates or whether
   the gap is real and blocking.
8. **Agent send response projection (removed from [A1-AS-02](#11-agent-send--response--failure-feedback) in round 3).**
   Per [ISSUE-008 Attempt 1](../../issues/open/ISSUE-008-agent-response-not-showing.md),
   an unbound send is rejected before backend execution and the
   production UI may not project the rejection. A2 must determine
   whether the production code projects the rejection into the
   conversation.
9. **Conversation draft contamination (ISSUE-009).**
   [ISSUE-009](../../issues/open/ISSUE-009-production-di-test-contaminates-conversation-store.md)
   records that a production-composition singleton test mutates
   `TownhallViewModel.DraftText` and disposal flushes the marker into
   the production conversation snapshot. A2 must determine whether
   the disposable-profile A3 isolation rule prevents this in practice.

### 17.5 Recommended first A2 wiring-audit slice

**Historical first-slice definition (A1 closeout).** This subsection
records the A1-named first A2 wiring-audit slice. It is preserved as
the historical charter for `A2_AGENT_SEND`. For live A2 progress
(completed slices, verdicts, and the next recommended slice), see
[§17.8](#178-current-a2-progress).

**Slice name:** `A2_AGENT_SEND`. The first A2 wiring-audit slice was
`A2_AGENT_SEND` and its evidence file is
[evidence/A2_AGENT_SEND.md](./evidence/A2_AGENT_SEND.md).

The scope of the `A2_AGENT_SEND` slice is the **agent send and response
feedback journey** ([§11](#11-agent-send--response--failure-feedback),
2 rows) together with the **Townhall projection** portion of
[§9](#9-townhall--conversations) and
[§10](#10-agent-creation-and-backend-onboarding). Rationale:

- It is the smallest journey that already has two open issues
  ([ISSUE-008](../../issues/open/ISSUE-008-agent-response-not-showing.md),
  [ISSUE-009](../../issues/open/ISSUE-009-production-di-test-contaminates-conversation-store.md))
  and two open deferred findings
  ([DF-008](../../deferred/open/DF-008-multiple-agent-connections.md),
  [DF-009](../../deferred/open/DF-009-real-acp-integrations.md))
  attached, so A2's findings will be concrete rather than speculative.
- It intersects with the most `A1-XX-*` rows (1, 3), so A2 will either
  confirm the document gap or surface a wiring that A1 missed.
- It exercises the cross-cutting seams A2 needs to learn to audit
  elsewhere: `AgentSessionService`, `AgentExecutionCoordinator`,
  `AgentRouter`, `AgentActorBackendBindingStore`, `ConversationStore`,
  `ConversationPersistenceService`, the Townhall projection path, and
  the production DI composition root that was identified in
  [ISSUE-009](../../issues/open/ISSUE-009-production-di-test-contaminates-conversation-store.md)
  as the test-isolation failure surface.
- A2 can complete the slice in one bounded evidence file
  (`evidence/A2_AGENT_SEND.md`) without touching the IDE layer, the
  editor, the terminal, the Git surface, the LSP/DAP seams, or the
  persistence engine — those journeys are easier to audit once the
  agent seams are mapped.

### 17.6 A1-acceptance gate

A1 is **accepted** on 2026-07-30. The
[AUDIT_PLAN.md §7.1](./AUDIT_PLAN.md#71-a1-acceptance-gate-authorizes-a2)
A1-acceptance gate has passed. Concretely:

- All `source_document` cells are clickable repo-relative markdown
  links to existing files. (Verified in corrective round 8.)
- All `claimed_completion_evidence` cells that point to files are
  clickable repo-relative markdown links to existing files. (Verified
  in corrective round 8.)
- Duplicate promises are merged into one row. (Verified through
  corrective round 4; the last two merges — `A1-AS-03 → A1-TC-05` and
  `A1-TC-06 → A1-TC-04` — were performed in round 4.)
- Non-goal rows are in the `A1-XX-*` section, not in the user-goal
  total. (Verified in corrective round 2.)
- Open deferred findings are not counted in the user-goal total.
  (Verified in corrective round 2.)
- A1 rows do not contain implementation verdicts. A1 records the
  document's claim, not whether it is implemented. (Verified in
  corrective round 2.)
- Composite promises are decomposed into independently-verifiable
  rows. (Verified in corrective round 2.)
- Every journey in
  [AUDIT_PLAN.md §4](./AUDIT_PLAN.md#4-inventory-scope--user-journeys)
  has at least one user-observable row, and the count per journey is
  recorded in [§17.2](#172-coverage-by-journey).
- The `A1-XX-*` "cannot be translated" section is recorded as a
  separate count (5 rows) and is not merged into the 57 user-goal
  total.
- The first A2 wiring-audit slice is named `A2_AGENT_SEND` in
  [§17.5](#175-recommended-first-a2-wiring-audit-slice) and in
  [A1_ACCEPTANCE.md §3](./A1_ACCEPTANCE.md#3-first-a2-wiring-audit-slice).

The A1-acceptance proceed decision is recorded in
[A1_ACCEPTANCE.md](./A1_ACCEPTANCE.md) and is the sole artifact that
authorizes A2. A2 does **not** begin in the A1-acceptance session. A2
begins in a new session after that recorded proceed decision. A2 has
since begun in subsequent sessions; completed slices and the next
recommended slice are recorded in
[§17.8](#178-current-a2-progress).

### 17.7 Corrective history

| # | Item | Status |
|---|------|--------|
| 1 | Phase 10 C# Language Intelligence user-observable goals added (§3, rows A1-FN-08 through A1-FN-15). | done in round 1 |
| 2 | All `source_document` cells converted to clickable repo-relative markdown links. | done in round 1 |
| 3 | All `claimed_completion_evidence` cells that point to files converted to clickable repo-relative markdown links. | done in round 1 |
| 4 | ID rule unified to `A1-<journey-key>-<nn>` in [AUDIT_PLAN.md §5](./AUDIT_PLAN.md#5-goal-matrix-schema); matrix matches. | done in round 1 |
| 5 | A1-acceptance gate separated from A4 V4-proceed decision in [AUDIT_PLAN.md §7](./AUDIT_PLAN.md#7-gates-a1-acceptance-and-a4-v4-proceed-decision); circular wording removed. | done in round 1 |
| 6 | `A1-XX-*` rows reframed to record document state, not implementation verdicts. | done in round 1 |
| 7 | Duplicate rows merged: A1-FN-07 → A1-SC-02, A1-TH-03 → A1-TC-04, A1-AS-04 → A1-TP-01. | done in round 2 |
| 8 | Composite row decomposed: A1-AC-04 split into A1-TC-02 (merge), A1-TC-03 (merge), A1-TC-05 (keep), A1-TC-08 (new, usage/cost), A1-TC-09 (new, explicit termination). The freed A1-TC-07 number is retired. | done in round 2 |
| 9 | Non-goal rows moved to XX: A1-MR-02 → A1-XX-02 (consolidated), A1-DB-02 → A1-XX-04, A1-TC-07 (workspace isolation) → A1-XX-05. | done in round 2 |
| 10 | Open deferred finding A1-AC-03 removed from goal count; covered by A1-XX-01. | done in round 2 |
| 11 | Retired IDs (A1-FN-07, A1-TH-03, A1-MR-02, A1-TC-07) preserved; remaining journey IDs kept at original positions. The renumbering in round 2 was reverted in round 3 because it would have caused ID reuse. | done in round 3 |
| 12 | A1-TC-08 source corrected (Phase 21 M3 + M3 evidence, not V3 §10.1). | done in round 3 |
| 13 | A1-TC-09 source corrected (Phase 21 M4 + M4 evidence, not M0 baseline). | done in round 3 |
| 14 | A1-AC-02 and A1-AS-02 implementation verdicts removed; current-state observations moved to §17.4 ambiguity. | done in round 3 |
| 15 | A1-XX-03 rewritten to acknowledge Phase 21 M2/M3/M5/M6 evidence. | done in round 3 |
| 16 | §16 duplicate bullet removed. | done in round 3 |
| 17 | A1-acceptance gate and A4 V4-proceed gate re-verified after round 3. | done in round 3 |
| 18 | `A1-AS-03` merged into `A1-TC-05`; `A1-TC-06` merged into `A1-TC-04`. Both merged IDs retired. User goal count 59 → 57. | done in round 4 |
| 19 | Fragment anchors verified against actual headings in target files. Broken anchors repaired. | done in round 4 |
| 20 | A1 closeout counts re-recorded with accurate values (57 user goals + 5 XX = 62). The 57+5=62 count was determined in round 4 after `A1-AS-03` and `A1-TC-06` were merged; earlier rounds recorded 59+5=64 and 65+5=68. | done in round 4 (counts re-verified in later rounds; round attribution corrected in round 6) |
| 21 | `git diff --check` and link verification re-run after this corrective round. | enforced every round |
| 22 | A2 not started in A1 corrective/acceptance rounds; no app, build, or test execution; no production code or test edits; no commit or push. | enforced in A1 rounds (current state: A2 in progress) |
| 23 | Round 5 closeout-staleness fixes: §17.6 line credited all duplicate merges to round 2 (now reflects round 4), §17.7 had duplicate item numbers 12–14 (renumbered to 20–22), §17.7 had a stale `59 + 5 = 64` count, the `Outstanding corrective items` header was renamed to `Corrective history`, the §17.3 header was widened to `Corrective changes through round 4`, and the current count was updated to `57 + 5 = 62`. | done in round 5 (round attribution of that count remained stale and was corrected in round 6) |
| 24 | Round 6 closeout-staleness fixes: §17.2 DB and AC `in this round` → `in round 2`; §17.5 agent-send slice updated from `3 rows` to `2 rows`; §17.5 deferred-finding count updated from `one` to `two`; §17.7 title widened to `rounds 1–5`; §17.7 item 20 attribution corrected from `round 2` to `round 4`. | done in round 6 |
| 25 | Round 7 closeout-staleness fixes: §17.7 title `rounds 1–5` collapsed to plain `Corrective history` so the range no longer has to be re-widened each round; §17.7 item 20 trailing parenthetical rephrased to distinguish round 5 re-verification of the count from the round 6 correction of the count's round attribution. | done in round 7 |
| 26 | Round 8 closeout-staleness fixes: §17.7 item 23's last clause no longer claims round 5 corrected the `57 + 5 = 62` attribution (it only updated the count); round attribution is now explicitly attributed to round 6. §17.7 item 25 rephrased to distinguish round 5 re-verification of the count from the round 6 correction of the count's round attribution, without claiming the prior parenthetical had asserted an attribution fix. | done in round 8 |
| 27 | A1 acceptance recorded. `A1_ACCEPTANCE.md` created with the A1-acceptance proceed decision; `AUDIT_PLAN.md` current-phase line and footer updated to reflect A1 accepted; `GOAL_MATRIX.md` header status, §17 status, §17.5 (slice name `A2_AGENT_SEND`), §17.6, and this item updated. Counts preserved at 57 user goals + 5 `A1-XX-*` rows = 62 total. A2 not begun in this session; no production code or test edits; no commit or push. | done in acceptance round (2026-07-30) |

All items above are completed **A1** corrective history. A1 was
**accepted on 2026-07-30** via
[A1_ACCEPTANCE.md](./A1_ACCEPTANCE.md), which is the sole artifact
that authorizes A2. A2 was **not** begun in the A1-acceptance session
(per rule). A2 progress after acceptance is recorded in
[§17.8](#178-current-a2-progress) and is not an A1 corrective round.

### 17.8 Current A2 progress

**A2 status:** **in progress** (not complete as a whole). Status date:
2026-07-31.

| Slice | Status | Evidence | Verdicts / dispositions |
|-------|--------|----------|-------------------------|
| `A2_AGENT_SEND` | **Complete and published** | [evidence/A2_AGENT_SEND.md](./evidence/A2_AGENT_SEND.md) | `A1-AS-01` = **Missing**; `A1-AS-02` = **Wired-with-gap** |
| `A2_MULTI_AGENT_ROUTING` | **Complete and published** | [evidence/A2_MULTI_AGENT_ROUTING.md](./evidence/A2_MULTI_AGENT_ROUTING.md) | `A1-MR-01` = **Missing**; `A1-MR-03` = **Wired-with-gap**; `A1-XX-02` = **confirmed absent** (scoped disposition only; not a user-goal verdict) |
| `A2_TRACE_MEMORY_USAGE_TERMINATION` | **Complete and published** | [evidence/A2_TRACE_MEMORY_USAGE_TERMINATION.md](./evidence/A2_TRACE_MEMORY_USAGE_TERMINATION.md) | `A1-TC-02` = **Missing**; `A1-TC-03` = **Missing**; `A1-TC-08` = **Missing**; `A1-TC-09` = **Missing**; `A1-XX-03` = scoped disposition only (not a user-goal verdict): production appends memory-influence evidence during session context assembly; production does not expose user-managed lifecycle-memory creation or management UI; trace and usage producers and explicit termination UI remain absent |
| `A2_RESTART_RECOVERY_AND_CONTEXT` | **Complete and published** | [evidence/A2_RESTART_RECOVERY_AND_CONTEXT.md](./evidence/A2_RESTART_RECOVERY_AND_CONTEXT.md) | `A1-TC-01` = **Wired-with-gap** (Townhall direct-conversation context selector user-reachable; no settings entry or configurable application default; overrides in-memory and lost on restart; Off zero-item/zero-token manifest with possible policy metadata); `A1-TC-04` = **Wired-with-gap** (conversation snapshot load/save/restore production-composed; persistence failures/recovery outcomes not user-visible; Zaide’s explicit shutdown does not dispose/flush `ConversationPersistenceService`; later framework/root-provider disposal unproven); `A1-TC-05` = **Wired-with-gap** (startup `Reconcile` not `Resume`; no automatic backend re-invocation; stored checkpoints may be `Recoverable`; normal cold start empty unpersisted binding store → revalidation `Indeterminate`; classification and re-send not projected to Townhall); `A1-XX-05` = scoped disposition only (not a user-goal verdict): conversation persistence application/user-config scoped; no multi-window sync; Phase 21 durable keys path-derived but production uses process CWD, not a proven opened-workspace-root provider |
| `A2_TOOLS_PERMISSIONS` | **Complete and published** | [evidence/A2_TOOLS_PERMISSIONS.md](./evidence/A2_TOOLS_PERMISSIONS.md) | `A1-TP-01` = **Wired-with-gap** (run-scoped Phase 17 broker paths for tool-capable Native Harness and ACP; no user-reachable backend-binding workflow; `AgentActionFactPayload` / `AgentActionAuditRecord` lack explicit initiating/target actor IDs; several pre-admission/early broker returns backend-visible only; Townhall projects only emitted `ActionResultReported`; ACP lacks delete/command mediation); `A1-TP-02` = **Wired-with-gap** (five-kind permission model, exact-request decisions, expiry, lifecycle revocation partially wired; no dedicated network/Git/secrets/destructive/memory dimensions; no selectable approval scope or user-reachable permission management/revocation UI; ACP `session/request_permission` automatic reject-preferring: `reject_once` when present else first option, which may be permissive; not user-reachable, not guaranteed fail-closed, separate from Phase 17 broker authorization); `A1-TP-03` = **Wired-with-gap** (base-revision checks, workspace-generation invalidation, single non-terminal action admission wired; `TryConsume()` is final authorization not final safety check; pre-consume stale detection preserves `Published`; post-consume validation can fail after `Consumed` without applying effect; no multi-file transactions, agent change sets, rollback UI/commands, or multi-file partial-apply cancellation semantics) |
| `A2_AGENT_CREATION_AND_BACKEND_ONBOARDING` | **Complete and published** | [evidence/A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./evidence/A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md) | `A1-AC-01` = **Missing** (historical Phase 5 Agent Panel creation path retired; no user create/rename/remove/configure-agent workflow); `A1-AC-02` = **Wired-with-gap** (Native Harness and ACP independently composed as sibling backends; in-memory per-actor binding and pull-based status projection exist; no user bind/configure/unbind/persist workflow; local selection-service auth is not bridged to real ACP `authenticate`; negotiated auth methods and capability changes are not user-projected); `A1-XX-01` = gap **confirmed** (scoped disposition only; not a user-goal verdict): binding infrastructure and status visibility exist, but supported user onboarding entry point remains absent |
| `A2_TOWNHALL_AND_CONVERSATIONS` | **Complete and published** | [evidence/A2_TOWNHALL_AND_CONVERSATIONS.md](./evidence/A2_TOWNHALL_AND_CONVERSATIONS.md) | `A1-TH-01` = **Wired-with-gap** (center-shell channels, channel activity, and All/Chat/Activity filters are user-reachable; presentation kinds and filter scope remain limited, and custom channels are not user-creatable); `A1-TH-02` = **Wired** (People → Zaide Agent uses one private direct conversation per unordered pair, with persisted selection, drafts, unread state, and read state); `A1-TH-04` = **Wired** (Agent Panel chrome retired; Townhall is the sole user-facing direct-conversation re-entry); `A1-TH-05` = **Wired-with-gap** (routing failures appear in the source; admitted execution and terminal outcomes appear in the target direct conversation; successful routed flow is not shown in the source and pre-admission rejection remains invisible) |
| `A2_FIRST_LAUNCH_AND_SETTINGS` | **Complete and published** | [evidence/A2_FIRST_LAUNCH_AND_SETTINGS.md](./evidence/A2_FIRST_LAUNCH_AND_SETTINGS.md) | `A1-FL-01` = **Wired-with-gap** (current multi-column shell and bottom-panel toggles user-reachable; historical Phase 0 three-panel/right-agent layout no longer describes it); `A1-FL-02` = **Wired-with-gap** (Dark, Fluent, Semi.Avalonia, and Navy palette composed; historical “Ayaka Violet” wording and user theme switcher absent); `A1-FL-03` = **Wired-with-gap** (schema-v3 load/save/migration and status-bar settings UI production-wired; load/write recovery and disk-write failure not user-visible); `A1-FL-04` = **Wired-with-gap** (separate secret store and environment fallback wired; on-disk permission/plaintext-absence behavior A3-unproven); `A1-FL-05` = **Wired** (editor and terminal defaults configurable, persisted, and live-applied); `A1-FL-06` = **Wired-with-gap** (settings recovery product-wired; performance budgets harness/closeout evidence, not product surface) |
| `A2_WORKSPACE_AND_PROJECT_OPENING` | **Complete and published** | [evidence/A2_WORKSPACE_AND_PROJECT_OPENING.md](./evidence/A2_WORKSPACE_AND_PROJECT_OPENING.md) | `A1-WO-01` = **Wired-with-gap** (folder open, tree, ignore rules, hidden-file toggle, and new file/folder paths user-reachable; file-tree failure messages not projected to UI); `A1-WO-02` = **Wired-with-gap** (one project-context service shared by status, LSP, Build, and Debug; ambiguous multi-project selection has no user-reachable picker and is mislabeled “Project error”); `A1-WO-03` = **Wired-with-gap** (folder open/close updates `WorkspacePath`, emits `WorkspaceFolderChanged`, refreshes project context and Source Control; Source Control refresh coupled to RootPath host path rather than workspace event) |
| `A2_FILE_NAVIGATION_AND_EDITING` | **Complete and published** | [evidence/A2_FILE_NAVIGATION_AND_EDITING.md](./evidence/A2_FILE_NAVIGATION_AND_EDITING.md) | `A1-FN-01`, `A1-FN-03`–`A1-FN-06`, `A1-FN-09`, and `A1-FN-11`–`A1-FN-14` = **Wired**; `A1-FN-02` = **Wired-with-gap** (left splitter is 180–320px, not the claimed 180–500px); `A1-FN-08` = **Wired-with-gap** (Problems projection wired, but diagnostics only for open tracked documents and cold success requires eligible project context plus external `csharp-ls`); `A1-FN-10` = **Wired-with-gap** (caret-dwell rather than pointer hover); `A1-FN-15` = **Wired-with-gap** (default-off setting and save path wired, but save suppresses formatting failures and uses a different apply path) |
| `A2_SEARCH_AND_COMMAND_DISCOVERY` | **Complete and published** | [evidence/A2_SEARCH_AND_COMMAND_DISCOVERY.md](./evidence/A2_SEARCH_AND_COMMAND_DISCOVERY.md) | `A1-SC-01` = **Wired-with-gap** (registry, defaults, settings overrides, empty-string unbind, conflict logging, and live keybinding materialization wired; no Settings keybindings editor and conflicts are log-only); `A1-SC-02` = **Wired-with-gap** (palette open/filter/order/availability/execute/focus path wired; pointer click does not reselect the clicked row before execution); `A1-SC-03` = **Wired** (Phase 9 find/replace, folding, and tab command IDs registered and palette-reachable when unbound) |
| `A2_BUILD_RUN_AND_TEST` | **Complete and published** | [evidence/A2_BUILD_RUN_AND_TEST.md](./evidence/A2_BUILD_RUN_AND_TEST.md) | `A1-BR-01` = **Wired** (project target selection reads the shared project context; locked build/run/test profiles, cancellation, one-at-a-time admission, output/state streams, and show-on-start surfaces are wired); `A1-BR-02` = **Wired** (build diagnostics parse into a separate Problems list with generation-safe navigation while preserving LSP items); `A1-BR-03` = **Wired** (console-first test parsing, summary/status/cases, dedicated Test Results panel, and shared cancellation); `A1-BR-04` = **Wired** (redirected managed-process output is separate from the PTY terminal and bottom-panel modes are mutually exclusive) |
| `A2_DEBUGGING_AND_OUTPUT` | **Complete and published** | [evidence/A2_DEBUGGING_AND_OUTPUT.md](./evidence/A2_DEBUGGING_AND_OUTPUT.md) | `A1-DB-01` = **Wired-with-gap** (DAP lifecycle, supported C# launch handoff, breakpoints, execution controls, stack/scopes/variables, current location, debug console, panel composition, and recovery are wired; NetCoreDbg availability, launch configurability, and visual/interactive limitations remain); `A1-XX-04` = scoped disposition only (DAP validation requires a disposable host with NetCoreDbg via `ZAIDE_NETCOREDBG_PATH` or `PATH`; not a user-goal verdict) |
| `A2_TERMINAL` | **Next recommended; explicitly not begun** | (no evidence file; no verdict assigned) | Scope: `A1-TR-01` and `A1-TR-02` |

Notes:

- A2 remains open after these thirteen slices; remaining user-goal rows
  still require wiring audit.
- `A1-XX-02` is recorded here only as a scoped disposition from the
  multi-agent routing slice, not as a third user-goal verdict and not
  as a change to the §15 row data.
- `A1-XX-03` is recorded here only as a scoped disposition from the
  trace/memory/usage/termination slice, not as a fifth user-goal
  verdict and not as a change to the §15 row data.
- `A1-XX-05` is recorded here only as a scoped disposition from the
  restart/recovery/context slice, not as a user-goal verdict and not
  as a change to the §15 row data. It is not represented as
  `Wired`, `Wired-with-gap`, `Missing`, or `Ambiguous`.
- `A1-XX-01` is recorded here only as a scoped disposition from the
  agent-creation/backend-onboarding slice, not as a third user-goal
  verdict and not as a change to the §15 row data.
- `A2_TERMINAL` is next recommended and explicitly not begun;
  no evidence file exists, no verdict has been assigned, and no
  production work or A3 execution has started.
- A3, A4, stabilization, and V4 work are not begun.

---

*Last updated: 2026-07-31 (`A2_AGENT_SEND`,
`A2_MULTI_AGENT_ROUTING`, `A2_TRACE_MEMORY_USAGE_TERMINATION`,
`A2_RESTART_RECOVERY_AND_CONTEXT`, `A2_TOOLS_PERMISSIONS`, and
`A2_AGENT_CREATION_AND_BACKEND_ONBOARDING`, and
`A2_TOWNHALL_AND_CONVERSATIONS`, and
`A2_FIRST_LAUNCH_AND_SETTINGS`, and
`A2_WORKSPACE_AND_PROJECT_OPENING`, and
`A2_FILE_NAVIGATION_AND_EDITING`, `A2_SEARCH_AND_COMMAND_DISCOVERY`,
`A2_BUILD_RUN_AND_TEST`, and `A2_DEBUGGING_AND_OUTPUT` complete and
published; A2 in
progress, not complete as a whole; next recommended slice
`A2_TERMINAL` explicitly not begun (no evidence file; no
verdict assigned; no production work or A3 execution); A3,
A4, stabilization, and V4 work not begun.)*
