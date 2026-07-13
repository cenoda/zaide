# Phase 9: Editor UX — Implementation Plan

## Status

**M0–M3 complete.** See `docs/phases/v2/phase-9/M0_EDITOR_UX_PROOF.md` for the
full proof document. Phase 8.1 (Settings Foundation), Phase 8.2 (Command
Registry and Keybindings), and Phase 8.3 (Authoritative Project Context) are
closed. This plan defines the next bounded product phase and must be completed
in milestone order.

## Pre-Implementation Verification (M0)

- [x] Re-checked the live `EditorViewModel`, `EditorTabViewModel`, `EditorView`,
      `EditorTabBar`, `MainWindowViewModel`, `MainWindow`, `StatusBarViewModel`,
      `CommandRegistry`, and Phase 8.2 keybinding lifecycle before adding code.
- [x] Confirmed AvaloniaEdit 12.0.0 API availability via compile-backed proof.
      **No direct package reference needed for search or folding**
      — `Avalonia.AvaloniaEdit` is transitive from `AvaloniaEdit.TextMate` 12.0.0.
      Key finding: `AbstractFoldingStrategy` **does not exist** in 12.0.0;
      M4 must implement folding directly with `NewFolding` +
      `FoldingManager.UpdateFoldings()`. All other expected APIs are available.
- [x] Confirmed the existing document/tab ownership model can carry ordering,
      active-tab movement, and dirty-close behavior. **Structural change
      recommended (not required):** switch `TextEditor.Document` instead of
      syncing `.Text` to isolate per-tab undo stacks and folding state.
- [x] Defined command IDs, categories, default gestures, availability rules,
      palette ordering/search rules, and focused test files in the proof
      document. See `M0_EDITOR_UX_PROOF.md` §3 and §7.
- [x] Recorded that `MainWindow` reuses one `EditorView` and one `TextEditor`.
      Locked reset/restore rules for search, folding, caret, selection, and
      focus on every active-tab switch and close. See proof document §4.
- [x] Defined selection-state projection end-to-end: `TextArea.SelectionChanged`
      input on `EditorView`, three new fields on `EditorViewModel`
      (`SelectionStart`, `SelectionLength`, `SelectionText`), and conditional
      `| Sel {len}` suffix on `StatusBarViewModel.CaretText`. Zero selection
      = `SelectionLength == 0`. See proof document §5.
- [x] Ran the sequential baseline gates:
      `dotnet build Zaide.slnx --no-restore` ✅ and
      `dotnet test Zaide.slnx --no-build` ✅ (1207 passed, 0 failed).

## M3 Search and Replace Contract (Locked)

### Matching algorithm

- **Literal substring only.** Regex is never used. Special regex characters
  (`.` `*` `+` `?` `(` `)` `[` `]` `\` `^` `$` `|` `{` `}`) are treated as
  literal characters.
- **Case-sensitive by default** (`CaseSensitive = true`).
  - Case-sensitive: `StringComparison.Ordinal`.
  - Case-insensitive: `StringComparison.OrdinalIgnoreCase`.
- **Non-overlapping matches.** Adjacent matches are found greedily left-to-right.
- **Empty query** produces zero matches and performs no mutation.

### Navigation (Find Next / Find Previous)

- **Find Next** advances to the next match (index + 1). Wraps from the last
  match to the first (index 0). Sets the editor selection to the match range
  and scrolls it into view.
- **Find Previous** goes to the previous match (index − 1). Wraps from the
  first match (index 0) to the last. Sets the editor selection and scrolls.
- **Initial position** after a search: index 0 (first match).

### Zero-match feedback

- `StatusMessage` is set to `"No matches found"` when the query produces zero
  matches.
- Find Next / Find Previous are unavailable (CanExecute = false) when there
  are zero matches.

### Replacement semantics

- **Replace Next:** If the current editor selection exactly covers the current
  match (same offset and length), the matched text is replaced with the
  replacement text. The document is then re-searched, and the next match at
  or after the replacement end-offset is selected. If no match follows,
  selection wraps to the first match. If the selection does NOT match, the
  command advances to the next match without replacing.
- **Replace All:** All literal matches in the active document are replaced in
  a single pass. The operation is wrapped in exactly **one undo group** using
  AvaloniaEdit's `UndoStack.StartUndoGroup()` / `EndUndoGroup()` API.
  After replacement, the document is re-searched; if no matches remain,
  `StatusMessage` reports `"Replaced N occurrences"`.

### Undo grouping

- Replace All uses `TextDocument.UndoStack.StartUndoGroup()` before the first
  replacement and `EndUndoGroup()` after the last. This makes the entire
  Replace All operation a single undoable user action.
- Individual Replace Next operations are separate undo entries (each SetText
  call is one undo step).

### Dirty state

- All content changes flow through `IEditorTextOperations.SetText()`, which
  in production calls `TextEditor.Text = ...`, triggering `EditorView.OnTextChanged`
  which sets `ViewModel.TextContent`, which sets `Document.Content`, which
  sets `IsDirty = true`. Dirty state is always truthful after any mutation.
- Find Next, Find Previous, and Dismiss do NOT mutate the document.

### Tab switching / closing

- Setting `EditorSearchViewModel.ActiveDocument` to a different value (or null)
  calls `Reset()` which clears: query, replacement text, matches, current match
  index, visibility, replace mode, and status message. It also clears the
  editor selection via `SetSelection(0, 0)`.
- In `MainWindow`, the active-tab subscription sets
  `_searchViewModel.ActiveDocument = active is not null ? _editorView : null`
  on every tab switch, ensuring stale state from the old document never
  reaches the new one.
- The old document's text is never read or mutated after the switch.

### Cancellation / dismissal

- `Dismiss()` closes the search surface without any document text mutation.
  It clears the query, matches, selection, replace mode, and status message.
- The editor selection is cleared (set to zero-length at offset 0).

### Commands registered by M3

| ID | Default gesture | Availability |
|---|---|---|
| `editor.find` | Ctrl+F | Active tab non-null |
| `editor.replace` | Ctrl+H | Active tab non-null |
| `editor.findNext` | F3 | Active tab non-null, non-empty query, matches > 0 |
| `editor.findPrevious` | Shift+F3 | Active tab non-null, non-empty query, matches > 0 |
| `editor.replaceNext` | (unbound) | Active tab non-null, non-empty query, matches > 0, replace mode |
| `editor.replaceAll` | (unbound) | Active tab non-null, non-empty query, matches > 0, replace mode |

### Architecture

- `SearchEngine` — pure static class, no dependencies. All matching logic.
- `IEditorTextOperations` — interface implemented by `EditorView` (View layer).
  Provides `GetText`, `SetText`, `SetSelection`, `GetSelectionOffset`,
  `GetSelectionLength`, `ReplaceAllMatches`.
- `EditorSearchViewModel` — singleton, depends on `ICommandRegistry` and
  `IEditorTextOperations` (set via `ActiveDocument` property). No Avalonia
  references.

## Scope

**Goal:** Make everyday single-document editing faster and predictable by
making registered editor actions discoverable, searchable, keyboard-configurable,
and consistent across the editor, tab bar, and status bar.

### Included

- Command Palette backed by the Phase 8 `ICommandRegistry`
- Editor command registration and settings-driven shortcut materialization
- Search and Replace in the active document
- Code folding in the active editor
- Tab navigation, ordering, closing, and dirty-state UX improvements
- Accurate editor/status-bar feedback for active document, caret, selection,
  search, and command outcomes
- Consistent focus restoration, caret movement, and selection behavior for
  every Phase 9 command

### Boundaries

- Reuse `Document`, `Workspace`, `EditorTabViewModel`, and the existing active
  document ownership model. Do not create a second document or tab manager.
- Search and Replace is active-document, literal-text behavior only. Workspace
  search, regex, globbing, and language-aware rename are out of scope.
- Folding is presentation/navigation behavior over the active editor text. It
  must not parse or semantically understand C#; language-semantic folding is a
  future LSP concern.
- Do not add multi-cursor editing, LSP completion/diagnostics/navigation,
  formatting, Build/Run/Test, debugging, a menu bar, or command parameters.
- Do not change Phase 8 settings persistence/schema ownership merely to make
  Phase 9 convenient. New persisted shortcut defaults must use the established
  keybinding contract and migrations only when genuinely required.
- Views remain thin. Services never reference Views or ViewModels; ViewModels
  never reference Views directly.

## Why This Is One Phase, Not Separate Sub-Phases

The five roadmap outcomes share the same active-document, command-registry, and
focus ownership seams. Splitting them into independently owned sub-phases would
create repeated M0 gates and cross-plan command/focus coupling without a useful
ownership boundary. Instead, Phase 9 uses independently verifiable milestones;
M5 is deliberately split into M5a and M5b because tab lifecycle commands and
pointer-driven ordering are separately risky and each can fit one session.

## Milestones

| Milestone | Scope and completion condition | Focused verification |
|---|---|---|
| **M0** | Live-code and library proof only. Publish `M0_EDITOR_UX_PROOF.md` with the exact AvaloniaEdit API findings, existing ownership graph, command inventory, command IDs/gestures, focus contract, milestones, test-file list, and exact full gates. No production behavior. | Add `Phase9M0EditorUxProofTests`; run `dotnet test Zaide.slnx --filter FullyQualifiedName~Phase9M0EditorUxProofTests`, then `git diff --check`. |
| **M1** | Add the UI-independent palette query/presentation seam and the command IDs required by Phase 9. The palette must enumerate only registry descriptors, use deterministic category/display-name/ID ordering, perform case-insensitive literal filtering, and report unavailable commands without executing them. Availability is read from each descriptor's `Command.CanExecute(null)`; `ICommandRegistry` does not add a separate availability query. Register no command twice. | `CommandPaletteViewModelTests` and `Phase9CommandRegistrationTests` cover ordering, filtering, empty/no-match states, unavailable/unknown commands, and duplicate-registration protection. |
| **M2** | Add the Command Palette overlay and wire it to M1. Opening, filtering, keyboard navigation, execution, dismissal, and focus restoration must be deterministic. Palette invocation itself is registry-backed and uses one documented default gesture; all editor commands added in Phase 9 use Phase 8 settings-driven keybinding materialization. | `CommandPaletteViewTests` plus view-model tests cover open/close, keyboard selection, execution once, unavailable selection, focus restoration, and live keybinding refresh. Manual smoke: open palette, run a command, dismiss it, and confirm focus returns to the editor. |
| **M3** | Implement active-document Search and Replace with a focused search surface. Define exact literal matching, case-sensitivity, next/previous wrap behavior, zero-match feedback, selection replacement, replace-next, replace-all, cancellation/close behavior, undo grouping, and dirty-state result. All text changes flow through the existing document/editor path. Because one `TextEditor` is shared by all tabs, clear/reset search presentation on a tab switch and never apply old-document selection or replacement to the new active tab. | `EditorSearchViewModelTests` and `EditorSearchIntegrationTests` cover empty query, case modes, wrap, zero/one/many matches, selection replacement, replace-next/all, undo, dirty state, caret/selection, and switching/closing tabs while the surface is open. |
| **M4** | Implement active-editor code folding using only APIs proven in M0. Define a deterministic, syntax-neutral initial folding heuristic, expand/collapse current/all commands, caret visibility after folding changes, and no-folding feedback for unsupported/invalid text. Do not introduce a C# parser or language service. Because the `TextEditor` is shared between tabs, folding state must be explicitly discarded or restored by document identity on every tab change. | `EditorFoldingTests` cover discovery, nested regions, expand/collapse, caret preservation, invalid/plain-text behavior, active-tab switch, and settings/font changes without stale folding state. Manual smoke uses a representative C# file. |
| **M5a** | Add registry-backed tab commands and complete tab lifecycle UX: next/previous tab, close active tab, close other tabs, close all tabs. Preserve existing unsaved-change confirmation and choose/document deterministic neighbor selection. | Extend `EditorTabViewModelTests` with command, dirty-confirmation, save-failure, cancel, active-neighbor, no-tab, and workspace-active-document assertions; add command-registration/keybinding tests. |
| **M5b** | Add pointer-driven tab reordering and polished dirty/active affordances without changing document ownership. Define drag threshold, valid drop positions, active-tab preservation, no-op behavior, scroll behavior, and subscription cleanup. Keyboard navigation from M5a remains the accessibility baseline. | `EditorTabBarLifecycleTests` and `EditorTabReorderTests` cover order mutations, no-op drops, active/dirty preservation, close after reorder, and cleanup. Manual smoke verifies drag ordering, close affordance, overflow scrolling, and dirty indicator. |
| **M6** | Integrate status-bar/editor feedback and close out the phase. Status text must truthfully reflect caret, selection, active document, search outcome, folding outcome, and command/save failure without stale updates after tab switches or closes. Truth-sync roadmap, architecture, libraries if changed, and limitations; run full regression and record manual evidence. | New/extended status-bar projection tests, all Phase 9 focused tests, then sequential `dotnet build Zaide.slnx --no-restore`, `dotnet test Zaide.slnx --no-build`, and `git diff --check`. |

## Command and Focus Contract (Locked by M0)

M0 must lock final names and defaults before M1. At minimum, Phase 9 needs
stable registry IDs for palette invocation, find, replace, find next/previous,
replace next/all, folding actions, and tab actions. Commands must be owned by
the ViewModel/service that owns their state; the `MainWindow` only materializes
resolved `KeyBinding` instances as established in Phase 8.2.

Every successful editor command must leave the UI in one of these explicit
states:

1. The palette/search surface owns focus while it is visible.
2. Dismissal restores focus to the still-live active editor when one exists.
3. Find navigation selects the matched range and makes it visible.
4. Replace and folding preserve a valid caret/selection or use a documented
   deterministic fallback.
5. Switching or closing a tab clears UI state that belongs to the old document;
   it must never mutate a newly active tab accidentally.

The current `EditorViewModel` has only `CaretLine` and `CaretColumn`; M0 must
define the selection fields, zero/no-selection representation, and status-bar
text before M3 or M6 adds selection-dependent behavior.

## Test and Verification Strategy

- Keep service/view-model behavior testable without Avalonia controls wherever
  possible. Use view tests only for input routing, overlay lifecycle, focus, and
  control-specific behavior.
- Add each plan-required test file in the milestone that names it; a planned
  test file missing at closeout means the milestone is incomplete.
- Run focused tests while implementing. Before marking any milestone complete,
  run its focused suite and `git diff --check`.
- Prefer one green, intentional commit at each milestone or milestone-slice
  boundary; do not combine unrelated milestones in one commit.
- Run build and test gates sequentially, never concurrently:
  `dotnet build Zaide.slnx --no-restore` followed by
  `dotnet test Zaide.slnx --no-build`.
- M2, M4, M5b, and M6 require recorded Linux manual smoke evidence because
  overlay focus, AvaloniaEdit folding, pointer drag, and visible status feedback
  cannot be established by unit tests alone.

## Phase 9 Limitations

- Palette entries are parameterless registry commands only; parameterized
  commands remain outside this phase.
- Search is active-document literal text only; no workspace search, regex, or
  semantic rename.
- Folding is syntax-neutral and local to the editor; LSP-backed folding remains
  future work.
- `AvaloniaEdit.Folding.AbstractFoldingStrategy` does **not exist** in
  AvaloniaEdit 12.0.0. M4 will implement folding directly with `NewFolding` +
  `FoldingManager.UpdateFoldings()` using a standalone heuristic strategy.
- Multi-cursor editing remains deferred beyond V2.
- Phase 10 owns C# language intelligence, diagnostics, navigation, and
  formatting; Phase 9 must not pre-build an LSP abstraction.

## Phase Exit Conditions

- [ ] All M0–M6 milestones, including M5a and M5b, are complete with their
      required test files present.
- [ ] Common editor actions are discoverable through the Command Palette and
      executable through registered, settings-configurable shortcuts.
- [ ] Search/Replace, folding, tab lifecycle/order, dirty-state behavior, and
      status feedback are truthful across active-tab changes.
- [ ] No second document/tab ownership model, LSP dependency, or out-of-scope
      multi-cursor behavior was introduced.
- [ ] Sequential full build/test gates pass and `git diff --check` is clean.
- [ ] Manual Linux smoke evidence covers palette focus, search/replace, folding,
      tab reorder/dirty close, shortcut refresh, and status feedback.
- [ ] `README.md`, `docs/roadmap/V2.md`, `docs/architecture/OVERVIEW.md`, and
      `docs/LIBRARIES.md` (only if dependencies changed) truthfully reflect the
      completed implementation and limitations.

## Exact Next Step

Implement **M4 only**: active-editor code folding using only APIs proven in M0.
Define a deterministic, syntax-neutral initial folding heuristic, expand/collapse
current/all commands, caret visibility after folding changes, and no-folding
feedback for unsupported/invalid text. Do not introduce a C# parser or language
service. Because the `TextEditor` is shared between tabs, folding state must be
explicitly discarded or restored by document identity on every tab change.
Do not implement search/replace (M3 complete), tab lifecycle (M5), or
status-bar changes (M6).

## Rollback Plan

- Planning baseline: `596ad85cad7b0eecf2cabb09327f11a22fd47f93` (`docs: add
  Phase 9 Editor UX implementation plan`).
- Before each implementation milestone, record its last green predecessor in
  that milestone's commit message or closeout evidence. Revert a failed
  milestone to that predecessor; do not discard a prior green milestone.
- If a structural reset is required rather than a normal bug fix, create
  `REVERT_LOG.md` as required by `docs-rules.md` before recording the revert.
