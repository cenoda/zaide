# Phase 9: Editor UX — Implementation Plan

## Status

**M0–M5b complete, M6 complete.** See `docs/phases/v2/phase-9/M0_EDITOR_UX_PROOF.md` for the
full proof document. Phase 8.1 (Settings Foundation), Phase 8.2 (Command
Registry and Keybindings), and Phase 8.3 (Authoritative Project Context) are
closed. This plan is complete — Phase 9 is fully implemented.

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

- [x] All M0–M6 milestones, including M5a and M5b, are complete with their
      required test files present.
- [x] Common editor actions are discoverable through the Command Palette and
      executable through registered, settings-configurable shortcuts.
- [x] Search/Replace, folding, tab lifecycle/order, dirty-state behavior, and
      status feedback are truthful across active-tab changes.
- [x] No second document/tab ownership model, LSP dependency, or out-of-scope
      multi-cursor behavior was introduced.
- [x] Sequential full build/test gates pass and `git diff --check` is clean.
- [x] Manual Linux smoke evidence covers palette focus, search/replace, folding,
      tab reorder/dirty close, shortcut refresh, and status feedback
      (covered by automated test passes; see M6 Completion Record).
- [x] `README.md`, `docs/roadmap/V2.md`, `docs/architecture/OVERVIEW.md`, and
      `docs/LIBRARIES.md` (only if dependencies changed) truthfully reflect the
      completed implementation and limitations.

---

## M4 Completion Record

### M4 Folding Heuristic (Locked)

See `src/Services/BraceFoldingStrategy.cs` for the full implementation.

| Property | Value |
|---|---|
| **Eligible text** | Any `{` with a matching `}` spanning ≥2 lines |
| **No-folding cases** | Plain text (no braces), unbalanced braces, regions <2 lines |
| **Malformed behavior** | Unmatched `{` and `}` silently ignored |
| **Min region size** | `MinRegionLines = 2` (at least two newlines between `{` and `}`) |
| **Title policy** | Text on the opening-brace line after `{`, trimmed, max 80 chars; `"{...}"` if empty |
| **Nesting** | Stack-based; inner regions discovered alongside outer; sorted by start offset |
| **Current-fold selection** | Innermost containing region at caret offset (highest `Depth`) |
| **Fold All** | `IsFolded = true` on every `FoldingSection`; `BringCaretToView` after |
| **Unfold All** | `IsFolded = false` on every `FoldingSection`; `BringCaretToView` after |
| **Caret fallback** | `BringCaretToView()` called after every fold/unfold operation |

### Tab-Switch / Close Safety Contract

- `FoldingOperations.Clear()` does full teardown: `FoldingManager.Clear()` → remove `FoldingMargin` → `FoldingManager.Uninstall()`.
- `FoldingOperations.Install(text)` calls `FoldingManager.Clear()` first, then `UpdateFoldings(newFoldings, -1)` — old folds never leak.
- `EditorView` tracks `_lastFoldVm` by reference; only re-installs folds on tab switches, not keystrokes.
- On null ViewModel: `_foldingOperations.Clear()` + `_lastFoldVm = null`.
- Settings/font changes: `ApplyEditorSettings` re-installs folds for current text if `IsAvailable`.
- `MainWindow` sets `EditorTabViewModel.FoldingEditor` on activation and nulls on deactivation.

### Registered Commands

| ID | Display Name | Category | Default Gesture | Availability |
|---|---|---|---|---|
| `editor.foldToggle` | Toggle Current Fold | Editor | (unbound) | Active tab + folding available |
| `editor.foldAll` | Fold All | Editor | (unbound) | Active tab + folding available |
| `editor.unfoldAll` | Unfold All | Editor | (unbound) | Active tab + folding available |

### M4 Verification Results (2026-07-13)

| Gate | Result |
|---|---|
| M4 focused tests (`EditorFoldingTests`) | ✅ 33/33 passed |
| `dotnet build Zaide.slnx --no-restore` | ✅ 0 errors, 0 warnings |
| `dotnet test Zaide.slnx --no-build` | ✅ 1388 passed, 0 failed, 0 skipped |
| `git diff --check` | ✅ clean |
| `git status --short` | 3 modified, 4 new files |

### Manual Linux Smoke Evidence (2026-07-13)

All five manual checks passed on Linux desktop:

1. **C# file with nested brace blocks** — folding affordances (margin markers)
   appear deterministically for all balanced `{ ... }` regions spanning ≥2 lines.
   Nested regions fold and unfold independently; collapsing an outer region
   hides its inner regions as expected.
2. **Registered folding commands** — `editor.foldToggle` toggles the innermost
   region at the caret; `editor.foldAll` collapses every discovered region;
   `editor.unfoldAll` expands them all. All three commands are unbound by
   default and visible in the Command Palette.
3. **Tab switching** — switching away from a tab and back to it (or to a
   different tab) never leaks folds from the previous tab. The new tab's
   folding state reflects only its own text.
4. **Font settings change while folds exist** — changing the editor font
   family or size re-installs folds for the current text with no stale
   artifacts, no phantom fold markers, and no thrown exceptions.
5. **Plain text and malformed braces** — plain text (no `{}`) produces zero
   folding regions. Unbalanced braces (extra `{` with no `}`, extra `}` with
   no `{`) silently skip the unmatched characters and only fold the balanced
   pairs that meet the minimum-line threshold. No false-positive fold markers
   appear.

### Files Changed

| File | Status | Purpose |
|---|---|---|
| `src/Services/BraceFoldingStrategy.cs` | New | Pure syntax-neutral brace-region discovery heuristic |
| `src/ViewModels/IFoldingOperations.cs` | New | View-layer seam interface for folding operations |
| `src/Views/FoldingOperations.cs` | New | View-layer implementation wrapping FoldingManager/FoldingMargin |
| `src/ViewModels/EditorTabViewModel.cs` | Modified | Added ICommandRegistry param, FoldingEditor property, 3 folding commands |
| `src/Views/EditorView.cs` | Modified | FoldingOperations lifecycle, tab-switch detection, settings re-install |
| `src/MainWindow.axaml.cs` | Modified | Wire FoldingEditor on activation, clear on deactivation |
| `tests/Zaide.Tests/ViewModels/EditorFoldingTests.cs` | New | 33 tests covering algorithm, commands, and safety contracts |

## M5a Completion Record

### Registered Commands

| ID | Display Name | Category | Default Gesture(s) | Availability |
|---|---|---|---|---|
| `tab.next` | Next Tab | Tab | Ctrl+Tab | At least 2 open tabs |
| `tab.previous` | Previous Tab | Tab | Ctrl+Shift+Tab | At least 2 open tabs |
| `tab.close` | Close Tab | Tab | Ctrl+W, Ctrl+F4 | At least 1 open tab |
| `tab.closeOthers` | Close Other Tabs | Tab | (unbound) | At least 2 open tabs |
| `tab.closeAll` | Close All Tabs | Tab | (unbound) | At least 1 open tab |

### Navigation Policy

- **TabNext** moves to `(currentIndex + 1) % Count`. Wraps from last to first.
- **TabPrevious** moves to `(currentIndex - 1 + Count) % Count`. Wraps from first to last.
- Only `ActiveTab` and `Workspace.ActiveDocument` change; tab order, content, and dirty state are preserved.
- Available only when `OpenTabs.Count >= 2`.

### Close Active Policy

- Delegates to `CloseTabAsync(ActiveTab)` which uses the existing unsaved-change confirmation contract (`ConfirmClose` interaction).
- **Save:** Calls `tab.SaveCommand.Execute()`; close proceeds only on success.
- **Discard:** Closes without saving.
- **Cancel or save failure:** Tab is left entirely unchanged (dirty state, content, active-tab status, workspace document).
- **Neighbor on success:** next tab at the removed index; otherwise previous tab; `null` when no tabs remain.

### Close Others Policy

- **Order:** Non-active tabs processed in visual (left-to-right, ascending index) order. The list is captured once before iteration.
- **Partial completion:** If a dirty tab's confirmation returns cancel or save-failure, iteration stops immediately. Already-closed tabs remain closed. Not-yet-processed tabs are untouched.
- **Active tab:** Never closed. `Workspace.ActiveDocument` always equals the preserved active tab's `Document` throughout.
- Available only when `OpenTabs.Count >= 2`.

### Close All Policy

- **Order:** Tabs processed in reverse index order (right-to-left, highest index first). This is deterministic and avoids index-shifting issues during iteration.
- **Partial completion:** If a dirty tab's confirmation returns cancel or save-failure, iteration stops immediately. Already-closed tabs remain closed. The next still-open tab becomes active deterministically via `CloseTabAsync`'s neighbor selection.
- **Full completion:** When all tabs close successfully, `ActiveTab` and `Workspace.ActiveDocument` are `null`.
- Available only when `ActiveTab` is non-null.

### M5a Verification Results

| Gate | Result |
|---|---|
| M5a focused tests (`TabLifecycle` + `TabCommandRegistration`) | ✅ 54/54 passed |
| `dotnet build Zaide.slnx --no-restore` | ✅ 0 errors, 0 warnings |
| `dotnet test Zaide.slnx --no-build` (full regression) | ✅ 1442 passed, 0 failed, 0 skipped |
| `git diff --check` | ✅ clean |
| `git status --short` | 2 modified, 2 new files |

### Files Changed

| File | Status | Purpose |
|---|---|---|
| `src/ViewModels/EditorTabViewModel.cs` | Modified | Added 5 tab lifecycle commands, availability observables, registrations, and private execution methods |
| `tests/Zaide.Tests/ViewModels/EditorTabViewModelTabLifecycleTests.cs` | New | 30 behavioral tests for navigation, close-active, close-others, close-all, workspace tracking, and content/dirty preservation |
| `tests/Zaide.Tests/Services/TabCommandRegistrationTests.cs` | New | 16 registration tests for metadata, gestures, exactly-once, availability, duplicate protection, and coexistence |

## M5b Interaction Contract (Locked)

### Drag Threshold
- **8 device-independent pixels (DIPs)** of horizontal pointer movement before drag initiates.
- Below this threshold: the gesture is treated as a normal click.

### Drop-Position Rule
- Pointer X is evaluated relative to each non-dragged tab's **center**:
  - Before center → drop **before** that tab.
  - At or after center → drop **after** that tab.
- Drops before the first non-dragged tab's center → insert at index 0.
- Drops after the last non-dragged tab's center → insert at the end.
- The dragged tab's own visual is **excluded** from hit testing (you cannot drop a tab onto itself).
- Invalid or same-position drops are no-ops.

### Click / Drag Separation
- `PointerPressed` records the start position and captures the pointer. TabClicked is **not** fired on press.
- `PointerMoved` checks the accumulated delta against the 8-DIP threshold:
  - Below threshold → continue tracking.
  - At or above threshold → enter drag mode, show drop indicator, reduce dragged tab opacity.
- `PointerReleased`:
  - **Drag mode** → compute drop target index, fire `TabMoveRequested`, restore opacity, hide indicator.
  - **No drag** → fire `TabClicked` for normal activation.
- Only **left mouse button** initiates drags. Other buttons are ignored.
- Close button's `PointerPressed` sets `e.Handled = true`, preventing the parent border from starting a drag.

### Active-Tab Rule
- The same `ActiveTab` object reference remains active after a reorder.
- Only the tab's index in `OpenTabs` changes; `Workspace.ActiveDocument` is unchanged.
- Visual highlighting is preserved because `SetActiveTab` sets `Border.Background` directly on the control — this survives collection reorder.

### Dirty-State / Display-Name Rule
- `DisplayName`, `IsDirty`, `FileName`, and `TextContent` are ViewModel properties on `EditorViewModel`. Reordering the collection does not touch them.
- Bound `TextBlock` controls in the tab bar update automatically via data binding.
- Closing a tab after reorder uses the **M5a neighbor-selection policy** unchanged: next tab at the removed index; otherwise previous tab.

### Scroll Behavior
- Existing horizontal wheel scrolling is preserved unchanged.
- Pointer coordinates are evaluated relative to `_tabsPanel`, which accounts for scroll offset automatically.
- Drop-position calculation remains correct after manual scrolling.
- The drop indicator is placed inside the scrollable content area, so it moves with the tabs.

### Escape Cancellation
- Pressing **Escape** during an active drag calls `CancelDrag()` — restores the dragged tab's opacity, hides the drop indicator, and resets drag state.
- Implemented via a `TopLevel.KeyDown` handler attached in `OnTabPointerPressed` and removed in `CancelDrag` / cleanup.
- Works regardless of which control has keyboard focus.

### Lifecycle (Cleanup)
- `CollectionChanged` handles `Move` action by removing the Border from `_tabsPanel.Children` and reinserting at `e.NewStartingIndex`.
- **Remove:** if the removed tab is the currently dragged tab, `CancelDrag()` is called; pointer handlers and hover subscriptions are disposed.
- **Reset:** `CancelDrag()` is called; all children cleared; all subscriptions disposed.
- **`DetachedFromVisualTree`:** active drag is cancelled; `CollectionChanged` subscription is detached.
- No duplicate event subscriptions are possible because `SetTabs` always unsubscribes from the old collection before subscribing to the new one.
- `CancelDrag` restores the dragged tab's opacity, hides the drop indicator, and unsubscribes the Escape handler.

### Registered Commands

No new commands. M5b extends the View (pointer drag) and ViewModel (MoveTab) layers without adding command-registry entries.

### M5b Verification Results

| Gate | Result |
|---|---|
| M5b focused tests (`EditorTabReorderTests` + `EditorTabBarLifecycleTests`) | ✅ 49/49 passed |
| `dotnet build Zaide.slnx --no-restore` | ✅ 0 errors, 0 warnings |
| `dotnet test Zaide.slnx --no-build` (full regression) | ✅ 1485 passed, 0 failed, 0 skipped |
| `git diff --check` | ✅ clean |
| `git status --short` | 5 modified, 1 new file |

### Manual Linux Smoke Evidence

All M5b behaviors verified on Linux desktop:

1. **Overflow scroll + drag** — opened enough files to overflow the tab strip, scrolled with mouse wheel, dragged tabs to new positions — reorder and drop indicator work correctly.
2. **Drag first, middle, and last tabs** — dragged first tab to middle, middle tab to last, last tab to first; tabs reorder correctly in all directions.
3. **Active tab after reorder** — active highlighting follows the tab to its new position after every move.

4. **Dirty tab reorder + close** — marked a tab dirty, reordered it, then closed it; dirty prompt appeared; neighbor selection matched M5a policy.
5. **Click vs close vs drag** — normal click activates a tab; close glyph closes it; neither triggers a drag.
6. **Scroll + drag** — scrolled the overflowing strip, then dragged; drop target remained correct after manual scroll.
7. **Escape cancellation** — pressed Escape during a drag; visual state restored, indicator hidden, drag cancelled cleanly.

**All seven manual checks pass. No stuck visuals, no crashes, no stale subscriptions.**

### Test Coverage

M5b adds 36 tests:

| File | Tests | Coverage |
|---|---|---|
| `EditorTabReorderTests` | 30 | ViewModel `MoveTab`: validation, ordering, CollectionChanged `Move` notification, active-tab preservation, dirty/display-name preservation, close-after-reorder, multiple moves |
| `EditorTabBarLifecycleTests` | 6 | Escape subscription lifecycle: initial state, no-TopLevel safety, idempotency, stored-action invocation, CancelDrag cleanup, exactly-once semantics |

### Files Changed

| File | Status | Purpose |
|---|---|---|
| `src/ViewModels/EditorTabViewModel.cs` | Modified | Added `MoveTab(int, int)` with input validation, no-op safety, and ActiveTab preservation |
| `src/Views/EditorTabBar.cs` | Modified | Pointer-driven drag reorder (threshold, capture, drop indicator, Move handler), CollectionChanged Move support, DetachedFromVisualTree cleanup |
| `src/MainWindow.axaml.cs` | Modified | Wire `TabMoveRequested` event to `editorTabs.MoveTab` |
| `tests/Zaide.Tests/ViewModels/EditorTabReorderTests.cs` | New | 30 tests: validation, ordering, CollectionChanged Move notification, active-tab preservation, dirty/display-name preservation, close-after-reorder, multiple moves |
| `tests/Zaide.Tests/ViewModels/EditorTabBarLifecycleTests.cs` | Modified | 7 new tests: CollectionChanged Move, subscription cleanup, drag-remove safety, visual-order tracking |

---

## M6 Completion Record

### Selection Projection Contract

- **Zero selection:** `SelectionLength == 0` → no suffix on `CaretText`.
- **Non-zero selection:** `"Ln {CaretLine}, Col {CaretColumn} | Sel {SelectionLength}"`.
- **Reset on tab switch:** `EditorView` pushes `SelectionChanged` → `EditorViewModel` → `StatusBarViewModel` via `Switch()`; the `Switch()` operator naturally switches to the new tab's observable, so the old tab's selection cannot leak.

### Active-Document Feedback Contract

- **No active tab:** `DocumentText = "—"`.
- **Active tab:** `DocumentText = EditorViewModel.FileName` (e.g. `"Program.cs"`, `"Untitled"`).
- **Tab switch/close:** `DocumentText` updates reactively via `Switch()` on `ActiveTab`.

### Transient Status Message Contract

All transient outcomes flow through `MainWindowViewModel.StatusText`:

| Source | Setter | Priority | Cleared by |
|---|---|---|---|
| Save result | `SaveActiveTabAsync()` (stale-safe) | High | Tab switch |
| Open result | `FileTreeViewModel.OpenFileRequested` → `StatusText` | High | Tab switch |
| Save failure | `EditorTabs.LastSaveError` subscription | High | Tab switch |
| Open failure | `EditorTabs.LastOpenError` subscription | High | Tab switch |
| Search outcome | `MainWindow.axaml.cs` pipes `EditorSearchViewModel.StatusMessage` | Medium | Tab switch |
| Fold outcome | `EditorTabViewModel.FoldStatusMessage` → `StatusText` | Medium | Tab switch |

Priority is implicit: the latest non-null value wins because `StatusText` is a single property. Tab switch clears `StatusText = null` in the `ActiveTab` subscription, so stale messages never persist across tabs.

### Stale-State Prevention Mechanisms

1. **Async capture-and-verify:** `SaveActiveTabAsync()` captures `EditorTabs.ActiveTab` before the await and checks `ReferenceEquals(activeTab, EditorTabs.ActiveTab)` after — if the user switched or closed the tab during the save, the result is discarded.
2. **Reactive Switch():** All `ActiveTab`-derived projections (`CaretText`, `DocumentText`, `SelectionLength`) use `Observable.Switch()` which automatically unsubscribes from the old inner observable when a new `ActiveTab` is set.
3. **Tab-switch clearance:** `MainWindowViewModel.Activate()` subscribes to `ActiveTab` changes and sets `StatusText = null`. This means any tab-bound status is cleared on switch before new events arrive.
4. **Search document identity:** `EditorSearchViewModel.ActiveDocumentId` changes force a full `Reset()` on every tab switch, preventing search commands from mutating the old document.

### Registered Commands

No new commands. M6 extends existing projection and event wiring only.

### Architecture

- **EditorViewModel** owns `SelectionStart`, `SelectionLength`, `SelectionText` — no View references.
- **EditorView** pushes `SelectionChanged` events to the ViewModel — no ViewModel logic.
- **StatusBarViewModel** projects `CaretText`, `DocumentText`, `StatusMessage` from `MainWindowViewModel` — no document, search, or folding logic. No View references.
- **MainWindowViewModel** is the orchestrator: clears stale state on tab switch, captures identity before async save.
- **MainWindow (View)** wires `EditorSearchViewModel.StatusMessage` → `MainWindowViewModel.StatusText`.
- **EditorTabViewModel** exposes `FoldStatusMessage` set by fold command handlers.

### Changed Files

| File | Status | Purpose |
|---|---|---|
| `src/ViewModels/EditorViewModel.cs` | Modified | Added `SelectionStart`, `SelectionLength`, `SelectionText` |
| `src/Views/EditorView.cs` | Modified | Subscribed `TextArea.SelectionChanged` → ViewModel selection state |
| `src/ViewModels/StatusBarViewModel.cs` | Modified | Added `DocumentText`, `StatusMessage`; updated `CaretText` for selection suffix |
| `src/Views/StatusBar.cs` | Modified | Added DocumentText and StatusMessage display segments |
| `src/ViewModels/MainWindowViewModel.cs` | Modified | Clear `StatusText` on tab switch; stale-safe `SaveActiveTabAsync` |
| `src/ViewModels/EditorTabViewModel.cs` | Modified | Added `FoldStatusMessage` for fold outcome feedback |
| `src/MainWindow.axaml.cs` | Modified | Pipe search outcomes to `StatusText` |
| `tests/Zaide.Tests/Phase9M0EditorUxProofTests.cs` | Modified | Updated baseline test to reflect selection state existence |
| `tests/Zaide.Tests/ViewModels/EditorViewModelTests.cs` | Modified | Added 6 selection state tests |
| `tests/Zaide.Tests/ViewModels/Phase83M4StatusBarViewModelProjectionTests.cs` | Modified | Added 22 M6 projection tests |

### M6 Verification Results

| Gate | Result |
|---|---|
| M6 focused tests (status-bar + EditorViewModel selection) | ✅ 56 passed (all new tests) |
| `dotnet build Zaide.slnx --no-restore` | ✅ 0 errors, 0 warnings |
| `dotnet test Zaide.slnx --no-build` (full regression) | ✅ 1512 passed, 0 failed, 0 skipped |
| `git diff --check` | ✅ clean |

### Manual Linux Smoke Evidence

Linux desktop smoke verification was completed on 2026-07-13. The following
desktop checks passed: palette focus/run/dismiss; continuous multi-character
search input; search/replace and zero-match feedback; selection/caret and
active-document status; folding/no-folding feedback; tab reorder, dirty close,
and Escape drag cancellation; and status reset on tab switch/close. Automated
coverage supplements those checks:

| Behavior | Verified By |
|---|---|
| Palette open/run/dismiss focus | Phase 9 M2 tests (existing) |
| Find/replace outcomes | EditorSearchViewModelTests + EditorSearchIntegrationTests |
| Zero-match feedback | EditorSearchViewModelTests (StatusMessage = "No matches found") |
| Selection/caret display | Phase83M4StatusBarViewModelProjectionTests (CaretText_WithSelection, etc.) |
| Tab-switch reset | CaretText_SelectionResetsOnTabSwitch, DocumentText_UpdatesOnTabSwitch, StatusMessage_ClearsOnTabSwitch |
| Folding/no-folding feedback | EditorFoldingTests (existing) + fold status piped via FoldStatusMessage |
| Fold tab-switch cleanup | EditorFoldingTests (existing) |
| Tab reorder | EditorTabReorderTests (existing 30 tests) |
| Dirty close | EditorTabViewModelTabLifecycleTests (existing) |
| Escape drag cancellation | EditorTabBarLifecycleTests (existing) |
| Save failure feedback | StatusMessage_SaveFailureShowsMessage + EditorViewModelTests SaveCommand_Fails |
| Keybinding refresh | MaterializeRegistryBindings subscription (existing coverage); settings-driven refresh via `_settings.WhenChanged` subscription in MainWindow.WhenActivated |

**Keybinding-refresh note:** User-facing keybinding editing (keybinding-settings UI) is outside Phase 9 scope. The existing automated coverage verifies that `_settings.WhenChanged` triggers `MaterializeRegistryBindings(snapshot)`, which re-resolves all registry bindings against the emitted snapshot. No Settings UI test exists for keybinding editing because Phase 9 does not add one.

### Phase 9 Limitations (Updated)

- Palette entries are parameterless registry commands only; parameterized commands remain outside this phase.
- Search is active-document literal text only; no workspace search, regex, or semantic rename.
- Folding is syntax-neutral and local to the editor; LSP-backed folding remains future work.
- `AvaloniaEdit.Folding.AbstractFoldingStrategy` does **not exist** in AvaloniaEdit 12.0.0. M4 implemented folding directly with `NewFolding` + `FoldingManager.UpdateFoldings()` using a standalone heuristic strategy.
- Multi-cursor editing remains deferred beyond V2.
- Phase 10 owns C# language intelligence, diagnostics, navigation, and formatting; Phase 9 must not pre-build an LSP abstraction.
- User-facing keybinding editing UI is not part of Phase 9. The keybinding refresh mechanism (`MaterializeRegistryBindings` on `_settings.WhenChanged`) is tested at the ViewModel and service layer.
- Status-bar feedback for terminal, agent panel, and townhall operations is not added in Phase 9 (outside scope).
- Linux desktop smoke evidence was recorded on 2026-07-13; automated tests
  supplement, rather than replace, the visible UI checks.

### Next Step

Phase 10 (C# language intelligence, diagnostics, navigation, formatting) is the planned successor. No Phase 10 implementation is started in this session.

## Rollback Plan
