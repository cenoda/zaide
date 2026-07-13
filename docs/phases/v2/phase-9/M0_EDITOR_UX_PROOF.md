# Phase 9 M0: Editor UX Proof

## Status: Complete

**Baseline commit (planning):** `596ad85cad7b0eecf2cabb09327f11a22fd47f93`
**Verified against live code at:** commit under test (this M0 session)

---

## 1. Exact Ownership Graph

### Document (Models.Document)
- **Kind:** Plain model (not `ReactiveObject`), no interfaces.
- **Owned by:** `EditorViewModel` (composition; one `Document` per `EditorViewModel`).
- **Key state:** `FilePath`, `Content`, `IsDirty`, `LastSaveError`.
- **Events:** `ContentChanged`, `DirtyStateChanged`, `SaveErrorChanged` — bridged by `EditorViewModel` to reactive properties.

### Workspace (Models.Workspace)
- **Kind:** Singleton service.
- **Owned by:** DI container; shared by `EditorTabViewModel` and `MainWindowViewModel`.
- **Key methods:** `OpenDocument(path, content)`, `CloseDocument(path)`, `SetActiveDocument(Document?)`.
- **Ownership:** Tracks open documents; `EditorTabViewModel` calls these methods during open/close/switch.

### EditorTabViewModel
- **Kind:** Singleton ViewModel.
- **Ownership:** `OpenTabs` (`ObservableCollection<EditorViewModel>`), current `ActiveTab`, `CloseTabCommand`, `OpenFileCommand`.
- **Lifecycle:** Manages tab creation, activation, and close. Resolves `EditorViewModel` transiently via `IServiceProvider`.
- **Active-tab switch:** When `ActiveTab` is set, calls `_workspace.SetActiveDocument(value?.Document)`. The `MainWindow` swaps `EditorView.ViewModel = active` in response.

### EditorViewModel
- **Kind:** Transient ViewModel (one per open file).
- **Ownership:** One `Document`, caret state (`CaretLine`, `CaretColumn`).
- **Commands:** `SaveCommand` (ReactiveCommand<Unit, bool>).
- **Caret state:** `int CaretLine` (1-based), `int CaretColumn` (1-based).
- **No selection state today.** Needs `SelectionStart`, `SelectionLength`, `SelectionText` for Phase 9.

### MainWindow (View)
- **Kind:** `ReactiveWindow<MainWindowViewModel>`.
- **Ownership:** One `EditorView` (field `_editorView`), one `EditorTabBar` (field `_editorTabBar`), one `StatusBar` (field `_statusBar`).
- **Active-tab wiring (line 186-194 in WhenActivated):**
  ```csharp
  this.WhenAnyValue(x => x.ViewModel!.EditorTabs.ActiveTab)
      .Subscribe(active =>
      {
          _editorView.ViewModel = active;
          _editorView.IsVisible = active is not null;
          _editorTabBar.SetActiveTab(active);
          ...
      });
  ```
- **Keybinding materialization:** `MaterializeRegistryBindings()` called during `WhenActivated` and re-called on `_settings.WhenChanged`.

### EditorView (View)
- **Kind:** `ReactiveUserControl<EditorViewModel>`.
- **Ownership:** One `TextEditor` (AvaloniaEdit), one `TextMate.Installation`, one `IndentGuideRenderer`.
- **Sync:** One-way VM→Editor (via `TextContent` subscription with guard flag) and one-way Editor→VM (caret position via `Caret.PositionChanged`).
- **View-swap note:** When `ViewModel` changes (due to active-tab switch), all subscriptions switch to the new VM via `.Switch()`. The `TextEditor` instance is **not** replaced — only its `Document` and text content change.
- **Caret tracking (lines 179-187):** `_textEditor.TextArea.Caret.PositionChanged` pushes `ViewModel.CaretLine` and `ViewModel.CaretColumn`.

### EditorTabBar (View)
- **Kind:** `UserControl` (not ReactiveUserControl).
- **Ownership:** `Dictionary<EditorViewModel, Border>` for tab items, hover delay `CancellationTokenSource` map.
- **Events:** `TabCloseRequested`, `TabClicked` — wired by `MainWindow.WhenActivated` to `EditorTabViewModel` commands.

### StatusBarViewModel
- **Kind:** Singleton ViewModel.
- **Ownership:** Observes `MainWindowViewModel.EditorTabs.ActiveTab` to derive `CaretText`, `LanguageText`.
- **Reactive chain:**
  ```
  mainWindow.EditorTabs.ActiveTab
    → tab?.CaretLine, tab?.CaretColumn
    → "Ln {line}, Col {column}" | default "Ln 1, Col 1"
  ```

### Shared EditorView/TextEditor constraint
- **One `TextEditor` instance**, swapped by changing its `Document` property (via VM→Editor sync) and `ViewModel` reference.
- Every active-tab change must explicitly **clear or restore** per-document state belonging to the outgoing document: search panel state, folding state, caret position, selection.

---

## 2. Phase 8.2 Command/Keybinding Lifecycle

### Registration sites

| Registration site | Command IDs |
|---|---|
| `MainWindowViewModel` constructor (lines 189-196) | `file.save`, `workspace.openFolder`, `workspace.closeFolder`, `view.toggleBottomPanel` |
| `FileTreeViewModel` constructor | `explorer.toggleHiddenFiles` |
| `SourceControlViewModel` constructor | `sourcecontrol.commit`, `sourcecontrol.refresh` |

### Keybinding materialization flow

1. `MainWindow` constructor receives `ICommandRegistry` (DI), `ISettingsService`.
2. `MainWindow.WhenActivated` calls `MaterializeRegistryBindings()`.
3. `MaterializeRegistryBindings(SettingsModel snapshot)`:
   - Removes previously materialized `KeyBinding` instances from `KeyBindings`.
   - Wraps snapshot in `SnapshotSettingsAccessor` (implements `ISettingsService`).
   - Calls `_registry.ResolveKeyBindings(snapshotService)`.
   - For each `ResolvedKeyBinding`, retrieves `CommandDescriptor`, creates `KeyBinding` via `KeyBindingConverter.TryCreateKeyBinding()`.
   - Adds new `KeyBinding` instances to `KeyBindings`.
4. `_settings.WhenChanged` subscription calls `MaterializeRegistryBindings(snapshot)` with the emitted snapshot.
5. Resolution order: user overrides → default gestures. Gesture conflicts resolved lexicographically by command ID.

### Currently registered command IDs (7 total)

| ID | Category | DisplayName | Default gestures |
|---|---|---|---|
| `file.save` | File | Save | Ctrl+S |
| `workspace.openFolder` | Workspace | Open Folder | Ctrl+O |
| `workspace.closeFolder` | Workspace | Close Folder | (unbound) |
| `view.toggleBottomPanel` | View | Toggle Bottom Panel | Ctrl+Oem3, Ctrl+J |
| `explorer.toggleHiddenFiles` | Explorer | Toggle Hidden Files | Ctrl+Shift+H |
| `sourcecontrol.commit` | Source Control | Commit | (unbound) |
| `sourcecontrol.refresh` | Source Control | Refresh | (unbound) |

---

## 3. Proposed Phase 9 Command IDs, Categories, Default Gestures

### Palette (M1-M2)

| ID | Category | DisplayName | Default gestures | Availability | Palette ordering |
|---|---|---|---|---|---|
| `palette.open` | Palette | Open Command Palette | Ctrl+Shift+P | Always available (global) | Category "Palette", alpha within |

### Search/Replace (M3)

| ID | Category | DisplayName | Default gestures | Availability |
|---|---|---|---|---|
| `editor.find` | Editor | Find | Ctrl+F | Active tab non-null |
| `editor.replace` | Editor | Replace | Ctrl+H | Active tab non-null |
| `editor.findNext` | Editor | Find Next | F3 | Active tab non-null, non-empty search pattern |
| `editor.findPrevious` | Editor | Find Previous | Shift+F3 | Active tab non-null, non-empty search pattern |
| `editor.replaceNext` | Editor | Replace Next | (unbound) | Active tab non-null, non-empty search pattern, replace mode |
| `editor.replaceAll` | Editor | Replace All | (unbound) | Active tab non-null, non-empty search pattern, replace mode |

### Folding (M4)

| ID | Category | DisplayName | Default gestures | Availability |
|---|---|---|---|---|
| `editor.foldToggle` | Editor | Toggle Current Fold | (unbound) | Active tab non-null, folding available |
| `editor.foldAll` | Editor | Fold All | (unbound) | Active tab non-null, folding available |
| `editor.unfoldAll` | Editor | Unfold All | (unbound) | Active tab non-null, folding available |

### Tab Lifecycle (M5a-M5b)

| ID | Category | DisplayName | Default gestures | Availability |
|---|---|---|---|---|
| `tab.next` | Tab | Next Tab | Ctrl+Tab | At least 2 open tabs |
| `tab.previous` | Tab | Previous Tab | Ctrl+Shift+Tab | At least 2 open tabs |
| `tab.close` | Tab | Close Tab | Ctrl+W, Ctrl+F4 | At least 1 open tab |
| `tab.closeOthers` | Tab | Close Other Tabs | (unbound) | At least 2 open tabs |
| `tab.closeAll` | Tab | Close All Tabs | (unbound) | At least 1 open tab |

### Status Bar Selection (M6)

| ID | Category | DisplayName | Default gestures | Availability |
|---|---|---|---|---|
| (none — implicit, no new command needed) | | | | |

### Palette ordering/filtering rules (M1-M2)
- **Ordering by:** Category (alphabetical), then DisplayName (alphabetical), case-insensitive.
- **Filtering:** Case-insensitive substring match against DisplayName.
- **Unavailable commands:** Shown but visually disabled (greyed); `CanExecute(null)` determines availability.
- **Unknown command IDs:** Not shown (palette enumerates only registered descriptors).
- **Duplicate registration:** `CommandRegistry.Register` throws `InvalidOperationException`. Same as Phase 8.2 contract.

### Palette priority in search results
- No weighted scoring. Filter is a simple case-insensitive substring match.
- Commands with matching DisplayName are displayed in category+name order.

---

## 4. Active-Tab Switch/Close Rules

### On active-tab switch
The following state belongs to the **outgoing** tab/document and must be explicitly cleared before the new tab is presented:

| State | Owner | Reset action |
|---|---|---|
| Search panel visibility | `TextEditor.SearchPanel` | `SearchPanel.Close()` or `SearchPanel.IsClosed = true` |
| Search query/pattern | `SearchPanel.SearchPattern` | Reset to `""` |
| Search match highlighting | `SearchPanel` (built-in) | Automatically cleared when SearchPanel is closed/pattern cleared |
| Folding state | `FoldingManager` (installed on `TextArea`) | Clear all folding sections: `FoldingManager.Clear()` |
| Folding margin | `FoldingMargin` (in `TextArea.LeftMargins`) | Remove/re-add as needed per-tab |
| Caret position | `TextEditor.TextArea.Caret` | Restored from `EditorViewModel.CaretLine`/`CaretColumn` (already synced by EditorView) |
| Selection | `TextEditor.TextArea.Selection` | `TextArea.ClearSelection()` |
| Undo stack | `TextDocument.UndoStack` (shared `TextEditor`) | `Document` owns `string Content`, not a `TextDocument`. Each tab does **not** have its own `TextDocument`. The shared `TextEditor` owns the single `TextDocument`. `TextEditor.Undo()`/`Redo()` and `TextDocument.UndoStack` operate on this shared instance. On tab switch when `_textEditor.Text = newContent` is set, AvaloniaEdit replaces the `TextDocument` content, which clears the undo stack. **Explicit discard/restore required.** On switch, call `_textEditor.Document.UndoStack.ClearAll()` to guarantee no stale undo state from the old document. |

### On tab close
- Same as switch, but the tab is removed from `OpenTabs` and disposed.
- The `EditorViewModel` is no longer referenced; its `Document` is owned by `Workspace` until `CloseDocument` is called.
- After close, if a neighbour tab is activated, the switch rules apply.

### Dirty close confirmation flow (unchanged from Phase 8.2)
1. `EditorTabViewModel.CloseTabAsync` checks `tab.IsDirty`.
2. If dirty, raises `ConfirmClose` interaction.
3. View shows unsaved-changes dialog; result determines save-skip-cancel.
4. On cancel, no state change — tab stays active.

---

## 5. Selection Projection (End-to-End)

### EditorView → EditorViewModel
Currently, `EditorView` tracks caret position via `Caret.PositionChanged`. For selection, the view needs to push additional state.

**Input events to subscribe in EditorView:**
- `TextArea.SelectionChanged` event (fires when selection changes, including via keyboard, mouse, or programmatic API).

**EditorViewModel state fields (to be added):**

| Field | Type | Default | Notes |
|---|---|---|---|
| `SelectionStart` | int | 0 | Offset of selection start; 0 when no selection |
| `SelectionLength` | int | 0 | Length of selection in characters; 0 when no selection |
| `SelectionText` | string? | null | The selected text; null when no selection |

**Zero/no-selection representation:**
- `SelectionLength == 0` → no active selection. `SelectionText` is `null`.
- `SelectionStart` is always valid (the anchor point). When `SelectionLength == 0`, `SelectionStart` is ignored by status-bar display.

### EditorViewModel → StatusBarViewModel
**Status bar display contract (unchanged for no selection):**
- No selection: `"Ln {CaretLine}, Col {CaretColumn}"` (current behavior).
- With selection: `"Ln {CaretLine}, Col {CaretColumn} | Sel {SelectionLength}"`.

**Implementation:**
- `StatusBarViewModel` currently projects `CaretText` via:
  ```csharp
  tab.WhenAnyValue(t => t.CaretLine, t => t.CaretColumn,
      (line, column) => $"Ln {line}, Col {column}")
  ```
- For Phase 9, add `SelectionLength` to the tuple:
  ```csharp
  tab.WhenAnyValue(t => t.CaretLine, t => t.CaretColumn, t => t.SelectionLength,
      (line, column, sel) => sel == 0
          ? $"Ln {line}, Col {column}"
          : $"Ln {line}, Col {column} | Sel {sel}")
  ```

---

## 6. AvaloniaEdit 12.0.0 API Proof

**Assembly:** `Avalonia.AvaloniaEdit` 12.0.0 (transitive dependency of `AvaloniaEdit.TextMate` 12.0.0)
**NuGet reference:** `AvaloniaEdit.TextMate` 12.0.0 is explicit; `Avalonia.AvaloniaEdit` is pulled transitively.
**Verification method:** Compiled C# reflection tool (`MetadataLoadContext`) against `net10.0` TFM assembly.

### Search namespace (`AvaloniaEdit.Search`)

| Type | Status | Key members |
|---|---|---|
| `SearchPanel` | ✅ Available | `static Install(TextEditor)`, `Open()`, `Close()`, `Reactivate()`, `FindNext(int startOffset)`, `FindPrevious()`, `ReplaceNext()`, `ReplaceAll()`, `SearchPattern`, `ReplacePattern`, `IsReplaceMode`, `MatchCase`, `WholeWords`, `UseRegex`, `IsClosed`, `IsOpened` |
| `ISearchStrategy` | ✅ Available | `FindAll(ITextSource, int, int)`, `FindNext(ITextSource, int, int)` |
| `ISearchResult` | ✅ Available | `ReplaceWith(string)` |
| `SearchMode` | ✅ Available (enum) | Values |
| `SearchCommands` | ✅ Available | RoutedCommand properties |
| `SearchStrategyFactory` | ✅ Available | Factory methods |
| `SearchPatternException` | ✅ Available | Exception type |
| `SearchOptionsChangedEventArgs` | ✅ Available | Event args |

**Finding:** No direct package reference needed. `Avalonia.AvaloniaEdit` 12.0.0 exposes all search types. A simple `using AvaloniaEdit.Search;` in the project will find `SearchPanel`.

### Folding namespace (`AvaloniaEdit.Folding`)

| Type | Status | Key members |
|---|---|---|
| `FoldingManager` | ✅ Available | `static Install(TextArea)`, `Clear()`, `CreateFolding(int, int)`, `RemoveFolding(FoldingSection)`, `GetNextFoldedFoldingStart(int)`, `GetNextFolding(int)`, `GetFoldingsAt(int)`, `GetFoldingsContaining(int)`, `UpdateFoldings(IEnumerable<NewFolding>, int)`, `static Uninstall(FoldingManager)`, `AllFoldings` |
| `FoldingSection` | ✅ Available | `IsFolded` (get/set), `Title`, `TextContent`, `Tag` |
| `NewFolding` | ✅ Available | `StartOffset`, `EndOffset`, `Name`, `DefaultClosed`, `IsDefinition` |
| `FoldingMargin` | ✅ Available | Margin for visual folding UI |
| `FoldingElementGenerator` | ✅ Available | Generator for folding visual elements |
| `XmlFoldingStrategy` | ✅ Available | XML-specific folding strategy |
| `AbstractFoldingStrategy` | ❌ **NOT FOUND** | Does not exist in 12.0.0 |

**Blocker for M4:** No `AbstractFoldingStrategy` base class exists. M4 must implement folding discovery directly using `NewFolding` objects and `FoldingManager.UpdateFoldings()`. A simple heuristic folding strategy (brace matching or indentation-based) will be a standalone class, not an inheritance from a missing base.

**Finding:** No direct package reference needed. `Avalonia.AvaloniaEdit` 12.0.0 exposes all folding types. `using AvaloniaEdit.Folding;` works transitively.

### Caret/Selection (`AvaloniaEdit.Editing`)

| Type | Status | Key members |
|---|---|---|
| `Caret` | ✅ Available | `Line`, `Column`, `Offset`, `Position`, `BringCaretToView()`, `Location`, `CaretBrush` |
| `Selection` | ✅ Available | `static Create(TextArea, int, int)`, `Create(TextArea, ISegment)`, `IsEmpty`, `StartPosition`, `EndPosition`, `Length`, `GetText()`, `ReplaceSelectionWithText(string)`, `Segments`, `SurroundingSegment` |
| `SimpleSelection` | ✅ Available | `ReplaceSelectionWithText(string)`, `IsEmpty`, `Length`, `StartPosition`, `EndPosition` |
| `TextArea` | ✅ Available | `Caret`, `Selection`, `Document`, `ClearSelection()`, `SelectionChanged` (event) |

### Document/Undo grouping

| Type | Status | Key members |
|---|---|---|
| `TextDocument` | ✅ Available | `UndoStack`, `RunUpdate()`, `BeginUpdate()/EndUpdate()`, `Insert()`, `Remove()`, `Replace()`, `Text`, `LineCount`, `GetLineByNumber()`, `GetLineByOffset()`, `CreateAnchor()` |
| `UndoStack` | ✅ Available | `StartUndoGroup()`, `StartUndoGroup(object)`, `StartContinuedUndoGroup(object)`, `EndUndoGroup()`, `Undo()`, `Redo()`, `CanUndo`, `CanRedo`, `Push()`, `PushOptional()` |

### Undo grouping for search/replace
- `UndoStack.StartUndoGroup()` / `EndUndoGroup()` can group Replace All into a single undo step.
- `TextDocument.RunUpdate()` returns `IDisposable` wrapping `BeginUpdate/EndUpdate`.
- Undo stack is per-document (per-tab), so undo isolation is natural when `TextEditor.Document` is swapped per tab.

---

## 7. Test-File Plan for M1–M6

| Milestone | Test file(s) | Focus |
|---|---|---|
| M1 | `CommandPaletteViewModelTests.cs` + `Phase9CommandRegistrationTests.cs` | Palette query/presentation, ordering, filtering, unavailable commands, duplicate protection |
| M2 | `CommandPaletteViewTests.cs` (+ view-model reuse) | Open/close, keyboard nav, execution, focus, live keybinding refresh |
| M3 | `EditorSearchViewModelTests.cs` + `EditorSearchIntegrationTests.cs` | Search modes, next/prev/wrap, replace, undo grouping, dirty state, tab switch |
| M4 | `EditorFoldingTests.cs` | Discovery, expand/collapse, caret, tab switch, plain text |
| M5a | Extend `EditorTabViewModelTests.cs` + registration tests | Tab commands, dirty confirm, neighbor selection |
| M5b | `EditorTabBarLifecycleTests.cs` + `EditorTabReorderTests.cs` | Reorder, no-op, active/dirty, scroll, cleanup |
| M6 | Extend status-bar projection tests + Phase 9 focused tests | Caret/selection/status, search/folding/tab feedback, full gate |

---

## 8. Compile-Backed Proof Test

A focused `Phase9M0EditorUxProofTests` file has been added to `tests/Zaide.Tests/`. It verifies:

- All seven existing canonical command IDs are registered.
- `AvaloniaEdit` 12.0.0 types (`SearchPanel`, `FoldingManager`, `NewFolding`, `Selection`, `Caret`, `UndoStack`) resolve at compile time.
- The `EditorViewModel` has no selection state yet (baseline for M3 addition).
- The `MainWindowViewModel` accepts an optional `ICommandRegistry` parameter used for registration.
- `StatusBarViewModel` subscribes to `ActiveTab` (baseline for selection projection).
- `EditorView` subscribes to `Caret.PositionChanged` (baseline for adding `SelectionChanged`).

---

## 9. Blocker Summary

| Blocker | Affected milestone | Description | Recommended action |
|---|---|---|---|
| `AbstractFoldingStrategy` not found | M4 | `AvaloniaEdit.Folding` namespace has no `AbstractFoldingStrategy` in version 12.0.0. Only `XmlFoldingStrategy` exists. | M4 should implement folding directly with `NewFolding` + `FoldingManager.UpdateFoldings()`. A standalone heuristic class (brace-based or indent-based) replaces the missing base. This is a design change, not a code defect — no structural change is needed. |
| No selection state on `EditorViewModel` | M3, M6 | Current `EditorViewModel` tracks only `CaretLine`/`CaretColumn`. No `SelectionStart`, `SelectionLength`, or `SelectionText`. | Add three properties to `EditorViewModel` before M3 implementation begins. This is a planned additive change, not a blocker. |
| Shared `TextEditor` undo + folding state | M3, M4, M6 | `EditorView` syncs content via `_textEditor.Text = newContent`. `Document` owns `string Content`, **not** a `TextDocument`. Therefore `TextEditor.Document = tab.TextDocument` is not currently possible. The shared `TextEditor`'s `TextDocument` is replaced on every tab switch, clearing the undo stack. | **Locked: existing string-sync model with explicit discard/restore.** On every active-tab switch, `EditorView` must: (1) call `SearchPanel.Close()`/`SearchPanel.IsClosed = true` if the panel was open, (2) call `FoldingManager.Clear()` if folding was installed, (3) call `_textEditor.Document.UndoStack.ClearAll()`. No structural change to `Document`. A future phase may add `TextDocument` ownership to `Document` if undo-per-tab becomes a requirement, but that is outside Phase 9 scope. |

---

## 10. Verification Results

| Gate | Result |
|---|---|
| `dotnet test --filter FullyQualifiedName~Phase9M0EditorUxProofTests` | ✅ 22/22 passed |
| `dotnet build Zaide.slnx --no-restore` | ✅ 0 errors |
| `dotnet test Zaide.slnx --no-build` | ✅ 1266 passed, 0 failed (M2 baseline) |
| `git diff --check` | ✅ clean |
